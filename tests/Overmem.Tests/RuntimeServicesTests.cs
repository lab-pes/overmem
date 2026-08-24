using Overmem.Abstractions.Processes;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;

namespace Overmem.Tests;

public sealed class RuntimeServicesTests
{
    [Fact]
    public void OperationJournal_KeepsMostRecentEntriesWithinCapacity()
    {
        var journal = new InMemoryOperationJournal(capacity: 2);

        journal.Record(new OperationLogEntry(Guid.NewGuid(), "first", "Succeeded", DateTimeOffset.UtcNow));
        journal.Record(new OperationLogEntry(Guid.NewGuid(), "second", "Succeeded", DateTimeOffset.UtcNow));
        journal.Record(new OperationLogEntry(Guid.NewGuid(), "third", "Succeeded", DateTimeOffset.UtcNow));

        var entries = journal.ListRecent();
        Assert.Collection(entries,
            entry => Assert.Equal("third", entry.Name),
            entry => Assert.Equal("second", entry.Name));
    }

    [Fact]
    public void AttachmentRegistry_TracksTouchAndRemoval()
    {
        var registry = new InMemoryAttachmentSessionRegistry();
        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "demo", ProcessArchitecture.X64);
        var attachedAt = DateTimeOffset.UtcNow;
        var lastSeenAt = attachedAt.AddSeconds(1);

        registry.Register(attachment, attachedAt);
        Assert.True(registry.TryTouch(attachment.AttachmentId, lastSeenAt));

        var active = Assert.Single(registry.ListActive());
        Assert.Equal(lastSeenAt, active.LastSeenAtUtc);
        Assert.True(registry.Remove(attachment.AttachmentId));
        Assert.Empty(registry.ListActive());
    }
}