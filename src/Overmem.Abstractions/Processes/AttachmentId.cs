namespace Overmem.Abstractions.Processes;

public readonly record struct AttachmentId(Guid Value)
{
    public static AttachmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}