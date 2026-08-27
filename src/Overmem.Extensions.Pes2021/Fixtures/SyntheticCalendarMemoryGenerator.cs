using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// In-memory fixture used to drive P0–P4 tests without depending on a real PES 2021 dump.
/// The generator produces deterministic byte buffers shaped exactly like the live calendar
/// arrays: stride comes from the profile, all known fields are filled in, dates are real
/// calendar dates and team IDs include the high range (32768, 32784, 49169) that the legacy
/// <c>IsStrongRecord</c> used to reject. The fake gateway then exposes those bytes through
/// <see cref="Overmem.Abstractions.IProcessMemoryGateway.ReadAsync"/>.
/// </summary>
public static class SyntheticCalendarMemoryGenerator
{
    public sealed record SyntheticRecord(
        int RecordIndex,
        CompetitionId CompetitionId,
        byte Round,
        DateOnly Date,
        TeamKey Home,
        TeamKey Away,
        byte HomeScoreRaw = 0,
        byte AwayScoreRaw = 0);

    /// <summary>
    /// A logical block of synthetic bytes with a start address and a stride profile.
    /// </summary>
    public sealed record SyntheticBlock(
        ulong BaseAddress,
        Pes2021FixtureProfile Profile,
        byte[] Bytes,
        int RecordCount);

    public static byte[] BuildRecord(
        Pes2021FixtureProfile profile,
        SyntheticRecord record)
    {
        var bytes = new byte[profile.Stride];
        WriteRecord(profile, bytes, 0, record);
        return bytes;
    }

    public static void WriteRecord(
        Pes2021FixtureProfile profile,
        byte[] buffer,
        int offset,
        SyntheticRecord record)
    {
        if (buffer.Length - offset < profile.Stride)
        {
            throw new ArgumentException(
                $"Buffer length {buffer.Length} with offset {offset} cannot hold stride {profile.Stride}.",
                nameof(buffer));
        }

        var layout = profile.RecordLayout;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + layout.CompetitionIdOffset, 2), record.CompetitionId.Value);
        buffer[offset + layout.RoundOffset] = record.Round;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + layout.YearOffset, 2), (ushort)record.Date.Year);
        buffer[offset + layout.MonthOffset] = (byte)record.Date.Month;
        buffer[offset + layout.DayOffset] = (byte)record.Date.Day;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + layout.HomeTeamIdOffset, 2), record.Home.TeamId);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + layout.HomeTeamLigaOffset, 2), record.Home.TeamLiga);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + layout.AwayTeamIdOffset, 2), record.Away.TeamId);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset + layout.AwayTeamLigaOffset, 2), record.Away.TeamLiga);
        buffer[offset + layout.HomeScoreOffset] = record.HomeScoreRaw;
        buffer[offset + layout.AwayScoreOffset] = record.AwayScoreRaw;
    }

    /// <summary>
    /// Builds a single contiguous block of <paramref name="records"/> consecutive records,
    /// each one stride bytes apart, with the absolute address starting at
    /// <paramref name="baseAddress"/>.
    /// </summary>
    public static SyntheticBlock BuildContiguousBlock(
        Pes2021FixtureProfile profile,
        ulong baseAddress,
        IReadOnlyList<SyntheticRecord> records)
    {
        var bytes = new byte[profile.Stride * records.Count];
        for (var index = 0; index < records.Count; index++)
        {
            WriteRecord(profile, bytes, index * profile.Stride, records[index]);
        }

        return new SyntheticBlock(baseAddress, profile, bytes, records.Count);
    }

    /// <summary>
    /// Builds several contiguous blocks separated by an arbitrary gap (the gap becomes the
    /// region boundary used by the fake gateway).
    /// </summary>
    public static IReadOnlyList<SyntheticBlock> BuildRegionedBlocks(
        Pes2021FixtureProfile profile,
        ulong firstBaseAddress,
        IReadOnlyList<IReadOnlyList<SyntheticRecord>> blocks,
        ulong gapBytesBetweenRegions)
    {
        var list = new List<SyntheticBlock>(blocks.Count);
        var cursor = firstBaseAddress;
        foreach (var regionRecords in blocks)
        {
            var block = BuildContiguousBlock(profile, cursor, regionRecords);
            list.Add(block);
            checked
            {
                cursor += (ulong)block.Bytes.Length + gapBytesBetweenRegions;
            }
        }

        return list;
    }

    /// <summary>
    /// Convenience helper used by the acceptance tests: emits the 380-fixture, 20-team
    /// competition-17 baseline from the example team map. The base address is configurable
    /// so the fake gateway can map it to any region. The synthetic block is intentionally
    /// large enough to exercise block-reader code paths in tests.
    /// </summary>
    public static IReadOnlyList<SyntheticBlock> BuildCompetition17Baseline(
        Pes2021FixtureProfile profile,
        ulong baseAddress,
        DateOnly seasonStart)
    {
        var teams = new (TeamKey Key, string Name)[]
        {
            (new TeamKey(4, 1027), "CHAPECOENSE"),
            (new TeamKey(8, 312), "FLAMENGO"),
            (new TeamKey(11, 313), "INTERNACIONAL"),
            (new TeamKey(18, 34), "VASCO DA GAMA"),
            (new TeamKey(16385, 72), "ATLÉTICO MINEIRO"),
            (new TeamKey(16386, 613), "BAHIA"),
            (new TeamKey(16393, 312), "FLUMINENSE"),
            (new TeamKey(16396, 1019), "MIRASSOL"),
            (new TeamKey(16397, 34), "PALMEIRAS"),
            (new TeamKey(16399, 674), "REMO"),
            (new TeamKey(16403, 484), "VITÓRIA"),
            (new TeamKey(32768, 482), "ATHLETICO PARANAENSE"),
            (new TeamKey(32771, 311), "BOTAFOGO"),
            (new TeamKey(32775, 68), "CRUZEIRO"),
            (new TeamKey(32778, 312), "GRÊMIO"),
            (new TeamKey(32784, 313), "SANTOS"),
            (new TeamKey(49157, 311), "CORINTHIANS"),
            (new TeamKey(49158, 482), "CORITIBA"),
            (new TeamKey(49166, 614), "RED BULL BRAGANTINO"),
            (new TeamKey(49169, 313), "SÃO PAULO"),
        };

        var records = new List<SyntheticRecord>(380);
        var totalRounds = 38;
        var matchesPerRound = teams.Length / 2;
        var cursor = seasonStart;
        for (var round = 0; round < totalRounds; round++)
        {
            for (var match = 0; match < matchesPerRound; match++)
            {
                var home = teams[(round + match) % teams.Length];
                var away = teams[(round + match + 1) % teams.Length];
                records.Add(new SyntheticRecord(
                    RecordIndex: records.Count,
                    CompetitionId: new CompetitionId(17),
                    Round: (byte)round,
                    Date: cursor,
                    Home: home.Key,
                    Away: away.Key));
            }

            cursor = cursor.AddDays(3);
        }

        return BuildRegionedBlocks(profile, baseAddress, [records], gapBytesBetweenRegions: 0);
    }

    /// <summary>
    /// Builds an interleaved calendar that contains two competitions (the baseline plus a
    /// second one) so the parser/anchor finder must filter by <c>competitionId</c> rather
    /// than relying on stride position alone.
    /// </summary>
    public static IReadOnlyList<SyntheticBlock> BuildInterleavedTwoCompetitionCalendar(
        Pes2021FixtureProfile profile,
        ulong baseAddress,
        CompetitionId first,
        CompetitionId second,
        int recordsPerCompetition,
        DateOnly firstSeasonStart,
        DateOnly secondSeasonStart,
        TeamKey[] firstTeams,
        TeamKey[] secondTeams)
    {
        if (firstTeams.Length < 2 || secondTeams.Length < 2)
        {
            throw new ArgumentException("Each competition must provide at least two teams.");
        }

        var records = new List<SyntheticRecord>(recordsPerCompetition * 2);
        var firstCursor = firstSeasonStart;
        var secondCursor = secondSeasonStart;
        for (var index = 0; index < recordsPerCompetition; index++)
        {
            var homeFirst = firstTeams[index % firstTeams.Length];
            var awayFirst = firstTeams[(index + 1) % firstTeams.Length];
            var homeSecond = secondTeams[index % secondTeams.Length];
            var awaySecond = secondTeams[(index + 1) % secondTeams.Length];

            records.Add(new SyntheticRecord(records.Count, first, (byte)(index % 38), firstCursor, homeFirst, awayFirst));
            records.Add(new SyntheticRecord(records.Count, second, (byte)(index % 38), secondCursor, homeSecond, awaySecond));

            firstCursor = firstCursor.AddDays(1);
            secondCursor = secondCursor.AddDays(1);
        }

        return BuildRegionedBlocks(profile, baseAddress, [records], gapBytesBetweenRegions: 0);
    }

    /// <summary>
    /// Emits a block that intentionally includes the legacy <c>IsStrongRecord</c> traps:
    /// IDs above 5000, the <c>0xFFFF</c> sentinels on both teams, and impossible calendar
    /// dates. Used to prove the parser rejects them with stable reasons.
    /// </summary>
    public static SyntheticBlock BuildTrapBlock(
        Pes2021FixtureProfile profile,
        ulong baseAddress,
        DateOnly validDate)
    {
        var records = new List<SyntheticRecord>
        {
            new(0, new CompetitionId(17), 0, validDate, new TeamKey(0, 0), new TeamKey(5001, 0)),
            new(1, new CompetitionId(17), 1, validDate, new TeamKey(32784, 313), new TeamKey(0xFFFF, 0xFFFF)),
            new(2, new CompetitionId(99), 2, new DateOnly(2026, 2, 30), new TeamKey(49169, 313), new TeamKey(32768, 482)),
            new(3, new CompetitionId(17), 3, validDate, new TeamKey(49169, 313), new TeamKey(32768, 482)),
        };

        return BuildContiguousBlock(profile, baseAddress, records);
    }

    public static IReadOnlyList<SyntheticRecord> Merge(IEnumerable<SyntheticRecord> first, IEnumerable<SyntheticRecord> second)
        => first.Concat(second).Select((record, index) => record with { RecordIndex = index }).ToList();
}
