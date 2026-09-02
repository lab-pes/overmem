using System;
using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Per-field write policy. A field can be patched only when:
///   - <see cref="WriteStatus"/> is <see cref="Pes2021PlayerEvidenceStatus.Confirmed"/>
///     (not <see cref="Pes2021PlayerEvidenceStatus.Candidate"/> or
///     <see cref="Pes2021PlayerEvidenceStatus.Unknown"/>);
///   - <see cref="Context"/> is in the field's <see cref="Pes2021PlayerFieldDefinition.ValidContexts"/>;
///   - <see cref="Authorization"/> carries a non-expired grant for the field;
///   - the caller's profile identity matches <see cref="ExpectedProfileId"/> /
///     <see cref="ExpectedProfileVersion"/>.
/// </summary>
public sealed record PlayerWriteAuthorization(
    string FieldName,
    string TokenId,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Reason);

/// <summary>
/// Result of evaluating the write policy. Always carries the decision plus a list
/// of reasons so callers can audit why a write was rejected.
/// </summary>
public sealed record PlayerWritePolicyResult(
    bool Allow,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Evaluates per-field write policy. Refuses any write that is not authorized,
/// not confirmed, or not in the right context. Stateless; safe for concurrent use.
/// </summary>
public static class Pes2021PlayerWritePolicy
{
    public static PlayerWritePolicyResult Evaluate(
        Pes2021PlayerProfile profile,
        Pes2021PlayerFieldDefinition field,
        Pes2021PlayerContext context,
        PlayerWriteAuthorization? authorization,
        string expectedProfileId,
        string expectedProfileVersion,
        DateTimeOffset nowUtc)
    {
        var reasons = new List<string>();

        if (!string.Equals(profile.ProfileId, expectedProfileId, StringComparison.Ordinal))
        {
            reasons.Add($"profile_id_mismatch:{profile.ProfileId}");
        }

        if (!string.Equals(profile.ProfileVersion, expectedProfileVersion, StringComparison.Ordinal))
        {
            reasons.Add($"profile_version_mismatch:{profile.ProfileVersion}");
        }

        if (field.WriteStatus != Pes2021PlayerEvidenceStatus.Confirmed)
        {
            reasons.Add($"write_status_not_confirmed:{field.WriteStatus}");
        }

        var allowedContext = false;
        foreach (var validContext in field.ValidContexts)
        {
            if (validContext == context)
            {
                allowedContext = true;
                break;
            }
        }

        if (!allowedContext)
        {
            reasons.Add($"context_incompatible:{context}");
        }

        if (authorization is null)
        {
            reasons.Add("authorization_missing");
        }
        else if (authorization.FieldName != field.Name)
        {
            reasons.Add($"authorization_field_mismatch:{authorization.FieldName}");
        }
        else if (authorization.ExpiresAtUtc <= nowUtc)
        {
            reasons.Add("authorization_expired");
        }

        var allow = reasons.Count == 0;
        if (allow)
        {
            reasons.Add("authorized");
        }

        return new PlayerWritePolicyResult(allow, reasons);
    }
}