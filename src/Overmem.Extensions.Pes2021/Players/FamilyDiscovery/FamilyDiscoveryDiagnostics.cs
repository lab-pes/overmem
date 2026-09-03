using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

/// <summary>
/// Diagnóstico de uma região individual durante a descoberta de famílias.
/// Registra tanto regiões examinadas quanto ignoradas, com justificativa.
/// </summary>
public sealed record FamilyRegionDiagnostic(
    string BaseAddress,
    string StopAddress,
    ulong Size,
    string State,
    string Type,
    string Protection,
    string Decision,
    string? SkipReason,
    ulong BytesRequested,
    ulong BytesRead);

/// <summary>
/// Diagnósticos agregados para uma execução completa do Family Discovery System.
/// Segue o padrão de <see cref="PlayerDiscoveryDiagnostics"/> estendido com métricas
/// específicas de famílias. Toda região, todo hit, toda rejeição é registrada;
/// nenhuma página não lida é oculta.
/// </summary>
public sealed record FamilyDiscoveryDiagnostics(
    int RegionsEnumerated,
    int RegionsExamined,
    int RegionsSkipped,
    ulong BytesRequested,
    ulong BytesRead,
    ulong BytesSkippedUnreadable,
    int TotalHits,
    int AcceptedHits,
    int RejectedHits,
    int FamiliesDiscovered,
    int AmbiguousFamilies,
    IReadOnlyDictionary<string, int> RejectionReasons,
    IReadOnlyDictionary<string, double> StageDurationMs,
    IReadOnlyList<FamilyRegionDiagnostic> Regions);
