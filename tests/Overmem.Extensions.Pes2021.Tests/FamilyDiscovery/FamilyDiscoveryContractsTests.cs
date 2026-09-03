using System.Reflection;
using System.Text.Json;
using Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

namespace Overmem.Extensions.Pes2021.Tests.FamilyDiscovery;

public class FamilyDiscoveryContractsTests
{
    [Fact]
    public void FamilyResultClass_ContainsExactly10Members()
    {
        var members = Enum.GetValues<FamilyResultClass>();
        Assert.Equal(10, members.Length);
    }

    [Fact]
    public void FamilyResultClass_AllMembersHaveUniqueValues()
    {
        var values = Enum.GetValues<FamilyResultClass>().Select(v => (int)v).ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void FamilyResultClass_ContainsExpectedMembers()
    {
        var names = Enum.GetNames<FamilyResultClass>();
        Assert.Contains("ExactRecordCopy", names);
        Assert.Contains("MaskedRecordCopy", names);
        Assert.Contains("SameLayoutFamily", names);
        Assert.Contains("AlternateStrideFamily", names);
        Assert.Contains("IdNameColocated", names);
        Assert.Contains("DenseIdTable", names);
        Assert.Contains("PointerTableCandidate", names);
        Assert.Contains("IsolatedHit", names);
        Assert.Contains("AmbiguousFamily", names);
        Assert.Contains("RefutedFalsePositive", names);
    }

    [Fact]
    public void RegionPolicy_ContainsExactly5Members()
    {
        var members = Enum.GetValues<RegionPolicy>();
        Assert.Equal(5, members.Length);
    }

    [Fact]
    public void FamilyDiscoveryErrors_AllConstantsAreUnique()
    {
        var fields = typeof(FamilyDiscoveryErrors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.True(fields.Count > 0, "No error constants found");
        Assert.Equal(fields.Count, fields.Distinct().Count());
    }

    [Fact]
    public void FamilyDiscoveryErrors_AllConstantsStartWithFdsPrefix()
    {
        var fields = typeof(FamilyDiscoveryErrors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        foreach (var value in fields)
        {
            Assert.StartsWith("FDS_", value);
        }
    }

    [Fact]
    public void FamilyHit_CanBeCreated()
    {
        var hit = new FamilyHit(
            Address: 0x7FFF00001000,
            PlayerId: 101473,
            PlayerName: "MESSI",
            ResultClass: FamilyResultClass.ExactRecordCopy,
            Score: 10,
            Reasons: new[] { "player_id_match", "validator_accepted" },
            Accepted: true);

        Assert.Equal(0x7FFF00001000UL, hit.Address);
        Assert.Equal(101473U, hit.PlayerId);
        Assert.Equal("MESSI", hit.PlayerName);
        Assert.True(hit.Accepted);
        Assert.Equal(2, hit.Reasons.Count);
    }

    [Fact]
    public void FamilyHit_RejectedHitPreservesReasons()
    {
        var hit = new FamilyHit(
            Address: 0x7FFF00002000,
            PlayerId: null,
            PlayerName: null,
            ResultClass: FamilyResultClass.RefutedFalsePositive,
            Score: 0,
            Reasons: new[] { "false_candidate_offset_3" },
            Accepted: false);

        Assert.False(hit.Accepted);
        Assert.Equal(FamilyResultClass.RefutedFalsePositive, hit.ResultClass);
    }

    [Fact]
    public void DiscoveredFamily_CanBeCreated()
    {
        var family = new DiscoveredFamily(
            FamilyId: "test-family-001",
            Class: FamilyResultClass.SameLayoutFamily,
            RegionBase: 0x7FFF00000000,
            RegionEnd: 0x7FFF01000000,
            CandidateStride: 380,
            CandidateResidue: 0,
            MatchedControls: 5,
            ExactMatches: 3,
            MaskedMatches: 2,
            IdOnlyMatches: 0,
            NameMatches: 0,
            NeighborConsistency: 0.95,
            Confidence: 0.90,
            Reasons: new[] { "stride_380_confirmed", "5_controls_matched" },
            Hits: Array.Empty<FamilyHit>());

        Assert.Equal(380, family.CandidateStride);
        Assert.Equal(5, family.MatchedControls);
        Assert.True(family.Confidence > 0.5);
    }

    [Fact]
    public void FamilyScanBudget_Unlimited_HasZeroForAllLimits()
    {
        var budget = FamilyScanBudget.Unlimited;
        Assert.Equal(0, budget.MaxBytes);
        Assert.Equal(0, budget.MaxRegions);
        Assert.Equal(0, budget.MaxHits);
        Assert.Equal(0, budget.MaxCandidates);
        Assert.Equal(0, budget.TimeoutMs);
    }

    [Fact]
    public void FamilyDiscoveryResult_CanBeCreated()
    {
        var diagnostics = new FamilyDiscoveryDiagnostics(
            RegionsEnumerated: 100,
            RegionsExamined: 42,
            RegionsSkipped: 58,
            BytesRequested: 3_200_000_000,
            BytesRead: 3_100_000_000,
            BytesSkippedUnreadable: 100_000_000,
            TotalHits: 50,
            AcceptedHits: 45,
            RejectedHits: 5,
            FamiliesDiscovered: 2,
            AmbiguousFamilies: 0,
            RejectionReasons: new Dictionary<string, int> { ["FDS_FALSE_POSITIVE_REFUTED"] = 5 },
            StageDurationMs: new Dictionary<string, double> { ["scan"] = 1500.0 },
            Regions: Array.Empty<FamilyRegionDiagnostic>());

        var result = new FamilyDiscoveryResult(
            Families: Array.Empty<DiscoveredFamily>(),
            AllHits: Array.Empty<FamilyHit>(),
            RejectedHits: Array.Empty<FamilyHit>(),
            Diagnostics: diagnostics);

        Assert.Equal(2, result.Diagnostics.FamiliesDiscovered);
        Assert.Equal(0, result.Diagnostics.AmbiguousFamilies);
    }

    [Fact]
    public void DiscoveredFamily_JsonRoundTrip()
    {
        var original = new DiscoveredFamily(
            FamilyId: "roundtrip-test",
            Class: FamilyResultClass.SameLayoutFamily,
            RegionBase: 0x7FFF00000000,
            RegionEnd: 0x7FFF01000000,
            CandidateStride: 380,
            CandidateResidue: 0,
            MatchedControls: 10,
            ExactMatches: 8,
            MaskedMatches: 2,
            IdOnlyMatches: 0,
            NameMatches: 0,
            NeighborConsistency: 0.95,
            Confidence: 0.88,
            Reasons: new[] { "validated" },
            Hits: new[]
            {
                new FamilyHit(0x1000, 101473, "MESSI", FamilyResultClass.ExactRecordCopy, 10, new[] { "match" }, true),
            });

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<DiscoveredFamily>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.FamilyId, deserialized.FamilyId);
        Assert.Equal(original.Class, deserialized.Class);
        Assert.Equal(original.CandidateStride, deserialized.CandidateStride);
        Assert.Equal(original.RegionBase, deserialized.RegionBase);
        Assert.Equal(original.MatchedControls, deserialized.MatchedControls);
        Assert.Equal(original.Confidence, deserialized.Confidence);
        Assert.Single(deserialized.Hits);
        Assert.Equal(101473U, deserialized.Hits[0].PlayerId);
    }

    [Fact]
    public void FamilyRegionDiagnostic_CanBeCreated()
    {
        var diag = new FamilyRegionDiagnostic(
            BaseAddress: "0x7FFF00000000",
            StopAddress: "0x7FFF01000000",
            Size: 0x01000000,
            State: "MEM_COMMIT",
            Type: "Private",
            Protection: "PAGE_READWRITE",
            Decision: "examined",
            SkipReason: null,
            BytesRequested: 16_777_216,
            BytesRead: 16_777_216);

        Assert.Equal("examined", diag.Decision);
        Assert.Null(diag.SkipReason);
    }

    [Fact]
    public void FamilyRegionDiagnostic_Skipped_IncludesReason()
    {
        var diag = new FamilyRegionDiagnostic(
            BaseAddress: "0x7FFF02000000",
            StopAddress: "0x7FFF02010000",
            Size: 0x10000,
            State: "MEM_RESERVE",
            Type: "Private",
            Protection: "PAGE_NOACCESS",
            Decision: "skipped",
            SkipReason: "state_mismatch",
            BytesRequested: 0,
            BytesRead: 0);

        Assert.Equal("skipped", diag.Decision);
        Assert.Equal("state_mismatch", diag.SkipReason);
    }
}
