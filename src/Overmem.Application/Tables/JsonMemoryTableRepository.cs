using System.Text.Json;

namespace Overmem.Application.Tables;

public sealed class JsonMemoryTableRepository : IMemoryTableRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public async Task SaveAsync(string filePath, MemoryTableDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(document);

        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var tempFilePath = Path.Combine(directoryPath ?? Path.GetTempPath(), $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken);
        }

        File.Move(tempFilePath, filePath, overwrite: true);
    }

    public async Task<MemoryTableDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = File.OpenRead(filePath);
        var document = await JsonSerializer.DeserializeAsync<MemoryTableDocument>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("The table file is empty or invalid.");

        return document;
    }
}