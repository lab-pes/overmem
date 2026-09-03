-- ml_player_info.lua
-- Exibe informações do jogador selecionado no menu ML/edição no overlay do Sider
-- Autor: gerado com base no CT de xAranaktu (PES 2021 - v21.1.0.CT)
--
-- REQUISITOS no sider.ini:
--   luajit.ext.enabled = 1
--   lua.module = "ml_player_info.lua"
--
-- USO:
--   No menu ML, selecione (ou entre na tela de edição de) um jogador.
--   Pressione SPACE para abrir o overlay — as informações aparecem.
--   Hook 1 atualiza ptrPlayer cada vez que o jogo copia dados do jogador selecionado.

local m = {}

-- ─── FFI Setup ────────────────────────────────────────────────────────────────
-- Nota: no Sider, 'ffi' é um global pré-injetado (luajit.ext.enabled = 1).
-- Não usar require("ffi") — 'require' não existe neste ambiente Lua.

-- ─── Estratégia de hook ─────────────────────────────────────────────────────
-- Hook em "PES2021.exe"+C650F6 (NUNCA tocado pelo CT).
--
-- O CT (xAranaktu) injeta em C650F0 (0F 10 02 0F 11 01 → E9 disp32 90).
-- Nós hookamos a instrução SEGUINTE (C650F6), que o CT nunca modifica.
--
--   C650DD: CB B8 02 00 00 00 0F 1F 40 00 66 0F 1F 84 00 00 00 00 00
--                                                ← AOB único (19 bytes)
--   C650F0: 0F 10 02           movups xmm0,[rdx]   ← CT injeta aqui (+19)
--   C650F3: 0F 11 01           movups [rcx],xmm0
--   C650F6: 0F 10 4A 10        movups xmm1,[rdx+10] ← NÓS hookamos aqui (+25)
--   C650FA: 0F 11 49 10        movups [rcx+10],xmm1
--   C650FE:                    ← retorno (+33)
--
-- O AOB de 19 bytes inclui CB + mov eax,2 + NOP4 + NOP9.
-- Essa sequência é única em todo o processo e NUNCA é modificada por ninguém
-- (CT modifica +19, nós modificamos +25 — ambos FORA do scan).
--
-- Quando CT ativo: cave do CT executa os movups de C650F0+F3 e retorna
--   para C650F6 → nosso hook dispara.
-- Quando CT inativo: fluxo natural passa por C650F0,F3,F6 → nosso hook dispara.
-- rdx aponta para a struct do jogador em ambos os casos.
-- eax==2 identifica cópia do jogador selecionado (mesma lógica do CT).
-- Roubamos 8 bytes (C650F6..FD) → JMP rel32 (5B) + 3 NOPs.

local AOB_PATTERN =
    '\xCB\xB8\x02\x00\x00\x00'             -- CB + mov eax,2      bytes [0..5]
 .. '\x0F\x1F\x40\x00'                     -- NOP 4               bytes [6..9]
 .. '\x66\x0F\x1F\x84\x00\x00\x00\x00\x00' -- NOP 9               bytes [10..18]
 -- (CT injeta em offset +19 = C650F0; nós em +25 = C650F6)

local AOB_INJECT = 25  -- offset de C650F6 dentro do padrão acima
local AOB_RETURN = 33  -- offset de C650FE (=25+8, após as 2 movups roubadas)

-- ─── Offsets da struct do jogador (ptrPlayer + offset) ───────────────────────
-- Todos verificados contra o XML do CT de xAranaktu.
local OFF_PLAYER_ID  = 0x30  -- u32: ID do jogador
local OFF_NAME       = 0x38  -- string ASCII, até 15 chars, null-terminated
local OFF_TEAM_ID    = 0x12C -- u16: ID do time atual
local OFF_LEAGUE_ID  = 0x12E -- u16: ID da liga
local OFF_AGE        = 0x1C  -- bits 0-5 (6 bits): idade
local OFF_REG_POS    = 0x07  -- bits 4-7 (4 bits): posição registrada
local OFF_MARKET_VAL = 0x174 -- i32: valor de mercado; raw × 100 = euros (mesmo fator ×100 do salário)
-- Clube de origem (contratante / detentor do passe):
--   +0x160 (u16) = team_id de origem; 0xFFFF = nenhum (jogador nativo)
--   +0x162 (u16) = league_id de origem
--   +0x164 (u16) = mesmo que +0x160 quando transferido/emprestado
-- Fim do empréstimo:
--   +0x16C (u16) = ano de fim do empréstimo LE; 0xFFFF = definitivo (sem fim)
--   +0x16E (u8)  = mês; +0x16F (u8) = dia
local OFF_ORIGIN_TEAM_ID   = 0x160 -- u16: club de origem / detentor do passe (0xFFFF = nativo)
local OFF_ORIGIN_LEAGUE_ID = 0x162 -- u16: liga de origem
local OFF_LOAN_END_YEAR    = 0x16C -- u16: ano de fim do empréstimo (0xFFFF = definitivo)
local OFF_LOAN_END_MONTH   = 0x16E -- u8: mês de fim do empréstimo
local OFF_LOAN_END_DAY     = 0x16F -- u8: dia de fim do empréstimo
-- Contrato e salário:
--   +0x138 (u16) = ano de fim de contrato
--   +0x13A (u8)  = mês; +0x13B (u8) = dia
--   +0x15C (i32) = salário anual em euros
-- Outros campos:
--   +0x1D  bits 3-7 (5 bits) = Playing Style (0=nenhum, 1-21)
--   +0x150 bits 0-4 (5 bits) = Team Role (0=nenhum, 1-22)
--   +0x143 bits 6-7 (2 bits) = Team Role Level (0-3)
--   +0x144 (u16) = Nationality (ID de país)
local OFF_CONTRACT_YEAR    = 0x138 -- u16: ano de fim de contrato
local OFF_CONTRACT_MONTH   = 0x13A -- u8: mês
local OFF_CONTRACT_DAY     = 0x13B -- u8: dia
local OFF_SALARY           = 0x15C -- i32: salário anual em unidades de 100 euros (×100 = valor real)

-- ─── Configuração de câmbio ───────────────────────────────────────────────────
-- Altere EUR_TO_BRL para atualizar a conversão de valores para Reais.
local EUR_TO_BRL = 6.0
local OFF_PLAYING_STYLE    = 0x1D  -- bits 3-7 (5 bits)
local OFF_TEAM_ROLE        = 0x150 -- bits 0-4 (5 bits)
local OFF_TEAM_ROLE_LEVEL  = 0x143 -- bits 6-7 (2 bits)
local OFF_NATIONALITY      = 0x144 -- u16: ID de país

-- Estilos de Jogo (enum 5 bits, offset +0x1D bits 3-7)
local PLAYING_STYLES = {
    [0]="-",[1]="Caçador de Gols",[2]="Pivô",[3]="Oportunista",
    [4]="Ponta Goleador",[5]="Camisa 10 Clássico",[6]="Meia Infiltrador",[7]="Box-to-Box",
    [8]="Volante Ancoragem",[9]="O Destruidor",[10]="Volante Ofensivo",
    [11]="Lateral Ofensivo",[12]="Lateral Defensivo",[13]="Centroavante Referência",
    [14]="Meia Criativo",[15]="Construção de Jogo",[16]="Goleiro Ofensivo",
    [17]="Goleiro Defensivo",[18]="Extremo Livre",[19]="Especialista em Cruzamentos",
    [20]="Orquestrador",[21]="Lateral Finalizador",
}

-- Funções no Time (enum 5 bits, offset +0x150 bits 0-4)
local TEAM_ROLES = {
    [0]="-",[1]="Jovem Promessa",[2]="Pupilo",[3]="Jogador de Equipe",
    [4]="Armador",[5]="Jogador Estrela",[6]="Líder",[7]="General",
    [8]="Criador",[9]="Maestro",[10]="Batalhador",[11]="Jogador Inteligente",
    [12]="Guerreiro",[13]="Jogador-Chave",[14]="Superestrela",[15]="Herói",
    [16]="Virtuoso",[17]="Regente",[18]="Ídolo",[19]="Lenda",
    [20]="Estrela em Ascensão",[21]="Bad Boy",[22]="Arrojado",
}

-- Nacionalidades (IDs de país do jogo base — subset mais comum)
local NATIONALITIES = {
    [1]="Irlandês",[2]="Ir. do Norte",[3]="Escocês",[4]="Galês",
    [5]="Inglês",[6]="Português",[7]="Espanhol",[8]="Francês",
    [9]="Belga",[10]="Holandês",[11]="Suíço",[12]="Italiano",
    [13]="Tcheco",[14]="Alemão",[15]="Dinamarquês",[16]="Norueguês",
    [17]="Sueco",[18]="Finlandês",[19]="Polonês",[20]="Eslovaco",
    [21]="Austríaco",[22]="Húngaro",[23]="Esloveno",[24]="Croata",
    [26]="Romeno",[27]="Búlgaro",[28]="Grego",[29]="Turco",
    [30]="Ucraniano",[31]="Russo",[32]="Bielorrusso",[33]="Georgiano",
    [34]="Armênio",[35]="Lituano",[36]="Letão",[37]="Estoniano",
    [38]="Serbo",[39]="Bósnio",[40]="Albanês",[41]="Macedônio",
    [42]="Montenegrino",[43]="Kosovo",[44]="Argelino",[45]="Tunisiano",
    [46]="Marroquino",[47]="Egípcio",[48]="Camaronês",[49]="Ganês",
    [50]="Nigeriano",[51]="Senegalês",[52]="Costa-Marfinense",
    [53]="Sul-Africano",[54]="Brasileiro",[55]="Argentino",[56]="Chileno",
    [57]="Uruguaio",[58]="Colombiano",[59]="Peruano",[60]="Paraguaio",
    [61]="Equatoriano",[62]="Boliviano",[63]="Venezuelano",
    [64]="Mexicano",[65]="Americano",[66]="Costarriquenho",
    [67]="Japonês",[68]="Coreano",[69]="Chinês",[70]="Australiano",
    [71]="Iraniano",[72]="Arabe Saudita",[73]="Emiradense",
    [74]="Qatariano",[75]="Iraquiano",[76]="Israelense",
    [77]="Tailandês",[78]="Malaio",[79]="Indonésio",
    [146]="Brasileiro",
    [190]="Turco",[194]="Austríaco",[197]="Belga",[199]="Búlgaro",
    [200]="Suíço",[202]="Tcheco",[203]="Dinamarquês",[204]="Inglês",
    [207]="Finlandês",[208]="Francês",[210]="Alemão",[211]="Grego",
    [212]="Húngaro",[214]="Irlandês",[215]="Italiano",[224]="Holandês",
    [225]="Ir. do Norte",[226]="Norueguês",[227]="Polonês",[228]="Português",
    [229]="Romeno",[232]="Escocês",[234]="Eslovaco",[235]="Esloveno",
    [236]="Espanhol",[237]="Sueco",[238]="Suíço",[239]="Ucraniano",
    [241]="Galês",
}

-- Atributos: { nome, offset, bit_inicio }
-- Cada atributo ocupa 7 bits a partir do bit indicado no byte dado.
local STATS = {
    { name = "Speed",   off = 0x14, bit = 7 },
    { name = "Accel",   off = 0x18, bit = 7 },
    { name = "Stamina", off = 0x1A, bit = 5 },
    { name = "OvAwr",   off = 0x03, bit = 0 },
    { name = "DvAwr",   off = 0x04, bit = 0 },
    { name = "Drib",    off = 0x05, bit = 6 },
    { name = "BallCtl", off = 0x06, bit = 5 },
    { name = "Finish",  off = 0x08, bit = 7 },
    { name = "LowPass", off = 0x09, bit = 6 },
    { name = "KkPwr",   off = 0x18, bit = 0 },
}

-- Mapa de posição registrada (4 bits, 0-12)
local POSITIONS = {
    [0]="GK",[1]="CB",[2]="LB",[3]="RB",
    [4]="DMF",[5]="CMF",[6]="LMF",[7]="RMF",
    [8]="AMF",[9]="LWF",[10]="RWF",[11]="SS",[12]="CF"
}

-- Nomes de liga (competition IDs do jogo base + extensões do FL25)
-- Fonte: competition-ids.md / sider7/doc/tournaments.txt
local LEAGUES = {
    [1]="Club Intl Cup",[2]="Champ. League",[5]="Europa League",
    [8]="Libertadores",[15]="AFC Champions",
    [17]="Premier League",[18]="Serie A",[19]="La Liga",
    [20]="Ligue 1",[21]="Eredivisie",[22]="Primeira Liga",
    [23]="FA Cup",[24]="Coppa Italia",[25]="Copa del Rey",
    [26]="Coupe de France",[27]="KNVB Beker",[28]="Taça de Portugal",
    [29]="Brasileirão",[30]="Liga Profesional",[31]="Copa do Brasil",
    [34]="Copa do Mundo",[41]="UEFA Euro",[43]="Copa América",
    [44]="Copa Ásia",[46]="CAN",[47]="KONAMI Cup",
    [50]="PEU League",[51]="PLA League",[52]="PAS League",
    [56]="Testimonial",[79]="Championship (ENG)",[80]="2a Div (ESP)",
    [81]="Ligue 2",[82]="Serie B",[86]="Community Shield",
    [87]="Supercopa ESP",[88]="Trophée Champions",[89]="Supercoppa ITA",
    [99]="KONAMI League",[115]="Jupiler Pro",[116]="RPL (RUS)",
    [117]="Super League (SUI)",[118]="Süper Lig",[119]="Liga BetPlay",
    [120]="CFA Super League",[133]="Scottish Prem.",[141]="3F Superliga",
    [162]="Thai League",[163]="Brasileirão B",
    -- IDs adicionados pelo FL25 (identificados pelo JSON de jogadores)
    [65535]="(Sem liga)",
}

-- ─── Logger: team_id → league_id ────────────────────────────────────────────
-- Registra pares (team_id, league_id) encontrados durante a navegação ML.
-- Arquivo TSV: team_id <TAB> league_id <TAB> team_name <TAB> league_name
local LOG_FILE  = "d:\\pes-desenv\\output\\team_league_raw.log"
-- Logger de empréstimos/origens
-- Arquivo TSV: player_id <TAB> name <TAB> team_id <TAB> origin_team_id <TAB> status <TAB> loan_end
local LOAN_FILE = "d:\\pes-desenv\\output\\ml_loans.log"

-- Nomes de time: carregados de ml_teams.cfg (id=nome, uma por linha)
-- Se o arquivo não existir, exibe só o ID.
local TEAMS = {}
local LEAGUES_FILE = {}
local TEAM_LEAGUE = {}  -- mapeamento team_id -> nome da liga doméstica

local function load_cfg(path, dest)
    local f = io.open(path, "r")
    if not f then return 0 end
    local n = 0
    for line in f:lines() do
        local id, name = line:match("^(%d+)=(.+)$")
        if id and name then
            dest[tonumber(id)] = name
            n = n + 1
        end
    end
    f:close()
    return n
end

-- Pré-carrega pares já registrados no log para evitar duplicatas entre sessões.
local function load_seen_from_log(seen)
    local f = io.open(LOG_FILE, "r")
    if not f then return end
    for line in f:lines() do
        local tid, lid = line:match("^(%d+)\t(%d+)\t")
        if tid and lid then
            seen[tid .. "_" .. lid] = true
        end
    end
    f:close()
end

-- Registra jogadores com clube de origem (empréstimos e definitivos) no LOAN_FILE.
local function log_loan(p)
    if not p.origin_team_id then return end  -- nativo, nada a registrar

    if not _G["__mlpi_seen_loans"] then
        _G["__mlpi_seen_loans"] = {}
        -- pré-carregar do arquivo para evitar duplicatas entre sessões
        local f = io.open(LOAN_FILE, "r")
        if f then
            for line in f:lines() do
                local pid = line:match("^(%d+)\t")
                if pid then _G["__mlpi_seen_loans"][pid] = true end
            end
            f:close()
        end
    end

    local key = tostring(p.id)
    if _G["__mlpi_seen_loans"][key] then return end
    _G["__mlpi_seen_loans"][key] = true

    local status, loan_end
    if p.is_loan then
        status   = "emprestimo"
        loan_end = string.format("%02d/%02d/%d", p.loan_end_day, p.loan_end_month, p.loan_end_year)
    else
        status   = "definitivo"
        loan_end = "-"
    end

    local f = io.open(LOAN_FILE, "a")
    if not f then return end
    f:write(string.format("%d\t%s\t%d\t%d\t%s\t%s\n",
        p.id, p.name, p.team_id, p.origin_team_id, status, loan_end))
    f:close()

    log(string.format("[ml_player_info] LOAN player=%d (%s) team=%d origin=%d %s %s",
        p.id, p.name, p.team_id, p.origin_team_id, status, loan_end))
end

-- Registra team_id → league_id no log se ainda não foi registrado nesta sessão.
local function log_team_league(p, team_name, league_name)
    if not _G["__mlpi_seen_teams"] then
        _G["__mlpi_seen_teams"] = {}
        load_seen_from_log(_G["__mlpi_seen_teams"])
        log(string.format("[ml_player_info] logger: %d pares pre-carregados do log",
            (function() local n=0 for _ in pairs(_G["__mlpi_seen_teams"]) do n=n+1 end return n end)()))
    end
    local seen = _G["__mlpi_seen_teams"]
    local key = p.team_id .. "_" .. p.league_id
    if seen[key] then return end
    seen[key] = true
    local f = io.open(LOG_FILE, "a")
    if not f then
        log("[ml_player_info] AVISO: nao conseguiu abrir " .. LOG_FILE)
        return
    end
    f:write(string.format("%d\t%d\t%s\t%s\n",
        p.team_id, p.league_id,
        team_name ~= "" and team_name or "?",
        league_name ~= "" and league_name or "?"))
    f:close()
    log(string.format("[ml_player_info] LOG team=%d league=%d  %s | %s",
        p.team_id, p.league_id, team_name, league_name))
end

local function load_teams(sider_dir)
    local n = load_cfg(sider_dir .. "modules\\ml_teams.cfg", TEAMS)
    log(string.format("[ml_player_info] ml_teams.cfg: %d times", n))
    n = load_cfg(sider_dir .. "modules\\ml_leagues.cfg", LEAGUES_FILE)
    log(string.format("[ml_player_info] ml_leagues.cfg: %d ligas (fallback)", n))
    n = load_cfg(sider_dir .. "modules\\ml_team_league.cfg", TEAM_LEAGUE)
    log(string.format("[ml_player_info] ml_team_league.cfg: %d times", n))
end

-- ─── Estado interno ───────────────────────────────────────────────────────────
-- Armazenados em _G para sobreviver a hot-reloads (Shift+R):
--   _G["__mlpi_ptr"]     → endereço onde o hook salva ptrPlayer (Lua number)
--   _G["__mlpi_hooked"]  → true se o hook já está instalado no processo

-- ─── Utilitários ──────────────────────────────────────────────────────────────

-- Converte número 64-bit para 8 bytes little-endian (lida com cdata e numbers)
local function u64_le(n)
    if type(n) ~= "number" then
        n = tonumber(ffi.cast("unsigned long long", n))
    end
    n = math.floor(n)
    local t = {}
    for i = 1, 8 do
        t[i] = string.char(math.floor(n) % 256)
        n = math.floor(n / 256)
    end
    return table.concat(t)
end

-- ─── Dump do struct para análise ─────────────────────────────────────────────
-- Grava dump hex anotado do struct do jogador em d:\pes-desenv\output\struct_dump.txt
-- Útil para mapear campos desconhecidos (ex: league_id real vs índice intermediário).
local DUMP_FILE = "d:\\pes-desenv\\output\\struct_dump.txt"
local _dump_seen = {}  -- evita regravações do mesmo jogador

local function dump_player_struct(player_addr, data, p)
    local key = p.id
    if _dump_seen[key] then return end
    _dump_seen[key] = true

    local f = io.open(DUMP_FILE, "a")
    if not f then return end

    f:write(string.format("\n=== player_id=%d  name=%s  team=%d  league_raw=%d ===\n",
        p.id, p.name, p.team_id, p.league_id))
    f:write(string.format("    base_addr=0x%X\n", player_addr))
    -- Anotações de campos de origem/empréstimo
    if p.origin_team_id then
        f:write(string.format("    +0x160 origin_team=%d  +0x162 origin_league=%d\n",
            p.origin_team_id, p.origin_league_id or 0))
        if p.is_loan then
            f:write(string.format("    EMPRESTADO até %02d/%02d/%d\n",
                p.loan_end_day, p.loan_end_month, p.loan_end_year))
        else
            f:write("    DEFINITIVO (origem diferente do time atual)\n")
        end
    else
        f:write("    NATIVO (sem clube de origem)\n")
    end

    -- Hex dump em linhas de 16 bytes, com offset e valores u16 anotados
    local len = #data
    for row = 0, math.floor((len-1)/16) do
        local off = row * 16
        local hex = {}
        local u16s = {}
        for i = 0, 15 do
            local b = string.byte(data, off + i + 1)
            if b then
                hex[#hex+1] = string.format("%02X", b)
            else
                hex[#hex+1] = "  "
            end
        end
        -- u16 LE a cada 2 bytes nesta linha
        for i = 0, 7 do
            local a = string.byte(data, off + i*2 + 1) or 0
            local b2 = string.byte(data, off + i*2 + 2) or 0
            u16s[#u16s+1] = string.format("%5d", a + b2*256)
        end
        f:write(string.format("  +%03X: %s  | u16: %s\n",
            off,
            table.concat(hex, " "),
            table.concat(u16s, " ")))
    end

    -- Ponteiros potenciais: u64 em cada offset alinhado de 8 bytes
    f:write("  -- Ponteiros u64 (offsets 0x00..0x50, alinhados 8) --\n")
    for off = 0, 0x50, 8 do
        local a = string.byte(data, off+1) or 0
        local b2 = string.byte(data, off+2) or 0
        local c = string.byte(data, off+3) or 0
        local d = string.byte(data, off+4) or 0
        local e = string.byte(data, off+5) or 0
        local ff = string.byte(data, off+6) or 0
        local g = string.byte(data, off+7) or 0
        local h = string.byte(data, off+8) or 0
        local lo = a + b2*256 + c*65536 + d*16777216
        local hi = e + ff*256 + g*65536 + h*16777216
        local addr64 = lo + hi * 0x100000000
        if addr64 > 0x10000 and addr64 < 0x7FFFFFFFFFFF then
            f:write(string.format("    +%03X -> 0x%X  (hi=0x%X)\n", off, addr64, hi))
        end
    end

    f:close()
    log(string.format("[ml_player_info] DUMP player_id=%d gravado em struct_dump.txt", p.id))
end

-- Converte número 32-bit (possivelmente negativo) para 4 bytes little-endian
local function u32_le(n)
    n = math.floor(n) % 0x100000000  -- trata negativo como unsigned
    return string.char(
        n % 256,
        math.floor(n / 256) % 256,
        math.floor(n / 65536) % 256,
        math.floor(n / 16777216) % 256
    )
end

-- Tenta alocar [size] bytes PERTO de [target_num] usando VirtualAlloc com hint.
-- Necessário para que um JMP relativo de 5 bytes alcance o cave (±2GB).
-- Estratégia idêntica ao CE: alloc(newmem,$1000,"PES2021.exe"+offset).
local function alloc_near(target_num, size)
    local MEM_COMMIT  = 0x1000
    local MEM_RESERVE = 0x2000
    local PAGE_XRW    = 0x40
    local MEM_RELEASE = 0x8000
    local MAX_DIST    = 0x70000000  -- 1.75 GB de margem de segurança

    -- Calcular faixa de busca alinhada a 64 KB
    local lo = math.max(0x10000, math.floor((target_num - MAX_DIST) / 0x10000) * 0x10000)
    local hi = target_num + MAX_DIST

    local addr = lo
    while addr <= hi do
        local p = ffi.C.VirtualAlloc(
            ffi.cast("void*", addr), size,
            MEM_COMMIT + MEM_RESERVE, PAGE_XRW
        )
        local got = tonumber(ffi.cast("unsigned long long", p))
        if got ~= 0 then
            local dist = (got >= target_num) and (got - target_num) or (target_num - got)
            if dist < MAX_DIST then
                return p, got  -- sucesso: cave dentro de ±1.75 GB
            end
            -- Alocou longe demais (Windows ignorou o hint), liberar e continuar
            ffi.C.VirtualFree(p, 0, MEM_RELEASE)
        end
        addr = addr + 0x10000
    end
    return nil
end

-- Lê `bit_len` bits a partir do bit `bit_start` do byte na posição `off` de `data`
local function read_bits(data, off, bit_start, bit_len)
    local b = string.byte(data, off + 1) or 0
    return math.floor(b / (2 ^ bit_start)) % (2 ^ bit_len)
end

-- Lê um atributo de 7 bits que começa no bit `bit` do byte em `off`.
-- Os 7 bits podem cruzar a fronteira para o próximo byte.
local function read_stat_7bit(data, off, bit)
    local lo = string.byte(data, off + 1) or 0
    local hi = string.byte(data, off + 2) or 0
    local combined = lo + hi * 256
    return math.floor(combined / (2 ^ bit)) % 128
end

local function read_u16_le(data, off)
    local a = string.byte(data, off + 1) or 0
    local b = string.byte(data, off + 2) or 0
    return a + b * 256
end

local function read_u32_le(data, off)
    local a = string.byte(data, off + 1) or 0
    local b = string.byte(data, off + 2) or 0
    local c = string.byte(data, off + 3) or 0
    local d = string.byte(data, off + 4) or 0
    return a + b * 256 + c * 65536 + d * 16777216
end

local function read_i32_le(data, off)
    local v = read_u32_le(data, off)
    if v >= 0x80000000 then v = v - 0x100000000 end
    return v
end

-- Lê string ASCII null-terminated a partir do offset `off`, até `maxlen` bytes
local function read_ascii_str(data, off, maxlen)
    local chars = {}
    for i = 1, maxlen do
        local b = string.byte(data, off + i)
        if not b or b == 0 then break end
        chars[#chars + 1] = string.char(b)
    end
    return #chars > 0 and table.concat(chars) or "---"
end

-- ─── Instalação do Hook ───────────────────────────────────────────────────────
local function install_hook()
    if _G["__mlpi_hooked"] then
        log("[ml_player_info] hook ja ativo (reload), reutilizando")
        return true
    end

    if not _G["__mlpi_ffi"] then
        ffi.cdef[[
            void* VirtualAlloc(void* lpAddress, size_t dwSize,
                               unsigned long flAllocationType,
                               unsigned long flProtect);
            int VirtualFree(void* lpAddress, size_t dwSize,
                            unsigned long dwFreeType);
        ]]
        _G["__mlpi_ffi"] = true
    end

    -- 1. Encontrar o AOB único de 19 bytes (C650DD) — nunca modificado por ninguém
    local match_raw = memory.search_process(AOB_PATTERN)
    if not match_raw then
        log("[ml_player_info] ERRO: AOB nao encontrado")
        return false
    end
    local match = tonumber(ffi.cast("unsigned long long", match_raw))
    log(string.format("[ml_player_info] AOB em 0x%x (inject em 0x%x)", match, match + AOB_INJECT))

    -- O ponto de injeção é match+AOB_INJECT (C650F6)
    -- Precisamos que o cave esteja dentro de ±2GB de C650F6
    local inject_addr = match + AOB_INJECT
    local cave_raw, cave_addr = alloc_near(inject_addr, 64)
    if not cave_raw then
        log("[ml_player_info] ERRO: nao encontrou memoria livre perto do inject point")
        return false
    end
    log(string.format("[ml_player_info] cave em 0x%x", cave_addr))

    local ptr_storage_addr = cave_addr + 0x30
    _G["__mlpi_ptr"] = ptr_storage_addr

    local hook_cdata = ffi.cast("char*", cave_raw)

    -- 3. Shellcode:
    --   [00] 83 F8 02          cmp eax, 2
    --   [03] 75 0F             jne skip (para [14])
    --   [05] 50                push rax
    --   [06] 48 B8 xx...       mov rax, ptr_storage_addr (10 bytes)
    --   [10] 48 89 10          mov [rax], rdx   ← salva ptrPlayer
    --   [13] 58                pop rax
    --   [14] 0F 10 4A 10       movups xmm1,[rdx+10]  ← instrução original (C650F6)
    --   [18] 0F 11 49 10       movups [rcx+10],xmm1  ← instrução original (C650FA)
    --   [1C] FF 25 00 00 00 00 jmp [rip+0]
    --   [22] xx...xx           return_addr = C650FE (8 bytes)
    local return_addr = match + AOB_RETURN  -- = C650DD + 33 = C650FE
    local shellcode =
        '\x83\xF8\x02'              -- cmp eax, 2
     .. '\x75\x0F'                  -- jne +0x0F → [14]
     .. '\x50'                      -- push rax
     .. '\x48\xB8'                  -- mov rax, imm64
     .. u64_le(ptr_storage_addr)    -- 8 bytes
     .. '\x48\x89\x10'              -- mov [rax], rdx
     .. '\x58'                      -- pop rax
     .. '\x0F\x10\x4A\x10'          -- movups xmm1,[rdx+10]
     .. '\x0F\x11\x49\x10'          -- movups [rcx+10],xmm1
     .. '\xFF\x25\x00\x00\x00\x00'  -- jmp [rip+0]
     .. u64_le(return_addr)         -- 8 bytes

    memory.write(hook_cdata, shellcode)
    memory.write(ffi.cast("char*", cave_raw) + 0x30,
                 '\x00\x00\x00\x00\x00\x00\x00\x00')

    local v1 = memory.read(hook_cdata, 1)
    if not v1 or string.byte(v1, 1) ~= 0x83 then
        log("[ml_player_info] ERRO: shellcode nao escrito")
        return false
    end

    -- 4. Patch: 5 bytes JMP + 3 NOPs em inject_addr (= match + AOB_INJECT = C650F6)
    --    JMP displacement = cave_addr - (inject_addr + 5)
    local jmp_from   = inject_addr + 5   -- RIP após o JMP = C650FB
    local disp       = cave_addr - jmp_from
    local game_patch = '\xE9' .. u32_le(disp) .. '\x90\x90\x90'
    memory.write(match_raw + AOB_INJECT, game_patch)

    local vp = memory.read(match_raw + AOB_INJECT, 1)
    if not vp or string.byte(vp, 1) ~= 0xE9 then
        log("[ml_player_info] ERRO: game patch nao escrito")
        return false
    end

    _G["__mlpi_hooked"] = "v7"
    log(string.format("[ml_player_info] hook v7 instalado. inject=0x%x ptr_storage=0x%x",
                      inject_addr, ptr_storage_addr))
    return true
end

-- ─── Leitura da struct do jogador ─────────────────────────────────────────────
local function read_player()
    local ptr_storage_addr = _G["__mlpi_ptr"]
    if not ptr_storage_addr then return nil end

    -- Ler 8 bytes do ptr_storage (onde o hook escreveu ptrPlayer)
    local ptr_cdata = ffi.cast("char*", ptr_storage_addr)
    local raw = memory.read(ptr_cdata, 8)
    if not raw or #raw < 8 then return nil end

    -- Reconstruir o endereço de 64 bits a partir de dois u32 (evita memory.unpack("u64"))
    -- Endereços de processo no Windows x64 user-space ficam abaixo de 2^47, portanto
    -- cabe exatamente num double Lua (mantissa de 53 bits).
    local lo32 = read_u32_le(raw, 0)  -- bytes 1-4 (little-endian baixo)
    local hi32 = read_u32_le(raw, 4)  -- bytes 5-8 (little-endian alto)
    local player_addr = lo32 + hi32 * 0x100000000

    -- Rejeita NULL e endereços fora do espaço de usuário x64
    if player_addr == 0
    or player_addr < 0x10000
    or player_addr > 0x7FFFFFFFFFFF then
        return nil
    end

    -- Ler struct expandido (0x500 bytes — cobre campos além de 0x300 para descoberta)
    local player_cdata = ffi.cast("char*", player_addr)
    local data = memory.read(player_cdata, 0x500)
    if not data or #data < 0x180 then return nil end

    local p = {}
    p._raw       = data  -- guardado para debug de bytes crus (F7)
    p.id         = read_u32_le(data, OFF_PLAYER_ID)
    p.name       = read_ascii_str(data, OFF_NAME, 15)
    p.team_id    = read_u16_le(data, OFF_TEAM_ID)
    p.league_id  = read_u16_le(data, OFF_LEAGUE_ID)
    p.age        = read_bits(data, OFF_AGE, 0, 6)
    p.reg_pos    = read_bits(data, OFF_REG_POS, 4, 4)
    p.market_val = read_i32_le(data, OFF_MARKET_VAL)

    -- Clube de origem e tipo de contrato
    local origin_team = read_u16_le(data, OFF_ORIGIN_TEAM_ID)
    local origin_liga = read_u16_le(data, OFF_ORIGIN_LEAGUE_ID)
    local loan_end_yr = read_u16_le(data, OFF_LOAN_END_YEAR)
    local loan_end_mo = string.byte(data, OFF_LOAN_END_MONTH + 1) or 0
    local loan_end_dy = string.byte(data, OFF_LOAN_END_DAY + 1) or 0

    if origin_team ~= 0xFFFF and origin_team ~= p.team_id and origin_team > 0 then
        p.origin_team_id   = origin_team
        p.origin_league_id = origin_liga
        if loan_end_yr ~= 0xFFFF and loan_end_yr > 2000 then
            -- empréstimo com data de retorno
            p.is_loan         = true
            p.loan_end_year   = loan_end_yr
            p.loan_end_month  = loan_end_mo
            p.loan_end_day    = loan_end_dy
        else
            -- transferência definitiva (origem ≠ time atual = veio de outro clube)
            p.is_loan = false
        end
    end

    -- Contrato e salário
    p.contract_year  = read_u16_le(data, OFF_CONTRACT_YEAR)
    p.contract_month = string.byte(data, OFF_CONTRACT_MONTH + 1) or 0
    p.contract_day   = string.byte(data, OFF_CONTRACT_DAY   + 1) or 0
    -- Unidade armazenada = 100 euros (não euros diretos); multiply ×100 para obter valor real
    p.salary         = read_i32_le(data, OFF_SALARY) * 100

    -- Playing Style (bits 3-7 do byte +0x1D)
    p.playing_style = read_bits(data, OFF_PLAYING_STYLE, 3, 5)

    -- Team Role (bits 0-4 do byte +0x150) e Level (bits 6-7 do byte +0x143)
    p.team_role       = read_bits(data, OFF_TEAM_ROLE,       0, 5)
    p.team_role_level = read_bits(data, OFF_TEAM_ROLE_LEVEL, 6, 2)

    -- Nacionalidade
    p.nationality = read_u16_le(data, OFF_NATIONALITY)

    -- Atributos de 7 bits
    p.stats = {}
    for _, s in ipairs(STATS) do
        p.stats[s.name] = read_stat_7bit(data, s.off, s.bit)
    end

    -- Dump do struct completo para análise de campos desconhecidos
    dump_player_struct(player_addr, data, p)

    return p
end

-- ─── Modo Debug (F7 = ativar/desativar) ─────────────────────────────────────
local _debug_mode = false
local _debug_page  = 1
local DEBUG_PAGES  = 5  -- 0x000-0x0F8 / 0x100-0x1F8 / 0x200-0x2F8 / 0x300-0x3F8 / 0x400-0x4F8

local function fmt_dump_row(data, off)
    -- retorna nil se offset fora do buffer
    if off + 8 > #data then return nil end
    local b = {}
    for i = 0, 7 do
        b[i] = string.byte(data, off + i + 1) or 0
    end
    local u16a = b[0] + b[1]*256
    local u16b = b[2] + b[3]*256
    local i32a = b[0] + b[1]*256 + b[2]*65536 + b[3]*16777216
    if i32a >= 0x80000000 then i32a = i32a - 0x100000000 end
    local i32b = b[4] + b[5]*256 + b[6]*65536 + b[7]*16777216
    if i32b >= 0x80000000 then i32b = i32b - 0x100000000 end
    return string.format("%03X: %02X %02X %02X %02X  %02X %02X %02X %02X | u16=%5d %5d  i32=%10d %d",
        off, b[0],b[1],b[2],b[3], b[4],b[5],b[6],b[7],
        u16a, u16b, i32a, i32b)
end

local function overlay_debug(p)
    local data = p._raw
    local pages = {
        { start=0x000, stop=0x0F8 },
        { start=0x100, stop=0x1F8 },
        { start=0x200, stop=0x2F8 },
        { start=0x300, stop=0x3F8 },
        { start=0x400, stop=0x4F8 },
    }
    local pg = pages[_debug_page]
    local annotations = {
        [0x007]="pos(bits4-7)", [0x01C]="age(bits0-5)",
        [0x01D]="style(bits3-7)", [0x030]="player_id(u32)", [0x038]="name(str15)",
        [0x12C]="team_id(u16)", [0x12E]="league_id(u16)",
        [0x138]="contrato_fim(u16 ano)", [0x143]="role_lvl(bits6-7)",
        [0x144]="nationality(u16)", [0x150]="team_role(bits0-4)",
        [0x15C]="salario(i32 x100)", [0x160]="origin_team(u16)",
        [0x162]="origin_liga(u16)", [0x174]="mkt_val_custom(i32)",
    }
    local lines = {
        string.format("=== DEBUG pg %d/%d (F8=proxima F7=sair) ===", _debug_page, DEBUG_PAGES),
        string.format("Jogador: %d %s | mkt_custom=%d | sal_raw=%d",
            p.id, p.name, p.market_val or 0, p.salary and (p.salary/100) or 0),
        "OFF     B0 B1 B2 B3  B4 B5 B6 B7 | u16=A    B    i32=A          B",
    }
    for off = pg.start, pg.stop, 8 do
        local row = fmt_dump_row(data, off)
        if row then
            local ann = annotations[off] or ""
            lines[#lines+1] = row .. (ann ~= "" and "  <- " .. ann or "")
        end
    end
    return table.concat(lines, "\n")
end

-- ─── Overlay ─────────────────────────────────────────────────────────────────
local function try_log_current()
    if not _G["__mlpi_hooked"] then return end
    local p = read_player()
    if not p then return end
    local team_name   = TEAMS[p.team_id] or ""
    local league_name = TEAM_LEAGUE[p.team_id] or LEAGUES_FILE[p.league_id] or LEAGUES[p.league_id] or ""
    log_team_league(p, team_name, league_name)
    log_loan(p)
end

local function overlay_on(ctx)
    if not _G["__mlpi_hooked"] then
        return "[ml_player_info]\nHook nao instalado. Veja sider.log."
    end

    local p = read_player()
    if not p then
        return "[ml_player_info]\nAguardando selecao de jogador...\n(Navegue pelo menu ML ou entre na tela de edicao de jogador)"
    end

    if _debug_mode then
        return overlay_debug(p)
    end

    local pos_name = POSITIONS[p.reg_pos] or string.format("?(%d)", p.reg_pos)

    -- Valor de mercado (0x174 = valor customizado; 0 = calculado dinamicamente pelo jogo)
    -- raw × 100 = euros (mesmo fator ×100 do salário)
    local mv = (p.market_val or 0) * 100
    local mv_eur, mv_brl
    if mv > 0 then
        if mv >= 1000000 then
            mv_eur = string.format("€%.1fM", mv / 1000000.0)
            mv_brl = string.format("R$%.1fM", mv * EUR_TO_BRL / 1000000.0)
        elseif mv >= 1000 then
            mv_eur = string.format("€%.0fK", mv / 1000.0)
            mv_brl = string.format("R$%.0fK", mv * EUR_TO_BRL / 1000.0)
        else
            mv_eur = string.format("€%d", mv)
            mv_brl = string.format("R$%d", math.floor(mv * EUR_TO_BRL))
        end
    else
        mv_eur = "---"
        mv_brl = "---"
    end

    -- Liga do time atual
    local team_name   = TEAMS[p.team_id] or ""
    local league_name = TEAM_LEAGUE[p.team_id] or LEAGUES_FILE[p.league_id] or LEAGUES[p.league_id] or ""

    log_team_league(p, team_name, league_name)
    log_loan(p)

    -- Salário: anual + mensal (÷12) + mensal em BRL
    local ano_str, mes_str, brl_str = "?", "?", "?"
    if p.salary and p.salary > 0 then
        local sal_ano     = p.salary
        local sal_mes     = sal_ano / 12.0
        local sal_brl_mes = sal_mes * EUR_TO_BRL
        if sal_ano >= 1000000 then
            ano_str = string.format("€%.2fM/ano", sal_ano / 1000000.0)
        elseif sal_ano >= 1000 then
            ano_str = string.format("€%.0fK/ano", sal_ano / 1000.0)
        else
            ano_str = string.format("€%d/ano", sal_ano)
        end
        if sal_mes >= 1000000 then
            mes_str = string.format("€%.2fM", sal_mes / 1000000.0)
        elseif sal_mes >= 1000 then
            mes_str = string.format("€%.0fK", sal_mes / 1000.0)
        else
            mes_str = string.format("€%.0f", sal_mes)
        end
        if sal_brl_mes >= 1000000 then
            brl_str = string.format("R$%.2fM", sal_brl_mes / 1000000.0)
        elseif sal_brl_mes >= 1000 then
            brl_str = string.format("R$%.0fK", sal_brl_mes / 1000.0)
        else
            brl_str = string.format("R$%.0f", sal_brl_mes)
        end
    end

    -- Playing Style
    local style_str = PLAYING_STYLES[p.playing_style] or string.format("?(%d)", p.playing_style or 0)

    -- Team Role + Level (+ = nível)
    local role_name = TEAM_ROLES[p.team_role] or string.format("?(%d)", p.team_role or 0)
    local role_str
    if p.team_role and p.team_role > 0 then
        local lvl_plus = ({[0]="", [1]="★", [2]="★★", [3]="★★★"})[p.team_role_level] or ""
        role_str = role_name .. " " .. lvl_plus
    else
        role_str = "-"
    end

    -- Nacionalidade
    local nat_str = NATIONALITIES[p.nationality] or string.format("ID %d", p.nationality or 0)

    -- ── Linha 0: Separador ─────────────────────────────────────────────────────
    local line0 = "=== ML Player Info ==="

    -- ── Linha 1: Jogador ──────────────────────────────────────────────────────
    local line1 = string.format("Jogador: %d %s (%s), Pos: %s, Idade: %d",
        p.id, p.name, nat_str, pos_name, p.age)

    -- ── Linha 2: Contrato ─────────────────────────────────────────────────────
    local line2 = string.format("Contrato: %d %s | %d %s",
        p.team_id, team_name, p.league_id, league_name)
    if p.origin_team_id then
        local origin_name    = TEAMS[p.origin_team_id] or tostring(p.origin_team_id)
        local org_lg_id      = p.origin_league_id or 0
        local org_lg_name    = TEAM_LEAGUE[p.origin_team_id] or LEAGUES_FILE[org_lg_id] or LEAGUES[org_lg_id] or ""
        local label          = p.is_loan and "Emprestado por" or "Origem"
        line2 = line2 .. string.format(" | %s: %d %s | %d %s",
            label, p.origin_team_id, origin_name, org_lg_id, org_lg_name)
    end

    -- ── Linha 3: Valor + Salário ──────────────────────────────────────────────
    local line3 = string.format("Valor: %s (%s) - Salário: %s | Por mês %s/%s",
        mv_eur, mv_brl, ano_str, mes_str, brl_str)

    -- ── Linha 4: Estilo + Função ─────────────────────────────────────────────
    local line4 = string.format("Estilo: %s | Função: %s", style_str, role_str)

    return line0 .. "\n" .. line1 .. "\n" .. line2 .. "\n" .. line3 .. "\n" .. line4
end

-- ─── Modo Debug (F7 = ativar/desativar) ─────────────────────────────────────
-- Quando ativo, o overlay exibe dump hex de 0x130-0x180 para mapeamento de
-- campos desconhecidos (valor de mercado real, cláusula de rescisão, etc.)

-- Handler automático: registra silenciosamente ao navegar com teclado
local function on_key_down(ctx, vk)
    if vk == 0x76 then  -- F7: toggle modo debug
        _debug_mode = not _debug_mode
        _debug_page = 1
        return
    end
    if vk == 0x77 and _debug_mode then  -- F8: próxima página no debug
        _debug_page = (_debug_page % DEBUG_PAGES) + 1
        return
    end
    try_log_current()
end

-- Handler automático: registra silenciosamente ao navegar com controle
local function on_gamepad_input(ctx, btn)
    try_log_current()
end

-- ─── Init ─────────────────────────────────────────────────────────────────────
function m.init(ctx)
    load_teams(ctx.sider_dir)
    install_hook()
    ctx.register("overlay_on",     overlay_on)
    ctx.register("key_down",       on_key_down)
    ctx.register("gamepad_input",  on_gamepad_input)
end

return m
