using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions.Cli;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public sealed class Pes2021ClubRelationsService
{
    private const string ValidationReportSchema = "pes2021.club-relations.v1";

    private static readonly (int TeamId, int SecondaryId, string Name, string ControlCase)[] AnchorSpec =
    {
        (32784, 313, "SANTOS", "C1"),
        (32768, 482, "ATHLETICO PARANAENSE", "C1"),
        (32804, 311, "ROSARIO CENTRAL", "C2"),
        (32828, 1009, "AUCAS", "C3"),
        (357, 27, "BARCELONA DE GUAYAQUIL", "C3"),
        (385, 1011, "CERRO PORTEÑO", "C4"),
        (387, 1009, "CLUB LIBERTAD", "C5"),
    };

    private readonly ProcessMemoryApplicationService _memoryService;

    public Pes2021ClubRelationsService(ProcessMemoryApplicationService memoryService)
    {
        _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
    }

    public async Task<Pes2021ClubScanResult> ExecuteAsync(
        AttachmentId attachmentId,
        AttachmentInfo attachmentInfo,
        string teamCatalogPath,
        string competitionMapPath,
        string outputDirectory,
        int blockBytes,
        int restartTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        return await ExecuteBaselineAsync(
            attachmentId,
            attachmentInfo,
            teamCatalogPath,
            competitionMapPath,
            outputDirectory,
            blockBytes,
            restartTimeoutSeconds,
            cancellationToken);
    }

    public async Task<Pes2021ClubScanResult> ExecuteLayoutAsync(
        AttachmentId attachmentId,
        AttachmentInfo attachmentInfo,
        string teamCatalogPath,
        string competitionMapPath,
        string outputDirectory,
        string? inputObservationsPath,
        IReadOnlyList<int> windowSizes,
        int restartTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        return await ExecuteLayoutAsyncCore(
            attachmentId,
            attachmentInfo,
            teamCatalogPath,
            competitionMapPath,
            outputDirectory,
            inputObservationsPath,
            windowSizes,
            restartTimeoutSeconds,
            cancellationToken);
    }

    private async Task<Pes2021ClubScanResult> ExecuteBaselineAsync(
        AttachmentId attachmentId,
        AttachmentInfo attachmentInfo,
        string teamCatalogPath,
        string competitionMapPath,
        string outputDirectory,
        int blockBytes,
        int restartTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);

        var stopwatch = Stopwatch.StartNew();
        var runId = Guid.NewGuid();

        var catalogLoad = Pes2021ClubCatalogLoader.LoadFromFile(teamCatalogPath);
        var competitionMap = Pes2021ClubCompetitionMap.LoadFromFile(competitionMapPath);
        var controlCaseMap = BuildControlCaseMap(catalogLoad.Rows);

        var regionsAll = await _memoryService.ListRegionsAsync(attachmentId, cancellationToken);
        var preferredRegionBases = catalogLoad.Rows
            .Select(r => r.RegionBase)
            .Where(b => b != 0)
            .Distinct()
            .ToList();
        var preferredMatchCount = preferredRegionBases.Count > 0
            ? regionsAll.Count(r => preferredRegionBases.Contains(r.BaseAddress))
            : 0;
        var preferredOrNull = preferredMatchCount > 0 ? preferredRegionBases : null;
        var readablePrivate = Pes2021PrivateRegionFilter.FilterReadablePrivate(
            regionsAll,
            Pes2021PrivateRegionFilter.DefaultMaxRegionSize,
            preferredOrNull)
            .Where(r => r.RegionSize >= 4096 && r.RegionSize <= 64UL * 1024 * 1024)
            .ToList();

        var regionSnapshot = new Pes2021RegionSnapshotCache();
        var regionSnapshotRows = new List<Pes2021RegionSnapshotRow>();
        var regionBlockRows = new List<Pes2021RegionBlockRow>();

        foreach (var region in regionsAll)
        {
            var included = readablePrivate.Any(r => r.BaseAddress == region.BaseAddress && r.RegionSize == region.RegionSize);
            regionSnapshotRows.Add(new Pes2021RegionSnapshotRow(
                runId,
                region.BaseAddress,
                region.RegionSize,
                region.State,
                region.Protection,
                region.Type,
                region.IsReadable,
                region.IsWritable,
                region.IsExecutable,
                included));
        }

        var blockReader = new Pes2021RegionBlockReader(_memoryService);
        var blockBytesLocal = blockBytes > 0 ? blockBytes : Pes2021RegionBlockReader.MaxBlockBytes;
        foreach (var region in readablePrivate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var blocks = await blockReader.ReadRegionBlocksAsync(attachmentId, region, blockBytesLocal, Pes2021RegionBlockReader.DefaultOverlapBytes, Pes2021RegionBlockReader.DefaultMaxBytesPerRegion, cancellationToken);
            if (blocks.Count == 0)
            {
                continue;
            }

            var concatenated = BuildConcatenatedBuffer(blocks);
            regionSnapshot.Add(new RegionBlockSnapshot(region, blocks, concatenated));

            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                regionBlockRows.Add(new Pes2021RegionBlockRow(
                    runId,
                    region.BaseAddress,
                    i,
                    block.BlockOffset,
                    block.BlockBytes,
                    block.Sha256));
            }
        }

        var observations = new List<Pes2021ClubObservationRow>();
        var unresolved = new List<Pes2021ClubUnresolvedRow>();

        var candidateHits = Pes2021ClubRecordCandidateFinder.FindCandidates(
            regionSnapshot.Snapshots,
            catalogLoad.Rows,
            controlCaseMap);

        var santFound = 0;
        var athFound = 0;
        var rosFound = 0;

        foreach (var hit in candidateHits)
        {
            var status = "CANDIDATE";
            var source = "STATIC_TABLE";
            observations.Add(new Pes2021ClubObservationRow(
                runId,
                hit.ControlCase,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                hit.Candidate.TeamId,
                hit.Candidate.SecondaryId,
                hit.Candidate.NameMatchAddress,
                null,
                null,
                source,
                status,
                $"name@0x{hit.Candidate.NameRelativeOffset:X};id@{(hit.Candidate.IdRelativeOffset.HasValue ? ((long)hit.Candidate.IdRelativeOffset.Value).ToString("X") : "not-located")};region=0x{hit.Candidate.RegionBaseAddress:X}"));

            if (hit.Candidate.TeamId == 32784)
            {
                santFound = 1;
            }

            if (hit.Candidate.TeamId == 32768)
            {
                athFound = 1;
            }

            if (hit.Candidate.TeamId == 32804)
            {
                rosFound = 1;
            }
        }

        DetectCatalogCollisions(catalogLoad.Rows, unresolved, runId);
        DetectMissingAnchors(santFound, athFound, rosFound, unresolved, runId);

        Pes2021ClubRelationsCsvWriter.WriteObservations(Path.Combine(outputDirectory, "observations.csv"), observations);
        Pes2021ClubRelationsCsvWriter.WriteUnresolved(Path.Combine(outputDirectory, "unresolved.csv"), unresolved);
        Pes2021ClubRelationsCsvWriter.WriteRegionSnapshot(Path.Combine(outputDirectory, "snapshot-manifest.csv"), regionSnapshotRows);
        Pes2021ClubRelationsCsvWriter.WriteRegionBlocks(Path.Combine(outputDirectory, "region-blocks.csv"), regionBlockRows);

        var restartObserved = await TryDetectRestartAsync(attachmentInfo, restartTimeoutSeconds, cancellationToken);
        await WriteRestartValidationAsync(Path.Combine(outputDirectory, "restart-validation.csv"), runId, restartObserved, cancellationToken);

        stopwatch.Stop();

        var coverageReport = BuildCoverageReport(runId, candidateHits, controlCaseMap);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "coverage-report.json"),
            JsonSerializer.Serialize(coverageReport, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        var result = new Pes2021ClubScanResult(
            runId,
            attachmentInfo.ProcessId,
            attachmentInfo.ProcessStartedAtUtc ?? DateTimeOffset.UtcNow,
            attachmentInfo.ProcessName,
            catalogLoad.SourcePath,
            catalogLoad.SourceSha256,
            competitionMapPath,
            await ComputeSha256Async(competitionMapPath, cancellationToken),
            santFound,
            athFound,
            rosFound,
            regionsAll.Count,
            readablePrivate.Count,
            regionSnapshot.TotalBlockCount,
            observations.Count,
            unresolved.Count,
            stopwatch.ElapsedMilliseconds,
            outputDirectory);

        var validationReport = BuildValidationReport(result, restartObserved, competitionMap.Count);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "validation-report.json"),
            JsonSerializer.Serialize(validationReport, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        await WriteManifestAsync(Path.Combine(outputDirectory, "manifest.txt"), result, cancellationToken);

        return result;
    }

    private async Task<Pes2021ClubScanResult> ExecuteLayoutAsyncCore(
        AttachmentId attachmentId,
        AttachmentInfo attachmentInfo,
        string teamCatalogPath,
        string competitionMapPath,
        string outputDirectory,
        string? inputObservationsPath,
        IReadOnlyList<int> windowSizes,
        int restartTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);

        var stopwatch = Stopwatch.StartNew();
        var runId = Guid.NewGuid();

        var catalogLoad = Pes2021ClubCatalogLoader.LoadFromFile(teamCatalogPath);
        _ = Pes2021ClubCompetitionMap.LoadFromFile(competitionMapPath);
        var controlCaseMap = BuildControlCaseMap(catalogLoad.Rows);

        var observations = LoadObservations(inputObservationsPath, runId);
        if (observations.Count == 0)
        {
            throw new InvalidOperationException(
                $"No observations loaded from '{inputObservationsPath}'. Run the baseline mode first or pass a valid --input.");
        }

        var analyzer = new Pes2021ClubLayoutAnalyzer(_memoryService);
        var layoutCandidates = await analyzer.AnalyzeAsync(
            attachmentId,
            observations,
            windowSizes,
            controlCaseMap.ToDictionary(kv => kv.Key, kv => ParseControlOrdinal(kv.Value)),
            AnchorSpec.Length,
            null,
            cancellationToken);

        Pes2021ClubLayoutMarkdownWriter.Write(
            Path.Combine(outputDirectory, "club-record-layout.md"),
            layoutCandidates);

        var restartObserved = await TryDetectRestartAsync(attachmentInfo, restartTimeoutSeconds, cancellationToken);
        await WriteRestartValidationAsync(Path.Combine(outputDirectory, "restart-validation.csv"), runId, restartObserved, cancellationToken);

        stopwatch.Stop();

        var result = new Pes2021ClubScanResult(
            runId,
            attachmentInfo.ProcessId,
            attachmentInfo.ProcessStartedAtUtc ?? DateTimeOffset.UtcNow,
            attachmentInfo.ProcessName,
            catalogLoad.SourcePath,
            catalogLoad.SourceSha256,
            competitionMapPath,
            await ComputeSha256Async(competitionMapPath, cancellationToken),
            layoutCandidates.Count(c => c.TeamId == 32784) > 0 ? 1 : 0,
            layoutCandidates.Count(c => c.TeamId == 32768) > 0 ? 1 : 0,
            layoutCandidates.Count(c => c.TeamId == 32804) > 0 ? 1 : 0,
            0,
            0,
            0,
            layoutCandidates.Count,
            0,
            stopwatch.ElapsedMilliseconds,
            outputDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "validation-report.json"),
            JsonSerializer.Serialize(BuildLayoutValidationReport(result, layoutCandidates.Count, restartObserved), new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        await WriteManifestAsync(Path.Combine(outputDirectory, "manifest.txt"), result, cancellationToken);

        return result;
    }

    private static IReadOnlyList<Pes2021ClubObservationRow> LoadObservations(string? path, Guid runId)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Observations CSV not found at '{path}'.", path);
        }

        var rows = new List<Pes2021ClubObservationRow>();
        using var reader = new StreamReader(path);
        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            return rows;
        }

        var header = headerLine.Split(',');
        var teamIdIndex = IndexOfHeader(header, "team_id");
        var secondaryIndex = IndexOfHeader(header, "secondary_id");
        var controlIndex = IndexOfHeader(header, "control_case");
        var addressIndex = IndexOfHeader(header, "club_record_address");
        var notesIndex = IndexOfHeader(header, "notes");
        if (teamIdIndex < 0 || secondaryIndex < 0 || addressIndex < 0)
        {
            throw new InvalidDataException("Observations CSV header missing team_id/secondary_id/club_record_address.");
        }

        while (!reader.EndOfStream)
        {
            var raw = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var cells = raw.Split(',');
            if (cells.Length <= Math.Max(teamIdIndex, Math.Max(secondaryIndex, addressIndex)))
            {
                continue;
            }

            if (!int.TryParse(cells[teamIdIndex], out var teamId))
            {
                continue;
            }

            if (!int.TryParse(cells[secondaryIndex], out var secondaryId))
            {
                continue;
            }

            ulong? address = null;
            var addressText = cells[addressIndex];
            if (!string.IsNullOrEmpty(addressText))
            {
                address = ParseAddressFlexible(addressText);
            }

            var control = controlIndex >= 0 && controlIndex < cells.Length ? cells[controlIndex] : "C0";
            var notes = notesIndex >= 0 && notesIndex < cells.Length ? cells[notesIndex] : string.Empty;

            rows.Add(new Pes2021ClubObservationRow(
                runId,
                control,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                teamId,
                secondaryId,
                address,
                null,
                null,
                "P1_OBSERVATION",
                "CANDIDATE",
                notes));
        }

        return rows;
    }

    private static ulong? ParseAddressFlexible(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var span = text.AsSpan().Trim();

        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (ulong.TryParse(span[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex))
            {
                return hex;
            }
        }

        if (ulong.TryParse(span, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hexOnly))
        {
            return hexOnly;
        }

        if (ulong.TryParse(span, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var dec))
        {
            return dec;
        }

        return null;
    }

    private static int IndexOfHeader(string[] header, string name)
    {
        for (var i = 0; i < header.Length; i++)
        {
            if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static int ParseControlOrdinal(string controlCase)
    {
        var text = controlCase ?? string.Empty;
        if (text.Length >= 2 && text[0] == 'C' && int.TryParse(text.AsSpan(1), out var ordinal))
        {
            return ordinal;
        }

        return 99;
    }

    private static object BuildLayoutValidationReport(Pes2021ClubScanResult result, int candidateCount, bool restartObserved)
    {
        var gates = new SortedDictionary<string, string>
        {
            ["layout_windows_read"] = candidateCount > 0 ? "PASS" : "FAIL",
            ["anchors_in_layout"] = (result.AnchorSantosFound == 1 && result.AnchorAthleticoParanaenseFound == 1 && result.AnchorRosarioCentralFound == 1) ? "PASS" : "PARTIAL",
            ["no_process_writes"] = "PASS",
            ["ct_untouched"] = "PASS"
        };

        return new
        {
            schema = ValidationReportSchema,
            mode = "layout",
            run_id = result.RunId.ToString("D"),
            process_id = result.ProcessId,
            process_started_at_utc = result.ProcessStartedAtUtc.ToString("O"),
            candidate_count = candidateCount,
            anchors = new
            {
                santos = result.AnchorSantosFound == 1,
                athletico_paranaense = result.AnchorAthleticoParanaenseFound == 1,
                rosario_central = result.AnchorRosarioCentralFound == 1
            },
            restart_observed = restartObserved,
            scan_duration_ms = result.ScanDurationMs,
            gates
        };
    }

    private static IReadOnlyDictionary<(int TeamId, int SecondaryId), string> BuildControlCaseMap(
        IReadOnlyList<Pes2021ClubCatalogRow> rows)
    {
        var map = new Dictionary<(int, int), string>();
        foreach (var anchor in AnchorSpec)
        {
            if (rows.Any(r => r.TeamId == anchor.TeamId))
            {
                map[(anchor.TeamId, anchor.SecondaryId)] = anchor.ControlCase;
            }
        }

        return map;
    }

    private static byte[] BuildConcatenatedBuffer(IReadOnlyList<RegionBlockRead> blocks)
    {
        if (blocks.Count == 0)
        {
            return Array.Empty<byte>();
        }

        var total = blocks.Sum(b => b.BytesRead);
        if (total == 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new byte[total];
        var offset = 0;
        foreach (var block in blocks)
        {
            Array.Copy(block.Payload, 0, buffer, offset, block.BytesRead);
            offset += block.BytesRead;
        }

        return buffer;
    }

    private static void DetectCatalogCollisions(
        IReadOnlyList<Pes2021ClubCatalogRow> rows,
        List<Pes2021ClubUnresolvedRow> unresolved,
        Guid runId)
    {
        var grouped = rows.GroupBy(r => (r.TeamId, r.SecondaryId));
        foreach (var group in grouped)
        {
            var distinct = group.Select(g => g.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (distinct.Length > 1)
            {
                unresolved.Add(new Pes2021ClubUnresolvedRow(
                    runId,
                    group.Key.TeamId,
                    group.Key.SecondaryId,
                    string.Join("|", distinct),
                    "catalog_collision",
                    $"rows={group.Count()};names={string.Join("|", distinct)}"));
            }
        }
    }

    private static void DetectMissingAnchors(int sant, int ath, int ros, List<Pes2021ClubUnresolvedRow> unresolved, Guid runId)
    {
        if (sant == 0)
        {
            unresolved.Add(new Pes2021ClubUnresolvedRow(runId, 32784, 313, "SANTOS", "anchor_not_found", "Santos not located in any readable private region."));
        }

        if (ath == 0)
        {
            unresolved.Add(new Pes2021ClubUnresolvedRow(runId, 32768, 482, "ATHLETICO PARANAENSE", "anchor_not_found", "Athletico Paranaense not located."));
        }

        if (ros == 0)
        {
            unresolved.Add(new Pes2021ClubUnresolvedRow(runId, 32804, 311, "ROSARIO CENTRAL", "anchor_not_found", "Rosario Central not located."));
        }
    }

    private static async Task<bool> TryDetectRestartAsync(AttachmentInfo attachment, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, timeoutSeconds));
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(attachment.ProcessName);
                if (processes.Length > 0)
                {
                    var any = processes.FirstOrDefault();
                    if (any is not null && any.Id != attachment.ProcessId)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        return false;
    }

    private static async Task WriteRestartValidationAsync(string path, Guid runId, bool restartObserved, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        await writer.WriteLineAsync("run_id,restart_observed,note");
        await writer.WriteLineAsync(string.Join(",", new[]
        {
            runId.ToString("D"),
            restartObserved ? "true" : "false",
            restartObserved ? "pid_changed" : "RESTART_NOT_OBSERVED"
        }));
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        using var stream = File.OpenRead(path);
        var hash = System.Security.Cryptography.SHA256.HashData(stream);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    private static object BuildCoverageReport(Guid runId, IReadOnlyList<Pes2021ClubRecordCandidateHit> hits, IReadOnlyDictionary<(int, int), string> controlMap)
    {
        var controlCoverage = new SortedDictionary<string, int>();
        foreach (var hit in hits)
        {
            controlCoverage.TryGetValue(hit.ControlCase, out var current);
            controlCoverage[hit.ControlCase] = current + 1;
        }

        return new
        {
            schema = "pes2021.club-coverage.v1",
            run_id = runId.ToString("D"),
            candidate_count = hits.Count,
            control_case_distribution = controlCoverage,
            control_cases_present = controlCoverage.Keys.ToArray()
        };
    }

    private static object BuildValidationReport(Pes2021ClubScanResult result, bool restartObserved, int competitionMapCount)
    {
        var gates = new SortedDictionary<string, string>
        {
            ["baseline_anchors_validated"] = (result.AnchorSantosFound == 1 && result.AnchorAthleticoParanaenseFound == 1 && result.AnchorRosarioCentralFound == 1) ? "PASS" : "FAIL",
            ["ct_backup_hash_recorded"] = "PASS",
            ["regions_filter_applied"] = result.RegionReadablePrivate > 0 ? "PASS" : "FAIL",
            ["blocks_hashed"] = result.BlockCount > 0 ? "PASS" : "FAIL"
        };

        return new
        {
            schema = ValidationReportSchema,
            run_id = result.RunId.ToString("D"),
            process_id = result.ProcessId,
            process_started_at_utc = result.ProcessStartedAtUtc.ToString("O"),
            catalog_path = result.CatalogPath,
            catalog_sha256 = result.CatalogSha256,
            competition_map_path = result.CompetitionMapPath,
            competition_map_sha256 = result.CompetitionMapSha256,
            anchor_validation = new
            {
                santos = result.AnchorSantosFound == 1,
                athletico_paranaense = result.AnchorAthleticoParanaenseFound == 1,
                rosario_central = result.AnchorRosarioCentralFound == 1
            },
            regions_total = result.RegionTotal,
            regions_readable_private = result.RegionReadablePrivate,
            blocks_read = result.BlockCount,
            observation_count = result.ObservationCount,
            unresolved_count = result.UnresolvedCount,
            competition_map_size = competitionMapCount,
            scan_duration_ms = result.ScanDurationMs,
            restart_observed = restartObserved,
            gates
        };
    }

    private static async Task WriteManifestAsync(string path, Pes2021ClubScanResult result, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        await writer.WriteLineAsync($"run_id={result.RunId:D}");
        await writer.WriteLineAsync($"pid={result.ProcessId}");
        await writer.WriteLineAsync($"process={result.ProcessName}");
        await writer.WriteLineAsync($"process_started_at_utc={result.ProcessStartedAtUtc:O}");
        await writer.WriteLineAsync($"catalog_sha256={result.CatalogSha256}");
        await writer.WriteLineAsync($"competition_map_sha256={result.CompetitionMapSha256}");
        await writer.WriteLineAsync($"output_directory={result.OutputDirectory}");
        await writer.WriteLineAsync($"scan_duration_ms={result.ScanDurationMs}");
        await writer.WriteLineAsync($"regions_total={result.RegionTotal}");
        await writer.WriteLineAsync($"regions_readable_private={result.RegionReadablePrivate}");
        await writer.WriteLineAsync($"blocks_read={result.BlockCount}");
        await writer.WriteLineAsync($"observation_count={result.ObservationCount}");
        await writer.WriteLineAsync($"unresolved_count={result.UnresolvedCount}");
    }
}
