namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Stable rejection reasons emitted by the pure record parser/validator. New reasons
/// require a profile-schema version bump and an update to the wire-contract documentation.
/// </summary>
public static class PlayerRecordRejectionReasons
{
    public const string BufferTooSmall = "BUFFER_TOO_SMALL";
    public const string HeightOutOfRange = "HEIGHT_OUT_OF_RANGE";
    public const string WeightOutOfRange = "WEIGHT_OUT_OF_RANGE";
    public const string PlayerIdOutOfRange = "PLAYER_ID_OUT_OF_RANGE";
    public const string NameUnterminated = "NAME_UNTERMINATED";
    public const string NameEmpty = "NAME_EMPTY";
    public const string NameContainsControlBytes = "NAME_CONTAINS_CONTROL_BYTES";
    public const string ClubShirtNameUnterminated = "CLUB_SHIRT_NAME_UNTERMINATED";
    public const string NationalShirtNameUnterminated = "NATIONAL_SHIRT_NAME_UNTERMINATED";
    public const string MarketValueImplausible = "MARKET_VALUE_IMPLAUSIBLE";
    public const string NeighborStrideMismatch = "NEIGHBOR_STRIDE_MISMATCH";
    public const string PartialRead = "PARTIAL_READ";
}