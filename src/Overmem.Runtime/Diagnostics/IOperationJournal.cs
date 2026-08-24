namespace Overmem.Runtime.Diagnostics;

public interface IOperationJournal
{
    void Record(OperationLogEntry entry);

    IReadOnlyList<OperationLogEntry> ListRecent(int maxCount = 100);
}