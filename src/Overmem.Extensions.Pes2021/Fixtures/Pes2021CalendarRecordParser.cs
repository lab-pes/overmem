using System.Buffers.Binary;
using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Outcome of <see cref="Pes2021CalendarRecordParser.TryParse"/>. A successful parse yields
/// a <see cref="RawCalendarRecord"/>; a failure yields one of the stable rejection reasons
/// from <see cref="FixtureRejectionReasons"/> with the field offset for diagnostics.
/// </summary>
public sealed record Pes2021CalendarRecordParseResult(
    bool Success,
    RawCalendarRecord? Record,
    string? RejectionReason,
    int? RejectionOffset);

/// <summary>
/// Pure (no I/O, no file, no catalogs) parser for a single calendar record. All consumers of
/// the calendar go through this parser; the legacy per-record decode inside
/// <c>Pes2021AgendaService.TryReadRecordAsync</c> is being migrated to delegate here.
///
/// The parser is profile-driven: every offset, type, sentinel, and validation range comes
/// from <see cref="Pes2021FixtureProfile"/>.
/// </summary>
public static class Pes2021CalendarRecordParser
{
    public static Pes2021CalendarRecordParseResult TryParse(
        ReadOnlySpan<byte> buffer,
        int recordIndex,
        ulong address,
        Pes2021FixtureProfile profile)
    {
        if (buffer.Length < profile.Stride)
        {
            return new Pes2021CalendarRecordParseResult(
                false,
                null,
                FixtureRejectionReasons.PartialRead,
                null);
        }

        var layout = profile.RecordLayout;

        var competitionIdValue = ReadUInt16(buffer, layout.CompetitionIdOffset);
        if (competitionIdValue == CompetitionId.SentinelValue)
        {
            return new Pes2021CalendarRecordParseResult(
                false,
                null,
                FixtureRejectionReasons.WrongCompetition,
                layout.CompetitionIdOffset);
        }

        var round = buffer[layout.RoundOffset];
        if (round < profile.RecordValidation.MinimumRound || round > profile.RecordValidation.MaximumRound)
        {
            return new Pes2021CalendarRecordParseResult(
                false,
                null,
                FixtureRejectionReasons.ProfileConstraint,
                layout.RoundOffset);
        }

        var year = ReadUInt16(buffer, layout.YearOffset);
        if (year < profile.RecordValidation.MinimumYear || year > profile.RecordValidation.MaximumYear)
        {
            return new Pes2021CalendarRecordParseResult(
                false,
                null,
                FixtureRejectionReasons.InvalidDate,
                layout.YearOffset);
        }

        var month = buffer[layout.MonthOffset];
        var day = buffer[layout.DayOffset];
        if (!TryBuildDate(year, month, day, out var date))
        {
            return new Pes2021CalendarRecordParseResult(
                false,
                null,
                FixtureRejectionReasons.InvalidDate,
                layout.DayOffset);
        }

        var homeId = ReadUInt16(buffer, layout.HomeTeamIdOffset);
        var homeLiga = ReadUInt16(buffer, layout.HomeTeamLigaOffset);
        var awayId = ReadUInt16(buffer, layout.AwayTeamIdOffset);
        var awayLiga = ReadUInt16(buffer, layout.AwayTeamLigaOffset);

        if (IsSentinel(profile.RecordValidation.TeamIdSentinels, homeId)
            || IsSentinel(profile.RecordValidation.TeamIdSentinels, homeLiga)
            || IsSentinel(profile.RecordValidation.TeamIdSentinels, awayId)
            || IsSentinel(profile.RecordValidation.TeamIdSentinels, awayLiga))
        {
            return new Pes2021CalendarRecordParseResult(
                false,
                null,
                FixtureRejectionReasons.SentinelTeam,
                layout.HomeTeamIdOffset);
        }

        var homeScore = buffer[layout.HomeScoreOffset];
        var awayScore = buffer[layout.AwayScoreOffset];

        var record = new RawCalendarRecord(
            recordIndex,
            address,
            new CompetitionId(competitionIdValue),
            round,
            year,
            month,
            day,
            new TeamKey(homeId, homeLiga),
            new TeamKey(awayId, awayLiga),
            homeScore,
            awayScore);

        return new Pes2021CalendarRecordParseResult(true, record, null, null);
    }

    /// <summary>
    /// Parses a contiguous run of records from a single block. The block does not have to be
    /// a multiple of stride; the trailing partial slice is reported through
    /// <see cref="Pes2021CalendarRecordParseResult.RejectionReason"/> with
    /// <see cref="FixtureRejectionReasons.PartialRead"/>.
    /// </summary>
    public static IReadOnlyList<Pes2021CalendarRecordParseResult> ParseBlock(
        ReadOnlySpan<byte> block,
        ulong baseAddress,
        int startRecordIndex,
        Pes2021FixtureProfile profile)
    {
        var stride = profile.Stride;
        var recordCount = block.Length / stride;
        var remainder = block.Length - (recordCount * stride);
        var results = new List<Pes2021CalendarRecordParseResult>(recordCount + (remainder > 0 ? 1 : 0));

        for (var index = 0; index < recordCount; index++)
        {
            var slice = block.Slice(index * stride, stride);
            var address = checked(baseAddress + (ulong)(startRecordIndex + index) * (ulong)stride);
            results.Add(TryParse(slice, startRecordIndex + index, address, profile));
        }

        if (remainder > 0)
        {
            results.Add(new Pes2021CalendarRecordParseResult(
                false,
                null,
                FixtureRejectionReasons.PartialRead,
                remainder));
        }

        return results;
    }

    private static bool TryBuildDate(ushort year, byte month, byte day, out DateOnly date)
    {
        if (month < 1 || month > 12 || day < 1 || day > 31)
        {
            date = default;
            return false;
        }

        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }

    private static bool IsSentinel(IReadOnlyList<ushort> sentinels, ushort value)
    {
        for (var index = 0; index < sentinels.Count; index++)
        {
            if (sentinels[index] == value)
            {
                return true;
            }
        }

        return false;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> buffer, int offset)
        => BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset, 2));
}
