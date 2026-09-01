using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Score-based validator for a decoded player record. Cheap checks run first; expensive
/// string/neighbor checks run only when cheap checks pass. The result always carries the
/// score and the contributing reasons so callers can audit the decision.
/// </summary>
public static class Pes2021PlayerRecordValidator
{
    private const int ScoreHeightInRange = 1;
    private const int ScoreWeightInRange = 1;
    private const int ScorePlayerIdInRange = 1;
    private const int ScorePlayerNameNonEmpty = 1;
    private const int ScoreMarketValuePlausible = 1;
    private const int ScoreForwardNeighbor = 1;
    private const int ScoreBackwardNeighbor = 1;
    private const int MaxCheapScore = ScoreHeightInRange + ScoreWeightInRange + ScorePlayerIdInRange
        + ScorePlayerNameNonEmpty + ScoreMarketValuePlausible;

    /// <summary>
    /// Validates a decoded record in isolation (cheap checks only).
    /// </summary>
    public static PlayerRecordValidationResult Validate(DecodedPlayerRecord record, Pes2021PlayerProfile profile)
    {
        var reasons = new List<string>();
        var score = 0;

        var height = FindField(record, "height");
        if (height?.RawLong is long h)
        {
            if (h >= profile.RecordValidation.MinimumHeight && h <= profile.RecordValidation.MaximumHeight)
            {
                score += ScoreHeightInRange;
                reasons.Add("height_in_range");
            }
            else
            {
                reasons.Add("height_out_of_range");
            }
        }
        else
        {
            reasons.Add("height_missing");
        }

        var weight = FindField(record, "weight");
        if (weight?.RawLong is long w)
        {
            if (w >= profile.RecordValidation.MinimumWeight && w <= profile.RecordValidation.MaximumWeight)
            {
                score += ScoreWeightInRange;
                reasons.Add("weight_in_range");
            }
            else
            {
                reasons.Add("weight_out_of_range");
            }
        }
        else
        {
            reasons.Add("weight_missing");
        }

        if (record.PlayerId >= profile.RecordValidation.MinimumPlayerId
            && record.PlayerId <= profile.RecordValidation.MaximumPlayerId)
        {
            score += ScorePlayerIdInRange;
            reasons.Add("player_id_in_range");
        }
        else
        {
            reasons.Add("player_id_out_of_range");
        }

        if (!string.IsNullOrWhiteSpace(record.PlayerName))
        {
            score += ScorePlayerNameNonEmpty;
            reasons.Add("player_name_non_empty");
        }
        else
        {
            reasons.Add("player_name_empty");
        }

        var marketValue = FindField(record, "marketValue");
        if (marketValue?.RawLong is long mv)
        {
            var abs = mv < 0 ? -mv : mv;
            if (abs <= 2_000_000_000)
            {
                score += ScoreMarketValuePlausible;
                reasons.Add("market_value_plausible");
            }
            else
            {
                reasons.Add("market_value_implausible");
            }
        }

        var accept = score >= MaxCheapScore - 1;
        return new PlayerRecordValidationResult(accept, score, MaxCheapScore, reasons);
    }

    /// <summary>
    /// Validates a record against a small window of neighbors. Neighbor records are
    /// expected to be pre-decoded. The cheap checks must pass before this is invoked.
    /// </summary>
    public static PlayerRecordValidationResult ValidateWithNeighbors(
        DecodedPlayerRecord record,
        Pes2021PlayerProfile profile,
        IReadOnlyList<DecodedPlayerRecord>? forwardNeighbors,
        IReadOnlyList<DecodedPlayerRecord>? backwardNeighbors)
    {
        var baseResult = Validate(record, profile);
        var reasons = new List<string>(baseResult.Reasons.Count + 2);
        reasons.AddRange(baseResult.Reasons);
        var score = baseResult.Score;

        if (forwardNeighbors is { Count: > 0 })
        {
            score += ScoreForwardNeighbor;
            reasons.Add("forward_neighbors_present");
        }
        else
        {
            reasons.Add("forward_neighbors_absent");
        }

        if (backwardNeighbors is { Count: > 0 })
        {
            score += ScoreBackwardNeighbor;
            reasons.Add("backward_neighbors_present");
        }
        else
        {
            reasons.Add("backward_neighbors_absent");
        }

        var maxScore = baseResult.MaxScore + ScoreForwardNeighbor + ScoreBackwardNeighbor;
        var accept = baseResult.Accept && (score >= maxScore - 1);
        return new PlayerRecordValidationResult(accept, score, maxScore, reasons);
    }

    private static DecodedFieldValue? FindField(DecodedPlayerRecord record, string name)
    {
        foreach (var field in record.Fields)
        {
            if (field.Name == name) return field;
        }

        return null;
    }
}