using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public static class Pes2021ClubRelationsCsvWriter
{
    private const string ObservationsHeader =
        "run_id,control_case,ui_club,ui_league,ui_country,player_id,player_name,team_id,secondary_id,club_record_address,country_id_raw,competition_id_raw,source,status,notes";

    private const string UnresolvedHeader =
        "run_id,team_id,secondary_id,name,reason,notes";

    private const string RegionSnapshotHeader =
        "run_id,region_base_address,region_size,region_state,region_protection,region_type,is_readable,is_writable,is_executable,is_included";

    private const string RegionBlockHeader =
        "run_id,region_base_address,block_index,block_offset,block_bytes,sha256";

    public static void WriteObservations(string path, IReadOnlyList<Pes2021ClubObservationRow> rows)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        writer.WriteLine(ObservationsHeader);
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", new[]
            {
                row.RunId.ToString("D", CultureInfo.InvariantCulture),
                Escape(row.ControlCase),
                Escape(row.UiClub),
                Escape(row.UiLeague),
                Escape(row.UiCountry),
                row.PlayerId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Escape(row.PlayerName ?? string.Empty),
                row.TeamId.ToString(CultureInfo.InvariantCulture),
                row.SecondaryId.ToString(CultureInfo.InvariantCulture),
                row.ClubRecordAddress?.ToString("X", CultureInfo.InvariantCulture) ?? string.Empty,
                row.CountryIdRaw?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.CompetitionIdRaw?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Escape(row.Source),
                Escape(row.Status),
                Escape(row.Notes)
            }));
        }
    }

    public static void WriteUnresolved(string path, IReadOnlyList<Pes2021ClubUnresolvedRow> rows)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        writer.WriteLine(UnresolvedHeader);
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", new[]
            {
                row.RunId.ToString("D", CultureInfo.InvariantCulture),
                row.TeamId.ToString(CultureInfo.InvariantCulture),
                row.SecondaryId.ToString(CultureInfo.InvariantCulture),
                Escape(row.Name ?? string.Empty),
                Escape(row.Reason),
                Escape(row.Notes)
            }));
        }
    }

    public static void WriteRegionSnapshot(string path, IReadOnlyList<Pes2021RegionSnapshotRow> rows)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        writer.WriteLine(RegionSnapshotHeader);
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", new[]
            {
                row.RunId.ToString("D", CultureInfo.InvariantCulture),
                row.RegionBaseAddress.ToString("X", CultureInfo.InvariantCulture),
                row.RegionSize.ToString(CultureInfo.InvariantCulture),
                Escape(row.RegionState),
                Escape(row.RegionProtection),
                Escape(row.RegionType),
                row.IsReadable ? "true" : "false",
                row.IsWritable ? "true" : "false",
                row.IsExecutable ? "true" : "false",
                row.IsIncluded ? "true" : "false"
            }));
        }
    }

    public static void WriteRegionBlocks(string path, IReadOnlyList<Pes2021RegionBlockRow> rows)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        writer.WriteLine(RegionBlockHeader);
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(",", new[]
            {
                row.RunId.ToString("D", CultureInfo.InvariantCulture),
                row.RegionBaseAddress.ToString("X", CultureInfo.InvariantCulture),
                row.BlockIndex.ToString(CultureInfo.InvariantCulture),
                row.BlockOffset.ToString(CultureInfo.InvariantCulture),
                row.BlockBytes.ToString(CultureInfo.InvariantCulture),
                Escape(row.Sha256)
            }));
        }
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
