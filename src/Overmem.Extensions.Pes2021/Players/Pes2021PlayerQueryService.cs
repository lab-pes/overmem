using System.Collections.Generic;
using System.Linq;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Read-only query service for decoded player records. Returns decoded snapshots; never
/// reads memory on its own. Ambiguous lookups return every match; the caller must narrow
/// by (recordAddress, fingerprint).
/// </summary>
public sealed class Pes2021PlayerQueryService
{
    private readonly Pes2021PlayerCatalog _catalog;

    public Pes2021PlayerQueryService(Pes2021PlayerCatalog catalog)
    {
        _catalog = catalog;
    }

    public PlayerQueryResult QueryByPlayerId(uint playerId)
    {
        var matches = _catalog.Snapshot().Where(p => p.PlayerId == playerId).ToList();
        return new PlayerQueryResult(matches.Count > 1, matches);
    }

    public PlayerNameQueryResult QueryByName(string name, bool exactMatch = true)
    {
        if (string.IsNullOrEmpty(name))
        {
            return new PlayerNameQueryResult(System.Array.Empty<DecodedPlayerRecord>());
        }

        var snapshot = _catalog.Snapshot();
        var matches = exactMatch
            ? snapshot.Where(p => string.Equals(p.PlayerName, name, System.StringComparison.Ordinal)).ToList()
            : snapshot.Where(p => p.PlayerName is not null && p.PlayerName.Contains(name, System.StringComparison.OrdinalIgnoreCase)).ToList();
        return new PlayerNameQueryResult(matches);
    }

    public IReadOnlyList<DecodedPlayerRecord> Snapshot() => _catalog.Snapshot();
}