using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Provides a built-in default <see cref="Pes2021FixtureProfile"/> for the legacy dump
/// commands that have not yet been migrated to take an explicit profile argument. The
/// default mirrors <c>docs/pes2021/competition-fixtures/examples/pes2021-fixture-profile.example.json</c>
/// and is intentionally loaded lazily so test hosts can override it.
/// </summary>
public static class Pes2021FixtureProfileDefaults
{
    private static readonly object Sync = new();
    private static Pes2021FixtureProfile? _cached;

    /// <summary>
    /// Replaces the cached default profile. The dump/summary/compare methods read the
    /// profile lazily, so this hook lets tests pin a different profile before the first
    /// call.
    /// </summary>
    public static void Override(Pes2021FixtureProfile profile)
    {
        lock (Sync)
        {
            _cached = profile;
        }
    }

    public static Pes2021FixtureProfile GetOrLoad()
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

    private static Pes2021FixtureProfile? TryLoadFromDisk()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "profiles", "pes2021-fixture-profile.json"),
            Path.Combine(Environment.CurrentDirectory, "profiles", "pes2021-fixture-profile.json"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    return Pes2021FixtureProfileLoader.LoadFromFile(path);
                }
                catch (Pes2021FixtureProfileException)
                {
                    // Fall through to the built-in default; surface errors only when the
                    // built-in cannot be parsed (which should never happen).
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Hard-coded mirror of the example profile. It guarantees the legacy dump commands
    /// keep working when no <c>profiles/</c> directory is shipped with the binary. Every
    /// offset and limit is the same one the example JSON advertises.
    /// </summary>
    public static Pes2021FixtureProfile BuildBuiltIn()
    {
        var layout = new Pes2021RecordLayout(
            Stride: 596,
            CompetitionIdOffset: 0,
            RoundOffset: 2,
            YearOffset: 4,
            MonthOffset: 6,
            DayOffset: 7,
            HomeTeamIdOffset: 16,
            HomeTeamLigaOffset: 18,
            AwayTeamIdOffset: 20,
            AwayTeamLigaOffset: 22,
            HomeScoreOffset: 24,
            AwayScoreOffset: 27);

        var calendar = new Pes2021CalendarLimits(
            DefaultBlockRecords: 1024,
            MaxBlockRecords: 2048,
            RecordLimit: 13014,
            MaxConsecutiveNonCompetitionRecords: 32);

        var validation = new Pes2021RecordValidation(
            MinimumYear: 2020,
            MaximumYear: 2040,
            MinimumRound: 0,
            MaximumRound: 80,
            TeamIdSentinels: new ushort[] { 0xFFFF });

        var regionFilter = new Pes2021RegionFilter(
            States: new[] { "Commit" },
            Types: new[] { "Private" },
            RequireReadable: true,
            RequireWritable: true,
            AllowExecutable: false,
            ChunkBytes: 1 << 20);

        var anchor = new Pes2021AnchorValidation(
            RecordsBefore: 8,
            RecordsAfter: 16,
            MinimumPlausibleRun: 4,
            MinimumCompetitionRun: 3,
            MediumScore: 8,
            HighScore: 12);

        var normalization = new Pes2021Normalization(
            Strategy: NormalizationStrategy.KnownSeasonStartIndex,
            KnownSeasonStartIndex: 12288,
            ValidationSampleIndices: new[] { 0, 12288, 12667 });

        var maps = new Pes2021ProfileMaps(
            CompetitionMapPath: "competition-map.example.csv",
            TeamMapPath: "competition-17-team-map.csv");

        return new Pes2021FixtureProfile(
            SchemaVersion: Pes2021FixtureProfileLoader.SupportedSchemaVersion,
            ProfileId: "pes2021-pc-reference-competition-17-builtin",
            ProfileVersion: "0.1.0-builtin",
            EvidenceStatus: "BUILTIN_DEFAULT",
            ProcessNames: new[] { "PES2021" },
            RecordLayout: layout,
            Calendar: calendar,
            RecordValidation: validation,
            RegionFilter: regionFilter,
            AnchorValidation: anchor,
            Normalization: normalization,
            Maps: maps,
            Sha256: string.Empty,
            SourcePath: "<builtin>");
    }
}
