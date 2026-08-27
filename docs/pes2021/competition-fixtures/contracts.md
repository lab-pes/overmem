# Contratos de domínio e saída

Este documento define a forma lógica dos tipos. Nomes C# podem receber o prefixo `Pes2021`, mas os campos wire devem permanecer estáveis em `camelCase` tanto na CLI quanto no MCP.

## Tipos de domínio

```csharp
public readonly record struct CompetitionId(ushort Value);

public readonly record struct TeamKey(ushort TeamId, ushort TeamLiga);

public enum FixtureExtractionStatus
{
    FixturesOnly
}

public enum RawScoreState
{
    RawZeroOrUnplayed,
    RawNonzeroUnvalidated
}

public enum NameResolutionStatus
{
    ExactComposite,
    UniqueTeamIdFallback,
    Unresolved,
    Ambiguous,
    Conflict
}

public enum CacheDisposition
{
    ProvidedAddress,
    Reused,
    Discovered,
    Rediscovered,
    Refused
}
```

`CompetitionId` e os membros de `TeamKey` armazenam `u16`. O valor `0xFFFF` é sentinela inválida para participante. Regras adicionais de competição pertencem ao perfil, não ao tipo básico.

## Registro cru e fixture enriquecida

```csharp
public sealed record RawCalendarRecord(
    int RecordIndex,
    ulong Address,
    CompetitionId CompetitionId,
    byte Round,
    ushort Year,
    byte Month,
    byte Day,
    TeamKey Home,
    TeamKey Away,
    byte HomeScoreRaw,
    byte AwayScoreRaw);

public sealed record FixtureParticipant(
    TeamKey Key,
    string? Name,
    NameResolutionStatus ResolutionStatus,
    string? ResolutionSource);

public sealed record Fixture(
    int RecordIndex,
    string Address,
    CompetitionId CompetitionId,
    byte Round,
    DateOnly Date,
    FixtureParticipant Home,
    FixtureParticipant Away,
    byte HomeScoreRaw,
    byte AwayScoreRaw,
    RawScoreState ScoreState);
```

Regras:

- `RecordIndex` é relativo a `CalendarArrayBaseAddress` quando ela for confirmada; caso contrário, é relativo a `CompetitionBlockBaseAddress` e `recordIndexOrigin` deve informar isso.
- `Address` é diagnóstico da sessão e nunca deve ser reutilizado em outro processo.
- `DateOnly` só é criado após validação real de calendário, não apenas `1..31`.
- `RawScoreState` não autoriza cálculo de classificação.

## Sessão e base descoberta

```csharp
public sealed record ProcessInstanceIdentity(
    Guid AttachmentId,
    int ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    string ProcessName);

public sealed record CalendarSession(
    ProcessInstanceIdentity Process,
    string ProfileId,
    string ProfileVersion,
    string ProfileSha256,
    int RecordStride,
    int RecordLimit,
    string? CalendarArrayBaseAddress,
    string CompetitionBlockBaseAddress,
    string AnchorAddress,
    int? AnchorIndex,
    string ValidationSampleSha256,
    DateTimeOffset ValidatedAtUtc,
    CacheDisposition CacheDisposition);
```

Se `ProcessStartedAtUtc` não puder ser obtido, o cache deve exigir attachment id, PID, perfil e revalidação de amostra. A ausência reduz a confiança e aparece nos diagnósticos.

```csharp
public sealed record FixtureAnchorResult(
    CalendarSession Session,
    CompetitionId CompetitionId,
    TeamKey? RequestedTeamKey,
    ushort RequestedTeamId,
    string AnchorAddress,
    string CompetitionBlockBaseAddress,
    string? CalendarArrayBaseAddress,
    int? AnchorIndex,
    DiscoveryConfidence Confidence,
    IReadOnlyList<AnchorCandidate> Candidates,
    ExtractionDiagnostics Diagnostics);
```

`DiscoveryConfidence` deve carregar `level`, `score`, `maxScore` e `reasons`. Níveis permitidos: `low`, `medium`, `high`. `high` exige identidade de processo, amostra válida, sequência no stride e normalização não ambígua.

## Diagnóstico

```csharp
public sealed record ExtractionDiagnostics(
    CacheDisposition CacheDisposition,
    int RegionsEnumerated,
    int RegionsAccepted,
    int RegionsRejected,
    ulong BytesRequested,
    ulong BytesRead,
    int ReadCalls,
    int BlocksRead,
    int RecordsDecoded,
    int RecordsAccepted,
    int RecordsRejected,
    IReadOnlyDictionary<string, int> RejectionReasons,
    IReadOnlyDictionary<string, double> StageDurationMs,
    IReadOnlyList<RegionDiagnostic> Regions,
    IReadOnlyList<string> Warnings);
```

Motivos de rejeição estáveis:

- `wrong_competition`
- `invalid_date`
- `sentinel_team`
- `team_mismatch`
- `team_liga_mismatch`
- `stride_sequence_too_short`
- `outside_region`
- `partial_read`
- `profile_constraint`
- `ambiguous_normalization`

## Resultado da extração

```csharp
public sealed record CompetitionFixtureExtractionResult(
    string SchemaVersion,
    FixtureExtractionStatus Status,
    string Warning,
    CalendarSession Session,
    CompetitionId CompetitionId,
    string? CompetitionName,
    NameResolutionStatus CompetitionNameStatus,
    string RecordIndexOrigin,
    int FixtureCount,
    int DistinctTeamCount,
    IReadOnlyList<TeamKey> UnresolvedTeamKeys,
    IReadOnlyList<CatalogConflict> CatalogConflicts,
    IReadOnlyList<Fixture> Fixtures,
    ExtractionDiagnostics Diagnostics);
```

Valores fixos para v1:

- `schemaVersion`: `pes2021.competition-fixtures.v1`
- `status`: `FIXTURES_ONLY` na serialização
- `warning`: `Raw scores do not prove that a fixture was completed. Do not derive standings from this payload.`
- ordenação: `date`, `round`, `recordIndex`, `home.key.teamId`, `away.key.teamId`

## Invariantes validáveis do resultado

- `fixtureCount == fixtures.Count`.
- `distinctTeamCount` conta `TeamKey`, não `teamId`.
- cada fixture tem o `competitionId` solicitado.
- nenhum participante possui `teamId=65535`.
- `unresolvedTeamKeys` é distinto, ordenado e contém todos os participantes sem nome.
- conflito nunca produz `ExactComposite` ou fallback.
- endereço de cada fixture respeita `base + index * stride` segundo `recordIndexOrigin`.
- uma saída com warnings continua completa; nenhum fixture é ocultado por falta de nome.

## Compatibilidade com o modelo atual

`Pes2021CalendarRecordSnapshot` pode ser mantido como adaptador durante a migração. A direção permitida é `RawCalendarRecord -> Fixture -> modelo legado`; o parser novo não deve produzir dois modelos por caminhos independentes.

