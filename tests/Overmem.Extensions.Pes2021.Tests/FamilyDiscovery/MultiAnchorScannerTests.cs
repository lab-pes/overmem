using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

namespace Overmem.Extensions.Pes2021.Tests.FamilyDiscovery;

public class MultiAnchorScannerTests
{
    private static Pes2021PlayerProfile CreateTestProfile()
    {
        var fields = new[]
        {
            new Pes2021PlayerFieldDefinition("playerId", 0, 4, Pes2021PlayerFieldType.U32Le, "unsigned", "le", Pes2021PlayerTransform.None, Pes2021PlayerEvidenceStatus.Confirmed, Pes2021PlayerEvidenceStatus.Confirmed, Array.Empty<Pes2021PlayerContext>(), false, null, null),
            new Pes2021PlayerFieldDefinition("playerName", 44, 46, Pes2021PlayerFieldType.FixedAscii, "unsigned", "le", Pes2021PlayerTransform.TrimAsciiZ, Pes2021PlayerEvidenceStatus.Confirmed, Pes2021PlayerEvidenceStatus.Confirmed, Array.Empty<Pes2021PlayerContext>(), false, null, null),
        };

        var layout = new Pes2021PlayerRecordLayout(380, 0, fields);
        var validation = new Pes2021PlayerRecordValidation(150, 210, 50, 120, 1, 200000);
        var filter = new Pes2021PlayerRegionFilter(new[] { "MEM_COMMIT" }, new[] { "Private" }, true, true, false, 4096);
        var anchor = new Pes2021PlayerAnchorValidation(4, 8, 3, 5, 8, 12, 3, new uint[] { 101473, 58120 });
        var limits = new Pes2021PlayerLimits(256, 1024, 50000, 10000);
        var sources = new Pes2021PlayerProfileSources(null, null, null);

        return new Pes2021PlayerProfile("1.0", "test", "1.0", Pes2021PlayerEvidenceStatus.Candidate, new[] { "PES2021.exe" }, layout, validation, filter, anchor, limits, sources, "hash", "");
    }

    private static DecodedPlayerRecord CreateTestRecord(uint playerId, string name)
    {
        var raw = new byte[380];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0, 4), playerId);
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        Array.Copy(nameBytes, 0, raw, 44, Math.Min(nameBytes.Length, 46));

        return new DecodedPlayerRecord(
            Address: 0x1000,
            RecordIndex: 0,
            PlayerId: playerId,
            PlayerName: name,
            ClubShirtName: null,
            NationalShirtName: null,
            Fields: Array.Empty<DecodedFieldValue>(),
            RawRecord: raw,
            RawRecordSha256: "sha",
            Warnings: Array.Empty<string>());
    }

    [Fact]
    public void RegionPolicyFilter_FiltersCorrectly()
    {
        var regions = new List<MemoryRegionInfo>
        {
            new MemoryRegionInfo(0x1000, 0x1000, "MEM_COMMIT", "PAGE_READWRITE", "MEM_PRIVATE", true, true, false), // Default
            new MemoryRegionInfo(0x2000, 0x1000, "MEM_COMMIT", "PAGE_READWRITE", "MEM_MAPPED", true, true, false), // Mapped
            new MemoryRegionInfo(0x3000, 0x1000, "MEM_COMMIT", "PAGE_READONLY", "MEM_PRIVATE", true, false, false), // ReadOnly
            new MemoryRegionInfo(0x4000, 0x1000, "MEM_COMMIT", "PAGE_EXECUTE_READWRITE", "MEM_PRIVATE", true, true, true), // Executable
            new MemoryRegionInfo(0x5000, 0x1000, "MEM_RESERVE", "PAGE_NOACCESS", "MEM_PRIVATE", false, false, false), // Unreadable
            new MemoryRegionInfo(0x1000, 0x1000, "MEM_COMMIT", "PAGE_READWRITE", "MEM_PRIVATE", true, true, false) // Duplicate
        };

        // 1. Default Player Arena
        var (accDefault, rejDefault) = RegionPolicyFilter.Filter(regions, RegionPolicy.DefaultPlayerArena);
        Assert.Single(accDefault);
        Assert.Equal(0x1000UL, accDefault[0].BaseAddress);
        Assert.Equal(4, rejDefault.Count); // 2000, 3000, 4000, 5000 (duplicate 1000 is ignored)

        // 2. Include Mapped
        var (accMapped, _) = RegionPolicyFilter.Filter(regions, RegionPolicy.IncludeMapped);
        Assert.Equal(2, accMapped.Count); // 1000, 2000

        // 3. Include ReadOnly
        var (accRO, _) = RegionPolicyFilter.Filter(regions, RegionPolicy.IncludeReadOnly);
        Assert.Equal(3, accRO.Count); // 1000, 2000, 3000

        // 4. Include Executable
        var (accExec, _) = RegionPolicyFilter.Filter(regions, RegionPolicy.IncludeExecutable);
        Assert.Equal(4, accExec.Count); // 1000, 2000, 3000, 4000

        // 5. All (everything readable)
        var (accAll, _) = RegionPolicyFilter.Filter(regions, RegionPolicy.All);
        Assert.Equal(4, accAll.Count); // Unreadable (5000) is filtered out
    }

    [Fact]
    public async Task ScanAsync_FindsMultipleAnchorsInSinglePass()
    {
        var gateway = new FakeProcessMemoryGateway();
        var scanner = new MultiAnchorScanner(gateway);
        var profile = CreateTestProfile();
        
        var controls = new[]
        {
            CreateTestRecord(101473, "MESSI"),
            CreateTestRecord(58120, "NEYMAR")
        };
        var fingerprints = FingerprintBuilder.Build(profile, controls);

        var memory = new byte[8192];
        Array.Copy(controls[0].RawRecord, 0, memory, 1000, 380); // Messi at 1000
        Array.Copy(controls[1].RawRecord, 0, memory, 2000, 380); // Neymar at 2000
        gateway.MapRegion(0x10000, memory);

        var regions = new[] { new MemoryRegionInfo(0x10000, 8192, "MEM_COMMIT", "PAGE_READWRITE", "MEM_PRIVATE", true, true, false) };

        var result = await scanner.ScanAsync(
            AttachmentId.New(), 
            fingerprints, 
            profile, 
            RegionPolicy.DefaultPlayerArena, 
            FamilyScanBudget.Unlimited, 
            regions, 
            CancellationToken.None);

        Assert.Equal(2, result.Hits.Count);
        Assert.Contains(result.Hits, h => h.PlayerId == 101473 && h.Address == 0x10000 + 1000);
        Assert.Contains(result.Hits, h => h.PlayerId == 58120 && h.Address == 0x10000 + 2000);
        Assert.All(result.Hits, h => Assert.True(h.Accepted));
        Assert.All(result.Hits, h => Assert.Equal(FamilyResultClass.MaskedRecordCopy, h.ResultClass));
    }

    [Fact]
    public async Task ScanAsync_CrossesBlockBoundariesWithoutDuplication()
    {
        var gateway = new FakeProcessMemoryGateway();
        var scanner = new MultiAnchorScanner(gateway);
        var profile = CreateTestProfile();
        var controls = new[] { CreateTestRecord(101473, "MESSI") };
        var fingerprints = FingerprintBuilder.Build(profile, controls);

        var memory = new byte[8192];
        // Coloca o registro exatamente no limite do chunk (chunk size default no profile é 4096)
        // Offset 4096 - 100 = 3996. O registro cruza de 3996 até 4376.
        var crossBoundaryOffset = 4096 - 100;
        Array.Copy(controls[0].RawRecord, 0, memory, crossBoundaryOffset, 380);
        gateway.MapRegion(0x10000, memory);

        var regions = new[] { new MemoryRegionInfo(0x10000, 8192, "MEM_COMMIT", "PAGE_READWRITE", "MEM_PRIVATE", true, true, false) };

        var result = await scanner.ScanAsync(
            AttachmentId.New(), 
            fingerprints, 
            profile, 
            RegionPolicy.DefaultPlayerArena, 
            FamilyScanBudget.Unlimited, 
            regions, 
            CancellationToken.None);

        // Deve encontrar exatamente 1 hit (não duplica, nem falha em encontrar)
        Assert.Single(result.Hits);
        Assert.Equal((ulong)(0x10000 + crossBoundaryOffset), result.Hits[0].Address);
    }
    
    [Fact]
    public async Task ScanAsync_RejectsShiftedFalsePositive()
    {
        var gateway = new FakeProcessMemoryGateway();
        var scanner = new MultiAnchorScanner(gateway);
        var profile = CreateTestProfile();
        var control = CreateTestRecord(101473, "MESSI");
        var fingerprints = FingerprintBuilder.Build(profile, new[] { control });

        var memory = new byte[4096];
        
        // Simula um "ID match" mas o resto do registro está deslocado por 3 bytes
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(1000, 4), 101473);
        Array.Copy(control.RawRecord, 3, memory, 1004, 377 - 3);

        gateway.MapRegion(0x10000, memory);
        var regions = new[] { new MemoryRegionInfo(0x10000, 4096, "MEM_COMMIT", "PAGE_READWRITE", "MEM_PRIVATE", true, true, false) };

        var result = await scanner.ScanAsync(
            AttachmentId.New(), 
            fingerprints, 
            profile, 
            RegionPolicy.DefaultPlayerArena, 
            FamilyScanBudget.Unlimited, 
            regions, 
            CancellationToken.None);

        // Deve achar o hit (por causa do ID), mas ele deve ser classificado como falso positivo rejeitado
        Assert.Single(result.Hits);
        Assert.False(result.Hits[0].Accepted);
        Assert.Equal(FamilyResultClass.RefutedFalsePositive, result.Hits[0].ResultClass);
    }
}
