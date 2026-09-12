using System.ComponentModel;
using ModelContextProtocol.Server;
using Overmem.Abstractions.Processes;

namespace Overmem.Extensions.Pes2021.Tools;

/// <summary>Legacy gearlabs tool names delegating to the maintained calendar implementation.</summary>
[McpServerToolType]
public sealed class Pes2021LegacyCalendarTools(Pes2021AgendaService agendaService)
{
    [McpServerTool(Name = "pes2021_inspect_daily_calendar_candidate"), Description("Legacy alias for inspecting a secondary calendar candidate; returns the current secondary calendar schema.")]
    public Task<Pes2021SecondaryCalendarCandidateReport> InspectDailyCalendarCandidate(
        Guid attachmentId, ulong baseValue, IReadOnlyList<int>? sampleDays = null, CancellationToken cancellationToken = default)
        => agendaService.InspectSecondaryCalendarCandidateAsync(new AttachmentId(attachmentId), baseValue, sampleDays, cancellationToken);

    [McpServerTool(Name = "pes2021_scan_daily_calendar_candidates"), Description("Legacy alias for scanning secondary calendar candidates; returns the current secondary calendar schema.")]
    public Task<IReadOnlyList<Pes2021SecondaryCalendarCandidateReport>> ScanDailyCalendarCandidates(
        Guid attachmentId, ulong startValue, ulong stopValue, ulong step = 0x10, int maxResults = 10,
        IReadOnlyList<int>? sampleDays = null, CancellationToken cancellationToken = default)
        => agendaService.ScanSecondaryCalendarCandidatesAsync(new AttachmentId(attachmentId), startValue, stopValue, step, maxResults, sampleDays, cancellationToken);

    [McpServerTool(Name = "pes2021_find_daily_calendar_base_by_date"), Description("Legacy alias for finding the secondary calendar base by date.")]
    public Task<Pes2021SecondaryCalendarBaseResult> FindDailyCalendarBaseByDate(
        Guid attachmentId, int year, int month, int day, string? moduleName = null,
        int maxResults = 256, CancellationToken cancellationToken = default)
        => agendaService.FindSecondaryBaseByDateAsync(new AttachmentId(attachmentId), year, month, day, moduleName, maxResults, cancellationToken);

    [McpServerTool(Name = "pes2021_dump_daily_calendar_day"), Description("Legacy alias for dumping a secondary calendar day; returns the current secondary calendar schema.")]
    public Task<Pes2021SecondaryCalendarDayReport> DumpDailyCalendarDay(
        Guid attachmentId, int year, int month, int day, ulong? baseAddress = null, CancellationToken cancellationToken = default)
        => agendaService.DumpSecondaryDayAsync(new AttachmentId(attachmentId), year, month, day, baseAddress, cancellationToken);
}
