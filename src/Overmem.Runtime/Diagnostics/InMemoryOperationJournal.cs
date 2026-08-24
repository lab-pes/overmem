namespace Overmem.Runtime.Diagnostics;

public sealed class InMemoryOperationJournal : IOperationJournal
{
    private readonly int _capacity;
    private readonly LinkedList<OperationLogEntry> _entries = [];
    private readonly object _sync = new();

    public InMemoryOperationJournal(int capacity = 512)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _capacity = capacity;
    }

    public IReadOnlyList<OperationLogEntry> ListRecent(int maxCount = 100)
    {
        if (maxCount <= 0)
        {
            return [];
        }

        lock (_sync)
        {
            return _entries.Take(maxCount).ToArray();
        }
    }

    public void Record(OperationLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_sync)
        {
            _entries.AddFirst(entry);
            while (_entries.Count > _capacity)
            {
                _entries.RemoveLast();
            }
        }
    }
}