# Planos de Evolução do Overmem

Quatro planos de implementação independentes, ordenados por impacto arquitetural e dependências entre si.

---

## Plano 1: Desacoplamento do PES 2021 — Plugin System

### Objetivo

Extrair todo o código específico do PES 2021 (`Pes2021AgendaService`, `Pes2021AgendaTools`, modelos, CLI commands e testes) para um projeto separado `Overmem.Extensions.Pes2021`, transformando o core do Overmem em uma engine 100% agnóstica a jogos.

### Motivação

Hoje o PES 2021 está acoplado em 4 projetos: `Overmem.Application`, `Overmem.McpServer`, `Overmem.Cli` e `Overmem.Hosting`. Qualquer contribuidor que queira usar o Overmem para outro jogo (ex: FIFA, eFootball 2025) herdaria dependências do PES 2021 sem necessidade.

### Impacto Arquitetural

> [!IMPORTANT]
> Este é o plano com maior impacto estrutural. Ele altera a topologia de projetos da solution e move código entre assemblies. Recomendo executá-lo **antes** dos demais para que os planos 2, 3 e 4 já nasçam sobre a arquitetura desacoplada.

### Proposed Changes

#### Core — Abstrações de Extensão

##### [NEW] `src/Overmem.Abstractions/Extensions/IOvermemExtension.cs`
Interface de contrato para extensões/plugins:
```csharp
public interface IOvermemExtension
{
    string Name { get; }
    string Description { get; }
    IServiceCollection RegisterServices(IServiceCollection services);
}
```

##### [MODIFY] [OvermemPlatformServiceCollectionExtensions.cs](file:///d:/git-lab-pes/overmem/src/Overmem.Hosting/OvermemPlatformServiceCollectionExtensions.cs)
- Remover a linha `services.AddSingleton<Pes2021AgendaService>();`.
- Adicionar método `AddOvermemExtension<T>()` genérico.
- Remover `using Overmem.Application.Pes2021`.

##### [MODIFY] [Overmem.Hosting.csproj](file:///d:/git-lab-pes/overmem/src/Overmem.Hosting/Overmem.Hosting.csproj)
- Remover referência transitiva ao PES 2021 (que vem via `Overmem.Application`).

---

#### Novo Projeto — Overmem.Extensions.Pes2021

##### [NEW] `src/Overmem.Extensions.Pes2021/Overmem.Extensions.Pes2021.csproj`
- Referencia: `Overmem.Abstractions`, `Overmem.Application`.
- Contém: `Pes2021AgendaService`, `Pes2021AgendaProfile`, `Pes2021AgendaModels`.

##### [MOVE] `src/Overmem.Application/Pes2021/` → `src/Overmem.Extensions.Pes2021/`
Mover os 3 arquivos:
- `Pes2021AgendaModels.cs`
- `Pes2021AgendaProfile.cs`
- `Pes2021AgendaService.cs`

##### [NEW] `src/Overmem.Extensions.Pes2021/Pes2021Extension.cs`
Implementação de `IOvermemExtension` que registra `Pes2021AgendaService` no DI.

---

#### McpServer e CLI — Desacoplamento do PES 2021

##### [MODIFY] [OvermemServiceCollectionExtensions.cs](file:///d:/git-lab-pes/overmem/src/Overmem.McpServer/OvermemServiceCollectionExtensions.cs)
- Remover `.WithTools<Pes2021AgendaTools>()` do registro hardcoded.
- Adicionar registro dinâmico via extensões.

##### [MOVE] `src/Overmem.McpServer/Tools/Pes2021AgendaTools.cs` → `src/Overmem.Extensions.Pes2021/Tools/Pes2021AgendaTools.cs`

##### [MODIFY] [CliArguments.cs](file:///d:/git-lab-pes/overmem/src/Overmem.Cli/CliArguments.cs)
- Extrair os 10+ records `Pes2021*CliCommand` e o parsing correspondente para `Overmem.Extensions.Pes2021`.
- O CLI core mantém apenas os comandos genéricos (`modules`, `regions`, `read`, `write`, `scan-*`, `table-*`, `discover-pointers`).

##### [MODIFY] [CliApplication.cs](file:///d:/git-lab-pes/overmem/src/Overmem.Cli/CliApplication.cs)
- Mover os cases `pes2021-*` do `switch` para o projeto da extensão.

---

#### Testes

##### [MOVE] `tests/Overmem.Tests/Pes2021AgendaServiceTests.cs` → `tests/Overmem.Extensions.Pes2021.Tests/`
- Criar projeto de testes separado para a extensão.

##### [MODIFY] [Overmem.slnx](file:///d:/git-lab-pes/overmem/Overmem.slnx)
- Adicionar os novos projetos na solution.

---

### Verificação

- `dotnet build` compila todos os projetos sem erros.
- `dotnet test` passa 99/99 testes (os testes PES 2021 agora rodam no novo projeto de testes).
- O `Overmem.Application.csproj` não contém mais nenhuma referência a `Pes2021`.

---
---

## Plano 2: Scanner de Ponteiros Completo (Pointer Maps)

### Objetivo

Implementar o fluxo completo de Pointer Scanner que gera, armazena, filtra e reclassifica Pointer Maps — equivalente ao que o Cheat Engine oferece para encontrar ponteiros estáveis entre reinícios do processo.

### Motivação

Hoje o [PointerDiscoveryService](file:///d:/git-lab-pes/overmem/src/Overmem.Application/Pointers/PointerDiscoveryService.cs) faz uma busca BFS de profundidade limitada em uma única execução. Ele não persiste resultados, não compara entre sessões e não rankeia por estabilidade temporal. Para jogos que realocam endereços a cada reinício, o scanner precisa gerar um "mapa" de caminhos de ponteiros e comparar mapas de sessões diferentes.

### Proposed Changes

#### Abstrações — Pointer Map Contracts

##### [NEW] `src/Overmem.Abstractions/Pointers/PointerMapEntry.cs`
```csharp
public sealed record PointerMapEntry(
    ulong BaseAddress,
    IReadOnlyList<long> Offsets,
    string? ModuleName,
    long? ModuleRelativeBaseOffset,
    ulong ResolvedAddress,
    int Score);
```

##### [NEW] `src/Overmem.Abstractions/Pointers/PointerMap.cs`
```csharp
public sealed record PointerMap(
    string MapId,
    DateTimeOffset CapturedAt,
    string ProcessName,
    ulong TargetAddress,
    int MaxDepth,
    long MaxOffset,
    IReadOnlyList<PointerMapEntry> Entries);
```

##### [NEW] `src/Overmem.Abstractions/Pointers/PointerMapComparisonResult.cs`
```csharp
public sealed record PointerMapComparisonResult(
    string FirstMapId,
    string SecondMapId,
    int SharedPathCount,
    int FirstOnlyCount,
    int SecondOnlyCount,
    IReadOnlyList<PointerMapEntry> StablePaths,
    IReadOnlyList<PointerMapEntry> UnstablePaths);
```

---

#### Application — Pointer Map Service

##### [NEW] `src/Overmem.Application/Pointers/IPointerMapRepository.cs`
Interface para persistir/carregar Pointer Maps em JSON.

##### [NEW] `src/Overmem.Application/Pointers/JsonPointerMapRepository.cs`
Implementação com leitura/escrita de arquivos JSON.

##### [MODIFY] [IPointerDiscoveryService.cs](file:///d:/git-lab-pes/overmem/src/Overmem.Application/Pointers/IPointerDiscoveryService.cs)
Adicionar métodos:
- `Task<PointerMap> GenerateMapAsync(...)` — executa o scan completo e retorna um mapa nomeado.
- `Task<PointerMapComparisonResult> CompareAsync(PointerMap first, PointerMap second)` — compara dois mapas e retorna caminhos estáveis.
- `Task<IReadOnlyList<PointerMapEntry>> RankByStabilityAsync(IReadOnlyList<PointerMap> maps)` — rankeia ponteiros que aparecem em múltiplos mapas.

##### [MODIFY] [PointerDiscoveryService.cs](file:///d:/git-lab-pes/overmem/src/Overmem.Application/Pointers/PointerDiscoveryService.cs)
Implementar os 3 novos métodos. O `GenerateMapAsync` reutiliza o `DiscoverAsync` existente e empacota o resultado como `PointerMap`.

---

#### MCP & CLI

##### [NEW] `src/Overmem.McpServer/Tools/PointerMapTools.cs`
Ferramentas MCP: `generate_pointer_map`, `compare_pointer_maps`, `rank_pointer_stability`.

##### [MODIFY] [CliArguments.cs](file:///d:/git-lab-pes/overmem/src/Overmem.Cli/CliArguments.cs)
Adicionar commands: `pointer-map-generate`, `pointer-map-compare`, `pointer-map-rank`.

---

### Verificação

- Testes unitários com `FakeGateway` simulando ponteiros estáveis e instáveis entre 2 mapas.
- Teste de comparação verificando que ponteiros que existem em ambos os mapas são classificados como `Stable`.
- Teste de ranking verificando ordenação por score de estabilidade.

---
---

## Plano 3: Importação/Exportação de `.CT` (Cheat Engine Tables)

### Objetivo

Implementar leitura e escrita completas do formato `.CT` (XML do Cheat Engine), permitindo que o Overmem importe tabelas existentes da comunidade e exporte suas próprias memory tables nesse formato universal.

### Motivação

O formato `.CT` é o padrão de facto da comunidade de modding e engenharia reversa. Hoje o Overmem tem seu próprio formato JSON (`MemoryTableDocument`), mas não interopera com as milhares de tabelas `.CT` já existentes. Ao suportar importação/exportação, o Overmem se torna compatível com o ecossistema do Cheat Engine sem exigir migração manual.

### Proposed Changes

#### Novo Projeto — Overmem.CheatEngine

##### [NEW] `src/Overmem.CheatEngine/Overmem.CheatEngine.csproj`
- Referencia apenas `Overmem.Abstractions` e `Overmem.Application` (para `MemoryTableDocument`).
- Sem dependências externas além de `System.Xml.Linq`.

##### [NEW] `src/Overmem.CheatEngine/CheatTableDocument.cs`
Modelo de domínio representando um arquivo `.CT` completo:
```csharp
public sealed record CheatTableDocument(
    int CheatEngineTableVersion,
    string? LuaScript,
    IReadOnlyList<CheatTableEntry> Entries);

public sealed record CheatTableEntry(
    int Id,
    string Description,
    bool IsGroupHeader,
    string? VariableType,
    string? Address,
    IReadOnlyList<CheatTableOffset> Offsets,
    string? AssemblerScript,
    IReadOnlyList<CheatTableEntry> Children);

public sealed record CheatTableOffset(long Value);
```

##### [NEW] `src/Overmem.CheatEngine/CheatTableReader.cs`
Parser XML→`CheatTableDocument`. Usa `XDocument` para ler o formato CE nativo, percorrendo recursivamente os nós `<CheatEntry>`.

##### [NEW] `src/Overmem.CheatEngine/CheatTableWriter.cs`
Serializer `CheatTableDocument`→XML. Gera arquivo `.CT` válido e importável pelo Cheat Engine.

##### [NEW] `src/Overmem.CheatEngine/CheatTableConverter.cs`
Converte entre formatos:
- `CheatTableDocument` → `MemoryTableDocument` (importação).
- `MemoryTableDocument` → `CheatTableDocument` (exportação).

Mapeia `MemoryValueKind` ↔ strings CE (`"4 Bytes"`, `"Float"`, `"8 Bytes"`, `"String"`, etc.)

---

#### MCP & CLI

##### [NEW] `src/Overmem.McpServer/Tools/CheatTableTools.cs`
Ferramentas MCP: `import_cheat_table`, `export_cheat_table`, `inspect_cheat_table`.

##### [MODIFY] [CliArguments.cs](file:///d:/git-lab-pes/overmem/src/Overmem.Cli/CliArguments.cs)
Adicionar commands: `ct-import`, `ct-export`, `ct-inspect`.

---

#### Testes

##### [NEW] `tests/Overmem.CheatEngine.Tests/`
- `CheatTableReaderTests`: Ler [PES 2021 - v21.1.0.CT](file:///d:/git-lab-pes/overmem/files/PES%202021%20-%20v21.1.0.CT) (já no repositório!) e validar contagem de entries, IDs e descriptions.
- `CheatTableWriterTests`: Round-trip (ler → escrever → reler → comparar).
- `CheatTableConverterTests`: Converter CT→MemoryTable→CT e verificar integridade.

---

### Verificação

- Round-trip: `Read("PES 2021 - v21.1.0.CT")` → `Write(tempFile)` → `Read(tempFile)` produz o mesmo `CheatTableDocument`.
- Importação: Converter CT para `MemoryTableDocument` e verificar que entries com `<Address>` e `<Offsets>` são mapeados corretamente para `MemoryTableEntry`.
- Exportação: Converter `MemoryTableDocument` para CT e validar que o XML gerado é importável pelo Cheat Engine.

---
---

## Plano 4: CLI Interativa / TUI com Spectre.Console

### Objetivo

Criar um modo interativo (`overmem shell`) que mantém uma sessão de terminal persistente, permitindo ao operador humano navegar processos, inspecionar memória, buscar valores e gerenciar tabelas sem precisar redigitar argumentos a cada comando.

### Motivação

Hoje a CLI é *one-shot*: cada execução cria um attach, executa um comando e encerra. Isso é ideal para agentes de IA (MCP) e scripts, mas frustrante para operadores humanos que precisam explorar memória iterativamente. Uma TUI com autocomplete, tabelas formatadas e estado persistente transforma o Overmem em uma alternativa real ao Cheat Engine para quem prefere terminal.

> [!NOTE]
> Este plano depende dos comandos já existentes no CLI (genéricos + PES 2021). A implementação do shell interativo não altera o código dos comandos existentes — ela os reutiliza.

### Proposed Changes

#### Dependências

##### [MODIFY] [Overmem.Cli.csproj](file:///d:/git-lab-pes/overmem/src/Overmem.Cli/Overmem.Cli.csproj)
Adicionar:
```xml
<PackageReference Include="Spectre.Console" Version="0.49.*" />
```

---

#### Core do Shell Interativo

##### [NEW] `src/Overmem.Cli/Shell/OvermemShell.cs`
Loop principal do REPL:
1. Exibe prompt estilizado com estado atual (processo attached, PID, nome).
2. Lê input com autocomplete de comandos.
3. Parseia via `CliArgumentParser.Parse()` (reutiliza 100% do parser existente).
4. Executa via `CliApplication.RunAsync()` (reutiliza 100% do executor existente).
5. Mantém estado de sessão: attachment ativo, último resultado de search, última base descoberta.

##### [NEW] `src/Overmem.Cli/Shell/ShellState.cs`
Estado da sessão interativa:
```csharp
public sealed class ShellState
{
    public AttachmentInfo? CurrentAttachment { get; set; }
    public ValueSearchSessionId? LastSearchSession { get; set; }
    public ulong? LastBaseAddress { get; set; }
    public string? LastProcessName { get; set; }
}
```

##### [NEW] `src/Overmem.Cli/Shell/ShellPrompt.cs`
Prompt estilizado com Spectre.Console:
- `[green]overmem[/]` quando sem attachment.
- `[green]overmem[/]:[blue]PES2021.exe[/]([yellow]1234[/])>` quando attached.

##### [NEW] `src/Overmem.Cli/Shell/ShellCommandCompleter.cs`
Autocomplete para comandos (`modules`, `regions`, `read`, `write`, `scan-pattern`, etc.) e opções (`--pid`, `--name`, `--address`, etc.).

---

#### Comandos Exclusivos do Shell

##### [NEW] `src/Overmem.Cli/Shell/ShellCommands.cs`
Comandos que só fazem sentido no modo interativo:
- `attach --name PES2021.exe` → faz attach e salva no `ShellState`. Todos os comandos subsequentes herdam o PID.
- `detach` → desfaz o attach ativo.
- `status` → mostra o estado atual (processo, PID, última busca, última base).
- `history` → mostra os últimos N comandos executados.
- `clear` → limpa o terminal.
- `exit` / `quit` → encerra o shell.

---

#### Formatação Rica com Spectre.Console

##### [NEW] `src/Overmem.Cli/Shell/OutputFormatter.cs`
Em vez de JSON puro, formata resultados em tabelas Spectre:
- `modules` → tabela com colunas `Name`, `Base`, `Size`.
- `regions` → tabela com colunas `Base`, `Size`, `Type`, `Protection`.
- `scan-value` → tabela com colunas `Address`, `Value`, `Offset`.
- `discover-pointers` → tabela com colunas `Base`, `Module`, `Offsets`, `Score`, `Validated`.

O output JSON continua disponível via flag `--json` para compatibilidade com pipes e scripts.

---

#### Entry Point

##### [MODIFY] [Program.cs](file:///d:/git-lab-pes/overmem/src/Overmem.Cli/Program.cs)
Detectar se o primeiro argumento é `shell` ou se nenhum argumento foi passado:
- `overmem shell` → inicia o REPL interativo.
- `overmem modules --name PES2021.exe` → executa one-shot (comportamento atual preservado).

---

### Verificação

- Testes unitários para `ShellState` (attach/detach/status).
- Testes unitários para `ShellCommandCompleter` (verificar que autocomplete retorna comandos válidos).
- Teste de integração: simular sequência `attach → modules → scan-value → detach → exit`.
- Teste visual manual: rodar `overmem shell` e verificar prompt, tabelas formatadas e autocomplete.

---

## Ordem de Execução Recomendada

| Ordem | Plano | Justificativa |
| :---: | :--- | :--- |
| 1º | **Plano 1: Plugin System** | Fundação arquitetural. Os planos 2-4 devem nascer sobre a estrutura desacoplada. |
| 2º | **Plano 3: Importação/Exportação .CT** | Projeto isolado (`Overmem.CheatEngine`), sem dependências dos outros planos. Alta visibilidade para a comunidade. |
| 3º | **Plano 2: Pointer Maps** | Evolução do core existente (`PointerDiscoveryService`). Beneficia-se da arquitetura limpa do Plano 1. |
| 4º | **Plano 4: CLI Interativa** | Camada de apresentação. Consome todos os serviços dos planos anteriores. |

## Open Questions

> [!IMPORTANT]
> 1. **Plano 1**: Deseja que o `Overmem.McpServer` já carregue extensões dinamicamente via reflection/scanning de assemblies, ou prefere manter o registro explícito por enquanto (ex: `services.AddPes2021Extension()`)?
> 2. **Plano 2**: Qual o formato de persistência preferido para Pointer Maps? JSON (consistente com Memory Tables) ou um formato binário compacto para mapas com milhões de entradas?
> 3. **Plano 3**: Deseja suportar scripts Lua embutidos nos `.CT` (campo `<AssemblerScript>` e `<LuaScript>`) apenas como leitura/preservação, ou também como execução?
> 4. **Plano 4**: Deseja que o shell interativo funcione apenas no Windows, ou já deve ser cross-platform (considerando que `Overmem.Windows` é Windows-only)?
