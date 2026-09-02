using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Pure decoder for a single 380-byte player record. All offsets and types come from the
/// profile JSON; the parser never hard-codes field positions. The parser preserves the
/// raw 380-byte span and its SHA-256 so downstream code can re-verify or fingerprint
/// without re-reading memory. Cheap validation runs first; expensive name scans run
/// only when cheap checks pass.
/// </summary>
public static class Pes2021PlayerRecordParser
{
    public static PlayerRecordParseResult TryParse(
        ReadOnlySpan<byte> buffer,
        int recordIndex,
        ulong address,
        Pes2021PlayerProfile profile)
    {
        if (buffer.Length < profile.Stride)
        {
            return new PlayerRecordParseResult(
                false,
                null,
                PlayerRecordRejectionReasons.BufferTooSmall,
                null,
                Array.Empty<string>());
        }

        var warnings = new List<string>();
        var span = buffer.Slice(profile.RecordLayout.StartOffset, profile.Stride);
        var copy = span.ToArray();
        var sha = Convert.ToHexString(SHA256.HashData(copy)).ToLowerInvariant();

        uint playerId = 0;
        bool playerIdDecoded = false;
        string? playerName = null;
        byte? heightRaw = null;
        byte? weightRaw = null;

        var fields = new List<DecodedFieldValue>();
        var validation = profile.RecordValidation;

        foreach (var field in profile.RecordLayout.Fields)
        {
            if (field.Offset + field.Width > span.Length)
            {
                return new PlayerRecordParseResult(
                    false,
                    null,
                    PlayerRecordRejectionReasons.BufferTooSmall,
                    field.Offset,
                    warnings);
            }

            var raw = span.Slice(field.Offset, field.Width);
            switch (field.Type)
            {
                case Pes2021PlayerFieldType.U8:
                {
                    var v = raw[0];
                    fields.Add(new DecodedFieldValue(
                        field.Name, v, null, null,
                        field.ReadStatus, field.Transform, Array.Empty<string>()));
                    if (field.Name == "height")
                    {
                        heightRaw = v;
                        if (v < validation.MinimumHeight || v > validation.MaximumHeight)
                        {
                            return new PlayerRecordParseResult(
                                false,
                                null,
                                PlayerRecordRejectionReasons.HeightOutOfRange,
                                field.Offset,
                                warnings);
                        }
                    }
                    else if (field.Name == "weight")
                    {
                        weightRaw = v;
                        if (v < validation.MinimumWeight || v > validation.MaximumWeight)
                        {
                            return new PlayerRecordParseResult(
                                false,
                                null,
                                PlayerRecordRejectionReasons.WeightOutOfRange,
                                field.Offset,
                                warnings);
                        }
                    }

                    break;
                }
                case Pes2021PlayerFieldType.I8:
                {
                    var v = (sbyte)raw[0];
                    fields.Add(new DecodedFieldValue(
                        field.Name, v, null, null,
                        field.ReadStatus, field.Transform, Array.Empty<string>()));
                    break;
                }
                case Pes2021PlayerFieldType.U16Le:
                {
                    var v = BinaryPrimitives.ReadUInt16LittleEndian(raw);
                    fields.Add(new DecodedFieldValue(
                        field.Name, v, null, null,
                        field.ReadStatus, field.Transform, Array.Empty<string>()));
                    break;
                }
                case Pes2021PlayerFieldType.U32Le:
                {
                    var v = BinaryPrimitives.ReadUInt32LittleEndian(raw);
                    fields.Add(new DecodedFieldValue(
                        field.Name, v, null, null,
                        field.ReadStatus, field.Transform, Array.Empty<string>()));
                    if (field.Name == "playerId")
                    {
                        playerId = v;
                        playerIdDecoded = true;
                        if (v < validation.MinimumPlayerId || v > validation.MaximumPlayerId)
                        {
                            return new PlayerRecordParseResult(
                                false,
                                null,
                                PlayerRecordRejectionReasons.PlayerIdOutOfRange,
                                field.Offset,
                                warnings);
                        }
                    }

                    break;
                }
                case Pes2021PlayerFieldType.I32Le:
                {
                    var v = BinaryPrimitives.ReadInt32LittleEndian(raw);
                    double? display = field.Transform switch
                    {
                        Pes2021PlayerTransform.RawMul100Eur => v * 100.0,
                        _ => null,
                    };
                    fields.Add(new DecodedFieldValue(
                        field.Name, v, null, display,
                        field.ReadStatus, field.Transform, Array.Empty<string>()));
                    if (field.Name == "marketValue")
                    {
                        var abs = v < 0 ? -v : v;
                        if (abs > 2_000_000_000)
                        {
                            return new PlayerRecordParseResult(
                                false,
                                null,
                                PlayerRecordRejectionReasons.MarketValueImplausible,
                                field.Offset,
                                warnings);
                        }
                    }

                    break;
                }
                case Pes2021PlayerFieldType.FixedAscii:
                {
                    var (text, reason, terminator) = DecodeFixedAscii(raw, field.Width);
                    if (reason is null && (field.Name == "playerName" || field.Name == "clubShirtName" || field.Name == "nationalShirtName"))
                    {
                        try
                        {
                            var nullAt = -1;
                            for (var ni = 0; ni < raw.Length; ni++)
                            {
                                if (raw[ni] == 0) { nullAt = ni; break; }
                            }
                            if (nullAt < 0) nullAt = raw.Length;
                            var utf8Bytes = new byte[nullAt];
                            for (var bi = 0; bi < nullAt; bi++) utf8Bytes[bi] = raw[bi];
                            text = System.Text.Encoding.UTF8.GetString(utf8Bytes);
                        }
                        catch
                        {
                            // fall back to ASCII
                        }
                    }
                    var isPrimaryName = field.Name == "playerName";

                    if (isPrimaryName && reason is not null)
                    {
                        return new PlayerRecordParseResult(
                            false,
                            null,
                            reason,
                            field.Offset,
                            warnings);
                    }

                    if (isPrimaryName)
                    {
                        playerName = text;
                    }
                    else if (reason is null && (field.Name == "clubShirtName" || field.Name == "nationalShirtName")
                             && terminator < 0)
                    {
                        warnings.Add($"{field.Name}_no_embedded_terminator");
                    }

                    fields.Add(new DecodedFieldValue(
                        field.Name, null, text, null,
                        field.ReadStatus, field.Transform,
                        terminator >= 0 ? Array.Empty<string>() : new[] { "no_embedded_terminator" }));
                    break;
                }
                case Pes2021PlayerFieldType.I8X4:
                {
                    var bytes = new long[4];
                    for (var i = 0; i < 4; i++)
                    {
                        bytes[i] = (sbyte)raw[i];
                    }

                    fields.Add(new DecodedFieldValue(
                        field.Name, bytes[0], null, null,
                        field.ReadStatus, field.Transform, Array.Empty<string>()));
                    break;
                }
                default:
                    return new PlayerRecordParseResult(
                        false,
                        null,
                        PlayerRecordRejectionReasons.PartialRead,
                        field.Offset,
                        warnings);
            }
        }

        if (!playerIdDecoded)
        {
            return new PlayerRecordParseResult(
                false,
                null,
                PlayerRecordRejectionReasons.PartialRead,
                null,
                warnings);
        }

        var record = new DecodedPlayerRecord(
            Address: address,
            RecordIndex: recordIndex,
            PlayerId: playerId,
            PlayerName: playerName,
            ClubShirtName: null,
            NationalShirtName: null,
            Fields: fields,
            RawRecord: copy,
            RawRecordSha256: sha,
            Warnings: warnings);

        return new PlayerRecordParseResult(true, record, null, null, warnings);
    }

    private static (string? Text, string? Reason, int TerminatorIndex) DecodeFixedAscii(
        ReadOnlySpan<byte> raw, int width)
    {
        var terminator = raw.IndexOf((byte)0);
        var effective = terminator >= 0 ? raw.Slice(0, terminator) : raw;

        foreach (var b in effective)
        {
            if (b < 0x20 || b == 0x7F)
            {
                return (null, PlayerRecordRejectionReasons.NameContainsControlBytes, terminator);
            }
        }

        if (effective.IsEmpty)
        {
            return (string.Empty, PlayerRecordRejectionReasons.NameEmpty, terminator);
        }

        var bytes = effective.ToArray();
        var text = IsLikelyUtf8(bytes)
            ? Encoding.UTF8.GetString(bytes)
            : Encoding.ASCII.GetString(bytes);
        return (text, null, terminator);
    }

    private static bool IsLikelyUtf8(byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b < 0x80) continue;
            if (b >= 0xC2 && b <= 0xDF)
            {
                if (i + 1 >= bytes.Length) return false;
                var next = bytes[i + 1];
                if ((next & 0xC0) != 0x80) return false;
                i++;
                continue;
            }
            if (b >= 0xE0 && b <= 0xEF)
            {
                if (i + 2 >= bytes.Length) return false;
                if ((bytes[i + 1] & 0xC0) != 0x80) return false;
                if ((bytes[i + 2] & 0xC0) != 0x80) return false;
                i += 2;
                continue;
            }
            return false;
        }
        return true;
    }
}