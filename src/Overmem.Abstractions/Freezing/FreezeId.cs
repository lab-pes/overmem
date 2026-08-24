namespace Overmem.Abstractions.Freezing;

public readonly record struct FreezeId(Guid Value)
{
    public static FreezeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}