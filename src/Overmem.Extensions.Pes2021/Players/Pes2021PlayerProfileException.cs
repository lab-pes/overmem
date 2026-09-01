namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Thrown when a player-record profile fails validation. The <see cref="Code"/> matches one
/// of the stable codes listed in <c>docs/pes2021/player-memory/error-codes.md</c> so CLI/MCP
/// surfaces can render it without parsing the message.
/// </summary>
public sealed class Pes2021PlayerProfileException : InvalidOperationException
{
    public Pes2021PlayerProfileException(string code, string message)
        : base($"[{code}] {message}")
    {
        Code = code;
    }

    public string Code { get; }
}
