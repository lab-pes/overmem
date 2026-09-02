using System;
using Overmem.Extensions.Pes2021.Players;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021PlayerWritePolicyTests
{
    [Fact]
    public void Evaluate_RejectsFieldWithUnknownWriteStatus()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var field = profile.RecordLayout.Fields.Single(f => f.Name == "unknown_12c");
        var result = Pes2021PlayerWritePolicy.Evaluate(
            profile, field, Pes2021PlayerContext.EditBaseConfirmed,
            authorization: null,
            profile.ProfileId, profile.ProfileVersion, DateTimeOffset.UtcNow);

        Assert.False(result.Allow);
        Assert.Contains(result.Reasons, r => r.StartsWith("write_status_not_confirmed"));
    }

    [Fact]
    public void Evaluate_AllowsConfirmedField_WhenContextMatches()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var field = profile.RecordLayout.Fields.Single(f => f.Name == "marketValue");
        var authorization = new PlayerWriteAuthorization(
            FieldName: field.Name,
            TokenId: "tok-1",
            GrantedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(5),
            Reason: "unit test");

        var result = Pes2021PlayerWritePolicy.Evaluate(
            profile, field, Pes2021PlayerContext.MasterLeagueConfirmed,
            authorization, profile.ProfileId, profile.ProfileVersion, DateTimeOffset.UtcNow);

        Assert.False(result.Allow);
        Assert.Contains(result.Reasons, r => r == "write_status_not_confirmed:Candidate");
    }

    [Fact]
    public void Evaluate_RejectsExpiredAuthorization()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var field = profile.RecordLayout.Fields.Single(f => f.Name == "marketValue");
        var authorization = new PlayerWriteAuthorization(
            FieldName: field.Name,
            TokenId: "tok-2",
            GrantedAtUtc: DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            Reason: "stale");

        var result = Pes2021PlayerWritePolicy.Evaluate(
            profile, field, Pes2021PlayerContext.MasterLeagueConfirmed,
            authorization, profile.ProfileId, profile.ProfileVersion, DateTimeOffset.UtcNow);

        Assert.False(result.Allow);
        Assert.Contains(result.Reasons, r => r == "authorization_expired");
    }

    [Fact]
    public void Evaluate_RejectsFieldLevelMismatch()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var field = profile.RecordLayout.Fields.Single(f => f.Name == "marketValue");
        var authorization = new PlayerWriteAuthorization(
            FieldName: "annualSalary",
            TokenId: "tok-3",
            GrantedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(5),
            Reason: "wrong field");

        var result = Pes2021PlayerWritePolicy.Evaluate(
            profile, field, Pes2021PlayerContext.MasterLeagueConfirmed,
            authorization, profile.ProfileId, profile.ProfileVersion, DateTimeOffset.UtcNow);

        Assert.False(result.Allow);
        Assert.Contains(result.Reasons, r => r.StartsWith("authorization_field_mismatch"));
    }

    [Fact]
    public void Evaluate_RejectsProfileIdentityMismatch()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var field = profile.RecordLayout.Fields.Single(f => f.Name == "marketValue");
        var result = Pes2021PlayerWritePolicy.Evaluate(
            profile, field, Pes2021PlayerContext.MasterLeagueConfirmed,
            authorization: null,
            expectedProfileId: "different-profile",
            expectedProfileVersion: profile.ProfileVersion,
            DateTimeOffset.UtcNow);

        Assert.False(result.Allow);
        Assert.Contains(result.Reasons, r => r.StartsWith("profile_id_mismatch"));
    }
}