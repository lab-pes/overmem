using System.IO;
using System.Text.Json;

namespace Overmem.Extensions.Pes2021.Cli;

/// <summary>
/// Writes a JSON payload to disk atomically so external consumers (Lua/Sider modules) never
/// observe a partial file. The implementation follows the contract in
/// <c>docs/pes2021/competition-fixtures/api.md</c>: the JSON is first serialized to a
/// <c>.tmp</c> sibling in the same directory, fsync-flushed and then renamed over the
/// target. The rename is a single atomic operation on NTFS.
/// </summary>
public static class Pes2021AtomicFileWriter
{
    public static void WriteJson<T>(string outputPath, T payload, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        var absolutePath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = absolutePath + ".tmp";
        var fileStream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        try
        {
            JsonSerializer.Serialize(fileStream, payload, options);
            fileStream.Flush(flushToDisk: true);
        }
        finally
        {
            fileStream.Dispose();
        }

        if (File.Exists(absolutePath))
        {
            File.Replace(tempPath, absolutePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, absolutePath);
        }
    }
}
