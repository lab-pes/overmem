using System.Collections.Generic;
using System.IO;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Provides a built-in default <see cref="Pes2021PlayerProfile"/> for the legacy players
/// extension surface that has not yet been migrated to take an explicit profile argument.
/// The default mirrors
/// <c>files/pes2021/player-memory/pes2021-player-record-v1.json</c> and is intentionally
/// loaded lazily so test hosts can override it.
/// </summary>
public static class Pes2021PlayerProfileDefaults
{
    private static readonly object Sync = new();
    private static Pes2021PlayerProfile? _cached;

    public static void Override(Pes2021PlayerProfile profile)
    {
        lock (Sync)
        {
            _cached = profile;
        }
    }

    public static Pes2021PlayerProfile GetOrLoad()
    {
        lock (Sync)
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var resolved = TryLoadFromDisk() ?? BuildBuiltIn();
            _cached = resolved;
            return resolved;
        }
    }

    private static Pes2021PlayerProfile? TryLoadFromDisk()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "profiles", "pes2021-player-edit-v1.json"),
            Path.Combine(AppContext.BaseDirectory, "profiles", "pes2021-player-edit.json"),
            Path.Combine(AppContext.BaseDirectory, "profiles", "pes2021-player-record-v1.json"),
            Path.Combine(AppContext.BaseDirectory, "profiles", "pes2021-player-record.json"),
            Path.Combine(Environment.CurrentDirectory, "profiles", "pes2021-player-edit-v1.json"),
            Path.Combine(Environment.CurrentDirectory, "profiles", "pes2021-player-edit.json"),
            Path.Combine(Environment.CurrentDirectory, "profiles", "pes2021-player-record-v1.json"),
            Path.Combine(Environment.CurrentDirectory, "profiles", "pes2021-player-record.json"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    return Pes2021PlayerProfileLoader.LoadFromFile(path);
                }
                catch (Pes2021PlayerProfileException)
                {
                    // Fall through to the built-in default; surface errors only when the
                    // built-in itself cannot be parsed.
                }
            }
        }

        return null;
    }

    public static Pes2021PlayerProfile BuildBuiltIn()
    {
        var fields = new List<Pes2021PlayerFieldDefinition>
        {
            new("height", 0, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Confirmed,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Strong structural candidate; participates in cheap validation."),
            new("weight", 1, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Confirmed,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Strong structural candidate; participates in cheap validation."),
            new("playerId", 48, 4, Pes2021PlayerFieldType.U32Le, "unsigned", "little",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Confirmed,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Live structural confirmation. Opaque u32 non-zero value. High-bit flags (0x40000000, 0x80000000) are structurally observed valid records; do not reject by small numeric ceiling. Duplicates exist; do not use alone for write targeting."),
            new("commentaryId", 52, 4, Pes2021PlayerFieldType.U32Le, "unsigned", "little",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "CT/v5 candidate."),
            new("playerName", 56, 61, Pes2021PlayerFieldType.FixedAscii, "n/a", "n/a",
                Pes2021PlayerTransform.TrimAsciiZ,
                Pes2021PlayerEvidenceStatus.Confirmed,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Live structural confirmation; used as fingerprint for write identity."),
            new("clubShirtName", 117, 61, Pes2021PlayerFieldType.FixedAscii, "n/a", "n/a",
                Pes2021PlayerTransform.TrimAsciiZ,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "CT/v5 candidate."),
            new("nationalShirtName", 178, 61, Pes2021PlayerFieldType.FixedAscii, "n/a", "n/a",
                Pes2021PlayerTransform.TrimAsciiZ,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "CT/v5 candidate."),
            new("nationality", 324, 2, Pes2021PlayerFieldType.U16Le, "unsigned", "little",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "CT/v5 candidate."),
            new("marketValue", 372, 4, Pes2021PlayerFieldType.I32Le, "signed", "little",
                Pes2021PlayerTransform.RawMul100Eur,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Address/function user-validated in EDIT via Lua; display scale pending UI correlation."),
            new("contractEndYear", 312, 2, Pes2021PlayerFieldType.U16Le, "unsigned", "little",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "UI correlation pending."),
            new("contractEndMonth", 314, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "UI correlation pending."),
            new("contractEndDay", 315, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "UI correlation pending."),
            new("affection", 318, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Controlled value variation required for promotion."),
            new("affectionFlags", 319, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.Bitfield,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                true,
                new[]
                {
                    new Pes2021PlayerBitField("maxAffection", 0, 1,
                        Pes2021PlayerEvidenceStatus.Candidate, Pes2021PlayerEvidenceStatus.Candidate),
                    new Pes2021PlayerBitField("listedPlayer", 1, 1,
                        Pes2021PlayerEvidenceStatus.Candidate, Pes2021PlayerEvidenceStatus.Candidate),
                },
                "Paired before/after evidence required."),
            new("teamRoleLevel", 323, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.Bitfield,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                true,
                new[]
                {
                    new Pes2021PlayerBitField("teamRoleLevel", 6, 2,
                        Pes2021PlayerEvidenceStatus.Candidate, Pes2021PlayerEvidenceStatus.Candidate),
                },
                "UI correlation required."),
            new("staminaBar", 326, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.Bitfield,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                true,
                new[]
                {
                    new Pes2021PlayerBitField("staminaBar", 0, 7,
                        Pes2021PlayerEvidenceStatus.Candidate, Pes2021PlayerEvidenceStatus.Candidate),
                    new Pes2021PlayerBitField("blinkingFormArrow", 7, 1,
                        Pes2021PlayerEvidenceStatus.Candidate, Pes2021PlayerEvidenceStatus.Candidate),
                },
                "Runtime change correlation required."),
            new("currentFormArrow", 327, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.Bitfield,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                true,
                new[]
                {
                    new Pes2021PlayerBitField("currentFormArrow", 0, 3,
                        Pes2021PlayerEvidenceStatus.Candidate, Pes2021PlayerEvidenceStatus.Candidate),
                },
                "Runtime/UI correlation required."),
            new("unavailableDays", 328, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Injury/suspension experiment required."),
            new("transferFlags", 330, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.Bitfield,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                true,
                new[]
                {
                    new Pes2021PlayerBitField("transferListed", 1, 1,
                        Pes2021PlayerEvidenceStatus.Candidate, Pes2021PlayerEvidenceStatus.Candidate),
                    new Pes2021PlayerBitField("loanListed", 2, 1,
                        Pes2021PlayerEvidenceStatus.Candidate, Pes2021PlayerEvidenceStatus.Candidate),
                },
                "Transfer-screen correlation required."),
            new("teamRole", 336, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.Bitfield,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                true,
                new[]
                {
                    new Pes2021PlayerBitField("teamRole", 0, 5,
                        Pes2021PlayerEvidenceStatus.Candidate, Pes2021PlayerEvidenceStatus.Candidate),
                },
                "UI correlation required."),
            new("personalityAxes", 337, 4, Pes2021PlayerFieldType.I8X4, "signed", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "UI or controlled-delta evidence required."),
            new("impact", 341, 1, Pes2021PlayerFieldType.U8, "unsigned", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Controlled-delta evidence required."),
            new("annualSalary", 348, 4, Pes2021PlayerFieldType.I32Le, "signed", "little",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Candidate,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Authoritative ML-copy discovery plus UI correlation required."),
            new("unknown_12c", 300, 2, Pes2021PlayerFieldType.U16Le, "unsigned", "little",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Unknown,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed },
                false, null,
                "Physical bytes present in EDIT; semantics not promoted."),
            new("unknown_12e", 302, 2, Pes2021PlayerFieldType.U16Le, "unsigned", "little",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Unknown,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed },
                false, null,
                "Physical bytes present in EDIT; semantics not promoted."),
            new("unknown_178", 376, 1, Pes2021PlayerFieldType.I8, "signed", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Unknown,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Remains unknown."),
            new("unknown_179", 377, 1, Pes2021PlayerFieldType.I8, "signed", "n/a",
                Pes2021PlayerTransform.None,
                Pes2021PlayerEvidenceStatus.Unknown,
                Pes2021PlayerEvidenceStatus.Candidate,
                new[] { Pes2021PlayerContext.EditBaseCandidate, Pes2021PlayerContext.EditBaseConfirmed,
                        Pes2021PlayerContext.MasterLeagueCandidate, Pes2021PlayerContext.MasterLeagueConfirmed },
                false, null,
                "Remains unknown."),
        };

        var layout = new Pes2021PlayerRecordLayout(Pes2021PlayerProfileLoader.ExpectedStride, 0, fields);
        var validation = new Pes2021PlayerRecordValidation(
            MinimumHeight: 120,
            MaximumHeight: 220,
            MinimumWeight: 30,
            MaximumWeight: 160,
            MinimumPlayerId: 1,
            MaximumPlayerId: uint.MaxValue);
        var regionFilter = new Pes2021PlayerRegionFilter(
            States: new[] { "Commit" },
            Types: new[] { "Private" },
            RequireReadable: true,
            RequireWritable: true,
            AllowExecutable: false,
            ChunkBytes: 1 << 20);
        var anchorValidation = new Pes2021PlayerAnchorValidation(
            RecordsBefore: 4,
            RecordsAfter: 8,
            MinimumRun: 3,
            MinimumAnchorScore: 5,
            MediumScore: 8,
            HighScore: 12,
            ControlPlayerIds: new uint[] { 58120 });
        var limits = new Pes2021PlayerLimits(
            DefaultBlockRecords: 256,
            MaxBlockRecords: 2048,
            MaxRecordsReturned: 50_000,
            ScanBudgetMs: 30_000);
        var sources = new Pes2021PlayerProfileSources(
            CtPath: "files\\PES 2021 - v21.1.0.CT",
            CtSha256: "DA67EB5C8F7B13243AD5BE654D618EA5E4BAEB52449FECBC453144AF6C89AF7C",
            SchemaV5LuaSha256: "6BD22B451085FE4D4209D7DB5FA93152CE78683439D760CA88D33BFC7144050E");

        return new Pes2021PlayerProfile(
            SchemaVersion: Pes2021PlayerProfileLoader.SupportedSchemaVersion,
            ProfileId: "pes2021-player-edit-v1",
            ProfileVersion: "0.1.0-builtin",
            EvidenceStatus: Pes2021PlayerEvidenceStatus.Candidate,
            ProcessNames: new[] { "PES2021" },
            RecordLayout: layout,
            RecordValidation: validation,
            RegionFilter: regionFilter,
            AnchorValidation: anchorValidation,
            Limits: limits,
            Sources: sources,
            Sha256: string.Empty,
            SourcePath: "<builtin>");
    }
}
