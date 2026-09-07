using System;
using System.Collections.Generic;
using System.Linq;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public sealed record StrideInferenceResult(
    FamilyResultClass ResultClass,
    int? InferredStride,
    int? InferredResidue,
    string? Reason);

/// <summary>
/// Infere automaticamente o stride a partir de uma lista de hits candidatos,
/// calculando distâncias entre pares e agrupando por resíduo.
/// </summary>
public static class StrideInferenceEngine
{
    public static StrideInferenceResult InferStride(
        IReadOnlyList<FamilyHit> hits,
        int minControlsForPromotion)
    {
        if (hits.Count < minControlsForPromotion)
        {
            return new StrideInferenceResult(
                FamilyResultClass.IsolatedHit,
                null,
                null,
                $"Menos que o mínimo de controles ({minControlsForPromotion}) para inferir stride.");
        }

        // Ordena os hits por endereço
        var sortedHits = hits.OrderBy(h => h.Address).ToList();
        
        // Coleta distâncias entre hits consecutivos
        var deltas = new Dictionary<int, int>(); // delta -> count
        
        for (int i = 0; i < sortedHits.Count - 1; i++)
        {
            var delta = (int)(sortedHits[i + 1].Address - sortedHits[i].Address);
            if (delta > 0)
            {
                if (!deltas.ContainsKey(delta))
                    deltas[delta] = 0;
                deltas[delta]++;
            }
        }

        if (deltas.Count == 0)
        {
            return new StrideInferenceResult(
                FamilyResultClass.IsolatedHit,
                null,
                null,
                "Não foi possível calcular deltas (todos os hits no mesmo endereço?).");
        }

        // Tenta encontrar o delta mais comum
        // Um stride verdadeiro vai aparecer como múltiplos do delta também.
        // Coleta deltas com frequência mínima e também seus divisores e múltiplos comuns.
        var candidateDeltas = deltas.Where(kvp => kvp.Value >= minControlsForPromotion - 1).Select(kvp => kvp.Key).ToList();

        // Também testa divisores e múltiplos dos deltas encontrados (2x, 0.5x)
        // para descobrir strides que são metade ou dobro de distâncias observadas.
        var derivedStrides = new HashSet<int>();
        foreach (var d in candidateDeltas)
        {
            derivedStrides.Add(d);
            if (d % 2 == 0 && d / 2 > 0) derivedStrides.Add(d / 2);
            derivedStrides.Add(d * 2);
        }
        // Adiciona todos os deltas observados (mesmo com frequência baixa) como candidatos
        foreach (var d in deltas.Keys)
        {
            derivedStrides.Add(d);
        }

        var strideScores = new Dictionary<int, (int Count, int Residue)>();

        foreach (var stride in derivedStrides)
        {
            // Tenta ver qual resíduo é mais comum para este stride
            var residues = sortedHits.GroupBy(h => (int)(h.Address % (ulong)stride))
                                     .OrderByDescending(g => g.Count())
                                     .FirstOrDefault();

            if (residues != null && residues.Count() >= minControlsForPromotion)
            {
                strideScores[stride] = (residues.Count(), residues.Key);
            }
        }

        if (strideScores.Count == 0)
        {
            return new StrideInferenceResult(
                FamilyResultClass.IsolatedHit,
                null,
                null,
                "Nenhum stride candidato validou o número mínimo de hits.");
        }

        var bestScore = strideScores.Values.Max(v => v.Count);
        var bestStrides = strideScores.Where(kvp => kvp.Value.Count == bestScore).Select(kvp => kvp.Key).ToList();

        // Se houver empate, preferimos o stride que efetivamente apareceu como delta entre hits consecutivos
        if (bestStrides.Count > 1)
        {
            var presentInDeltas = bestStrides.Where(s => candidateDeltas.Contains(s)).ToList();
            if (presentInDeltas.Count == 1)
            {
                bestStrides = presentInDeltas;
            }
        }

        if (bestStrides.Count > 1)
        {
            return new StrideInferenceResult(
                FamilyResultClass.AmbiguousFamily,
                null,
                null,
                "Empate não resolvido entre múltiplos strides candidatos.");
        }

        var winnerStride = bestStrides[0];
        var winnerData = strideScores[winnerStride];
        
        var resultClass = winnerStride == 380 
            ? FamilyResultClass.SameLayoutFamily 
            : FamilyResultClass.AlternateStrideFamily;

        return new StrideInferenceResult(
            resultClass,
            winnerStride,
            winnerData.Residue,
            $"Stride inferido: {winnerStride}, hits alinhados: {winnerData.Count}");
    }
}
