using Overmem.Abstractions;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Application.Freezing;
using Overmem.Extensions.Pes2021;
using Overmem.Runtime;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021AgendaServiceTests
{
    [Fact]
    public async Task GetGuide_ExtractsReferencesFromTheLocalCt()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                <?xml version="1.0" encoding="utf-8"?>
                <CheatTable CheatEngineTableVersion="27">
                  <CheatEntries>
                    <CheatEntry>
                      <ID>1</ID>
                      <Description>"ML Calendar"</Description>
                      <GroupHeader>1</GroupHeader>
                      <CheatEntries>
                        <CheatEntry>
                          <ID>2</ID>
                          <Description>"Competition Code"</Description>
                          <VariableType>4 Bytes</VariableType>
                          <Address>140000000</Address>
                          <Offsets>
                            <Offset>10</Offset>
                          </Offsets>
                        </CheatEntry>
                        <CheatEntry>
                          <ID>3</ID>
                          <Description>"SIG-B Calendar Table"</Description>
                          <VariableType>4 Bytes</VariableType>
                          <Address>140000000</Address>
                        </CheatEntry>
                      </CheatEntries>
                    </CheatEntry>
                  </CheatEntries>
                </CheatTable>
                """);

            var service = CreateService(new FakeGateway(), new FakeFreezeCoordinator());

            var guide = await service.GetGuideAsync(tempFile);

            Assert.True(guide.CheatTableFound);
            Assert.Equal(0x254, guide.RecordStride);
            Assert.Equal(0x2C4, guide.SecondaryDayStride);
            Assert.Contains(guide.SeasonAnchorYears, year => year == 2026);
            Assert.Contains(guide.SearchPriorities, priority => priority.Label == "secondary_calendar");
            Assert.Contains(guide.References, reference => reference.Description.Contains("competition code", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(guide.References, reference => reference.Description.Contains("SIG-B", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GetGuide_WithVersion21_1_0_CheatTable()
    {
        var ctPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "files", "PES 2021 - v21.1.0.CT"));
        if (!File.Exists(ctPath))
        {
            ctPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "files", "PES 2021 - v21.1.0.CT"));
        }

        Assert.True(File.Exists(ctPath), $"CT file not found at: {ctPath}");

        var service = CreateService(new FakeGateway(), new FakeFreezeCoordinator());
        var guide = await service.GetGuideAsync(ctPath);

        Assert.True(guide.CheatTableFound);
        Assert.Equal(0x254, guide.RecordStride);
        Assert.Equal(0x2C4, guide.SecondaryDayStride);
        Assert.Equal(ctPath, guide.CheatTablePath);
    }

    [Fact]
    public async Task GetGuide_WithoutExplicitPath_UsesDefaultVersion21_1_0_CheatTable()
    {
        var service = CreateService(new FakeGateway(), new FakeFreezeCoordinator());
        var guide = await service.GetGuideAsync();

        Assert.Equal(0x254, guide.RecordStride);
        Assert.Equal(0x2C4, guide.SecondaryDayStride);
        Assert.Contains(guide.SeasonAnchorYears, year => year == 2026);
    }

    [Fact]
    public async Task DumpDate_ReturnsTheFixturesOnTheRequestedDay()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x1000, CreateCalendarRecord(29, 1, 2026, 4, 18, 101, 11, 202, 22));
        gateway.AddBlock(0x1254, CreateCalendarRecord(29, 2, 2026, 4, 19, 303, 33, 404, 44));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.DumpDateAsync(attachmentId, 2026, 4, 18, baseAddress: 0x1000, maxRecs: 2);

        Assert.Equal(1, report.TotalMatches);
        Assert.Equal(1, report.TotalCompetitions);
        var match = Assert.Single(report.Matches);
        Assert.Equal(29, match.CompetitionCode);
        Assert.Equal(1, match.Round);
        Assert.Equal(2026, match.Year);
        Assert.Equal(4, match.Month);
        Assert.Equal(18, match.Day);
        Assert.Equal("0|29|20260418|1|101|202|11", match.EventId);

        Assert.Equal("main-backed", report.SourceRole);
        Assert.Equal("visible", report.Visibility);
        Assert.Equal("unknown", report.StopState);

        var competition = Assert.Single(report.Competitions);
        Assert.Equal(29, competition.CompetitionCode);
        Assert.Equal(1, competition.MatchCount);
        Assert.Equal("1", competition.Rounds);
    }

    [Fact]
    public async Task DumpDate_AcceptsLegacyStageCompetitionCodesAboveThreeHundred()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x1000, CreateCalendarRecord(1032, 53, 2026, 2, 4, 101, 481, 202, 636));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.DumpDateAsync(attachmentId, 2026, 2, 4, baseAddress: 0x1000, maxRecs: 1);

        var match = Assert.Single(report.Matches);
        Assert.Equal(1032, match.CompetitionCode);
        Assert.Equal(53, match.Round);

        var competition = Assert.Single(report.Competitions);
        Assert.Equal(1032, competition.CompetitionCode);
        Assert.Equal(1, competition.MatchCount);
        Assert.Equal("53", competition.Rounds);
    }

    [Fact]
    public async Task FindSecondaryBaseByDate_AndDumpSecondaryDay_ReturnTheSecondaryHeaderEvent()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x4000, CreateSecondaryCalendar(2026, 9, 22, eventType: 0x003F, eventValue: -1));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var baseResult = await service.FindSecondaryBaseByDateAsync(attachmentId, 2026, 9, 22);
        Assert.Equal((ulong)0x4000, baseResult.BaseAddress);
        Assert.Equal(264, baseResult.DayIndex);
        var candidate = Assert.Single(baseResult.Candidates, item => item.CandidateBaseAddress == 0x4000);
        Assert.Equal(0, candidate.SlotIndex);
        Assert.True(candidate.Score > 0);

        var report = await service.DumpSecondaryDayAsync(attachmentId, 2026, 9, 22, baseResult.BaseAddress);
        Assert.Equal((uint)0, report.Count);
        Assert.Empty(report.Items);
        var headerEvent = Assert.Single(report.HeaderEvents);
        Assert.Equal(0, headerEvent.SlotIndex);
        Assert.Equal(2026, headerEvent.Year);
        Assert.Equal(9, headerEvent.Month);
        Assert.Equal(22, headerEvent.Day);
        Assert.Equal((ushort)0x003F, headerEvent.Type);
        Assert.Equal((short)-1, headerEvent.Value);

        Assert.Equal("secondary-backed", report.SourceRole);
        Assert.Equal("visible", report.Visibility);
        Assert.Equal("unknown", report.StopState);
    }

    [Fact]
    public async Task ScanRuntimeDayIndexClusters_GroupsHeapHitsAndDecodesThePreview()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x9000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x2F0, 0x4C0], [264, 265, 639, 640, 641, 726]));
        gateway.AddBlock(0xF000, CreateRuntimeClusterBlock(264, 0x080, [0x080], [264, 999, 1000]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.ScanRuntimeDayIndexClustersAsync(attachmentId, 2026, 9, 22, maxResults: 32, clusterGap: 0x300, previewBytes: 32);

        Assert.Equal(264, report.DayIndex);
        Assert.Equal(4, report.TotalHits);
        Assert.Equal(2, report.ClusterCount);

        var cluster = report.Clusters[0];
        Assert.Equal((ulong)0x9120, cluster.ClusterStartAddress);
        Assert.Equal(3, cluster.HitCount);
        Assert.Equal(0x1D0, cluster.TypicalStride);
        Assert.Equal("Private", cluster.RegionType);
        Assert.True(cluster.RegionIsWritable);
        Assert.Equal(new[] { 264, 265, 639, 640, 641, 726, 0, 0 }, cluster.PreviewInt32);
    }

    [Fact]
    public async Task ScanRuntimeDayIndexClusters_IgnoresMisalignedInt32Noise()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        var block = CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330], [264, 265, 656, 726]);
        WriteInt32(block, 0x157, 264);
        gateway.AddBlock(0x9000, block);
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.ScanRuntimeDayIndexClustersAsync(attachmentId, 2026, 9, 22, maxResults: 32, clusterGap: 0x300, previewBytes: 32);

        Assert.Equal(264, report.DayIndex);
        Assert.Equal(2, report.TotalHits);
        var cluster = Assert.Single(report.Clusters);
        Assert.Equal(2, cluster.HitCount);
        Assert.Equal((ulong)0x9120, cluster.ClusterStartAddress);
        Assert.Equal((ulong)0x9330, cluster.ClusterEndAddress);
    }

    [Fact]
    public async Task DumpRuntimeDayPayloadFamily_FiltersPreferredStrideAndDecodesPreviewRecords()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x1000 + (ulong)(656 * 0x254), CreateCalendarRecord(67, 11, 2026, 9, 23, 101, 11, 202, 22));
        gateway.AddBlock(0x1000 + (ulong)(726 * 0x254), CreateCalendarRecord(67, 12, 2026, 9, 23, 303, 33, 404, 44));
        gateway.AddBlock(0x900000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x540], [264, 265, 656, 726, 29563, 0]));
        gateway.AddBlock(0xA00000, CreateRuntimeClusterBlock(264, 0x080, [0x080, 0x158, 0x230], [264, 100, 200]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
            2026,
            9,
            22,
            calendarBaseAddress: 0x1000,
            preferredStrides: [528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32);

        Assert.Equal(264, report.DayIndex);
        Assert.Equal(6, report.TotalHits);
        Assert.Equal(new[] { 528 }, report.PreferredStrides);

        var cluster = Assert.Single(report.Clusters);
        Assert.Equal((ulong)0x900120, cluster.ClusterStartAddress);
        Assert.Equal(528, cluster.TypicalStride);
        Assert.Equal(new[] { 264, 265, 656, 726, 29563, 0, 0, 0 }, cluster.PreviewInt32);

        var decoded656 = Assert.Single(cluster.PreviewRecords, item => item.Value == 656);
        Assert.True(decoded656.Resolved);
        Assert.NotNull(decoded656.Record);
        Assert.Equal(67, decoded656.Record!.CompetitionCode);

        var decoded726 = Assert.Single(cluster.PreviewRecords, item => item.Value == 726);
        Assert.True(decoded726.Resolved);
        Assert.NotNull(decoded726.Record);
        Assert.Equal(12, decoded726.Record!.Round);

        var decodedLarge = Assert.Single(cluster.PreviewRecords, item => item.Value == 29563);
        Assert.False(decodedLarge.Resolved);
        Assert.Null(decodedLarge.Record);
    }

    [Fact]
    public async Task DumpRuntimeDayPayloadFamily_HonorsStartAndStopAddressWithinRegion()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        var block = CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x1530, 0x1740], [264, 265, 656, 726]);
        gateway.AddBlock(0x900000, block);
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
            2026,
            9,
            22,
            startAddress: 0x900100,
            stopAddress: 0x900600,
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32);

        Assert.Equal((ulong)0x900100, report.ScanStartAddress);
        Assert.Equal((ulong)0x900600, report.ScanStopAddress);
        Assert.Equal(2, report.TotalHits);

        var cluster = Assert.Single(report.Clusters);
        Assert.Equal((ulong)0x900120, cluster.ClusterStartAddress);
        Assert.Equal((ulong)0x900330, cluster.ClusterEndAddress);
        Assert.Equal(2, cluster.HitCount);
    }

    [Fact]
    public async Task DumpRuntimeDayPayloadFamily_ClipsPreviewReadAtRegionEnd()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        var block = new byte[0x200];
        WriteInt32(block, 0x1F0, 264);
        WriteInt32(block, 0x1F4, 264);
        WriteInt32(block, 0x1F8, 656);
        WriteInt32(block, 0x1FC, 726);
        gateway.AddBlock(0x950000, block);
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
            2026,
            9,
            22,
            minHitCount: 2,
            clusterGap: 0x20,
            previewBytes: 64);

        var cluster = Assert.Single(report.Clusters);
        Assert.Equal((ulong)0x9501F0, cluster.ClusterStartAddress);
        Assert.Equal(new[] { 264, 264, 656, 726 }, cluster.PreviewInt32.ToArray());
    }

    [Fact]
    public async Task CompareRuntimeDayPayloadFamily_HighlightsValuesUniqueToTheCurrentDay()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x1000 + (ulong)(656 * 0x254), CreateCalendarRecord(67, 11, 2026, 9, 23, 101, 11, 202, 22));

        gateway.AddBlock(0x910000, CreateRuntimeClusterBlock(263, 0x120, [0x120, 0x330, 0x540], [263, 264, 656]));
        gateway.AddBlock(0x920000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x540], [264, 265, 656, 29563]));
        gateway.AddBlock(0x930000, CreateRuntimeClusterBlock(265, 0x120, [0x120, 0x330, 0x540], [265, 266, 656]));

        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.CompareRuntimeDayPayloadFamilyAsync(
            attachmentId,
            2026,
            9,
            22,
            calendarBaseAddress: 0x1000,
            preferredStrides: [528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32);

        Assert.Equal(263, report.PreviousDay.DayIndex);
        Assert.Equal(264, report.CurrentDay.DayIndex);
        Assert.Equal(265, report.NextDay.DayIndex);

        var cluster = Assert.Single(report.Clusters);
        Assert.Equal(528, cluster.TypicalStride);
        Assert.Equal((ulong)0x920120, cluster.CurrentClusterStartAddress);
        Assert.Equal((ulong)0x910120, cluster.PreviousClusterStartAddress);
        Assert.Equal((ulong)0x930120, cluster.NextClusterStartAddress);
        Assert.Equal("preview-overlap", cluster.PreviousMatchStrategy);
        Assert.Equal("preview-overlap", cluster.NextMatchStrategy);
        Assert.True(cluster.PreviousSharedPreviewValueCount > 0);
        Assert.True(cluster.NextSharedPreviewValueCount > 0);

        Assert.Contains(cluster.AddedVsPrevious, item => item.Value == 265);
        Assert.Contains(cluster.AddedVsPrevious, item => item.Value == 29563);
        Assert.Contains(cluster.RemovedVsPrevious, item => item.Value == 263);

        Assert.Contains(cluster.AddedVsNext, item => item.Value == 264);
        Assert.Contains(cluster.AddedVsNext, item => item.Value == 29563);
        Assert.Contains(cluster.RemovedVsNext, item => item.Value == 266);
    }

    [Fact]
    public async Task CompareRuntimeDayPayloadFamily_MatchesSameStrideClustersByPreviewOverlapInsteadOfOrdinal()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();

        gateway.AddBlock(0xA10000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x540], [264, 700, 701, 702]));
        gateway.AddBlock(0xA20000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x540], [264, 800, 801, 802]));

        gateway.AddBlock(0xA30000, CreateRuntimeClusterBlock(265, 0x120, [0x120, 0x330, 0x540], [265, 800, 801, 803]));
        gateway.AddBlock(0xA40000, CreateRuntimeClusterBlock(265, 0x120, [0x120, 0x330, 0x540], [265, 700, 701, 703]));

        gateway.AddBlock(0xA50000, CreateRuntimeClusterBlock(266, 0x120, [0x120, 0x330, 0x540], [266, 800, 801, 804]));
        gateway.AddBlock(0xA60000, CreateRuntimeClusterBlock(266, 0x120, [0x120, 0x330, 0x540], [266, 700, 701, 704]));

        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.CompareRuntimeDayPayloadFamilyAsync(
            attachmentId,
            2026,
            9,
            23,
            preferredStrides: [528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32);

        Assert.Equal(2, report.Clusters.Count);

        Assert.All(report.Clusters, cluster => Assert.Equal("preview-overlap", cluster.PreviousMatchStrategy));
        Assert.All(report.Clusters, cluster => Assert.Equal("preview-overlap", cluster.NextMatchStrategy));
        Assert.All(report.Clusters, cluster => Assert.True(cluster.PreviousSharedPreviewValueCount >= 2));
        Assert.All(report.Clusters, cluster => Assert.True(cluster.NextSharedPreviewValueCount >= 2));
        Assert.Equal(
            new[] { (ulong)0xA10120, (ulong)0xA20120 },
            report.Clusters.Select(cluster => cluster.PreviousClusterStartAddress!.Value).OrderBy(static value => value).ToArray());
        Assert.Equal(
            new[] { (ulong)0xA50120, (ulong)0xA60120 },
            report.Clusters.Select(cluster => cluster.NextClusterStartAddress!.Value).OrderBy(static value => value).ToArray());
    }

    [Fact]
    public async Task DumpRuntimeDayPayloadClusterDetail_ReturnsPerHitLocalWindows()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x920000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x540], [264, 265, 656, 726, 29563, 0]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.DumpRuntimeDayPayloadClusterDetailAsync(
            attachmentId,
            2026,
            9,
            22,
            clusterOrdinal: 0,
            preferredStrides: [528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32,
            intsBeforeHit: 2,
            intsAfterHit: 4);

        Assert.Equal(0, report.ClusterOrdinal);
        Assert.Equal(528, report.TypicalStride);
        Assert.Equal((ulong)0x920120, report.ClusterStartAddress);

        var firstWindow = report.HitWindows[0];
        Assert.Equal((ulong)0x920120, firstWindow.HitAddress);
        Assert.Equal(new[] { -8, -4, 0, 4, 8, 12 }, firstWindow.Values.Select(item => item.RelativeOffset).ToArray());
        Assert.Equal(new[] { 0, 0, 264, 265, 656, 726 }, firstWindow.Values.Select(item => item.Value).ToArray());
    }

    [Fact]
    public async Task AnalyzeRuntimeDayPayloadCluster_ExtractsCommonPrefixAndTailSignature()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x1000 + (ulong)(264 * 0x254), CreateCalendarRecord(29, 26, 2026, 9, 26, 101, 11, 202, 22));
        gateway.AddBlock(0x1000 + (ulong)(265 * 0x254), CreateCalendarRecord(29, 26, 2026, 9, 26, 303, 33, 404, 44));
        gateway.AddBlock(0x1000 + (ulong)(656 * 0x254), CreateCalendarRecord(67, 11, 2026, 9, 23, 101, 11, 202, 22));
        gateway.AddBlock(0x1000 + (ulong)(726 * 0x254), CreateCalendarRecord(67, 12, 2026, 9, 23, 303, 33, 404, 44));
        gateway.AddBlock(0x920000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x540], [264, 265, 656, 726, 29563, 0]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.AnalyzeRuntimeDayPayloadClusterAsync(
            attachmentId,
            2026,
            9,
            22,
            clusterOrdinal: 0,
            calendarBaseAddress: 0x1000,
            preferredStrides: [528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32,
            intsBeforeHit: 2,
            intsAfterHit: 6);

        Assert.Equal(new[] { 264 }, report.CommonAnchorPrefix);
        Assert.Contains(29563, report.UnresolvedPreviewValues);

        var signature = report.HitSignatures[0];
        Assert.Equal(new[] { 265, 656, 726, 29563, 0 }, signature.TailValues);
        Assert.Contains(signature.TailRuns, run => run.StartValue == 265 && run.EndValue == 265 && run.Length == 1);
    }

    [Fact]
    public async Task ClassifyRuntimeDayVariant_DetectsPlaceholderSpecialRuntime()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x4000, CreateSecondaryCalendar(2026, 9, 22, eventType: 0x003F, eventValue: -1));
        gateway.AddBlock(0x900000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x540], [264, 265, 656, 726, 29563, 0]));
        gateway.AddBlock(0x910000, CreateRuntimeClusterBlock(264, 0x100, [0x100, 0x2D8, 0x4B0], [264, 265, 639, 640, 12717, 22659]));
        gateway.AddBlock(0x920000, CreateRuntimeClusterBlock(264, 0x140, [0x140, 0x350, 0x560], [264, 265, 652, 653, 654, 655]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.ClassifyRuntimeDayVariantAsync(
            attachmentId,
            2026,
            9,
            22,
            preferredStrides: [472, 528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32);

        Assert.Equal("placeholder_special_runtime", report.VariantKey);
        Assert.Equal("high", report.Confidence);
        Assert.Equal("national_team_callup", report.SemanticEventKey);
        Assert.Equal("high", report.SemanticEventConfidence);
        Assert.True(report.HasSpecial472Family);
        Assert.Equal((uint)0, report.SecondaryCount);
        Assert.Equal("runtime-projected", report.SourceRole);
        Assert.Equal("visible", report.Visibility);
        Assert.Equal("stop", report.StopState);
    }

    [Fact]
    public async Task ClassifyRuntimeDayVariant_DetectsAgendaDefinedOrganizedRuntime()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x4000, CreateSecondaryCalendar(2026, 6, 23, eventType: 0x003F, eventValue: -1, count: 28, items: Enumerable.Range(824, 28).Select(static value => (ushort)value).ToArray()));
        gateway.AddBlock(0x900000, CreateRuntimeClusterBlock(173, 0x120, [0x120, 0x330, 0x540], [173, 824, 825, 826, 835, 836]));
        gateway.AddBlock(0x910000, CreateRuntimeClusterBlock(173, 0x120, [0x120, 0x330, 0x540], [173, 842, 843, 844, 861, 862]));
        gateway.AddBlock(0x920000, CreateRuntimeClusterBlock(173, 0x120, [0x120, 0x330, 0x540], [173, 877, 878, 879, 890, 891]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.ClassifyRuntimeDayVariantAsync(
            attachmentId,
            2026,
            6,
            23,
            preferredStrides: [528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32);

        Assert.Equal("agenda_defined_organized_runtime", report.VariantKey);
        Assert.Equal("medium", report.Confidence);
        Assert.Equal("national_team_callup", report.SemanticEventKey);
        Assert.Equal("high", report.SemanticEventConfidence);
        Assert.False(report.HasSpecial472Family);
        Assert.Equal((uint)28, report.SecondaryCount);
        Assert.Equal(3, report.ClusterCount);
        Assert.Equal(3, report.TemporalComparison.IsolatedClusterCount);
        Assert.Equal("secondary-backed", report.SourceRole);
        Assert.Equal("visible", report.Visibility);
        Assert.Equal("stop", report.StopState);
    }

    [Fact]
    public async Task ClassifyRuntimeDayVariant_IncludesTemporalSummaryForReorderedMultiClusterDays()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();

        gateway.AddBlock(0x4000, CreateSecondaryCalendar(2026, 9, 23, eventType: 0x003F, eventValue: -1));

        gateway.AddBlock(0xA10000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x540], [264, 700, 701, 702]));
        gateway.AddBlock(0xA20000, CreateRuntimeClusterBlock(264, 0x120, [0x120, 0x330, 0x540], [264, 800, 801, 802]));

        gateway.AddBlock(0xA30000, CreateRuntimeClusterBlock(265, 0x120, [0x120, 0x330, 0x540], [265, 800, 801, 803]));
        gateway.AddBlock(0xA40000, CreateRuntimeClusterBlock(265, 0x120, [0x120, 0x330, 0x540], [265, 700, 701, 703]));

        gateway.AddBlock(0xA50000, CreateRuntimeClusterBlock(266, 0x120, [0x120, 0x330, 0x540], [266, 800, 801, 804]));
        gateway.AddBlock(0xA60000, CreateRuntimeClusterBlock(266, 0x120, [0x120, 0x330, 0x540], [266, 700, 701, 704]));

        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.ClassifyRuntimeDayVariantAsync(
            attachmentId,
            2026,
            9,
            23,
            preferredStrides: [528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32);

        Assert.Equal("unknown_runtime", report.VariantKey);
        Assert.Equal(2, report.ClusterCount);
        Assert.Equal(2, report.TemporalComparison.ComparedClusterCount);
        Assert.Equal(2, report.TemporalComparison.PreviousMatchedClusterCount);
        Assert.Equal(2, report.TemporalComparison.NextMatchedClusterCount);
        Assert.Equal(2, report.TemporalComparison.StableClusterCount);
        Assert.Equal(0, report.TemporalComparison.IsolatedClusterCount);
        Assert.Equal(2, report.TemporalComparison.PreviousPreviewOverlapMatchCount);
        Assert.Equal(2, report.TemporalComparison.NextPreviewOverlapMatchCount);
        Assert.Equal(0, report.TemporalComparison.PreviousFallbackMatchCount);
        Assert.Equal(0, report.TemporalComparison.NextFallbackMatchCount);
        Assert.Contains(report.Reasons, reason => reason == "temporal_stable_clusters=2");
        Assert.Contains(report.Reasons, reason => reason == "temporal_isolated_clusters=0");
    }

    [Fact]
    public async Task ClassifyRuntimeDayVariant_DetectsConfirmedTransferWindowStart()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x4000, CreateSecondaryCalendar(2026, 7, 1, eventType: 0x0009, eventValue: -1, extraHeaderEvents: [(0x003B, (short)-1)]));
        gateway.AddBlock(0x900000, CreateRuntimeClusterBlock(181, 0x120, [0x120, 0x330, 0x540], [181, 182, 183, 184, 185, 186]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.ClassifyRuntimeDayVariantAsync(
            attachmentId,
            2026,
            7,
            1,
            preferredStrides: [528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32);

        Assert.Equal("transfer_window_start", report.SemanticEventKey);
        Assert.Equal("high", report.SemanticEventConfidence);
        Assert.Equal("runtime-projected", report.SourceRole);
        Assert.Equal("visible", report.Visibility);
        Assert.Equal("stop", report.StopState);
    }

    [Fact]
    public async Task ClassifyRuntimeDayVariant_DetectsConfirmedTransferWindowEnd()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x4000, CreateSecondaryCalendar(2026, 8, 31, eventType: 0x0009, eventValue: -1, extraHeaderEvents: [(0x000A, (short)-1)]));
        gateway.AddBlock(0x900000, CreateRuntimeClusterBlock(242, 0x120, [0x120, 0x330, 0x540], [242, 243, 244, 245, 246, 247]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.ClassifyRuntimeDayVariantAsync(
            attachmentId,
            2026,
            8,
            31,
            preferredStrides: [528],
            minHitCount: 2,
            clusterGap: 0x300,
            previewBytes: 32);

        Assert.Equal("transfer_window_end", report.SemanticEventKey);
        Assert.Equal("high", report.SemanticEventConfidence);
        Assert.Equal("runtime-projected", report.SourceRole);
        Assert.Equal("visible", report.Visibility);
        Assert.Equal("stop", report.StopState);
    }

    [Fact]
    public async Task InventoryAnnualEvents_ReturnsConfirmedSemanticAnchors()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x1000, CreateCalendarRecord(29, 1, 2026, 1, 31, 101, 11, 202, 22));
        gateway.AddBlock(0x1254, CreateCalendarRecord(29, 2, 2026, 7, 2, 303, 33, 404, 44));
        gateway.AddBlock(0x4000, CreateSecondaryCalendarForDates(
            [
                CreateSecondaryDaySpec(2026, 1, 1, 0x0001, -1, 0, null, [(0x0009, -1), (0x003C, -1), (0x003E, -1)]),
                CreateSecondaryDaySpec(2026, 7, 1, 0x0009, -1, 0, null, [(0x003B, -1)]),
                CreateSecondaryDaySpec(2026, 9, 22, 0x003F, -1)
            ]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.InventoryAnnualEventsAsync(
            attachmentId,
            2026,
            calendarBaseAddress: 0x1000,
            secondaryBaseAddress: 0x4000);

        var januaryWindowStart = Assert.Single(report.Days, item => item.Date == "2026-01-01");
        Assert.Equal("transfer_window_start", januaryWindowStart.SemanticEventKey);
        Assert.Equal("high", januaryWindowStart.SemanticEventConfidence);
        Assert.Equal("known_semantic_event", januaryWindowStart.InventoryPatternKey);
        Assert.Equal("secondary-backed", januaryWindowStart.SourceRole);
        Assert.Equal("visible", januaryWindowStart.Visibility);
        Assert.Equal("stop", januaryWindowStart.StopState);

        var julyWindowStart = Assert.Single(report.Days, item => item.Date == "2026-07-01");
        Assert.Equal("transfer_window_start", julyWindowStart.SemanticEventKey);
        Assert.Equal("high", julyWindowStart.SemanticEventConfidence);
        Assert.Equal(1, julyWindowStart.NextDayMainMatchCount);
        Assert.Equal("secondary-backed", julyWindowStart.SourceRole);
        Assert.Equal("visible", julyWindowStart.Visibility);
        Assert.Equal("stop", julyWindowStart.StopState);
    }

    [Fact]
    public async Task InventoryAnnualEvents_LabelsConfirmedAdministrativeEventsAndCandidates()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x1000, CreateCalendarRecord(29, 37, 2026, 12, 1, 101, 11, 202, 22));
        gateway.AddBlock(0x4000, CreateSecondaryCalendarForDates(
            [
                CreateSecondaryDaySpec(2026, 12, 1, 0x003F, -1),
                CreateSecondaryDaySpec(2026, 12, 2, 0x003F, -1),
                CreateSecondaryDaySpec(2026, 12, 5, 0x003F, -1),
                CreateSecondaryDaySpec(2026, 1, 30, 0x0004, -1),
                CreateSecondaryDaySpec(2026, 3, 19, 0x0025, -1),
                CreateSecondaryDaySpec(2026, 4, 16, 0x0027, -1),
                CreateSecondaryDaySpec(2026, 6, 13, 0x000D, -1),
                CreateSecondaryDaySpec(2026, 6, 26, 0x0033, -1),
                CreateSecondaryDaySpec(2026, 6, 30, 0x0010, -1, extraHeaderEvents: [(0x000F, (short)-1), (0x0018, (short)-1)]),
                CreateSecondaryDaySpec(2026, 8, 26, 0x002B, -1),
                CreateSecondaryDaySpec(2026, 8, 27, 0x0021, -1, extraHeaderEvents: [(0x0009, (short)-1)]),
                CreateSecondaryDaySpec(2026, 12, 10, 0x0023, -1),
                CreateSecondaryDaySpec(2026, 12, 12, 0x0039, -1),
                CreateSecondaryDaySpec(2026, 12, 11, 0x003F, -1),
                CreateSecondaryDaySpec(2026, 12, 13, 0x003F, -1),
                CreateSecondaryDaySpec(2026, 12, 29, 0x0020, -1, extraHeaderEvents: [(0x002C, (short)-1)]),
                CreateSecondaryDaySpec(2026, 12, 30, 0x000D, -1),
                CreateSecondaryDaySpec(2026, 12, 31, 0x001A, -21, extraHeaderEvents: [(0x000E, (short)-1), (0x0011, (short)-1)])
            ]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.InventoryAnnualEventsAsync(
            attachmentId,
            2026,
            calendarBaseAddress: 0x1000,
            secondaryBaseAddress: 0x4000);

        Assert.Equal("transfer_window_boundary_prelude_candidate", Assert.Single(report.Days, item => item.Date == "2026-01-30").SemanticEventKey);
        Assert.Equal("libertadores_group_stage_matchday_2_followup", Assert.Single(report.Days, item => item.Date == "2026-03-19").SemanticEventKey);
        Assert.Equal("libertadores_group_stage_matchday_4_followup", Assert.Single(report.Days, item => item.Date == "2026-04-16").SemanticEventKey);
        Assert.Equal("libertadores_round_of_16_first_leg_placeholder", Assert.Single(report.Days, item => item.Date == "2026-06-13").SemanticEventKey);
        Assert.Equal("club_ranking_update", Assert.Single(report.Days, item => item.Date == "2026-06-26").SemanticEventKey);
        Assert.Equal("coach_offer_response_deadline", Assert.Single(report.Days, item => item.Date == "2026-06-30").SemanticEventKey);
        Assert.Equal("europe_best_player_award_prelude", Assert.Single(report.Days, item => item.Date == "2026-08-26").SemanticEventKey);
        Assert.Equal("europe_best_player_award_announcement", Assert.Single(report.Days, item => item.Date == "2026-08-27").SemanticEventKey);
        var decemberFirst = Assert.Single(report.Days, item => item.Date == "2026-12-01");
        Assert.Equal(1, decemberFirst.MainMatchCount);
        Assert.Equal("unknown_event", decemberFirst.SemanticEventKey);
        Assert.Equal("calendar-match-day", decemberFirst.DayRole);
        Assert.Equal("no-stop", decemberFirst.StopState);
        var decemberSecond = Assert.Single(report.Days, item => item.Date == "2026-12-02");
        Assert.Equal("world_best_player_award_announcement", decemberSecond.SemanticEventKey);
        Assert.Equal("semantic-event-day", decemberSecond.DayRole);
        Assert.Equal("stop", decemberSecond.StopState);
        Assert.Equal("hidden_competition_projection_source_marker", Assert.Single(report.Days, item => item.Date == "2026-12-10").SemanticEventKey);
        Assert.Equal("hidden_competition_projection_source_marker", Assert.Single(report.Days, item => item.Date == "2026-12-12").SemanticEventKey);
        Assert.Equal("asian_best_player_award_announcement", Assert.Single(report.Days, item => item.Date == "2026-12-29").SemanticEventKey);
        Assert.Equal("south_america_best_player_award_announcement", Assert.Single(report.Days, item => item.Date == "2026-12-30").SemanticEventKey);
        Assert.Equal("last_day_of_season", Assert.Single(report.Days, item => item.Date == "2026-12-31").SemanticEventKey);
        Assert.DoesNotContain(report.Days, item => item.Date == "2026-12-05");
        Assert.DoesNotContain(report.Days, item => item.Date == "2026-12-11");
        Assert.DoesNotContain(report.Days, item => item.Date == "2026-12-13");
    }

    [Fact]
    public async Task InventoryAnnualEvents_RecognizesLibertadoresMarkerSignaturesOutsideConfirmedDates()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x1000, CreateCalendarRecord(29, 37, 2026, 12, 1, 101, 11, 202, 22));
        gateway.AddBlock(0x1254, CreateCalendarRecord(31, 53, 2026, 6, 20, 303, 33, 404, 44));
        gateway.AddBlock(0x4000, CreateSecondaryCalendarForDates(
            [
                CreateSecondaryDaySpec(2026, 3, 26, 0x0025, -1, extraHeaderEvents: [(0x003F, (short)-1)]),
                CreateSecondaryDaySpec(2026, 4, 23, 0x0027, -1, extraHeaderEvents: [(0x003F, (short)-1)]),
                CreateSecondaryDaySpec(2026, 6, 20, 0x000D, -1, extraHeaderEvents: [(0x003F, (short)-1)])
            ]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.InventoryAnnualEventsAsync(
            attachmentId,
            2026,
            calendarBaseAddress: 0x1000,
            secondaryBaseAddress: 0x4000);

        var groupStageMatchday2 = Assert.Single(report.Days, item => item.Date == "2026-03-26");
        Assert.Equal("libertadores_group_stage_matchday_2_followup_candidate", groupStageMatchday2.SemanticEventKey);
        Assert.Equal("medium", groupStageMatchday2.SemanticEventConfidence);

        var groupStageMatchday4 = Assert.Single(report.Days, item => item.Date == "2026-04-23");
        Assert.Equal("libertadores_group_stage_matchday_4_followup_candidate", groupStageMatchday4.SemanticEventKey);
        Assert.Equal("medium", groupStageMatchday4.SemanticEventConfidence);

        var roundOf16 = Assert.Single(report.Days, item => item.Date == "2026-06-20");
        Assert.Equal(1, roundOf16.MainMatchCount);
        Assert.Equal("libertadores_round_of_16_first_leg_placeholder_candidate", roundOf16.SemanticEventKey);
        Assert.Equal("medium", roundOf16.SemanticEventConfidence);
    }

    [Fact]
    public async Task InventoryAnnualEvents_ClassifiesRecurringUnknownPatterns()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway();
        gateway.AddBlock(0x1000, CreateCalendarRecord(29, 1, 2026, 1, 17, 101, 11, 202, 22));
        gateway.AddBlock(0x1254, CreateCalendarRecord(29, 2, 2026, 2, 1, 303, 33, 404, 44));
        gateway.AddBlock(0x4000, CreateSecondaryCalendarForDates(
            [
                CreateSecondaryDaySpec(2026, 1, 16, 0x003F, -1, 12, Enumerable.Range(100, 12).Select(static value => (ushort)value).ToArray()),
                CreateSecondaryDaySpec(2026, 1, 19, 0x003F, -1),
                CreateSecondaryDaySpec(2026, 2, 3, 0xFFFF, -1, 4, Enumerable.Range(200, 4).Select(static value => (ushort)value).ToArray())
            ]));
        var service = CreateService(gateway, new FakeFreezeCoordinator());

        var report = await service.InventoryAnnualEventsAsync(
            attachmentId,
            2026,
            calendarBaseAddress: 0x1000,
            secondaryBaseAddress: 0x4000);

        Assert.Equal("placeholder_bridge_candidate", Assert.Single(report.Days, item => item.Date == "2026-01-16").InventoryPatternKey);
        Assert.DoesNotContain(report.Days, item => item.Date == "2026-01-19");
        Assert.Equal("rare_header_marker_candidate", Assert.Single(report.Days, item => item.Date == "2026-02-03").InventoryPatternKey);
    }

    private static Pes2021AgendaService CreateService(IProcessMemoryGateway gateway, IProcessFreezeCoordinator freezeCoordinator)
    {
        var memoryService = new ProcessMemoryApplicationService(gateway, freezeCoordinator, new InMemoryAttachmentSessionRegistry(), new InMemoryOperationJournal(), SystemClock.Instance, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessMemoryApplicationService>.Instance);
        return new Pes2021AgendaService(memoryService);
    }

    private static byte[] CreateCalendarRecord(int competitionCode, int roundValue, int year, int month, int day, int homeId, int homeLiga, int awayId, int awayLiga)
    {
        var bytes = new byte[0x254];
        WriteUInt16(bytes, 0x00, competitionCode);
        bytes[0x02] = (byte)roundValue;
        WriteUInt16(bytes, 0x04, year);
        bytes[0x06] = (byte)month;
        bytes[0x07] = (byte)day;
        WriteUInt16(bytes, 0x10, homeId);
        WriteUInt16(bytes, 0x12, homeLiga);
        WriteUInt16(bytes, 0x14, awayId);
        WriteUInt16(bytes, 0x16, awayLiga);
        bytes[0x18] = 0;
        bytes[0x1B] = 0;
        return bytes;
    }

    private static byte[] CreateSecondaryCalendar(int year, int month, int day, ushort eventType, short eventValue, uint count = 0, IReadOnlyList<ushort>? items = null, IReadOnlyList<(ushort Type, short Value)>? extraHeaderEvents = null)
    {
        return CreateSecondaryCalendarForDates([CreateSecondaryDaySpec(year, month, day, eventType, eventValue, count, items, extraHeaderEvents)]);
    }

    private static byte[] CreateSecondaryCalendarForDates(IReadOnlyList<SecondaryDaySpec> days)
    {
        var bytes = new byte[0x2C4 * 365];
        for (var dayIndex = 0; dayIndex < 365; dayIndex++)
        {
            var dayOffset = dayIndex * 0x2C4;
            for (var itemOffset = 0x8C; itemOffset < 0x2B8; itemOffset += 2)
            {
                WriteUInt16(bytes, dayOffset + itemOffset, 0xFFFF);
            }

            WriteUInt32(bytes, dayOffset + 0x2BC, dayIndex == 364 ? 0xFFFFFFFFu : 0u);
            WriteUInt16(bytes, dayOffset + 0x00, 0xFFFF);
        }

        foreach (var day in days)
        {
            var dayIndex = (new DateOnly(day.Year, day.Month, day.Day).DayNumber - new DateOnly(day.Year, 1, 1).DayNumber);
            var dayOffset = dayIndex * 0x2C4;
            WriteUInt16(bytes, dayOffset + 0x00, day.Year);
            bytes[dayOffset + 0x02] = (byte)day.Month;
            bytes[dayOffset + 0x03] = (byte)day.Day;
            WriteUInt16(bytes, dayOffset + 0x04, day.EventType);
            WriteInt16(bytes, dayOffset + 0x06, day.EventValue);
            WriteUInt16(bytes, dayOffset + 0x08, 0xFFFF);
            WriteUInt32(bytes, dayOffset + 0x2BC, day.Count);

            if (day.ExtraHeaderEvents is not null)
            {
                for (var index = 0; index < day.ExtraHeaderEvents.Count; index++)
                {
                    var slotOffset = dayOffset + ((index + 1) * 8);
                    var (type, value) = day.ExtraHeaderEvents[index];
                    WriteUInt16(bytes, slotOffset + 0x00, day.Year);
                    bytes[slotOffset + 0x02] = (byte)day.Month;
                    bytes[slotOffset + 0x03] = (byte)day.Day;
                    WriteUInt16(bytes, slotOffset + 0x04, type);
                    WriteInt16(bytes, slotOffset + 0x06, value);
                }
            }

            if (day.Items is not null)
            {
                var maxItems = Math.Min(day.Items.Count, (0x2B8 - 0x8C) / 2);
                for (var index = 0; index < maxItems; index++)
                {
                    WriteUInt16(bytes, dayOffset + 0x8C + (index * 2), day.Items[index]);
                }
            }
        }

        return bytes;
    }

    private static SecondaryDaySpec CreateSecondaryDaySpec(int year, int month, int day, ushort eventType, short eventValue, uint count = 0, IReadOnlyList<ushort>? items = null, IReadOnlyList<(ushort Type, short Value)>? extraHeaderEvents = null)
        => new(year, month, day, eventType, eventValue, count, items, extraHeaderEvents);

    private static byte[] CreateRuntimeClusterBlock(int dayIndex, int previewOffset, IReadOnlyList<int> hitOffsets, IReadOnlyList<int> previewValues)
    {
        var bytes = new byte[0x2000];
        foreach (var hitOffset in hitOffsets)
        {
            WriteInt32(bytes, hitOffset, dayIndex);
        }

        for (var index = 0; index < previewValues.Count; index++)
        {
            WriteInt32(bytes, previewOffset + (index * sizeof(int)), previewValues[index]);
        }

        return bytes;
    }

    private static void WriteUInt16(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteInt16(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private sealed class FakeGateway : IProcessMemoryGateway
    {
        private readonly Dictionary<ulong, byte[]> _blocks = new();

        public void AddBlock(ulong address, byte[] bytes) => _blocks[address] = bytes;

        public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemoryRegionInfo>>(_blocks
                .OrderBy(pair => pair.Key)
                .Select(pair => new MemoryRegionInfo(pair.Key, (ulong)pair.Value.Length, "Commit", "ReadWrite", "Private", IsReadable: true, IsWritable: true, IsExecutable: false))
                .ToArray());

        public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default)
        {
            var pattern = ParsePattern(request.Pattern);
            var addresses = new List<ulong>();

            foreach (var (baseAddress, bytes) in _blocks.OrderBy(pair => pair.Key))
            {
                for (var offset = 0; offset <= bytes.Length - pattern.Length; offset++)
                {
                    if (PatternMatches(bytes, offset, pattern))
                    {
                        addresses.Add(baseAddress + (ulong)offset);
                        if (addresses.Count >= request.MaxResults)
                        {
                            return Task.FromResult(new PatternScanResult(request.Pattern, request.ModuleName, addresses));
                        }
                    }
                }
            }

            return Task.FromResult(new PatternScanResult(request.Pattern, request.ModuleName, addresses));
        }

        public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
        {
            foreach (var (baseAddress, bytes) in _blocks)
            {
                if (request.Address < baseAddress)
                {
                    continue;
                }

                var start = (long)(request.Address - baseAddress);
                if (start < 0 || start + request.Size > bytes.Length)
                {
                    continue;
                }

                var slice = bytes.AsSpan((int)start, request.Size).ToArray();
                return Task.FromResult(new ReadMemoryResult(request.Address, request.ValueKind, Convert.ToHexString(slice), slice.Length));
            }

            throw new InvalidOperationException("The requested address was not mapped in the fake gateway.");
        }

        public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        private static (byte Value, bool Wildcard)[] ParsePattern(string pattern)
            => pattern
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => token == "??"
                    ? ((byte)0, true)
                    : ((byte)Convert.ToInt32(token, 16), false))
                .ToArray();

        private static bool PatternMatches(byte[] bytes, int offset, (byte Value, bool Wildcard)[] pattern)
        {
            for (var index = 0; index < pattern.Length; index++)
            {
                if (pattern[index].Wildcard)
                {
                    continue;
                }

                if (bytes[offset + index] != pattern[index].Value)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class FakeFreezeCoordinator : IProcessFreezeCoordinator
    {
        public Task<FreezeInfo> FreezeAsync(FreezeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> UnfreezeAsync(FreezeId freezeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> UnfreezeByAttachmentAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<FreezeInfo>> ListAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed record SecondaryDaySpec(
        int Year,
        int Month,
        int Day,
        ushort EventType,
        short EventValue,
        uint Count,
        IReadOnlyList<ushort>? Items,
        IReadOnlyList<(ushort Type, short Value)>? ExtraHeaderEvents);
}
