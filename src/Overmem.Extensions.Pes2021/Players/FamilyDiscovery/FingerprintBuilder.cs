using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

/// <summary>
/// Construtor puro de fingerprints. Gera a máscara declarativa baseada nos campos com
/// status `Confirmed` no perfil (ignorando campos dinâmicos) e constrói o <see cref="FingerprintSet"/>.
/// Nenhuma dependência de leitura de memória.
/// </summary>
public static class FingerprintBuilder
{
    private const string MaskVersion = "v1";

    public static FingerprintSet Build(
        Pes2021PlayerProfile profile,
        IReadOnlyList<DecodedPlayerRecord> controlPlayers)
    {
        if (controlPlayers.Count == 0)
            throw new ArgumentException("Pelo menos um jogador de controle é necessário.", nameof(controlPlayers));

        var maskAndOffsets = BuildMask(profile);
        var mask = maskAndOffsets.Mask;
        var dynamicOffsets = maskAndOffsets.DynamicOffsets;

        var fingerprints = new List<PlayerFingerprint>(controlPlayers.Count);

        foreach (var player in controlPlayers)
        {
            var idBytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(idBytes, player.PlayerId);

            byte[]? nameBytes = null;
            if (!string.IsNullOrWhiteSpace(player.PlayerName))
            {
                nameBytes = Encoding.UTF8.GetBytes(player.PlayerName);
            }

            byte[]? exactRecord = null;
            byte[]? maskedRecord = null;

            if (player.RawRecord != null && player.RawRecord.Length >= profile.Stride)
            {
                exactRecord = new byte[profile.Stride];
                Array.Copy(player.RawRecord, exactRecord, profile.Stride);

                maskedRecord = new byte[profile.Stride];
                Array.Copy(exactRecord, maskedRecord, profile.Stride);
                for (var i = 0; i < profile.Stride; i++)
                {
                    maskedRecord[i] &= mask[i];
                }
            }

            fingerprints.Add(new PlayerFingerprint(
                PlayerId: player.PlayerId,
                IdBytes: idBytes,
                PlayerName: player.PlayerName,
                NameBytes: nameBytes,
                ExactRecord: exactRecord,
                MaskedRecord: maskedRecord,
                Mask: mask,
                MaskVersion: MaskVersion));
        }

        return new FingerprintSet(
            profile.ProfileId,
            profile.ProfileVersion,
            fingerprints,
            dynamicOffsets);
    }

    private static (byte[] Mask, IReadOnlyList<int> DynamicOffsets) BuildMask(Pes2021PlayerProfile profile)
    {
        var mask = new byte[profile.Stride];
        var dynamicOffsets = new List<int>();

        // Por padrão, ignora tudo (0x00)
        Array.Fill(mask, (byte)0x00);

        foreach (var field in profile.RecordLayout.Fields)
        {
            // Valida se o campo cabe no stride
            if (field.Offset < 0 || field.Offset + field.Width > profile.Stride)
                continue;

            if (field.ReadStatus == Pes2021PlayerEvidenceStatus.Confirmed)
            {
                // Preenche a máscara com 1s (0xFF) para campos confirmados
                for (var i = 0; i < field.Width; i++)
                {
                    mask[field.Offset + i] = 0xFF;
                }
            }
            else
            {
                // Regista como offset dinâmico ignorado
                for (var i = 0; i < field.Width; i++)
                {
                    dynamicOffsets.Add(field.Offset + i);
                }
            }
        }

        // Os bytes que não estão mapeados em nenhum campo são automaticamente ignorados (0x00)
        for (var i = 0; i < profile.Stride; i++)
        {
            if (mask[i] == 0x00 && !dynamicOffsets.Contains(i))
            {
                dynamicOffsets.Add(i);
            }
        }

        return (mask, dynamicOffsets);
    }
}
