using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Builds and serializes the catalog export payload. Uses atomic file writes via
/// <see cref="Overmem.Extensions.Pes2021.Cli.Pes2021AtomicFileWriter"/> so external
/// consumers (Sider modules, Lua scripts) never observe a partial file.
/// </summary>
public static class Pes2021PlayerCatalogExporter
{
    public const string SchemaVersion = "pes2021.players.v1";
    public const string Kind = "player_catalog";

    public static PlayerCatalogExport Build(PlayerDiscoveryResult result)
    {
        var entries = result.Players.Select(BuildEntry).ToList();
        var summary = new PlayerCatalogSummary(
            RecordsDecoded: result.Diagnostics.RecordsDecoded,
            RecordsAccepted: result.Diagnostics.RecordsAccepted,
            RecordsRejected: result.Diagnostics.RecordsRejected,
            DuplicatePlayerIds: result.Diagnostics.DuplicatePlayerIds,
            UniquePlayerIds: result.Players.Select(p => p.PlayerId).Distinct().Count());

        return new PlayerCatalogExport(
            SchemaVersion: SchemaVersion,
            Kind: Kind,
            Session: result.Session,
            Summary: summary,
            Players: entries,
            Diagnostics: result.Diagnostics,
            Warnings: result.Players.SelectMany(p => p.Warnings).Distinct().ToList());
    }

    private static PlayerCatalogEntry BuildEntry(DecodedPlayerRecord record)
    {
        var fields = record.Fields.Select(BuildField).ToList();
        return new PlayerCatalogEntry(
            RecordAddress: $"0x{record.Address:X}",
            PlayerId: record.PlayerId,
            Fingerprint: record.PlayerName,
            Context: "EDIT_BASE_CANDIDATE",
            RawRecordSha256: record.RawRecordSha256,
            Fields: fields);
    }

    private static PlayerCatalogField BuildField(DecodedFieldValue field)
    {
        var rawElement = ToJsonElement(field);
        System.Text.Json.JsonElement? displayElement = null;
        if (field.Display is double d)
        {
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteNumberValue(d);
            }
            displayElement = JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
        }

        var transform = field.Transform == Pes2021PlayerTransform.None ? null : field.Transform.ToString();
        var evidence = field.EvidenceStatus.ToString().ToUpperInvariant();
        return new PlayerCatalogField(
            Name: field.Name,
            Raw: rawElement,
            Display: displayElement,
            Transform: transform,
            EvidenceStatus: evidence,
            Warnings: field.Warnings);
    }

    private static System.Text.Json.JsonElement ToJsonElement(DecodedFieldValue field)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            if (field.RawString is not null)
            {
                writer.WriteStringValue(field.RawString);
            }
            else if (field.RawLong is long l)
            {
                writer.WriteNumberValue(l);
            }
            else
            {
                writer.WriteNullValue();
            }
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }
}