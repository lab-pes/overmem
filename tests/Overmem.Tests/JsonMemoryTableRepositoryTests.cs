using Overmem.Abstractions.Memory;
using Overmem.Application.Tables;

namespace Overmem.Tests;

public sealed class JsonMemoryTableRepositoryTests
{
    [Fact]
    public async Task SaveAndLoadRoundTripPreservesDocument()
    {
        var repository = new JsonMemoryTableRepository();
        var path = Path.Combine(Path.GetTempPath(), $"overmem-table-{Guid.NewGuid():N}.json");
        var document = new MemoryTableDocument(
            MemoryTableDocument.CurrentSchemaVersion,
            "Player",
            [new MemoryTableEntry("health", "Health", MemoryValueKind.Int32, MemoryTableAddressKind.Absolute, AbsoluteAddress: 0x1000, Freeze: new MemoryTableFreezeConfiguration("999", 20))]);

        try
        {
            await repository.SaveAsync(path, document);
            var loaded = await repository.LoadAsync(path);

            Assert.Equal(document.SchemaVersion, loaded.SchemaVersion);
            Assert.Equal(document.Name, loaded.Name);
            Assert.Equal(document.Entries, loaded.Entries);
        }
        finally
        {
            File.Delete(path);
        }
    }
}