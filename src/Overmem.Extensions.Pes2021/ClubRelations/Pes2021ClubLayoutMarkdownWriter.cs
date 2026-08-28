using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public static class Pes2021ClubLayoutMarkdownWriter
{
    public static void Write(string path, IReadOnlyList<Pes2021ClubLayoutCandidate> candidates)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        writer.WriteLine("# club-record-layout");
        writer.WriteLine();
        writer.WriteLine("P2 baseline do plano `plan-runtime-club-league-country-resolution.md`.");
        writer.WriteLine("Somente estrutura bruta; nenhuma hipótese vira domínio ou código Lua.");
        writer.WriteLine();
        writer.WriteLine($"Candidatos lidos: **{candidates.Count}**.");
        writer.WriteLine();
        writer.WriteLine("## Campos agregados");
        writer.WriteLine();
        writer.WriteLine("Estabilidade classificada por offset entre todos os candidatos:");
        writer.WriteLine();
        writer.WriteLine("| Offset | Estabilidade | Amostras | Valores distintos |");
        writer.WriteLine("|---:|---|---:|---:|");
        var aggregated = new SortedDictionary<int, (int Samples, int Distinct, string Stability)>();
        foreach (var candidate in candidates)
        {
            foreach (var field in candidate.FieldSummary)
            {
                if (!aggregated.ContainsKey(field.Offset))
                {
                    aggregated[field.Offset] = (0, 0, field.Stability.ToString());
                }

                var current = aggregated[field.Offset];
                aggregated[field.Offset] = (
                    current.Samples + field.SampleCount,
                    Math.Max(current.Distinct, field.DistinctValueCount),
                    current.Stability);
            }
        }

        foreach (var pair in aggregated)
        {
            writer.WriteLine($"| 0x{pair.Key:X} | {pair.Value.Stability} | {pair.Value.Samples} | {pair.Value.Distinct} |");
        }

        writer.WriteLine();
        writer.WriteLine("## Janelas por candidato");
        writer.WriteLine();
        foreach (var candidate in candidates)
        {
            writer.WriteLine($"### `{candidate.Name}` (team_id={candidate.TeamId}, secondary_id={candidate.SecondaryId}, control=C{candidate.ControlOrdinal}, address=0x{candidate.Address:X})");
            writer.WriteLine();
            foreach (var window in candidate.Windows)
            {
                writer.WriteLine($"- Janela 0x{window.WindowSize:X}:");
                writer.WriteLine();
                writer.WriteLine("  | Offset | u8 | u16 (LE) | u32 (LE) | i32 |");
                writer.WriteLine("  |---:|---:|---:|---:|---:|");
                foreach (var entry in window.Entries)
                {
                    writer.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "  | 0x{0:X} | {1} | {2} | {3} | {4} |",
                        entry.Offset,
                        entry.AsByte,
                        entry.AsUInt16,
                        entry.AsUInt32,
                        entry.AsInt32));
                }

                writer.WriteLine();
            }
        }

        writer.WriteLine("## Notas");
        writer.WriteLine();
        writer.WriteLine("- Os valores `u32` são brutos (little-endian). Não aplicar suposição semântica.");
        writer.WriteLine("- As colunas `u8`, `u16` e `i32` são derivações do mesmo quad.");
        writer.WriteLine("- A estabilidade `ConstantAcrossClubs` indica campo candidato a chave estável de clube.");
        writer.WriteLine("- Nenhum valor deste arquivo é promovido a domínio/Lua/CT até passar pela Fase 3.");
    }
}
