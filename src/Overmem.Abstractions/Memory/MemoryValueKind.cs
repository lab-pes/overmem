namespace Overmem.Abstractions.Memory;

public enum MemoryValueKind
{
    Bytes = 0,
    Int32 = 1,
    Int64 = 2,
    Float = 3,
    Double = 4,
    Utf8String = 5,
    Utf16String = 6,
}