using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public enum ClubLayoutFieldStability
{
    Unknown,
    ConstantPerClub,
    ConstantAcrossClubs,
    VariablePerClub,
    LeagueSpecific,
    CountrySpecific
}

public sealed record Pes2021ClubLayoutWindow(
    int WindowSize,
    IReadOnlyList<Pes2021ClubLayoutWindowEntry> Entries);

public sealed record Pes2021ClubLayoutWindowEntry(
    int Offset,
    byte AsByte,
    ushort AsUInt16,
    uint AsUInt32,
    int AsInt32);

public sealed record Pes2021ClubLayoutField(
    int Offset,
    ClubLayoutFieldStability Stability,
    int SampleCount,
    int DistinctValueCount,
    IReadOnlyList<int> DistinctValues);

public sealed record Pes2021ClubLayoutCandidate(
    int TeamId,
    int SecondaryId,
    string Name,
    ulong Address,
    int ControlOrdinal,
    IReadOnlyList<Pes2021ClubLayoutWindow> Windows,
    IReadOnlyList<Pes2021ClubLayoutField> FieldSummary);
