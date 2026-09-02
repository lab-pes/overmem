using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Cli;
using Overmem.Extensions.Pes2021.Fixtures;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Plan / apply / verify / rollback for a single field patch on a single 380-byte
/// player record. Refuses to run against any process whose name is not in
/// <see cref="AllowedProcessNames"/>; the default allowlist is restricted to the
/// in-repo <c>Overmem.TestTarget</c> harness so a real PES2021.exe is never written
/// by accident during this development phase.
/// </summary>
public sealed class Pes2021PlayerTransactionCore
{
    public static readonly IReadOnlyList<string> DefaultAllowedProcessNames =
        new[] { "Overmem.TestTarget", "dotnet" };

    private readonly IProcessMemoryGateway _gateway;
    private readonly IReadOnlyList<string> _allowedProcessNames;

    public Pes2021PlayerTransactionCore(IProcessMemoryGateway gateway)
        : this(gateway, DefaultAllowedProcessNames)
    {
    }

    public Pes2021PlayerTransactionCore(IProcessMemoryGateway gateway, IReadOnlyList<string> allowedProcessNames)
    {
        _gateway = gateway;
        _allowedProcessNames = allowedProcessNames;
    }

    public IReadOnlyList<string> AllowedProcessNames => _allowedProcessNames;

    /// <summary>
    /// Default context the policy accepts when the caller does not specify one.
    /// The transaction core never patches a field whose declared <c>ValidContexts</c>
    /// does not include the active context.
    /// </summary>
    public Pes2021PlayerContext ActiveContext { get; set; } = Pes2021PlayerContext.EditBaseCandidate;

    /// <summary>
    /// Apply that enforces the per-field write policy. Refuses when:
    /// - the field's <c>WriteStatus</c> is not <c>Confirmed</c>;
    /// - the active context is not in the field's <c>ValidContexts</c>;
    /// - the caller's profile identity does not match the expected id/version;
    /// - the supplied <see cref="PlayerWriteAuthorization"/> is missing, expired,
    ///   or scoped to a different field.
    /// </summary>
    public async Task<PlayerApplyResult> ApplyWithPolicyAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021PlayerProfile profile,
        PlayerPatchPlan plan,
        PlayerWriteAuthorization? authorization,
        CancellationToken cancellationToken)
    {
        EnsureProcessAllowed(process.ProcessName);

        var field = profile.RecordLayout.Fields.Single(f => f.Name == plan.FieldName);
        var policy = Pes2021PlayerWritePolicy.Evaluate(
            profile, field, ActiveContext, authorization,
            profile.ProfileId, profile.ProfileVersion, DateTimeOffset.UtcNow);
        if (!policy.Allow)
        {
            return new PlayerApplyResult(
                PlanId: plan.PlanId,
                RollbackArtifactPath: plan.RollbackArtifactPath,
                Outcome: "rejected",
                Code: "PES2021_PLAYER_WRITE_NOT_AUTHORIZED",
                RawBefore: plan.RawOld,
                RawAfter: plan.RawOld,
                RawBeforeSha256: string.Empty,
                RawAfterSha256: string.Empty,
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }

        return await ApplyAsync(attachmentId, process, profile, plan, dryRun: false, cancellationToken);
    }

    /// <summary>
    /// Builds a patch plan for one field. Reads the current bytes, computes the
    /// expected raw/display values, and writes a rollback artifact to disk. The plan
    /// is byte-perfect: <see cref="ApplyAsync"/> verifies the bytes on disk match
    /// before writing.
    /// </summary>
    public async Task<PlayerPatchPlan> PlanAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021PlayerProfile profile,
        ulong recordAddress,
        Pes2021PlayerFieldDefinition field,
        long rawNew,
        string rollbackArtifactPath,
        CancellationToken cancellationToken)
    {
        EnsureProcessAllowed(process.ProcessName);

        if (string.IsNullOrWhiteSpace(rollbackArtifactPath))
        {
            throw new ArgumentException("Rollback artifact path is required.", nameof(rollbackArtifactPath));
        }

        if (rawNew < int.MinValue || rawNew > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(rawNew), rawNew, "Value is outside the allowed 32-bit range.");
        }

        var fieldBytes = await ReadFieldAsync(attachmentId, recordAddress, field, cancellationToken);
        var rawOld = DecodeRaw(field, fieldBytes);
        var planId = Guid.NewGuid().ToString("N");
        var oldHex = Convert.ToHexString(fieldBytes);
        var newBytes = EncodeRaw(field, rawNew);
        var newHex = Convert.ToHexString(newBytes);
        var rollbackId = Guid.NewGuid().ToString("N");

        var recordHashBefore = await HashRecordAsync(attachmentId, recordAddress, profile.Stride, cancellationToken);
        var fieldHash = Convert.ToHexString(SHA256.HashData(fieldBytes)).ToLowerInvariant();

        var rollback = new PlayerRollbackArtifact(
            RollbackId: rollbackId,
            PlanId: planId,
            RecordAddress: recordAddress,
            FieldOffset: field.Offset,
            FieldWidth: field.Width,
            OriginalHex: oldHex,
            OriginalSha256: fieldHash,
            RawOld: rawOld,
            RawNew: rawNew,
            FieldName: field.Name,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        var sessionId = $"{process.ProcessId}:{process.ProcessStartedAtUtc?.Ticks ?? 0}";
        await WriteRollbackAsync(rollbackArtifactPath, rollback, cancellationToken);

        return new PlayerPatchPlan(
            PlanId: planId,
            SessionId: sessionId,
            RecordAddress: recordAddress,
            FieldOffset: field.Offset,
            FieldWidth: field.Width,
            FieldName: field.Name,
            RawOld: rawOld,
            RawNew: rawNew,
            OldHex: oldHex,
            NewHex: newHex,
            RollbackArtifactPath: rollbackArtifactPath,
            CreatedAtUtc: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Applies the patch via compare-and-swap. If the bytes on disk do not match the
    /// plan's <see cref="PlayerPatchPlan.OldHex"/>, the apply is rejected with code
    /// <c>PES2021_PLAYER_EXPECTED_BYTES_M_MISMATCH</c> and no bytes are touched.
    /// </summary>
    public async Task<PlayerApplyResult> ApplyAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021PlayerProfile profile,
        PlayerPatchPlan plan,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        EnsureProcessAllowed(process.ProcessName);

        var fieldBytes = await ReadFieldAsync(attachmentId, plan.RecordAddress, profile.RecordLayout.Fields.Single(f => f.Name == plan.FieldName), cancellationToken);
        var actualHex = Convert.ToHexString(fieldBytes);
        if (!string.Equals(actualHex, plan.OldHex, StringComparison.OrdinalIgnoreCase))
        {
            return new PlayerApplyResult(
                PlanId: plan.PlanId,
                RollbackArtifactPath: plan.RollbackArtifactPath,
                Outcome: "expected_bytes_mismatch",
                Code: "PES2021_PLAYER_EXPECTED_BYTES_MISMATCH",
                RawBefore: plan.RawOld,
                RawAfter: plan.RawOld,
                RawBeforeSha256: string.Empty,
                RawAfterSha256: string.Empty,
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }

        if (dryRun)
        {
            return new PlayerApplyResult(
                PlanId: plan.PlanId,
                RollbackArtifactPath: plan.RollbackArtifactPath,
                Outcome: "dry_run",
                Code: null,
                RawBefore: plan.RawOld,
                RawAfter: plan.RawNew,
                RawBeforeSha256: string.Empty,
                RawAfterSha256: string.Empty,
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }

        var newBytes = EncodeRaw(profile.RecordLayout.Fields.Single(f => f.Name == plan.FieldName), plan.RawNew);
        var recordHashBefore = await HashRecordAsync(attachmentId, plan.RecordAddress, profile.Stride, cancellationToken);

        var write = await _gateway.WriteAsync(
            new WriteMemoryRequest(attachmentId, plan.RecordAddress + (ulong)plan.FieldOffset, MemoryValueKind.Bytes, Convert.ToHexString(newBytes), newBytes.Length),
            cancellationToken);

        if (write.BytesWritten != newBytes.Length)
        {
            return new PlayerApplyResult(
                PlanId: plan.PlanId,
                RollbackArtifactPath: plan.RollbackArtifactPath,
                Outcome: "verify_failed",
                Code: "PES2021_PLAYER_VERIFY_FAILED",
                RawBefore: plan.RawOld,
                RawAfter: plan.RawOld,
                RawBeforeSha256: recordHashBefore,
                RawAfterSha256: string.Empty,
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }

        var verify = await ReadFieldAsync(attachmentId, plan.RecordAddress, profile.RecordLayout.Fields.Single(f => f.Name == plan.FieldName), cancellationToken);
        var actualVerifyHex = Convert.ToHexString(verify);
        if (!string.Equals(actualVerifyHex, plan.NewHex, StringComparison.OrdinalIgnoreCase))
        {
            return new PlayerApplyResult(
                PlanId: plan.PlanId,
                RollbackArtifactPath: plan.RollbackArtifactPath,
                Outcome: "verify_failed",
                Code: "PES2021_PLAYER_VERIFY_FAILED",
                RawBefore: plan.RawOld,
                RawAfter: plan.RawOld,
                RawBeforeSha256: recordHashBefore,
                RawAfterSha256: string.Empty,
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }

        var recordHashAfter = await HashRecordAsync(attachmentId, plan.RecordAddress, profile.Stride, cancellationToken);
        return new PlayerApplyResult(
            PlanId: plan.PlanId,
            RollbackArtifactPath: plan.RollbackArtifactPath,
            Outcome: "applied",
            Code: null,
            RawBefore: plan.RawOld,
            RawAfter: plan.RawNew,
            RawBeforeSha256: recordHashBefore,
            RawAfterSha256: recordHashAfter,
            VerifiedAtUtc: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Restores the bytes from the rollback artifact. The restore itself is a
    /// compare-and-swap: if the bytes at the address do not match the expected
    /// post-apply state, the restore refuses to overwrite.
    /// </summary>
    public async Task<PlayerRollbackResult> RollbackAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021PlayerProfile profile,
        PlayerPatchPlan plan,
        CancellationToken cancellationToken)
    {
        EnsureProcessAllowed(process.ProcessName);

        var rollback = await ReadRollbackAsync(plan.RollbackArtifactPath, cancellationToken);
        if (rollback is null)
        {
            return new PlayerRollbackResult(
                PlanId: plan.PlanId,
                RollbackId: string.Empty,
                Outcome: "rollback_failed",
                BytesRestored: 0,
                RawRestored: plan.RawOld,
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }

        var field = profile.RecordLayout.Fields.Single(f => f.Name == rollback.FieldName);
        var currentBytes = await ReadFieldAsync(attachmentId, plan.RecordAddress, field, cancellationToken);
        var expectedHex = Convert.ToHexString(EncodeRaw(field, plan.RawNew));
        if (!string.Equals(Convert.ToHexString(currentBytes), expectedHex, StringComparison.OrdinalIgnoreCase))
        {
            return new PlayerRollbackResult(
                PlanId: plan.PlanId,
                RollbackId: rollback.RollbackId,
                Outcome: "rollback_failed",
                BytesRestored: 0,
                RawRestored: plan.RawOld,
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }

        var originalBytes = Convert.FromHexString(rollback.OriginalHex);
        await _gateway.WriteAsync(
            new WriteMemoryRequest(attachmentId, plan.RecordAddress + (ulong)rollback.FieldOffset, MemoryValueKind.Bytes, Convert.ToHexString(originalBytes), originalBytes.Length),
            cancellationToken);

        var verify = await ReadFieldAsync(attachmentId, plan.RecordAddress, field, cancellationToken);
        var verifyHex = Convert.ToHexString(verify);
        if (!string.Equals(verifyHex, rollback.OriginalHex, StringComparison.OrdinalIgnoreCase))
        {
            return new PlayerRollbackResult(
                PlanId: plan.PlanId,
                RollbackId: rollback.RollbackId,
                Outcome: "rollback_failed",
                BytesRestored: 0,
                RawRestored: plan.RawOld,
                VerifiedAtUtc: DateTimeOffset.UtcNow);
        }

        return new PlayerRollbackResult(
            PlanId: plan.PlanId,
            RollbackId: rollback.RollbackId,
            Outcome: "rolled_back",
            BytesRestored: originalBytes.Length,
            RawRestored: rollback.RawOld,
            VerifiedAtUtc: DateTimeOffset.UtcNow);
    }

    private void EnsureProcessAllowed(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new InvalidOperationException("Process name is empty; refusing to plan.");
        }

        foreach (var allowed in _allowedProcessNames)
        {
            if (string.Equals(processName, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"Process '{processName}' is not in the transaction allowlist ({string.Join(", ", _allowedProcessNames)}). Refusing to plan.");
    }

    private async Task<byte[]> ReadFieldAsync(AttachmentId attachmentId, ulong recordAddress, Pes2021PlayerFieldDefinition field, CancellationToken cancellationToken)
    {
        var result = await _gateway.ReadAsync(
            new ReadMemoryRequest(attachmentId, recordAddress + (ulong)field.Offset, MemoryValueKind.Bytes, field.Width),
            cancellationToken);
        var bytes = Convert.FromHexString(result.Value);
        if (bytes.Length != field.Width)
        {
            throw new InvalidOperationException($"Read returned {bytes.Length} bytes; expected {field.Width}.");
        }

        return bytes;
    }

    private async Task<string> HashRecordAsync(AttachmentId attachmentId, ulong recordAddress, int stride, CancellationToken cancellationToken)
    {
        var result = await _gateway.ReadAsync(
            new ReadMemoryRequest(attachmentId, recordAddress, MemoryValueKind.Bytes, stride),
            cancellationToken);
        return Convert.ToHexString(SHA256.HashData(Convert.FromHexString(result.Value))).ToLowerInvariant();
    }

    private static long DecodeRaw(Pes2021PlayerFieldDefinition field, byte[] bytes)
    {
        switch (field.Type)
        {
            case Pes2021PlayerFieldType.U8: return bytes[0];
            case Pes2021PlayerFieldType.I8: return (sbyte)bytes[0];
            case Pes2021PlayerFieldType.U16Le: return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            case Pes2021PlayerFieldType.U32Le: return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            case Pes2021PlayerFieldType.I32Le: return BinaryPrimitives.ReadInt32LittleEndian(bytes);
            case Pes2021PlayerFieldType.I8X4: return bytes[0];
            case Pes2021PlayerFieldType.FixedAscii:
                throw new InvalidOperationException("FixedAscii fields cannot be patched as raw integers.");
            default:
                throw new InvalidOperationException($"Unsupported field type {field.Type}.");
        }
    }

    private static byte[] EncodeRaw(Pes2021PlayerFieldDefinition field, long value)
    {
        Span<byte> bytes = field.Width switch
        {
            1 => stackalloc byte[1],
            2 => stackalloc byte[2],
            4 => stackalloc byte[4],
            _ => throw new InvalidOperationException($"Unsupported field width {field.Width}."),
        };

        switch (field.Type)
        {
            case Pes2021PlayerFieldType.U8:
                bytes[0] = (byte)value;
                break;
            case Pes2021PlayerFieldType.I8:
                bytes[0] = (byte)((sbyte)value);
                break;
            case Pes2021PlayerFieldType.U16Le:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)value);
                break;
            case Pes2021PlayerFieldType.U32Le:
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)value);
                break;
            case Pes2021PlayerFieldType.I32Le:
                BinaryPrimitives.WriteInt32LittleEndian(bytes, (int)value);
                break;
            case Pes2021PlayerFieldType.I8X4:
                bytes[0] = (byte)((sbyte)value);
                break;
            default:
                throw new InvalidOperationException($"Unsupported field type {field.Type}.");
        }

        return bytes.ToArray();
    }

    private static async Task WriteRollbackAsync(string path, PlayerRollbackArtifact artifact, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        Pes2021AtomicFileWriter.WriteJson(path, artifact, options);
        await Task.CompletedTask;
    }

    private static async Task<PlayerRollbackArtifact?> ReadRollbackAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PlayerRollbackArtifact>(stream, cancellationToken: cancellationToken);
    }
}