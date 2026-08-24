namespace Overmem.Application.Tables;

public interface IMemoryTableRepository
{
    Task SaveAsync(string filePath, MemoryTableDocument document, CancellationToken cancellationToken = default);

    Task<MemoryTableDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}