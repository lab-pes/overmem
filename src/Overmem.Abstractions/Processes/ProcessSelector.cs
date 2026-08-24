namespace Overmem.Abstractions.Processes;

public sealed record ProcessSelector(int? ProcessId = null, string? ProcessName = null)
{
    public bool IsValid() => ProcessId is > 0 || !string.IsNullOrWhiteSpace(ProcessName);
}