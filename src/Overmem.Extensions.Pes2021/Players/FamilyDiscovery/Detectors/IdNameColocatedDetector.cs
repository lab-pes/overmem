using System;
using System.Collections.Generic;
using System.Text;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery.Detectors;

public sealed class IdNameColocatedDetector
{
    private readonly FingerprintSet _fingerprints;
    private readonly int _windowSize; // ex: 200 bytes

    public IdNameColocatedDetector(FingerprintSet fingerprints, int windowSize = 200)
    {
        _fingerprints = fingerprints;
        _windowSize = windowSize;
    }

    public FamilyHit? Detect(ulong address, byte[] data, int offset)
    {
        // First try to find ID
        foreach (var fp in _fingerprints.Fingerprints)
        {
            if (offset + 4 > data.Length)
                continue;

            if (data[offset] == fp.IdBytes[0] &&
                data[offset + 1] == fp.IdBytes[1] &&
                data[offset + 2] == fp.IdBytes[2] &&
                data[offset + 3] == fp.IdBytes[3])
            {
                // Found ID, now search for name nearby
                if (fp.NameBytes != null && fp.NameBytes.Length > 0)
                {
                    int endSearch = Math.Min(data.Length, offset + _windowSize);
                    for (int i = Math.Max(0, offset - _windowSize); i < endSearch - fp.NameBytes.Length; i++)
                    {
                        bool nameMatch = true;
                        for (int j = 0; j < fp.NameBytes.Length; j++)
                        {
                            if (data[i + j] != fp.NameBytes[j])
                            {
                                nameMatch = false;
                                break;
                            }
                        }

                        if (nameMatch)
                        {
                            return new FamilyHit(
                                address,
                                fp.PlayerId,
                                fp.PlayerName,
                                FamilyResultClass.IdNameColocated,
                                70, // Score
                                Array.Empty<string>(),
                                true);
                        }
                    }
                }
            }
        }

        return null;
    }
}
