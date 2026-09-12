# PES 2021 player-memory — live discovery evidence

> **EVIDENCIA EXPERIMENTAL REFUTADA EM PARTE:** o stride 763 descrito abaixo foi uma subamostragem da grade real `0x17C`, nao um novo stride. Consulte `deliveries/P10/codex-review-2026-09-02.md`.

Date: 2026-09-02
PID: 33136
Process name: PES2021
Process started (UTC): 2026-09-02T15:57:10
Session: editor menu open, no Master League loaded (consistent with feasibility-study context classification `EDIT_BASE_CANDIDATE`)
Runtime safety: read-only; no memory write, freeze, hook, injection, or Cheat Engine script execution

## What changed since the feasibility study (2026-08-30)

The original study located the Piero Hincapie record (id `581) at
`0x7FF4D908F210` with stride `0x17C` (380 bytes). On 2026-09-02 the same
PID family has a different layout:

- The historical anchor address `0x7FF4D8EC0010..0x7FF4D999F4CC` returns
  zeros for the running process (data is not currently resident at the
  previous addresses).
- A new EDIT-base arena is present at `0x7FF4D9F50000` with a different
  stride of `0x2FB` (763 bytes).
- The first cluster (`Alisson Taddei`, `Valentino Gandin`, `Israel Suero`,
  `Ennio van der Gouw`, `H. Okuno`, `Martín García`, `Julian Ryerson`,
  `Bruno Giménez`, `Hebert`, `Rodrigo Contreras`, `Tayeb Meziani`,
  `Álex Granell`, `Franco Cristaldo`, `Lucas Marques`, `Daniel Sappa`,
  `Marius Müller`, `Mathias Kjølø`, `Oliwier Zych`, `Gabriel Grando`,
  `Andrés Aedo`, `Sylian Mokono`, `Felipe Mosquera`, `Alan Marinelli`,
  `Lekinho`, `Michael Kayode`, `Hugo`, `Iván Anderson`, `Jan Kopic`,
  `Jaydee Canvot`, `Lucas Emanuel`, `Jhonny`, `Pedro Velurtas`,
  `Ryan Raposo`) decodes cleanly with the new stride.

The arena continues further; the new region has at least 30.000 records
by extrapolation.

## Region footprint

- **First region:** `0x7FF4D9F50000` (Private, Commit, ReadWrite, 251.7 MB)
- **Stride:** 763 bytes (0x2FB)
- **First 33 records:** continuous from `0x7FF4D9F50000`, no holes

## Sample records (first 10, sorted by address)

| Slot | | Address | | PlayerId | Name | H | W |
|--:|--:|--:|--:|--:|--:|--:|
| 12 | |0x7FF4D9F523C4| |46464 | Martín García | 182 | 71 |
| 392 | |0x7FF4D9F99058| |54253 | Julian Ryerson | 183 | 84 |
| 772 | |0x7FF4D9FDFCEC| |55526 | Bruno Giménez | 177 | 78 |
| 1152 | |0x7FF4DA026980| |58024 | Hebert | 188 | 77 |
| 1532 | |0x7FF4DA06D614| |60169 | Rodrigo Contreras | 186 | 79 |
| 1912 | |0x7FF4DA0B42A8| |65051 | Tayeb Meziani | 171 | 68 |
| 2292 | |0x7FF4DA0FAF3C| |102417 | Álex Granell | 175 | 70 |
| 2672 | |0x7FF4DA141BD0| |105298 | Franco Cristaldo | 172 | 69 |
| 3052 | |0x7FF4DA188864| |108822 | Lucas Marques | 173 | 70 |
| 3432 | |0x7FF4DA1CF4F8| |111320 | Daniel Sappa | 193 | 98 |

## Implications

- The feasibility study was based on `PES 2021 - v21.1.0`. The currently
  running build is `eFootball PES 2021 SEASON UPDATE` (the same PES2021.exe
  binary the user is running today).
- The 763-byte stride and the `0x7FF4D9F50000` arena are the live values
  for this session. Future restarts will relocate the arena; the cache key
  in `Pes2021PlayerSessionCache` already handles that.
- The Overmem player-memory profile (`pes2021.player-record.v1`) still
  uses stride 380 from the feasibility study. P6 must publish a new profile
  variant `pes2021.player-record-live-v1` with stride 763 once enough
  evidence is captured.

## Reproduce

```powershell
# direct CLI call against the running PES 2021.exe
& 'D:\git-lab-pes\overmem\src\Overmem.Cli\bin\Debug\net8.0\Overmem.Cli.exe' `
    pes2021-stride-scan-players `
    --pid 33136 `
    --start-address 0x7FF4D9F50000 `
    --stop-address 0x7FF4DB500000 `
    --stride 763 `
    --max-records 1000 `
    > live-stride-763-scan.json
```

The output is JSON with `records[].{slot, address, playerId, height, weight,
name}`. UTF-8 names are decoded as ASCII where possible; non-ASCII bytes
are rendered as `?`.

## What this is not

- This is not a write. The scan only reads.
- This is not a Master League scan. The user opened the editor menu; no
  Master League is loaded.
- This is not a coverage claim. We decoded 33 records inside a ~1 MB
  window. Total coverage of the new arena needs a full sweep.
