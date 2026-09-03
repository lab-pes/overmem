using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public static class Pes2021RegionBlockAssembler
{
    public static byte[] AssembleContiguousPrefix(IReadOnlyList<RegionBlockRead> blocks)
    {
        if (blocks.Count == 0)
        {
            return Array.Empty<byte>();
        }

        var ordered = blocks.OrderBy(block => block.BlockOffset).ToArray();
        if (ordered[0].BlockOffset != 0)
        {
            throw new InvalidDataException("Region block sequence must start at offset zero.");
        }

        ulong stop = 0;
        foreach (var block in ordered)
        {
            ValidateBlock(block);
            if (block.BlockOffset > stop)
            {
                throw new InvalidDataException(
                    $"Region block sequence has a gap before offset 0x{block.BlockOffset:X}.");
            }

            stop = Math.Max(stop, checked(block.BlockOffset + (ulong)block.BytesRead));
        }

        if (stop > int.MaxValue)
        {
            throw new InvalidDataException("Assembled region prefix exceeds supported array size.");
        }

        var buffer = new byte[(int)stop];
        var written = new bool[(int)stop];
        foreach (var block in ordered)
        {
            var destination = checked((int)block.BlockOffset);
            for (var index = 0; index < block.BytesRead; index++)
            {
                var outputIndex = destination + index;
                var value = block.Payload[index];
                if (written[outputIndex] && buffer[outputIndex] != value)
                {
                    throw new InvalidDataException(
                        $"Overlapping region blocks disagree at offset 0x{outputIndex:X}.");
                }

                buffer[outputIndex] = value;
                written[outputIndex] = true;
            }
        }

        return buffer;
    }

    private static void ValidateBlock(RegionBlockRead block)
    {
        if (block.BytesRead < 0 || block.BytesRead > block.Payload.Length)
        {
            throw new InvalidDataException(
                $"Invalid BytesRead={block.BytesRead} for payload length {block.Payload.Length}.");
        }
    }
}
