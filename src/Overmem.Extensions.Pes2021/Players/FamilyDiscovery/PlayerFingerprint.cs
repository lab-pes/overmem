using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

/// <summary>
/// Representa a "impressão digital" binária de um jogador usado como controle na busca.
/// Carrega múltiplas representações (exato, mascarado, id, nome) para permitir que o scanner
/// tente matches relaxados se o match exato falhar.
/// </summary>
public sealed record PlayerFingerprint(
    uint PlayerId,
    byte[] IdBytes,           // 4 bytes LE
    string? PlayerName,
    byte[]? NameBytes,        // UTF-8, sem terminador
    byte[]? ExactRecord,      // 380 bytes completos (quando disponível)
    byte[]? MaskedRecord,     // 380 bytes com campos dinâmicos zerados
    byte[] Mask,              // bitmask: 1=comparar, 0=ignorar
    string MaskVersion);      // versionamento da máscara

/// <summary>
/// Conjunto de fingerprints construídos a partir de uma lista de jogadores de controle e
/// do perfil ativo. Agrupa os controles para um único scan.
/// </summary>
public sealed record FingerprintSet(
    string ProfileId,
    string ProfileVersion,
    IReadOnlyList<PlayerFingerprint> Fingerprints,
    IReadOnlyList<int> DynamicByteOffsets);  // offsets ignorados pela máscara
