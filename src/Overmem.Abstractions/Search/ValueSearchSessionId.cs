namespace Overmem.Abstractions.Search;

public readonly record struct ValueSearchSessionId(Guid Value)
{
    public static ValueSearchSessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}