using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;

namespace Overmem.Application.Tables;

public sealed class MemoryTableService(IProcessMemoryGateway gateway, IMemoryTableRepository repository)
{
    public Task SaveAsync(string filePath, MemoryTableDocument document, CancellationToken cancellationToken = default)
    {
        ValidateDocument(document);
        return repository.SaveAsync(filePath, document, cancellationToken);
    }

    public async Task<MemoryTableDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var document = await repository.LoadAsync(filePath, cancellationToken);
        ValidateDocument(document);
        return document;
    }

    public async Task<MemoryTableSnapshot> RefreshAsync(AttachmentId attachmentId, MemoryTableDocument document, CancellationToken cancellationToken = default)
    {
        ValidateDocument(document);

        var snapshots = new List<MemoryTableEntrySnapshot>(document.Entries.Count);
        foreach (var entry in document.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var resolvedAddress = await ResolveAddressAsync(attachmentId, entry, cancellationToken);
                var result = await gateway.ReadAsync(new ReadMemoryRequest(attachmentId, resolvedAddress, entry.ValueKind, entry.Size), cancellationToken);
                snapshots.Add(new MemoryTableEntrySnapshot(entry.EntryId, entry.Name, resolvedAddress, result.Value, null));
            }
            catch (Exception ex)
            {
                snapshots.Add(new MemoryTableEntrySnapshot(entry.EntryId, entry.Name, null, null, ex.Message));
            }
        }

        return new MemoryTableSnapshot(document.SchemaVersion, document.Name, snapshots);
    }

    public static void ValidateDocument(MemoryTableDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(document.SchemaVersion))
        {
            throw new ArgumentException("SchemaVersion is required.", nameof(document));
        }

        if (document.SchemaVersion != MemoryTableDocument.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported schema version '{document.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(document.Name))
        {
            throw new ArgumentException("Table name is required.", nameof(document));
        }

        foreach (var entry in document.Entries)
        {
            ValidateEntry(entry);
        }
    }

    public static void ValidateEntry(MemoryTableEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.EntryId))
        {
            throw new ArgumentException("EntryId is required.", nameof(entry));
        }

        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            throw new ArgumentException("Entry name is required.", nameof(entry));
        }

        if (entry.RefreshIntervalMs is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entry), "RefreshIntervalMs must be greater than zero when provided.");
        }

        if (entry.Freeze is { IntervalMs: <= 0 })
        {
            throw new ArgumentOutOfRangeException(nameof(entry), "Freeze.IntervalMs must be greater than zero when provided.");
        }

        switch (entry.AddressKind)
        {
            case MemoryTableAddressKind.Absolute when entry.AbsoluteAddress == 0:
                throw new ArgumentException("AbsoluteAddress is required for absolute entries.", nameof(entry));
            case MemoryTableAddressKind.Pointer when entry.BaseAddress == 0:
                throw new ArgumentException("BaseAddress is required for pointer entries.", nameof(entry));
            case MemoryTableAddressKind.ModulePointer when string.IsNullOrWhiteSpace(entry.ModuleName):
                throw new ArgumentException("ModuleName is required for module pointer entries.", nameof(entry));
        }
    }

    private Task<ulong> ResolveAddressAsync(AttachmentId attachmentId, MemoryTableEntry entry, CancellationToken cancellationToken)
    {
        return entry.AddressKind switch
        {
            MemoryTableAddressKind.Absolute => Task.FromResult(entry.AbsoluteAddress),
            MemoryTableAddressKind.Pointer => ResolvePointerAsync(attachmentId, entry, cancellationToken),
            MemoryTableAddressKind.ModulePointer => ResolveModulePointerAsync(attachmentId, entry, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(entry), "Unsupported address kind."),
        };
    }

    private async Task<ulong> ResolvePointerAsync(AttachmentId attachmentId, MemoryTableEntry entry, CancellationToken cancellationToken)
    {
        var result = await gateway.ResolvePointerAsync(new ResolvePointerRequest(attachmentId, entry.BaseAddress, entry.Offsets ?? []), cancellationToken);
        return result.ResolvedAddress;
    }

    private async Task<ulong> ResolveModulePointerAsync(AttachmentId attachmentId, MemoryTableEntry entry, CancellationToken cancellationToken)
    {
        var result = await gateway.ResolveModulePointerAsync(new ResolveModulePointerRequest(attachmentId, entry.ModuleName!, entry.BaseOffset, entry.Offsets ?? []), cancellationToken);
        return result.ResolvedAddress;
    }
}