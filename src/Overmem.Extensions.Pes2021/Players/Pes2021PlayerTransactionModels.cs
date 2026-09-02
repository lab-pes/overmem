using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Serializable rollback artifact. Stores the original bytes at the target field,
/// their SHA-256, the patch metadata, and the session identity. The artifact is
/// required for any apply call: <see cref="Pes2021PlayerTransactionCore"/> refuses
/// to apply a patch without a rollback artifact on disk.
/// </summary>
public sealed record PlayerRollbackArtifact(
    string RollbackId,
    string PlanId,
    ulong RecordAddress,
    int FieldOffset,
    int FieldWidth,
    string OriginalHex,
    string OriginalSha256,
    long RawOld,
    long RawNew,
    string FieldName,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Plan returned by <see cref="Pes2021PlayerTransactionCore.PlanAsync"/>. Carries the
/// patch, the rollback artifact path, and a deterministic plan identifier.
/// </summary>
public sealed record PlayerPatchPlan(
    string PlanId,
    string SessionId,
    ulong RecordAddress,
    int FieldOffset,
    int FieldWidth,
    string FieldName,
    long RawOld,
    long RawNew,
    string OldHex,
    string NewHex,
    string RollbackArtifactPath,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Result of <see cref="Pes2021PlayerTransactionCore.ApplyAsync"/>. Every non-`applied`
/// outcome carries a stable code from <c>docs/pes2021/player-memory/error-codes.md</c>.
/// </summary>
public sealed record PlayerApplyResult(
    string PlanId,
    string RollbackArtifactPath,
    string Outcome,
    string? Code,
    long RawBefore,
    long RawAfter,
    string RawBeforeSha256,
    string RawAfterSha256,
    DateTimeOffset VerifiedAtUtc);

/// <summary>
/// Result of <see cref="Pes2021PlayerTransactionCore.RollbackAsync"/>. Reports the
/// number of bytes restored and the resulting raw value.
/// </summary>
public sealed record PlayerRollbackResult(
    string PlanId,
    string RollbackId,
    string Outcome,
    int BytesRestored,
    long RawRestored,
    DateTimeOffset VerifiedAtUtc);