using System.Collections.Generic;
using System.Linq;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Resolves <see cref="TeamKey"/> participants to a <see cref="FixtureParticipant"/> using
/// the catalog loaded by <see cref="Pes2021FixtureCatalogLoader"/>. The algorithm is the
/// one fixed in <c>requirements-and-decisions.md</c>: exact composite first, conflict wins
/// over every fallback, simple <c>teamId</c> only when there is exactly one entry and one
/// non-conflicting name. The resolver never hides collisions: every participant appears in
/// the output with its <see cref="NameResolutionStatus"/>.
/// </summary>
public static class Pes2021FixtureNameResolver
{
    public static IReadOnlyList<TeamKey> SortUnresolved(IEnumerable<TeamKey> keys)
    {
        var list = keys
            .Distinct()
            .OrderBy(static key => key.TeamId)
            .ThenBy(static key => key.TeamLiga)
            .ToArray();
        return list;
    }

    public static FixtureParticipant Resolve(TeamKey key, Pes2021FixtureCatalog catalog)
    {
        if (!key.IsValid)
        {
            return new FixtureParticipant(key, null, NameResolutionStatus.Unresolved, null);
        }

        if (catalog.TeamConflicts.Any(conflict => conflict.Key == key))
        {
            return new FixtureParticipant(key, null, NameResolutionStatus.Conflict, BuildConflictSource(catalog, key));
        }

        var exactEntries = catalog.TeamEntries.Where(entry => entry.Key == key).ToArray();
        if (exactEntries.Length > 0)
        {
            var distinctNames = exactEntries.Select(static entry => entry.Name).Distinct().ToArray();
            if (distinctNames.Length == 1)
            {
                return new FixtureParticipant(key, distinctNames[0], NameResolutionStatus.ExactComposite, exactEntries[0].SourcePath);
            }

            return new FixtureParticipant(key, null, NameResolutionStatus.Conflict, exactEntries[0].SourcePath);
        }

        if (catalog.TeamConflicts.Any(conflict => conflict.Key.TeamId == key.TeamId))
        {
            return new FixtureParticipant(key, null, NameResolutionStatus.Conflict, BuildConflictSource(catalog, key));
        }

        var teamIdEntries = catalog.TeamEntries.Where(entry => entry.Key.TeamId == key.TeamId).ToArray();
        if (teamIdEntries.Length == 1)
        {
            return new FixtureParticipant(key, teamIdEntries[0].Name, NameResolutionStatus.UniqueTeamIdFallback, teamIdEntries[0].SourcePath);
        }

        if (teamIdEntries.Length > 1)
        {
            return new FixtureParticipant(key, null, NameResolutionStatus.Ambiguous, teamIdEntries[0].SourcePath);
        }

        return new FixtureParticipant(key, null, NameResolutionStatus.Unresolved, null);
    }

    private static string? BuildConflictSource(Pes2021FixtureCatalog catalog, TeamKey key)
    {
        var conflict = catalog.TeamConflicts.FirstOrDefault(candidate => candidate.Key == key);
        if (conflict is null)
        {
            return null;
        }

        return string.Join(",", conflict.SourcePaths);
    }
}
