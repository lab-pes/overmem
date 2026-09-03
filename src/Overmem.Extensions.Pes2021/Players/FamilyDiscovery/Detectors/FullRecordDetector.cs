using System.Collections.Generic;
using System.Linq;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery.Detectors;

public sealed class FullRecordDetector
{
    private readonly FingerprintSet _fingerprints;

    public FullRecordDetector(FingerprintSet fingerprints)
    {
        _fingerprints = fingerprints;
    }

    public FamilyHit? Detect(ulong address, byte[] data, int offset)
    {
        if (offset + 380 > data.Length)
            return null; // Not enough data for a full record

        foreach (var fp in _fingerprints.Fingerprints)
        {
            if (fp.MaskedRecord != null)
            {
                bool match = true;
                for (int i = 0; i < 380; i++)
                {
                    if (fp.Mask[i] != 0 && data[offset + i] != fp.MaskedRecord[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    // Check if it's an exact match (ignoring mask)
                    bool exactMatch = true;
                    if (fp.ExactRecord != null)
                    {
                        for (int i = 0; i < 380; i++)
                        {
                            if (data[offset + i] != fp.ExactRecord[i])
                            {
                                exactMatch = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        exactMatch = false;
                    }

                    return new FamilyHit(
                        address,
                        fp.PlayerId,
                        fp.PlayerName,
                        exactMatch ? FamilyResultClass.ExactRecordCopy : FamilyResultClass.MaskedRecordCopy,
                        100, // Score
                        System.Array.Empty<string>(),
                        true);
                }
            }
        }

        return null;
    }
}
