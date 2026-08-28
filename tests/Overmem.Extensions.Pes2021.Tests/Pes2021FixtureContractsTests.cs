using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Overmem.Abstractions;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Application.Freezing;
using Overmem.Extensions.Pes2021.Cli;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Runtime;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021FixtureContractsTests
{
    [Fact]
    public void CompetitionId_RejectsSentinel_AndSerializesAsJsonNumber()
    {
        var id = new CompetitionId(17);
        Assert.True(id.IsValid);
        Assert.Equal("17", id.ToString());

        var sentinel = new CompetitionId(CompetitionId.SentinelValue);
        Assert.False(sentinel.IsValid);

        Assert.Equal(CompetitionId.FromUInt16(7), new CompetitionId(7));
        Assert.NotEqual(new CompetitionId(7), new CompetitionId(8));
        Assert.Equal(new CompetitionId(7).GetHashCode(), new CompetitionId(7).GetHashCode());
    }

    [Fact]
    public void TeamKey_RejectsSentinel_AndSerializesAsJsonObject()
    {
        var key = new TeamKey(32784, 313);
        Assert.True(key.IsValid);
        Assert.Equal("32784/313", key.ToString());

        var sentinel = new TeamKey(TeamKey.SentinelValue, 0);
        Assert.False(sentinel.IsValid);

        Assert.Equal(new TeamKey(1, 2), new TeamKey(1, 2));
        Assert.NotEqual(new TeamKey(1, 2), new TeamKey(2, 1));
    }

    [Fact]
    public void Parser_AcceptsAllDocumentedTeamIds_AndRejectsSentinel()
    {
        var profile = SampleProfile();
        var date = new DateOnly(2026, 4, 18);
        foreach (var teamId in new ushort[] { 0, 5000, 5001, 32768, 49169, 65534 })
        {
            var bytes = BuildRecord(profile, competitionId: 17, round: 1, date: date,
                homeId: teamId, homeLiga: 0, awayId: teamId, awayLiga: 0);
            var result = Pes2021CalendarRecordParser.TryParse(bytes, 0, 0, profile);
            Assert.True(result.Success, $"teamId {teamId} should be accepted");
            Assert.NotNull(result.Record);
        }

        var sentinel = BuildRecord(profile, competitionId: 17, round: 1, date: date,
            homeId: 0xFFFF, homeLiga: 0, awayId: 1, awayLiga: 0);
        var sentinelResult = Pes2021CalendarRecordParser.TryParse(sentinel, 0, 0, profile);
        Assert.False(sentinelResult.Success);
        Assert.Equal(FixtureRejectionReasons.SentinelTeam, sentinelResult.RejectionReason);
    }

    [Fact]
    public void Parser_BuildsRealDateOnly_AndRejectsImpossibleDate()
    {
        var profile = SampleProfile();
        var validBytes = BuildRecord(profile, competitionId: 17, round: 1,
            date: new DateOnly(2024, 2, 29), homeId: 1, homeLiga: 1, awayId: 2, awayLiga: 2);
        var validResult = Pes2021CalendarRecordParser.TryParse(validBytes, 0, 0, profile);
        Assert.True(validResult.Success);

        var impossible = BuildRecord(profile, competitionId: 17, round: 1,
            year: 2026, month: 4, day: 31, homeId: 1, homeLiga: 1, awayId: 2, awayLiga: 2);
        var impossibleResult = Pes2021CalendarRecordParser.TryParse(impossible, 0, 0, profile);
        Assert.False(impossibleResult.Success);
        Assert.Equal(FixtureRejectionReasons.InvalidDate, impossibleResult.RejectionReason);
    }

    [Fact]
    public void Parser_HonorsLittleEndianLayout()
    {
        var profile = SampleProfile();
        var bytes = new byte[profile.Stride];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(profile.RecordLayout.CompetitionIdOffset, 2), 0x0011);
        bytes[profile.RecordLayout.RoundOffset] = 0x05;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(profile.RecordLayout.YearOffset, 2), 2026);
        bytes[profile.RecordLayout.MonthOffset] = 7;
        bytes[profile.RecordLayout.DayOffset] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(profile.RecordLayout.HomeTeamIdOffset, 2), 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(profile.RecordLayout.HomeTeamLigaOffset, 2), 0x0139);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(profile.RecordLayout.AwayTeamIdOffset, 2), 0x0001);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(profile.RecordLayout.AwayTeamLigaOffset, 2), 0x0002);
        bytes[profile.RecordLayout.HomeScoreOffset] = 0x03;
        bytes[profile.RecordLayout.AwayScoreOffset] = 0x01;

        var result = Pes2021CalendarRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.True(result.Success);
        Assert.NotNull(result.Record);
        Assert.Equal(new CompetitionId(0x0011), result.Record!.CompetitionId);
        Assert.Equal(new TeamKey(0x8000, 0x0139), result.Record.Home);
        Assert.Equal(new TeamKey(0x0001, 0x0002), result.Record.Away);
        Assert.Equal(3, result.Record.HomeScoreRaw);
        Assert.Equal(1, result.Record.AwayScoreRaw);
    }

    [Fact]
    public void Parser_ParseBlock_FlagsPartialTrailingBytes()
    {
        var profile = SampleProfile();
        var block = new byte[profile.Stride + 17];
        var validRecord = BuildRecord(profile, 17, 1, new DateOnly(2026, 4, 18), 1, 1, 2, 2);
        Array.Copy(validRecord, 0, block, 0, profile.Stride);
        var results = Pes2021CalendarRecordParser.ParseBlock(block, baseAddress: 0, startRecordIndex: 0, profile);
        Assert.Single(results, static r => r.Success);
        var partial = results.Last();
        Assert.False(partial.Success);
        Assert.Equal(FixtureRejectionReasons.PartialRead, partial.RejectionReason);
    }

    [Fact]
    public void ProfileLoader_ValidatesOffsetsWithinStride()
    {
        const string invalidJson = """
        {
          "schemaVersion": "pes2021.fixture-profile.v1",
          "profileId": "test",
          "profileVersion": "0.1.0",
          "recordLayout": {
            "stride": 8,
            "competitionId": { "offset": 0, "type": "u16le" },
            "round": { "offset": 1, "type": "u8" },
            "year": { "offset": 2, "type": "u16le" },
            "month": { "offset": 4, "type": "u8" },
            "day": { "offset": 5, "type": "u8" },
            "homeTeamId": { "offset": 6, "type": "u16le" },
            "homeTeamLiga": { "offset": 8, "type": "u16le" },
            "awayTeamId": { "offset": 10, "type": "u16le" },
            "awayTeamLiga": { "offset": 12, "type": "u16le" },
            "homeScoreRaw": { "offset": 14, "type": "u8" },
            "awayScoreRaw": { "offset": 15, "type": "u8" }
          },
          "calendar": { "defaultBlockRecords": 16, "maxBlockRecords": 32, "recordLimit": 100, "maxConsecutiveNonCompetitionRecords": 4 },
          "recordValidation": { "minimumYear": 2020, "maximumYear": 2040, "minimumRound": 0, "maximumRound": 80 },
          "regionFilter": { "states": ["Commit"], "types": ["Private"], "requireReadable": true, "requireWritable": true, "allowExecutable": false, "chunkBytes": 1024 },
          "anchorValidation": { "recordsBefore": 2, "recordsAfter": 4, "minimumPlausibleRun": 2, "minimumCompetitionRun": 2, "mediumScore": 4, "highScore": 8 },
          "normalization": { "strategy": "competition-block-only", "validationSampleIndices": [] }
        }
        """;

        var bytes = Encoding.UTF8.GetBytes(invalidJson);
        var exception = Assert.Throws<Pes2021FixtureProfileException>(() => Pes2021FixtureProfileLoader.LoadFromBytes(bytes, "<inline>"));
        Assert.Equal("PES2021_PROFILE_INVALID", exception.Code);
        Assert.Contains("outside stride", exception.Message);
    }

    [Fact]
    public async Task ProfileLoader_RejectsUnknownStrategy()
    {
        var profile = SampleProfile();
        var badProfile = new
        {
            schemaVersion = profile.SchemaVersion,
            profileId = profile.ProfileId,
            profileVersion = profile.ProfileVersion,
            evidenceStatus = profile.EvidenceStatus,
            processNames = profile.ProcessNames,
            recordLayout = new
            {
                stride = profile.RecordLayout.Stride,
                competitionId = new { offset = profile.RecordLayout.CompetitionIdOffset, type = "u16le" },
                round = new { offset = profile.RecordLayout.RoundOffset, type = "u8" },
                year = new { offset = profile.RecordLayout.YearOffset, type = "u16le" },
                month = new { offset = profile.RecordLayout.MonthOffset, type = "u8" },
                day = new { offset = profile.RecordLayout.DayOffset, type = "u8" },
                homeTeamId = new { offset = profile.RecordLayout.HomeTeamIdOffset, type = "u16le" },
                homeTeamLiga = new { offset = profile.RecordLayout.HomeTeamLigaOffset, type = "u16le" },
                awayTeamId = new { offset = profile.RecordLayout.AwayTeamIdOffset, type = "u16le" },
                awayTeamLiga = new { offset = profile.RecordLayout.AwayTeamLigaOffset, type = "u16le" },
                homeScoreRaw = new { offset = profile.RecordLayout.HomeScoreOffset, type = "u8" },
                awayScoreRaw = new { offset = profile.RecordLayout.AwayScoreOffset, type = "u8" },
            },
            calendar = new
            {
                defaultBlockRecords = profile.Calendar.DefaultBlockRecords,
                maxBlockRecords = profile.Calendar.MaxBlockRecords,
                recordLimit = profile.Calendar.RecordLimit,
                maxConsecutiveNonCompetitionRecords = profile.Calendar.MaxConsecutiveNonCompetitionRecords,
            },
            recordValidation = new
            {
                minimumYear = profile.RecordValidation.MinimumYear,
                maximumYear = profile.RecordValidation.MaximumYear,
                minimumRound = profile.RecordValidation.MinimumRound,
                maximumRound = profile.RecordValidation.MaximumRound,
            },
            regionFilter = new
            {
                states = profile.RegionFilter.States,
                types = profile.RegionFilter.Types,
                requireReadable = profile.RegionFilter.RequireReadable,
                requireWritable = profile.RegionFilter.RequireWritable,
                allowExecutable = profile.RegionFilter.AllowExecutable,
                chunkBytes = profile.RegionFilter.ChunkBytes,
            },
            anchorValidation = new
            {
                recordsBefore = profile.AnchorValidation.RecordsBefore,
                recordsAfter = profile.AnchorValidation.RecordsAfter,
                minimumPlausibleRun = profile.AnchorValidation.MinimumPlausibleRun,
                minimumCompetitionRun = profile.AnchorValidation.MinimumCompetitionRun,
                mediumScore = profile.AnchorValidation.MediumScore,
                highScore = profile.AnchorValidation.HighScore,
            },
            normalization = new
            {
                strategy = "unknown-strategy",
                validationSampleIndices = profile.Normalization.ValidationSampleIndices,
            }
        };
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(badProfile);
        Assert.Throws<Pes2021FixtureProfileException>(() => Pes2021FixtureProfileLoader.LoadFromBytes(bytes, "<inline>"));
    }

    [Fact]
    public void CatalogLoader_AcceptsHeaderAndAliases_AndReportsConflicts()
    {
        var tempCompetition = Path.GetTempFileName();
        var tempTeam = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempCompetition, "competition_id,name\n17,REF\n");
            File.WriteAllText(tempTeam, "team_id,secondary_id,name,evidence_status\n32784,313,SANTOS,OBSERVED\n32784,313,SANTOS_DUP,OBSERVED\n32768,481,WRONG,OBSERVED\n");
            var catalog = Pes2021FixtureCatalogLoader.Load(tempCompetition, tempTeam);
            Assert.Single(catalog.CompetitionEntries);
            Assert.Contains(catalog.TeamWarnings, w => w.Contains("team_map_liga_alias_used:secondary_id"));

            var santosKey = new TeamKey(32784, 313);
            var conflict = Assert.Single(catalog.TeamConflicts);
            Assert.Equal(santosKey, conflict.Key);
            Assert.Contains("SANTOS", conflict.ConflictingNames);
            Assert.Contains("SANTOS_DUP", conflict.ConflictingNames);
        }
        finally
        {
            File.Delete(tempCompetition);
            File.Delete(tempTeam);
        }
    }

    [Fact]
    public void NameResolver_ProducesExactComposite_AndFallback_AndAmbiguous()
    {
        var catalog = new Pes2021FixtureCatalog(
            CompetitionMapPath: null, CompetitionMapSha256: null,
            CompetitionEntries: Array.Empty<CompetitionMapEntry>(),
            CompetitionWarnings: Array.Empty<string>(),
            TeamMapPath: null, TeamMapSha256: null,
            TeamEntries: new[]
            {
                new TeamMapEntry(new TeamKey(32784, 313), "SANTOS", null, null, null, "<test>", "<test>"),
                new TeamMapEntry(new TeamKey(32768, 482), "ATHLETICO PARANAENSE", null, null, null, "<test>", "<test>"),
                new TeamMapEntry(new TeamKey(32768, 481), "ATHLETICO PARANAENSE 2", null, null, null, "<test>", "<test>"),
                new TeamMapEntry(new TeamKey(4, 1027), "CHAPECOENSE", null, null, null, "<test>", "<test>"),
            },
            TeamConflicts: Array.Empty<CatalogConflict>(),
            TeamWarnings: Array.Empty<string>());

        var exact = Pes2021FixtureNameResolver.Resolve(new TeamKey(32784, 313), catalog);
        Assert.Equal(NameResolutionStatus.ExactComposite, exact.ResolutionStatus);
        Assert.Equal("SANTOS", exact.Name);

        var fallback = Pes2021FixtureNameResolver.Resolve(new TeamKey(4, 999), catalog);
        Assert.Equal(NameResolutionStatus.UniqueTeamIdFallback, fallback.ResolutionStatus);
        Assert.Equal("CHAPECOENSE", fallback.Name);

        var ambiguous = Pes2021FixtureNameResolver.Resolve(new TeamKey(32768, 999), catalog);
        Assert.Equal(NameResolutionStatus.Ambiguous, ambiguous.ResolutionStatus);
        Assert.Null(ambiguous.Name);

        var isolated = Pes2021FixtureNameResolver.Resolve(new TeamKey(99, 999), catalog);
        Assert.Equal(NameResolutionStatus.Unresolved, isolated.ResolutionStatus);

        var sentinel = Pes2021FixtureNameResolver.Resolve(new TeamKey(0xFFFF, 0), catalog);
        Assert.Equal(NameResolutionStatus.Unresolved, sentinel.ResolutionStatus);

        var conflictingCatalog = catalog with
        {
            TeamConflicts = new[] { new CatalogConflict(new TeamKey(32768, 482), new[] { "ATHLETICO PARANAENSE", "ATHLETICO" }, new[] { "<test>" }) }
        };
        var conflicting = Pes2021FixtureNameResolver.Resolve(new TeamKey(32768, 482), conflictingCatalog);
        Assert.Equal(NameResolutionStatus.Conflict, conflicting.ResolutionStatus);
    }

    [Fact]
    public void AtomicFileWriter_WritesAtomicallyAndDoesNotLeakTempFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"atomic-{Guid.NewGuid():N}.json");
        try
        {
            Pes2021AtomicFileWriter.WriteJson(tempFile, new { hello = "world" }, new System.Text.Json.JsonSerializerOptions());
            Assert.True(File.Exists(tempFile));
            Assert.False(File.Exists(tempFile + ".tmp"));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("hello", content);

            Pes2021AtomicFileWriter.WriteJson(tempFile, new { hello = "again" }, new System.Text.Json.JsonSerializerOptions());
            Assert.True(File.Exists(tempFile));
            Assert.False(File.Exists(tempFile + ".tmp"));
            content = File.ReadAllText(tempFile);
            Assert.Contains("again", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(tempFile + ".tmp")) File.Delete(tempFile + ".tmp");
        }
    }

    [Fact]
    public void DiagnosticsCollector_AggregatesCountersAndStageTimings()
    {
        var collector = new Pes2021ExtractionDiagnosticsCollector();
        collector.CacheDisposition = CacheDisposition.Discovered;
        collector.AddReadCall(1024, 1024);
        collector.AddReadCall(512, 512);
        collector.AddRecords(decoded: 10, accepted: 8, rejected: 2);
        collector.AddRejection(FixtureRejectionReasons.WrongCompetition);
        collector.AddRejection(FixtureRejectionReasons.SentinelTeam);
        collector.AddWarning("hello");
        using (collector.BeginStage("anchor"))
        {
            using (collector.BeginStage("inner"))
            {
            }
        }
        var diagnostics = collector.Build();
        Assert.Equal(2, diagnostics.ReadCalls);
        Assert.Equal(2, diagnostics.BlocksRead);
        Assert.Equal(1536UL, diagnostics.BytesRequested);
        Assert.Equal(1536UL, diagnostics.BytesRead);
        Assert.Equal(10, diagnostics.RecordsDecoded);
        Assert.Equal(8, diagnostics.RecordsAccepted);
        Assert.Equal(2, diagnostics.RecordsRejected);
        Assert.Equal(1, diagnostics.RejectionReasons[FixtureRejectionReasons.WrongCompetition]);
        Assert.Equal(1, diagnostics.RejectionReasons[FixtureRejectionReasons.SentinelTeam]);
        Assert.Single(diagnostics.Warnings);
        Assert.Contains("anchor", diagnostics.StageDurationMs.Keys);
        Assert.Contains("inner", diagnostics.StageDurationMs.Keys);
    }

    [Fact]
    public void CacheKey_ComparesAllIdentityFields()
    {
        var baseKey = new CalendarSessionCacheKey(new AttachmentId(Guid.NewGuid()), 1, null, "id", "v", "sha");
        var sameKey = new CalendarSessionCacheKey(baseKey.AttachmentId, 1, null, "id", "v", "sha");
        Assert.Equal(baseKey, sameKey);

        var differentPid = baseKey with { ProcessId = 2 };
        Assert.NotEqual(baseKey, differentPid);

        var differentStart = baseKey with { ProcessStartedAtUtc = DateTimeOffset.UtcNow };
        Assert.NotEqual(baseKey, differentStart);

        var differentProfile = baseKey with { ProfileId = "other" };
        Assert.NotEqual(baseKey, differentProfile);
    }

    [Fact]
    public async Task BlockReader_SpansDiscontiguousRegions()
    {
        var gateway = new NoWriteGateway();
        gateway.AddRegion(0x1000, 0x254, 17, new DateOnly(2026, 1, 31), homeId: (ushort)1, awayId: (ushort)2);
        gateway.AddRegion(0x1254, 0x254, 17, new DateOnly(2026, 7, 2), homeId: (ushort)3, awayId: (ushort)4);

        var service = CreateService(gateway);
        var summary = await service.CalendarSummaryAsync(default, 0x1000);
        Assert.Equal(2, summary.TotalMatches);
        Assert.Equal(2, summary.Dates.Count);
    }

    [Fact]
    public async Task BlockReader_PropagatesPartialReadAsError()
    {
        var gateway = new NoWriteGateway();
        gateway.AddRegion(0x1000, 0x254 / 2, 17, new DateOnly(2026, 1, 31), homeId: (ushort)1, awayId: (ushort)2);

        var service = CreateService(gateway);
        var summary = await service.CalendarSummaryAsync(default, 0x1000);
        Assert.Equal(0, summary.TotalMatches);
    }

    [Fact]
    public async Task FixtureService_ProducesFixuresOnlyStatus_AndSortsDeterministically()
    {
        var gateway = new NoWriteGateway();
        gateway.AddRegion(0x1000, 0x254, 17,
            new DateOnly(2026, 4, 18), (ushort)32784, (ushort)32768, homeLiga: 313, awayLiga: 482,
            round: 1, homeScore: 0, awayScore: 0);
        gateway.AddRegion(0x1000 + 0x254, 0x254, 17,
            new DateOnly(2026, 4, 18), (ushort)32784, (ushort)32768, homeLiga: 313, awayLiga: 482,
            round: 0, homeScore: 0, awayScore: 0);

        var service = CreateFixtureService(gateway);
        var tempCompetition = Path.GetTempFileName();
        var tempTeam = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempCompetition, "competition_id,name\n17,REF\n");
            File.WriteAllText(tempTeam, "team_id,team_liga,name\n32784,313,SANTOS\n32768,482,ATHLETICO PARANAENSE\n");
            var identity = new ProcessInstanceIdentity(new AttachmentId(Guid.NewGuid()), 0, null, "PES2021");
            var request = new CompetitionFixtureExtractionRequest(
                CompetitionId: new CompetitionId(17),
                TeamId: 32784,
                TeamLiga: 313,
                CalendarArrayBaseAddress: null,
                CompetitionBlockBaseAddress: null,
                AnchorAddress: 0x1000UL,
                ProfilePath: null,
                CompetitionMapPath: tempCompetition,
                TeamMapPath: tempTeam,
                BlockRecords: null,
                RecordLimit: null);
            var result = await service.ExtractCompetitionFixturesAsync(new AttachmentId(Guid.NewGuid()), identity, request, default);
            Assert.Equal(CompetitionFixtureExtractionResult.CurrentSchemaVersion, result.SchemaVersion);
            Assert.Equal(FixtureExtractionStatus.FixturesOnly, result.Status);
            Assert.Equal(CompetitionFixtureExtractionResult.CurrentWarning, result.Warning);
            Assert.Equal("calendar_array_base", result.RecordIndexOrigin);
            Assert.Equal(2, result.FixtureCount);
            Assert.Equal(2, result.DistinctTeamCount);
            Assert.Empty(result.CatalogConflicts);
            Assert.Equal("REF", result.CompetitionName);
            Assert.Equal(0, result.Fixtures[0].Round);
            Assert.Equal(1, result.Fixtures[1].Round);
            Assert.All(result.Fixtures, f => Assert.Equal(new CompetitionId(17), f.CompetitionId));
            Assert.All(result.Fixtures, f => Assert.NotEqual(0xFFFF, f.Home.Key.TeamId));
            Assert.All(result.Fixtures, f => Assert.NotEqual(0xFFFF, f.Away.Key.TeamId));
            Assert.Equal("SANTOS", result.Fixtures[0].Home.Name);
            Assert.Equal("ATHLETICO PARANAENSE", result.Fixtures[0].Away.Name);
            Assert.Contains(result.Diagnostics.StageDurationMs.Keys, k => k == "read_blocks");
        }
        finally
        {
            File.Delete(tempCompetition);
            File.Delete(tempTeam);
        }
    }

    [Fact]
    public async Task FixtureService_AmbiguousCatalogEntries_AreReportedAndNeverResolveName()
    {
        var gateway = new NoWriteGateway();
        gateway.AddRegion(0x1000, 0x254, 17,
            new DateOnly(2026, 4, 18), (ushort)32784, (ushort)32768, homeLiga: 313, awayLiga: 482);

        var service = CreateFixtureService(gateway);
        var tempCompetition = Path.GetTempFileName();
        var tempTeam = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempCompetition, "competition_id,name\n17,REF\n");
            File.WriteAllText(tempTeam, "team_id,team_liga,name\n32784,313,SANTOS\n32784,313,SANTOS_DUP\n");
            var identity = new ProcessInstanceIdentity(new AttachmentId(Guid.NewGuid()), 0, null, "PES2021");
            var request = new CompetitionFixtureExtractionRequest(
                CompetitionId: new CompetitionId(17),
                TeamId: 32784,
                TeamLiga: 313,
                CalendarArrayBaseAddress: null,
                CompetitionBlockBaseAddress: null,
                AnchorAddress: 0x1000UL,
                ProfilePath: null,
                CompetitionMapPath: tempCompetition,
                TeamMapPath: tempTeam,
                BlockRecords: null,
                RecordLimit: null);
            var result = await service.ExtractCompetitionFixturesAsync(new AttachmentId(Guid.NewGuid()), identity, request, default);
            Assert.Equal(NameResolutionStatus.Conflict, result.Fixtures[0].Home.ResolutionStatus);
            Assert.Single(result.CatalogConflicts);
            Assert.Equal(new TeamKey(32784, 313), result.CatalogConflicts[0].Key);
        }
        finally
        {
            File.Delete(tempCompetition);
            File.Delete(tempTeam);
        }
    }

    [Fact]
    public async Task FixtureService_TeamIdFallbackOnlyWhenSingleEntry()
    {
        var gateway = new NoWriteGateway();
        gateway.AddRegion(0x1000, 0x254, 17,
            new DateOnly(2026, 4, 18), (ushort)4, (ushort)32768, homeLiga: 1027, awayLiga: 482);

        var service = CreateFixtureService(gateway);
        var tempCompetition = Path.GetTempFileName();
        var tempTeam = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempCompetition, "competition_id,name\n17,REF\n");
            File.WriteAllText(tempTeam, "team_id,team_liga,name\n4,1027,CHAPECOENSE\n32768,482,ATHLETICO PARANAENSE\n");
            var identity = new ProcessInstanceIdentity(new AttachmentId(Guid.NewGuid()), 0, null, "PES2021");
            var request = new CompetitionFixtureExtractionRequest(
                CompetitionId: new CompetitionId(17),
                TeamId: 32784,
                TeamLiga: 313,
                CalendarArrayBaseAddress: null,
                CompetitionBlockBaseAddress: null,
                AnchorAddress: 0x1000UL,
                ProfilePath: null,
                CompetitionMapPath: tempCompetition,
                TeamMapPath: tempTeam,
                BlockRecords: null,
                RecordLimit: null);
            var result = await service.ExtractCompetitionFixturesAsync(new AttachmentId(Guid.NewGuid()), identity, request, default);
            var away = result.Fixtures[0].Away;
            Assert.Equal(NameResolutionStatus.ExactComposite, away.ResolutionStatus);
            Assert.Equal("ATHLETICO PARANAENSE", away.Name);
        }
        finally
        {
            File.Delete(tempCompetition);
            File.Delete(tempTeam);
        }
    }

    [Fact]
    public async Task FixtureService_RejectsInvalidInputCombinations()
    {
        var gateway = new NoWriteGateway();
        var service = CreateFixtureService(gateway);
        var identity = new ProcessInstanceIdentity(new AttachmentId(Guid.NewGuid()), 0, null, "PES2021");

        var bothBases = new CompetitionFixtureExtractionRequest(
            CompetitionId: new CompetitionId(17),
            TeamId: 32784,
            TeamLiga: 313,
            CalendarArrayBaseAddress: 0x1000UL,
            CompetitionBlockBaseAddress: 0x1000UL,
            AnchorAddress: null,
            ProfilePath: null,
            CompetitionMapPath: null,
            TeamMapPath: null,
            BlockRecords: null,
            RecordLimit: null);
        var ex = await Assert.ThrowsAsync<Pes2021FixtureExtractionException>(
            () => service.ExtractCompetitionFixturesAsync(new AttachmentId(Guid.NewGuid()), identity, bothBases, default));
        Assert.Equal(FixtureExtractorErrorCodes.InputInvalid, ex.Code);

        var sentinel = new CompetitionFixtureExtractionRequest(
            CompetitionId: new CompetitionId(CompetitionId.SentinelValue),
            TeamId: null,
            TeamLiga: null,
            CalendarArrayBaseAddress: null,
            CompetitionBlockBaseAddress: null,
            AnchorAddress: null,
            ProfilePath: null,
            CompetitionMapPath: null,
            TeamMapPath: null,
            BlockRecords: null,
            RecordLimit: null);
        var ex2 = await Assert.ThrowsAsync<Pes2021FixtureExtractionException>(
            () => service.ExtractCompetitionFixturesAsync(new AttachmentId(Guid.NewGuid()), identity, sentinel, default));
        Assert.Equal(FixtureExtractorErrorCodes.InputInvalid, ex2.Code);
    }

    [Fact]
    public async Task FixtureService_RejectsBadProvidedAnchor()
    {
        var gateway = new NoWriteGateway();
        gateway.AddRegion(0x1000, 0x254, 99,
            new DateOnly(2026, 4, 18), (ushort)1, (ushort)2, homeLiga: 1, awayLiga: 1);
        var service = CreateFixtureService(gateway);
        var identity = new ProcessInstanceIdentity(new AttachmentId(Guid.NewGuid()), 0, null, "PES2021");
        var request = new CompetitionFixtureExtractionRequest(
            CompetitionId: new CompetitionId(17),
            TeamId: null,
            TeamLiga: null,
            CalendarArrayBaseAddress: null,
            CompetitionBlockBaseAddress: null,
            AnchorAddress: 0x1000UL,
            ProfilePath: null,
            CompetitionMapPath: null,
            TeamMapPath: null,
            BlockRecords: null,
            RecordLimit: null);
        var ex = await Assert.ThrowsAsync<Pes2021FixtureExtractionException>(
            () => service.ExtractCompetitionFixturesAsync(new AttachmentId(Guid.NewGuid()), identity, request, default));
        Assert.Equal(FixtureExtractorErrorCodes.BaseInvalid, ex.Code);
    }

    [Fact]
    public async Task NoWriteGateway_AssertsWriteAsyncIsNeverCalled()
    {
        var gateway = new NoWriteGateway();
        var service = CreateFixtureService(gateway);
        var identity = new ProcessInstanceIdentity(new AttachmentId(Guid.NewGuid()), 0, null, "PES2021");
        var request = new CompetitionFixtureExtractionRequest(
            CompetitionId: new CompetitionId(17),
            TeamId: null,
            TeamLiga: null,
            CalendarArrayBaseAddress: null,
            CompetitionBlockBaseAddress: null,
            AnchorAddress: null,
            ProfilePath: null,
            CompetitionMapPath: null,
            TeamMapPath: null,
            BlockRecords: null,
            RecordLimit: null);
        await Assert.ThrowsAsync<Pes2021FixtureExtractionException>(
            () => service.ExtractCompetitionFixturesAsync(new AttachmentId(Guid.NewGuid()), identity, request, default));
        Assert.Equal(0, gateway.WriteCallCount);
    }

    [Fact]
    public async Task FixtureService_DiscoveredAnchorAmbiguousWhenNoStructuralTiebreaker()
    {
        var gateway = new NoWriteGateway();
        var sequence = new ushort[32];
        for (var index = 0; index < sequence.Length; index++)
        {
            sequence[index] = index < 6 ? (ushort)17 : (ushort)99;
        }
        gateway.AddRegion(0x1000, 0x254 * 32, 17,
            new DateOnly(2026, 4, 18), (ushort)32784, (ushort)32768,
            homeLiga: 313, awayLiga: 482, round: 0, homeScore: 0, awayScore: 0, competitionSequence: sequence);
        gateway.AddRegion(0x50000, 0x254 * 32, 17,
            new DateOnly(2026, 4, 18), (ushort)32784, (ushort)32768,
            homeLiga: 313, awayLiga: 482, round: 0, homeScore: 0, awayScore: 0, competitionSequence: sequence);
        var service = CreateFixtureService(gateway);
        var identity = new ProcessInstanceIdentity(new AttachmentId(Guid.NewGuid()), 0, null, "PES2021");
        var anchor = await service.FindFixtureAnchorAsync(new AttachmentId(Guid.NewGuid()), identity, SampleProfile(), new CompetitionId(17), 32784, 313, default);
        Assert.Null(anchor.AnchorAddress);
        Assert.Contains("ambiguous_tie", anchor.Diagnostics.RejectionReasons.Keys);
    }

    [Fact]
    public void JsonSerialization_EmitsCamelCaseAndScreamingSnake()
    {
        var result = new CompetitionFixtureExtractionResult(
            SchemaVersion: CompetitionFixtureExtractionResult.CurrentSchemaVersion,
            Status: FixtureExtractionStatus.FixturesOnly,
            Warning: CompetitionFixtureExtractionResult.CurrentWarning,
            Session: new CalendarSession(
                Process: new ProcessInstanceIdentity(new AttachmentId(Guid.NewGuid()), 1234, null, "PES2021"),
                ProfileId: "p", ProfileVersion: "1.0", ProfileSha256: "abc",
                RecordStride: 596, RecordLimit: 13014,
                CalendarArrayBaseAddress: null,
                CompetitionBlockBaseAddress: "0x1000",
                AnchorAddress: "0x1000",
                AnchorIndex: 0,
                ValidationSampleSha256: string.Empty,
                ValidatedAtUtc: new DateTimeOffset(2026, 8, 27, 19, 0, 0, TimeSpan.Zero),
                CacheDisposition: CacheDisposition.Discovered),
            CompetitionId: new CompetitionId(17),
            CompetitionName: "REF",
            CompetitionNameStatus: NameResolutionStatus.ExactComposite,
            RecordIndexOrigin: "competition_block_base",
            FixtureCount: 0,
            DistinctTeamCount: 0,
            UnresolvedTeamKeys: Array.Empty<TeamKey>(),
            CatalogConflicts: Array.Empty<CatalogConflict>(),
            Fixtures: Array.Empty<Fixture>(),
            Diagnostics: new ExtractionDiagnostics(
                CacheDisposition: CacheDisposition.Discovered,
                RegionsEnumerated: 0,
                RegionsAccepted: 0,
                RegionsRejected: 0,
                BytesRequested: 0,
                BytesRead: 0,
                ReadCalls: 0,
                BlocksRead: 0,
                RecordsDecoded: 0,
                RecordsAccepted: 0,
                RecordsRejected: 0,
                RejectionReasons: new Dictionary<string, int>(),
                StageDurationMs: new Dictionary<string, double>(),
                Regions: Array.Empty<RegionDiagnostic>(),
                Warnings: Array.Empty<string>()));

        var json = System.Text.Json.JsonSerializer.Serialize(result, Pes2021FixtureJson.Options);
        Assert.Contains("\"schemaVersion\":", json);
        Assert.Contains("\"status\": \"FIXTURES_ONLY\"", json);
        Assert.Contains("\"cacheDisposition\": \"DISCOVERED\"", json);
        Assert.Contains("\"competitionId\": 17", json);
    }

    private static Pes2021FixtureProfile SampleProfile()
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
        var calendar = new Pes2021CalendarLimits(1024, 2048, 13014, 32);
        var validation = new Pes2021RecordValidation(2020, 2040, 0, 80, new ushort[] { 0xFFFF });
        var regionFilter = new Pes2021RegionFilter(new[] { "Commit" }, new[] { "Private" }, true, true, false, 1 << 20);
        var anchor = new Pes2021AnchorValidation(8, 16, 4, 3, 8, 12);
        var normalization = new Pes2021Normalization(NormalizationStrategy.KnownSeasonStartIndex, 12288, new[] { 0, 12288 });
        var maps = new Pes2021ProfileMaps(null, null);
        return new Pes2021FixtureProfile(
            SchemaVersion: Pes2021FixtureProfileLoader.SupportedSchemaVersion,
            ProfileId: "test",
            ProfileVersion: "1.0",
            EvidenceStatus: "test",
            ProcessNames: new[] { "PES2021" },
            RecordLayout: layout,
            Calendar: calendar,
            RecordValidation: validation,
            RegionFilter: regionFilter,
            AnchorValidation: anchor,
            Normalization: normalization,
            Maps: maps,
            Sha256: "test-sha",
            SourcePath: "<inline>");
    }

    private static byte[] BuildRecord(Pes2021FixtureProfile profile, int competitionId, byte round, DateOnly date, ushort homeId, ushort homeLiga, ushort awayId, ushort awayLiga, byte homeScore = 0, byte awayScore = 0)
        => BuildRecord(profile, competitionId, round, date.Year, date.Month, date.Day, homeId, homeLiga, awayId, awayLiga, homeScore, awayScore);

    private static byte[] BuildRecord(Pes2021FixtureProfile profile, int competitionId, byte round, int year, int month, int day, ushort homeId, ushort homeLiga, ushort awayId, ushort awayLiga, byte homeScore = 0, byte awayScore = 0)
    {
        var bytes = new byte[profile.Stride];
        var layout = profile.RecordLayout;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(layout.CompetitionIdOffset, 2), (ushort)competitionId);
        bytes[layout.RoundOffset] = round;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(layout.YearOffset, 2), (ushort)year);
        bytes[layout.MonthOffset] = (byte)month;
        bytes[layout.DayOffset] = (byte)day;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(layout.HomeTeamIdOffset, 2), homeId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(layout.HomeTeamLigaOffset, 2), homeLiga);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(layout.AwayTeamIdOffset, 2), awayId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(layout.AwayTeamLigaOffset, 2), awayLiga);
        bytes[layout.HomeScoreOffset] = homeScore;
        bytes[layout.AwayScoreOffset] = awayScore;
        return bytes;
    }

    private static Pes2021AgendaService CreateService(IProcessMemoryGateway gateway)
    {
        var memoryService = new ProcessMemoryApplicationService(
            gateway,
            new ProbeFreezeCoordinator(),
            new InMemoryAttachmentSessionRegistry(),
            new InMemoryOperationJournal(),
            SystemClock.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessMemoryApplicationService>.Instance);
        return new Pes2021AgendaService(memoryService);
    }

    private static Pes2021CompetitionFixtureService CreateFixtureService(IProcessMemoryGateway gateway)
    {
        var memoryService = new ProcessMemoryApplicationService(
            gateway,
            new ProbeFreezeCoordinator(),
            new InMemoryAttachmentSessionRegistry(),
            new InMemoryOperationJournal(),
            SystemClock.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessMemoryApplicationService>.Instance);
        return new Pes2021CompetitionFixtureService(memoryService, SystemClock.Instance);
    }

    private sealed class ProbeFreezeCoordinator : IProcessFreezeCoordinator
    {
        public Task<FreezeInfo> FreezeAsync(FreezeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> UnfreezeAsync(FreezeId freezeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> UnfreezeByAttachmentAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<FreezeInfo>> ListAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class NoWriteGateway : IProcessMemoryGateway
    {
        private readonly SortedDictionary<ulong, byte[]> _regions = new();
        public int WriteCallCount { get; private set; }

        public void AddRegion(ulong baseAddress, int size, ushort competitionId, DateOnly date, ushort homeId, ushort awayId, ushort homeLiga = 0, ushort awayLiga = 0, byte round = 0, byte homeScore = 0, byte awayScore = 0)
            => AddRegion(baseAddress, size, competitionId, date, homeId, awayId, homeLiga, awayLiga, round, homeScore, awayScore, new ushort[] { competitionId });

        public void AddRegion(ulong baseAddress, int size, ushort headCompetitionId, DateOnly date, ushort homeId, ushort awayId, ushort homeLiga, ushort awayLiga, byte round, byte homeScore, byte awayScore, ushort[] competitionSequence)
        {
            var stride = 0x254;
            var recordCount = size / stride;
            var bytes = new byte[size];
            for (var index = 0; index < recordCount; index++)
            {
                var recordDate = date.AddDays(index);
                var offset = index * stride;
                var competitionId = index < competitionSequence.Length
                    ? competitionSequence[index]
                    : headCompetitionId;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 0, 2), competitionId);
                bytes[offset + 2] = round;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 4, 2), (ushort)recordDate.Year);
                bytes[offset + 6] = (byte)recordDate.Month;
                bytes[offset + 7] = (byte)recordDate.Day;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 16, 2), homeId);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 18, 2), homeLiga);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 20, 2), awayId);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 22, 2), awayLiga);
                bytes[offset + 24] = homeScore;
                bytes[offset + 27] = awayScore;
            }

            _regions[baseAddress] = bytes;
        }

        public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default) => Task.FromResult(new AttachmentInfo(new AttachmentId(Guid.NewGuid()), 0, "PES2021", ProcessArchitecture.X64));
        public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemoryRegionInfo>>(_regions.Select(pair => new MemoryRegionInfo(pair.Key, (ulong)pair.Value.Length, "Commit", "ReadWrite", "Private", true, true, false)).ToArray());
        public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
        {
            foreach (var (baseAddress, bytes) in _regions)
            {
                if (request.Address < baseAddress) continue;
                var start = (long)(request.Address - baseAddress);
                if (start < 0 || start + request.Size > bytes.Length) continue;
                var slice = bytes.AsSpan((int)start, request.Size).ToArray();
                return Task.FromResult(new ReadMemoryResult(request.Address, request.ValueKind, Convert.ToHexString(slice), slice.Length));
            }
            throw new InvalidOperationException("not mapped");
        }

        public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default)
        {
            WriteCallCount++;
            throw new InvalidOperationException("No write expected from the fixture path.");
        }
    }
}
