namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

/// <summary>
/// Códigos de erro estáveis para o Family Discovery System. Segue o mesmo padrão de
/// <see cref="PlayerRecordRejectionReasons"/>. Novos códigos exigem bump de versão
/// do contrato e atualização da documentação wire-contract.
/// </summary>
public static class FamilyDiscoveryErrors
{
    /// <summary>Nenhum jogador de controle foi fornecido.</summary>
    public const string NoControlPlayers = "FDS_NO_CONTROL_PLAYERS";

    /// <summary>Nenhuma região de memória passou no filtro da política.</summary>
    public const string NoRegionsAccepted = "FDS_NO_REGIONS_ACCEPTED";

    /// <summary>O orçamento de bytes, tempo ou hits foi excedido.</summary>
    public const string BudgetExceeded = "FDS_BUDGET_EXCEEDED";

    /// <summary>O processo-alvo foi encerrado ou reiniciado durante o scan.</summary>
    public const string ProcessTerminated = "FDS_PROCESS_TERMINATED";

    /// <summary>A revalidação das âncoras no início ou fim do scan falhou.</summary>
    public const string AnchorRevalidationFailed = "FDS_ANCHOR_REVALIDATION_FAILED";

    /// <summary>Poucos hits para inferir um stride com confiança (mínimo 3).</summary>
    public const string TooFewHitsForStride = "FDS_TOO_FEW_HITS_FOR_STRIDE";

    /// <summary>Empate entre strides candidatos que não pôde ser resolvido.</summary>
    public const string AmbiguousStrideTie = "FDS_AMBIGUOUS_STRIDE_TIE";

    /// <summary>Candidato refutado como falso positivo durante validação rigorosa.</summary>
    public const string FalsePositiveRefuted = "FDS_FALSE_POSITIVE_REFUTED";

    /// <summary>Leitura parcial de uma região — bloco ou página ilegível.</summary>
    public const string PartialRead = "FDS_PARTIAL_READ";

    /// <summary>Página inteira inacessível dentro de uma região aceita.</summary>
    public const string PageUnreadable = "FDS_PAGE_UNREADABLE";
}
