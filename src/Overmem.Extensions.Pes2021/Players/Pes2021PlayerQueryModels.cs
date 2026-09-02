using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Result of <see cref="Pes2021PlayerQueryService.QueryByPlayerIdAsync"/>. When
/// <see cref="Ambiguous"/> is true, <see cref="Results"/> contains every match; the
/// caller must narrow by <c>(recordAddress, fingerprint)</c>.
/// </summary>
public sealed record PlayerQueryResult(
    bool Ambiguous,
    IReadOnlyList<DecodedPlayerRecord> Results);

/// <summary>
/// Result of <see cref="Pes2021PlayerQueryService.QueryByNameAsync"/>. Empty matches are
/// reported as a successful empty result, not an error.
/// </summary>
public sealed record PlayerNameQueryResult(
    IReadOnlyList<DecodedPlayerRecord> Results);

/// <summary>
/// Top-level wire payload for the catalog export schema <c>pes2021.players.v1</c>. Built
/// by <see cref="Pes2021PlayerCatalogExporter"/>.
/// </summary>
public sealed record PlayerCatalogExport(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("session")] PlayerSession Session,
    [property: JsonPropertyName("summary")] PlayerCatalogSummary Summary,
    [property: JsonPropertyName("players")] IReadOnlyList<PlayerCatalogEntry> Players,
    [property: JsonPropertyName("diagnostics")] PlayerDiscoveryDiagnostics Diagnostics,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings);

/// <summary>
/// Aggregate counts for the catalog export.
/// </summary>
public sealed record PlayerCatalogSummary(
    [property: JsonPropertyName("recordsDecoded")] int RecordsDecoded,
    [property: JsonPropertyName("recordsAccepted")] int RecordsAccepted,
    [property: JsonPropertyName("recordsRejected")] int RecordsRejected,
    [property: JsonPropertyName("duplicatePlayerIds")] int DuplicatePlayerIds,
    [property: JsonPropertyName("uniquePlayerIds")] int UniquePlayerIds);

/// <summary>
/// One decoded player in the catalog export shape.
/// </summary>
public sealed record PlayerCatalogEntry(
    [property: JsonPropertyName("recordAddress")] string RecordAddress,
    [property: JsonPropertyName("playerId")] uint PlayerId,
    [property: JsonPropertyName("fingerprint")] string? Fingerprint,
    [property: JsonPropertyName("context")] string Context,
    [property: JsonPropertyName("rawRecordSha256")] string RawRecordSha256,
    [property: JsonPropertyName("fields")] IReadOnlyList<PlayerCatalogField> Fields);

/// <summary>
/// One field decoded in the catalog export shape.
/// </summary>
public sealed record PlayerCatalogField(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("raw")] System.Text.Json.JsonElement Raw,
    [property: JsonPropertyName("display")] System.Text.Json.JsonElement? Display,
    [property: JsonPropertyName("transform")] string? Transform,
    [property: JsonPropertyName("evidenceStatus")] string EvidenceStatus,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings);