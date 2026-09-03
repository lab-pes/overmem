using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Processes;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public sealed class FamilyCatalog
{
    private readonly string _catalogPath;
    private readonly AttachmentId _attachmentId;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public FamilyCatalog(string workspacePath, AttachmentId attachmentId)
    {
        _catalogPath = Path.Combine(workspacePath, $"pes2021_families_{attachmentId.Value.ToString("N")}.json");
        _attachmentId = attachmentId;
    }

    public async Task SaveAsync(FamilyDiscoveryResult result, CancellationToken cancellationToken)
    {
        var tempPath = _catalogPath + ".tmp";
        
        using var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(stream, result, _options, cancellationToken);
        
        stream.Position = 0;
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        var hashString = Convert.ToHexString(hash);
        
        stream.Dispose();

        File.Move(tempPath, _catalogPath, true);
        File.WriteAllText(_catalogPath + ".sha256", hashString);
    }

    public async Task<FamilyDiscoveryResult?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_catalogPath))
            return null;

        using var stream = new FileStream(_catalogPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return await JsonSerializer.DeserializeAsync<FamilyDiscoveryResult>(stream, _options, cancellationToken);
    }
}
