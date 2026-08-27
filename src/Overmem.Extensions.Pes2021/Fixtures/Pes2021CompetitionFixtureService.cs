using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// One-shot request for the competition fixture extractor. The service validates the
/// combination of inputs before touching memory: either an explicit address (validated
/// against the profile before reuse) or a competition/team pair for discovery. Mixing a
/// full base address with another base address is rejected.
/// </summary>
public sealed record CompetitionFixtureExtractionRequest(
    CompetitionId CompetitionId,
    ushort? TeamId,
    ushort? TeamLiga,
    ulong? CalendarArrayBaseAddress,
    ulong? CompetitionBlockBaseAddress,
    ulong? AnchorAddress,
    string? ProfilePath,
    string? CompetitionMapPath,
    string? TeamMapPath,
    int? BlockRecords,
    int? RecordLimit);

/// <summary>
/// Stable error codes emitted by the extractor. They match the codes listed in
/// <c>api.md</c> so the CLI/MCP surface can render them verbatim.
/// </summary>
public static class FixtureExtractorErrorCodes
{
    public const string ProfileInvalid = "PES2021_PROFILE_INVALID";
    public const string InputInvalid = "PES2021_INPUT_INVALID";
    public const string NoScanRegion = "PES2021_NO_SCAN_REGION";
    public const string AnchorNotFound = "PES2021_ANCHOR_NOT_FOUND";
    public const string AnchorAmbiguous = "PES2021_ANCHOR_AMBIGUOUS";
    public const string BaseInvalid = "PES2021_BASE_INVALID";
    public const string PartialRead = "PES2021_PARTIAL_READ";
    public const string CatalogInvalid = "PES2021_CATALOG_INVALID";
    public const string ExtractionEmpty = "PES2021_EXTRACTION_EMPTY";
}

/// <summary>
/// Orchestrator for the PES 2021 competition fixture extraction. Resolves the profile and
/// the catalogs, decides whether the address is provided, cached or discovered, walks the
/// competition block in chunks, applies name resolution, sorts deterministically and
/// builds the v1 payload. The service depends only on <see cref="IProcessMemoryGateway"/>;
/// it never calls <c>WriteAsync</c>.
/// </summary>
public sealed class Pes2021CompetitionFixtureService
{
    private readonly ProcessMemoryApplicationService _memoryService;
    private readonly IProcessMemoryGateway _gateway;
    private readonly ISystemClock _clock;
    private readonly Pes2021CalendarSessionCache _cache;
    private readonly Pes2021FixtureAnchorFinder _anchorFinder;

    public Pes2021CompetitionFixtureService(
        ProcessMemoryApplicationService memoryService,
        ISystemClock clock,
        Pes2021CalendarSessionCache? cache = null)
    {
        _memoryService = memoryService;
        _gateway = memoryService.Gateway;
        _clock = clock;
        _cache = cache ?? new Pes2021CalendarSessionCache(_gateway, clock);
        _anchorFinder = new Pes2021FixtureAnchorFinder(_gateway, clock);
    }

    /// <summary>
    /// Removes any cached session that belongs to <paramref name="attachmentId"/>. The CLI
    /// calls this on detach so a stale entry cannot survive an explicit end-of-session.
    /// </summary>
    public void InvalidateAttachment(AttachmentId attachmentId)
        => _cache.InvalidateByAttachment(attachmentId);

    /// <summary>
    /// Discovers the anchor for the requested competition/team pair. The result mirrors
    /// the CLI/MCP payload and is the building block used by the extractor.
    /// </summary>
    public async Task<FixtureAnchorResult> FindFixtureAnchorAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021FixtureProfile profile,
        CompetitionId competitionId,
        ushort teamId,
        ushort? teamLiga,
        CancellationToken cancellationToken)
    {
        ValidateInput(competitionId, teamId, teamLiga);

        var cacheKey = new CalendarSessionCacheKey(
            attachmentId,
            process.ProcessId,
            process.ProcessStartedAtUtc,
            profile.ProfileId,
            profile.ProfileVersion,
            profile.Sha256);
        var cached = _cache.TryGet(cacheKey, out var existing);
        var reuse = cached ? await _cache.TryReuseAsync(cacheKey, cancellationToken) : CacheDisposition.Refused;
        if (cached && reuse == CacheDisposition.Reused)
        {
            var session = BuildSession(process, profile, existing, CacheDisposition.Reused);
            return new FixtureAnchorResult(
                Session: session,
                CompetitionId: competitionId,
                RequestedTeamKey: teamLiga.HasValue ? new TeamKey(teamId, teamLiga.Value) : null,
                RequestedTeamId: teamId,
                AnchorAddress: existing?.AnchorAddress,
                CompetitionBlockBaseAddress: existing?.CompetitionBlockBaseAddress,
                CalendarArrayBaseAddress: existing?.CalendarArrayBaseAddress,
                AnchorIndex: existing?.AnchorIndex ?? 0,
                Confidence: new DiscoveryConfidence("medium", 0, 0, ["cache_reused"]),
                Candidates: Array.Empty<AnchorCandidate>(),
                Diagnostics: EmptyDiagnostics(CacheDisposition.Reused));
        }

        var regions = await _gateway.ListRegionsAsync(attachmentId, cancellationToken);
        var discovery = await _anchorFinder.FindAsync(
            attachmentId,
            process,
            profile,
            competitionId,
            teamId,
            teamLiga,
            regions,
            teamKeyFrequencies: null,
            cancellationToken);

        if (!string.IsNullOrEmpty(discovery.AnchorAddress))
        {
            var entry = new CalendarSessionCacheEntry(
                Disposition: CacheDisposition.Discovered,
                AnchorAddress: discovery.AnchorAddress!,
                CompetitionBlockBaseAddress: discovery.CompetitionBlockBaseAddress ?? discovery.AnchorAddress!,
                CalendarArrayBaseAddress: discovery.CalendarArrayBaseAddress,
                AnchorIndex: discovery.AnchorIndex ?? 0,
                ValidationSampleSha256: discovery.Session.ValidationSampleSha256,
                ValidatedAtUtc: discovery.Session.ValidatedAtUtc);
            _cache.Store(cacheKey, entry);
        }

        return discovery;
    }

    /// <summary>
    /// Runs the full extraction: profile + catalogs + anchor + block read + name resolution
    /// + deterministic sort. The returned payload is ready to be serialized as
    /// <c>pes2021.competition-fixtures.v1</c>.
    /// </summary>
    public async Task<CompetitionFixtureExtractionResult> ExtractCompetitionFixturesAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        CompetitionFixtureExtractionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateInput(request.CompetitionId, request.TeamId, request.TeamLiga);
        ValidateAddressCombination(request);

        var profile = LoadProfile(request.ProfilePath, request);
        var collector = new Pes2021ExtractionDiagnosticsCollector();
        var stageLoad = collector.BeginStage("load_profile_catalogs");

        Pes2021FixtureCatalog catalog;
        try
        {
            var profileDirectory = string.IsNullOrEmpty(profile.SourcePath) || profile.SourcePath == "<builtin>"
                ? Environment.CurrentDirectory
                : Path.GetDirectoryName(profile.SourcePath) ?? Environment.CurrentDirectory;
            catalog = Pes2021FixtureCatalogLoader.Load(
                request.CompetitionMapPath ?? profile.Maps.CompetitionMapPath,
                request.TeamMapPath ?? profile.Maps.TeamMapPath,
                profileDirectory,
                profileDirectory);
        }
        finally
        {
            stageLoad.Dispose();
        }

        foreach (var warning in catalog.CompetitionWarnings)
        {
            collector.AddWarning($"competition:{warning}");
        }

        foreach (var warning in catalog.TeamWarnings)
        {
            collector.AddWarning($"team:{warning}");
        }

        if (!string.IsNullOrWhiteSpace(request.CompetitionMapPath) && catalog.CompetitionEntries.Count == 0)
        {
            throw new Pes2021FixtureExtractionException(
                FixtureExtractorErrorCodes.CatalogInvalid,
                $"Competition map '{request.CompetitionMapPath}' produced no valid entries.");
        }

        if (!string.IsNullOrWhiteSpace(request.TeamMapPath) && catalog.TeamEntries.Count == 0)
        {
            throw new Pes2021FixtureExtractionException(
                FixtureExtractorErrorCodes.CatalogInvalid,
                $"Team map '{request.TeamMapPath}' produced no valid entries.");
        }

        var regions = await _gateway.ListRegionsAsync(attachmentId, cancellationToken);
        collector.AddRegions(BuildRegionDiagnostics(regions, profile.RegionFilter));

        var (anchorAddressHex, competitionBlockBaseHex, arrayBaseHex, anchorIndex, cacheDisposition) = await ResolveAnchorAsync(
            attachmentId,
            process,
            profile,
            request,
            regions,
            cancellationToken);
        collector.CacheDisposition = cacheDisposition;

        var stageRead = collector.BeginStage("read_blocks");
        var blockRecords = ClampBlockRecords(request.BlockRecords, profile.Calendar);
        var recordLimit = ClampRecordLimit(request.RecordLimit, profile.Calendar);

        var baseAddressHex = arrayBaseHex ?? competitionBlockBaseHex;
        if (!TryParseHex(baseAddressHex, out var baseAddress))
        {
            throw new Pes2021FixtureExtractionException(
                FixtureExtractorErrorCodes.BaseInvalid,
                $"Anchor address '{baseAddressHex}' is not a valid hex value.");
        }

        var accepted = new List<(Fixture Fixture, TeamKey Home, TeamKey Away, int RawIndex)>();
        var rejected = 0;
        var recordsDecoded = 0;
        var readCalls = 0;
        var bytesRequested = 0UL;
        var bytesRead = 0UL;
        var blockReadCount = 0;
        var consecutiveOther = 0;

        await foreach (var block in Pes2021CalendarBlockReader.ReadCalendarRecordBlocksAsync(
            _memoryService,
            attachmentId,
            baseAddress,
            recordLimit,
            profile,
            regions,
            cancellationToken))
        {
            blockReadCount++;
            readCalls += block.Metrics.ReadCalls;
            bytesRequested += block.Metrics.BytesRequested;
            bytesRead += block.Metrics.BytesRead;

            var results = Pes2021CalendarRecordParser.ParseBlock(block.Bytes, block.BaseAddress, block.StartRecordIndex, profile);
            foreach (var parseResult in results)
            {
                recordsDecoded++;
                if (!parseResult.Success || parseResult.Record is null)
                {
                    rejected++;
                    if (parseResult.RejectionReason is not null)
                    {
                        collector.AddRejection(parseResult.RejectionReason);
                    }

                    continue;
                }

                var parsed = parseResult.Record;
                if (parsed.CompetitionId.Value != request.CompetitionId.Value)
                {
                    rejected++;
                    collector.AddRejection(FixtureRejectionReasons.WrongCompetition);
                    consecutiveOther++;
                    if (consecutiveOther >= profile.Calendar.MaxConsecutiveNonCompetitionRecords)
                    {
                        break;
                    }

                    continue;
                }

                consecutiveOther = 0;
                if (!TryBuildDate(parsed.Year, parsed.Month, parsed.Day, out var date))
                {
                    rejected++;
                    collector.AddRejection(FixtureRejectionReasons.InvalidDate);
                    continue;
                }

                var homeParticipant = Pes2021FixtureNameResolver.Resolve(parsed.Home, catalog);
                var awayParticipant = Pes2021FixtureNameResolver.Resolve(parsed.Away, catalog);
                var scoreState = (parsed.HomeScoreRaw == 0 && parsed.AwayScoreRaw == 0)
                    ? RawScoreState.RawZeroOrUnplayed
                    : RawScoreState.RawNonzeroUnvalidated;

                accepted.Add((
                    new Fixture(
                        RecordIndex: parsed.RecordIndex,
                        Address: string.Create(CultureInfo.InvariantCulture, $"0x{parsed.Address:X}"),
                        CompetitionId: parsed.CompetitionId,
                        Round: parsed.Round,
                        Date: date,
                        Home: homeParticipant,
                        Away: awayParticipant,
                        HomeScoreRaw: parsed.HomeScoreRaw,
                        AwayScoreRaw: parsed.AwayScoreRaw,
                        ScoreState: scoreState),
                    parsed.Home,
                    parsed.Away,
                    parsed.RecordIndex));
            }

            if (consecutiveOther >= profile.Calendar.MaxConsecutiveNonCompetitionRecords)
            {
                break;
            }

            if (block.FailureReason is FixtureRejectionReasons.PartialRead)
            {
                throw new Pes2021FixtureExtractionException(
                    FixtureExtractorErrorCodes.PartialRead,
                    $"Partial read at 0x{block.BaseAddress:X} after {block.RecordCount} records.");
            }

            if (block.FailureReason is not null
                && block.FailureReason != FixtureRejectionReasons.OutsideRegion)
            {
                break;
            }

            if (block.RecordCount == 0)
            {
                break;
            }
        }

        stageRead.Dispose();

        collector.AddReadCall((int)bytesRequested, (int)bytesRead);
        collector.AddRecords(recordsDecoded, accepted.Count, rejected);

        var orderedFixtures = accepted
            .OrderBy(tuple => tuple.Fixture.Date)
            .ThenBy(tuple => tuple.Fixture.Round)
            .ThenBy(tuple => tuple.RawIndex)
            .ThenBy(tuple => tuple.Fixture.Home.Key.TeamId)
            .ThenBy(tuple => tuple.Fixture.Away.Key.TeamId)
            .Select(tuple => tuple.Fixture)
            .ToArray();

        var unresolvedKeys = Pes2021FixtureNameResolver.SortUnresolved(
            orderedFixtures
                .SelectMany(fixture => new[] { fixture.Home, fixture.Away })
                .Where(participant => !participant.Key.IsValid || string.IsNullOrEmpty(participant.Name))
                .Select(participant => participant.Key));

        var recordIndexOrigin = arrayBaseHex is not null ? "calendar_array_base" : "competition_block_base";
        var session = BuildSession(
            process,
            profile,
            new CalendarSessionCacheEntry(
                cacheDisposition,
                anchorAddressHex,
                competitionBlockBaseHex,
                arrayBaseHex,
                anchorIndex,
                ValidationSampleSha256: string.Empty,
                ValidatedAtUtc: _clock.UtcNow),
            cacheDisposition);

        var competitionName = catalog.CompetitionEntries
            .FirstOrDefault(entry => entry.CompetitionId.Value == request.CompetitionId.Value)?.Name;
        var competitionNameStatus = competitionName is not null
            ? NameResolutionStatus.ExactComposite
            : NameResolutionStatus.Unresolved;

        var distinctTeams = orderedFixtures
            .SelectMany(fixture => new[] { fixture.Home.Key, fixture.Away.Key })
            .Where(key => key.IsValid)
            .Distinct()
            .Count();

        if (orderedFixtures.Length == 0)
        {
            collector.AddWarning("extraction_empty");
        }

        return new CompetitionFixtureExtractionResult(
            SchemaVersion: CompetitionFixtureExtractionResult.CurrentSchemaVersion,
            Status: FixtureExtractionStatus.FixturesOnly,
            Warning: CompetitionFixtureExtractionResult.CurrentWarning,
            Session: session,
            CompetitionId: request.CompetitionId,
            CompetitionName: competitionName,
            CompetitionNameStatus: competitionNameStatus,
            RecordIndexOrigin: recordIndexOrigin,
            FixtureCount: orderedFixtures.Length,
            DistinctTeamCount: distinctTeams,
            UnresolvedTeamKeys: unresolvedKeys,
            CatalogConflicts: catalog.TeamConflicts,
            Fixtures: orderedFixtures,
            Diagnostics: collector.Build());
    }

    private async Task<(string AnchorAddress, string CompetitionBlockBaseAddress, string? CalendarArrayBaseAddress, int AnchorIndex, CacheDisposition CacheDisposition)> ResolveAnchorAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021FixtureProfile profile,
        CompetitionFixtureExtractionRequest request,
        IReadOnlyList<MemoryRegionInfo> regions,
        CancellationToken cancellationToken)
    {
        if (request.AnchorAddress.HasValue && request.AnchorAddress.Value > 0)
        {
            var anchorHex = ToHex(request.AnchorAddress.Value);
            var probe = await ValidateAnchorAsync(attachmentId, request.AnchorAddress.Value, request.CompetitionId, profile, cancellationToken);
            if (!probe.success)
            {
                throw new Pes2021FixtureExtractionException(
                    FixtureExtractorErrorCodes.BaseInvalid,
                    $"Provided anchor 0x{request.AnchorAddress.Value:X} does not decode as competition {request.CompetitionId.Value}.");
            }

            var blockBaseHex = request.CompetitionBlockBaseAddress.HasValue
                ? ToHex(request.CompetitionBlockBaseAddress.Value)
                : anchorHex;
            var arrayHex = request.CalendarArrayBaseAddress.HasValue
                ? ToHex(request.CalendarArrayBaseAddress.Value)
                : (profile.Normalization.Strategy == NormalizationStrategy.CompetitionBlockOnly ? null : blockBaseHex);

            return (anchorHex, blockBaseHex, arrayHex, probe.anchorIndex, CacheDisposition.ProvidedAddress);
        }

        if (!request.TeamId.HasValue)
        {
            throw new Pes2021FixtureExtractionException(
                FixtureExtractorErrorCodes.InputInvalid,
                "Provide --team-id or --anchor-address to locate the calendar.");
        }

        var discovery = await FindFixtureAnchorAsync(
            attachmentId,
            process,
            profile,
            request.CompetitionId,
            request.TeamId.Value,
            request.TeamLiga,
            cancellationToken);

        if (string.IsNullOrEmpty(discovery.AnchorAddress))
        {
            if (discovery.Candidates.Count > 1)
            {
                throw new Pes2021FixtureExtractionException(
                    FixtureExtractorErrorCodes.AnchorAmbiguous,
                    $"Anchor discovery returned {discovery.Candidates.Count} tied candidates.");
            }

            throw new Pes2021FixtureExtractionException(
                FixtureExtractorErrorCodes.AnchorNotFound,
                $"No valid anchor found for competition {request.CompetitionId.Value} and teamId {request.TeamId.Value}.");
        }

        return (
            discovery.AnchorAddress!,
            discovery.CompetitionBlockBaseAddress ?? discovery.AnchorAddress!,
            discovery.CalendarArrayBaseAddress,
            discovery.AnchorIndex ?? 0,
            discovery.Session.CacheDisposition);
    }

    private async Task<(bool success, int anchorIndex)> ValidateAnchorAsync(
        AttachmentId attachmentId,
        ulong address,
        CompetitionId competitionId,
        Pes2021FixtureProfile profile,
        CancellationToken cancellationToken)
    {
        var bytes = await ReadSliceAsync(attachmentId, address, profile.Stride, cancellationToken);
        if (bytes is null)
        {
            return (false, 0);
        }

        var parseResult = Pes2021CalendarRecordParser.TryParse(bytes, 0, address, profile);
        if (!parseResult.Success || parseResult.Record is null)
        {
            return (false, 0);
        }

        if (parseResult.Record.CompetitionId.Value != competitionId.Value)
        {
            return (false, 0);
        }

        return (true, 0);
    }

    private static Pes2021FixtureProfile LoadProfile(string? profilePath, CompetitionFixtureExtractionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(profilePath))
        {
            return Pes2021FixtureProfileLoader.LoadFromFile(profilePath);
        }

        if (request.BlockRecords is > 0 || request.RecordLimit is > 0 || !string.IsNullOrEmpty(request.CompetitionMapPath) || !string.IsNullOrEmpty(request.TeamMapPath))
        {
            return Pes2021FixtureProfileDefaults.GetOrLoad();
        }

        return Pes2021FixtureProfileDefaults.GetOrLoad();
    }

    private static int ClampBlockRecords(int? requested, Pes2021CalendarLimits limits)
    {
        var value = requested ?? limits.DefaultBlockRecords;
        if (value <= 0)
        {
            value = limits.DefaultBlockRecords;
        }

        return Math.Min(value, limits.MaxBlockRecords);
    }

    private static int ClampRecordLimit(int? requested, Pes2021CalendarLimits limits)
    {
        var value = requested ?? limits.RecordLimit;
        if (value <= 0)
        {
            value = limits.RecordLimit;
        }

        return Math.Min(value, limits.RecordLimit);
    }

    private static CalendarSession BuildSession(
        ProcessInstanceIdentity process,
        Pes2021FixtureProfile profile,
        CalendarSessionCacheEntry? entry,
        CacheDisposition disposition)
    {
        return new CalendarSession(
            Process: process,
            ProfileId: profile.ProfileId,
            ProfileVersion: profile.ProfileVersion,
            ProfileSha256: profile.Sha256,
            RecordStride: profile.Stride,
            RecordLimit: profile.Calendar.RecordLimit,
            CalendarArrayBaseAddress: entry?.CalendarArrayBaseAddress,
            CompetitionBlockBaseAddress: entry?.CompetitionBlockBaseAddress ?? string.Empty,
            AnchorAddress: entry?.AnchorAddress ?? string.Empty,
            AnchorIndex: entry?.AnchorIndex ?? 0,
            ValidationSampleSha256: entry?.ValidationSampleSha256 ?? string.Empty,
            ValidatedAtUtc: entry?.ValidatedAtUtc ?? DateTimeOffset.MinValue,
            CacheDisposition: disposition);
    }

    private static ExtractionDiagnostics EmptyDiagnostics(CacheDisposition disposition)
        => new(disposition, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(),
            new Dictionary<string, double>(),
            Array.Empty<RegionDiagnostic>(),
            Array.Empty<string>());

    private static IReadOnlyList<RegionDiagnostic> BuildRegionDiagnostics(
        IReadOnlyList<MemoryRegionInfo> regions,
        Pes2021RegionFilter filter)
    {
        var list = new List<RegionDiagnostic>(regions.Count);
        foreach (var region in regions)
        {
            var decision = "accepted";
            string? reason = null;
            if (filter.RequireReadable && !region.IsReadable)
            {
                decision = "rejected";
                reason = "not_readable";
            }
            else if (filter.RequireWritable && !region.IsWritable)
            {
                decision = "rejected";
                reason = "not_writable";
            }
            else if (!filter.AllowExecutable && region.IsExecutable)
            {
                decision = "rejected";
                reason = "executable_disallowed";
            }

            list.Add(new RegionDiagnostic(
                BaseAddress: ToHex(region.BaseAddress),
                StopAddress: ToHex(region.BaseAddress + region.RegionSize),
                Size: region.RegionSize,
                State: region.State,
                Type: region.Type,
                Protection: region.Protection,
                Readable: region.IsReadable,
                Writable: region.IsWritable,
                Executable: region.IsExecutable,
                Decision: decision,
                Reason: reason));
        }

        return list;
    }

    private async Task<byte[]?> ReadSliceAsync(AttachmentId attachmentId, ulong address, int stride, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _gateway.ReadAsync(
                new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, stride),
                cancellationToken);
            return Convert.FromHexString(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static string ToHex(ulong value)
        => string.Create(CultureInfo.InvariantCulture, $"0x{value:X}");

    private static bool TryParseHex(string text, out ulong value)
    {
        if (text.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return ulong.TryParse(text, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryBuildDate(ushort year, byte month, byte day, out DateOnly date)
    {
        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch
        {
            date = default;
            return false;
        }
    }

    private static void ValidateInput(CompetitionId competitionId, ushort? teamId, ushort? teamLiga)
    {
        if (!competitionId.IsValid)
        {
            throw new Pes2021FixtureExtractionException(
                FixtureExtractorErrorCodes.InputInvalid,
                $"competitionId 0x{competitionId.Value:X} is the reserved sentinel.");
        }

        if (teamId.HasValue && teamId.Value == TeamKey.SentinelValue)
        {
            throw new Pes2021FixtureExtractionException(
                FixtureExtractorErrorCodes.InputInvalid,
                "teamId 0xFFFF is the reserved sentinel.");
        }

        if (teamLiga.HasValue && teamLiga.Value == TeamKey.SentinelValue)
        {
            throw new Pes2021FixtureExtractionException(
                FixtureExtractorErrorCodes.InputInvalid,
                "teamLiga 0xFFFF is the reserved sentinel.");
        }
    }

    private static void ValidateAddressCombination(CompetitionFixtureExtractionRequest request)
    {
        var providedCount = new[] { request.CalendarArrayBaseAddress, request.CompetitionBlockBaseAddress, request.AnchorAddress }
            .Count(value => value.HasValue && value.Value > 0);
        if (providedCount > 1 && request.CalendarArrayBaseAddress.HasValue && request.CompetitionBlockBaseAddress.HasValue)
        {
            throw new Pes2021FixtureExtractionException(
                FixtureExtractorErrorCodes.InputInvalid,
                "Provide at most one of --calendar-base-address and --competition-block-base-address.");
        }
    }
}

/// <summary>
/// Thrown by <see cref="Pes2021CompetitionFixtureService"/> when the request fails a
/// pre-condition, the catalog is unusable, the anchor cannot be resolved or a partial read
/// happened. The error code matches one of the codes listed in <c>api.md</c>.
/// </summary>
public sealed class Pes2021FixtureExtractionException : InvalidOperationException
{
    public Pes2021FixtureExtractionException(string code, string message)
        : base($"[{code}] {message}")
    {
        Code = code;
    }

    public string Code { get; }
}
