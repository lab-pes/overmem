# Offline fixtures for competition extraction

This directory is reserved for sanitized, immutable PES 2021 calendar-memory fixtures used by `Overmem.Extensions.Pes2021.Tests`.

Each fixture must use its own versioned directory and contain:

```text
<fixture-id>/
  memory.bin
  manifest.json
  expected.json
  SHA256SUMS
```

Rules:

- capture only the minimum read-only memory interval needed by the test;
- use a logical base address in expected output instead of a live absolute address;
- record PES executable version/hash, active patch/mods, stride and capture procedure;
- classify the evidence as `observed`, `hypothesis`, `confirmed` or `refuted`;
- remove personal paths and unrelated adjacent memory;
- never modify a fixture after hashing it; create a new fixture id;
- do not commit a dump until it has been reviewed for accidental sensitive data.

The imported acceptance counts and example team map are already stored under `docs/pes2021/competition-fixtures/examples/`. They are not a substitute for `memory.bin`: the binary fixture must be captured and sanitized during package P7.

The full fixture schema, tests and gates are defined in `docs/pes2021/competition-fixtures/verification.md`.

