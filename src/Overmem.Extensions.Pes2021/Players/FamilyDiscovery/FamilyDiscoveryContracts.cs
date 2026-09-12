using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

/// <summary>
/// Classes de resultado do Family Discovery System. A lista é fechada; novos membros
/// exigem um bump de versão do contrato FDS.
/// </summary>
public enum FamilyResultClass
{
    /// <summary>380 bytes idênticos ao registro do jogador de controle.</summary>
    ExactRecordCopy,

    /// <summary>380 bytes com máscara de campos dinâmicos aplicada.</summary>
    MaskedRecordCopy,

    /// <summary>Mesmo stride e offsets de campo validados por múltiplos controles.</summary>
    SameLayoutFamily,

    /// <summary>Layout diferente do padrão, mas com IDs reconhecidos.</summary>
    AlternateStrideFamily,

    /// <summary>ID e nome próximos em offsets não-padrão.</summary>
    IdNameColocated,

    /// <summary>Array contíguo de IDs sem payload.</summary>
    DenseIdTable,

    /// <summary>Ponteiros x64 para registros conhecidos.</summary>
    PointerTableCandidate,

    /// <summary>Hit sem vizinhos do mesmo stride.</summary>
    IsolatedHit,

    /// <summary>Empate não resolvido entre strides/regiões.</summary>
    AmbiguousFamily,

    /// <summary>Candidato rejeitado com justificativa.</summary>
    RefutedFalsePositive,
}

/// <summary>
/// Políticas de região que determinam quais categorias VirtualQuery são examinadas
/// pelo scanner de famílias.
/// </summary>
public enum RegionPolicy
{
    /// <summary>MEM_COMMIT + Private + RW (comportamento atual do scanner).</summary>
    DefaultPlayerArena,

    /// <summary>Adiciona regiões Mapped à política default.</summary>
    IncludeMapped,

    /// <summary>Adiciona regiões somente leitura.</summary>
    IncludeReadOnly,

    /// <summary>Adiciona regiões executáveis e legíveis (opt-in).</summary>
    IncludeExecutable,

    /// <summary>Qualquer região legível, independente de tipo.</summary>
    All,
}

/// <summary>
/// Disposição de um hit individual encontrado durante o scan de famílias.
/// Preserva a justificativa de aceite ou rejeição para auditoria.
/// </summary>
public sealed record FamilyHit(
    ulong Address,
    uint? PlayerId,
    string? PlayerName,
    FamilyResultClass ResultClass,
    int Score,
    IReadOnlyList<string> Reasons,
    bool Accepted);

/// <summary>
/// Orçamento configurável para o scan de famílias. Valores zero significam "sem limite".
/// </summary>
public sealed record FamilyScanBudget(
    long MaxBytes,
    int MaxRegions,
    int MaxHits,
    int MaxCandidates,
    int TimeoutMs)
{
    /// <summary>Orçamento sem limites (scan completo).</summary>
    public static FamilyScanBudget Unlimited { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>
/// Uma família descoberta pelo FDS. <see cref="FamilyId"/> é um identificador determinístico
/// derivado de <see cref="RegionBase"/>, <see cref="CandidateStride"/> e <see cref="CandidateResidue"/>.
/// Empates ficam explícitos em <see cref="Class"/> = <see cref="FamilyResultClass.AmbiguousFamily"/>;
/// a engine nunca escolhe pelo menor endereço.
/// </summary>
public sealed record DiscoveredFamily(
    string FamilyId,
    FamilyResultClass Class,
    ulong RegionBase,
    ulong RegionEnd,
    int CandidateStride,
    int CandidateResidue,
    int MatchedControls,
    int ExactMatches,
    int MaskedMatches,
    int IdOnlyMatches,
    int NameMatches,
    double NeighborConsistency,
    double Confidence,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<FamilyHit> Hits);

/// <summary>
/// Resultado agregado de uma descoberta de famílias. <see cref="AllHits"/> inclui
/// hits aceitos e rejeitados; <see cref="RejectedHits"/> é o subconjunto rejeitado.
/// </summary>
public sealed record FamilyDiscoveryResult(
    IReadOnlyList<DiscoveredFamily> Families,
    IReadOnlyList<FamilyHit> AllHits,
    IReadOnlyList<FamilyHit> RejectedHits,
    FamilyDiscoveryDiagnostics Diagnostics);
