using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021SourceManifestTests
{
    [Fact]
    public void Manifest_LoadsAndHasExpectedSchemaVersion()
    {
        var manifest = LoadManifest();
        Assert.Equal("pes2021.player-memory.source-manifest.v1", manifest.SchemaVersion);
        Assert.NotEmpty(manifest.Sources);
    }

    [Fact]
    public void Manifest_DisablesRuntimeDependencyOnEverySource()
    {
        var manifest = LoadManifest();
        Assert.All(manifest.Sources, source => Assert.False(source.RuntimeDependency));
    }

    [Fact]
    public void Manifest_OvermemPathForCtIsByteIdentical_WhenPresent()
    {
        var manifest = LoadManifest();
        var ct = manifest.Sources.Single(source => source.Id == "pes2021-ct");

        Assert.True(ct.ByteIdenticalCopy);
        Assert.False(string.IsNullOrWhiteSpace(ct.OvermemPath));

        var overmemAbsolutePath = ResolveRepoRelative(ct.OvermemPath!);
        Assert.True(File.Exists(overmemAbsolutePath), $"missing local CT copy at '{overmemAbsolutePath}'");

        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(overmemAbsolutePath))).ToLowerInvariant();
        Assert.Equal(ct.ExpectedSha256.ToLowerInvariant(), actual);
    }

    [Fact]
    public void Manifest_NoSourcePathContainsAbsoluteLiveAddress()
    {
        var manifest = LoadManifest();
        foreach (var source in manifest.Sources)
        {
            Assert.DoesNotContain("0x7FF4D908", source.ExternalPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("0x7FF4D908", source.OvermemPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Manifest_ConstraintsBlockAbsoluteAddressesAndExternalDependency()
    {
        var manifest = LoadManifest();
        Assert.True(manifest.Constraints.NoRuntimeDependency);
        Assert.True(manifest.Constraints.NoAbsoluteLiveAddresses);
        Assert.True(manifest.Constraints.NoExternalRepositoryRequirement);
        Assert.True(manifest.Constraints.TreatLuaAsEvidenceOnly);
    }

    private static Manifest LoadManifest()
    {
        var docsDirectory = ResolveRepoRelative(Path.Combine("docs", "pes2021", "player-memory"));
        var manifestPath = Path.Combine(docsDirectory, "source-manifest.json");
        Assert.True(File.Exists(manifestPath), $"missing manifest at '{manifestPath}'");

        var bytes = File.ReadAllBytes(manifestPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        var manifest = JsonSerializer.Deserialize<Manifest>(bytes, options);
        Assert.NotNull(manifest);
        return manifest!;
    }

    private static string ResolveRepoRelative(string relative)
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(Pes2021SourceManifestTests).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var root = Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar));
    }

    private sealed class Manifest
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string CaptureDate { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public ManifestSource[] Sources { get; set; } = System.Array.Empty<ManifestSource>();
        public ManifestConstraints Constraints { get; set; } = new();
    }

    private sealed class ManifestSource
    {
        public string Id { get; set; } = string.Empty;
        public string? ExternalPath { get; set; }
        public string? OvermemPath { get; set; }
        public string ExpectedSha256 { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool RuntimeDependency { get; set; }
        public bool ByteIdenticalCopy { get; set; }
    }

    private sealed class ManifestConstraints
    {
        public bool NoRuntimeDependency { get; set; }
        public bool NoAbsoluteLiveAddresses { get; set; }
        public bool NoExternalRepositoryRequirement { get; set; }
        public bool TreatLuaAsEvidenceOnly { get; set; }
    }
}
