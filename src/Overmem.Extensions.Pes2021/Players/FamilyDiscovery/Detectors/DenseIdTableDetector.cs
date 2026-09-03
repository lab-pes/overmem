using System;
using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery.Detectors;

public sealed class DenseIdTableDetector
{
    private readonly FingerprintSet _fingerprints;

    public DenseIdTableDetector(FingerprintSet fingerprints)
    {
        _fingerprints = fingerprints;
    }

    public FamilyHit? Detect(ulong address, byte[] data, int offset)
    {
        if (offset + 4 > data.Length)
            return null;

        // Try to read a sequence of IDs
        int maxSequentialIds = 0;
        uint firstId = 0;
        string? firstName = null;

        for (int i = offset; i < data.Length - 4; i += 4)
        {
            uint possibleId = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i, 4));
            
            bool found = false;
            foreach (var fp in _fingerprints.Fingerprints)
            {
                if ((fp.PlayerId & 0x00FFFFFF) == (possibleId & 0x00FFFFFF))
                {
                    found = true;
                    if (maxSequentialIds == 0)
                    {
                        firstId = fp.PlayerId;
                        firstName = fp.PlayerName;
                    }
                    break;
                }
            }

            if (found)
            {
                maxSequentialIds++;
            }
            else
            {
                break;
            }
        }

        if (maxSequentialIds >= 3)
        {
            return new FamilyHit(
                address,
                firstId,
                firstName,
                FamilyResultClass.DenseIdTable,
                80 + (maxSequentialIds * 2), // Score scales with density
                Array.Empty<string>(),
                true);
        }

        return null;
    }
}
