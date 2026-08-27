using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Shared JSON options used by every PES 2021 fixture payload (CLI, MCP, atomic writer and
/// in-process callers). The options apply <see cref="JsonNamingPolicy.CamelCase"/> for both
/// property names and dictionary keys, and serialize enums as SCREAMING_SNAKE_CASE so the
/// wire contract matches <c>docs/pes2021/competition-fixtures/contracts.md</c>.
/// </summary>
public static class Pes2021FixtureJson
{
    private static readonly JsonStringEnumConverter ScreamingSnakeEnumConverter = new(JsonNamingPolicy.SnakeCaseUpper);

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { ScreamingSnakeEnumConverter },
    };
}



