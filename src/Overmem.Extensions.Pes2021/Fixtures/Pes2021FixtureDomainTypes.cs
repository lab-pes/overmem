using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Stable identity for a competition inside a PES 2021 Master League calendar record.
/// The value is a <c>u16</c> read from <c>record+0x00</c>. A value of <c>0xFFFF</c> is reserved
/// as an invalid sentinel and is never produced by the parser.
/// </summary>
[JsonConverter(typeof(CompetitionIdJsonConverter))]
public readonly record struct CompetitionId(ushort Value) : IEquatable<CompetitionId>
{
    public const ushort SentinelValue = 0xFFFF;

    public bool IsValid => Value != SentinelValue;

    public static CompetitionId FromUInt16(ushort value) => new(value);

    public bool Equals(CompetitionId other) => Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    private sealed class CompetitionIdJsonConverter : JsonConverter<CompetitionId>
    {
        public override CompetitionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.Number)
            {
                throw new JsonException($"Expected a numeric competitionId, got {reader.TokenType}.");
            }

            var value = reader.GetUInt16();
            return new CompetitionId(value);
        }

        public override void Write(Utf8JsonWriter writer, CompetitionId value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// Composite identity of a team participant inside a calendar record. The combination
/// of <see cref="TeamId"/> and <see cref="TeamLiga"/> is the runtime identity; resolving a
/// team by <see cref="TeamId"/> alone is an exceptional fallback that must be audited.
/// </summary>
[JsonConverter(typeof(TeamKeyJsonConverter))]
public readonly record struct TeamKey(ushort TeamId, ushort TeamLiga) : IEquatable<TeamKey>
{
    public const ushort SentinelValue = 0xFFFF;

    public bool IsValid => TeamId != SentinelValue && TeamLiga != SentinelValue;

    public static TeamKey FromUInt16(ushort teamId, ushort teamLiga) => new(teamId, teamLiga);

    public bool Equals(TeamKey other) => TeamId == other.TeamId && TeamLiga == other.TeamLiga;
    public override int GetHashCode() => HashCode.Combine(TeamId, TeamLiga);

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{TeamId}/{TeamLiga}");

    private sealed class TeamKeyJsonConverter : JsonConverter<TeamKey>
    {
        public override TeamKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected a teamKey object, got {reader.TokenType}.");
            }

            ushort teamId = 0;
            ushort teamLiga = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new TeamKey(teamId, teamLiga);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Unexpected token {reader.TokenType} in teamKey.");
                }

                var propertyName = reader.GetString();
                reader.Read();
                switch (propertyName)
                {
                    case "teamId":
                        teamId = reader.GetUInt16();
                        break;
                    case "teamLiga":
                        teamLiga = reader.GetUInt16();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            throw new JsonException("teamKey object was not closed.");
        }

        public override void Write(Utf8JsonWriter writer, TeamKey value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("teamId", value.TeamId);
            writer.WriteNumber("teamLiga", value.TeamLiga);
            writer.WriteEndObject();
        }
    }
}

public enum FixtureExtractionStatus
{
    FixturesOnly,
}

public enum RawScoreState
{
    RawZeroOrUnplayed,
    RawNonzeroUnvalidated,
}

public enum NameResolutionStatus
{
    ExactComposite,
    UniqueTeamIdFallback,
    Unresolved,
    Ambiguous,
    Conflict,
}

public enum CacheDisposition
{
    ProvidedAddress,
    Reused,
    Discovered,
    Rediscovered,
    Refused,
}

public enum NormalizationStrategy
{
    CompetitionBlockOnly,
    KnownSeasonStartIndex,
    ScanArrayBoundary,
}
