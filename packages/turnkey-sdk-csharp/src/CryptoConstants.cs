// Ported from upstream-snapshots/turnkey-sdk-unity/Runtime/UnityConstants.cs (commit 039d8e4).
// The "Unity" prefix is dropped because nothing here is Unity-specific; the constants exist
// because BouncyCastle requires explicit curve parameters whereas @noble/curves does not.

namespace KyuzanInc.Turnkey.Sdk
{
    /// <summary>
    /// Constants that are not part of the official @turnkey/crypto package but are
    /// required by the BouncyCastle-backed primitives in this port.
    /// </summary>
    public static class CryptoConstants
    {
        // Curve name for BouncyCastle's ECNamedCurveTable.GetByName().
        public const string CURVE_NAME = "secp256r1";

        // P-256 public key sizes.
        public const int COMPRESSED_PUBLIC_KEY_SIZE = 33;

        // P-256 curve parameters for point decompression. In the official Node.js SDK,
        // these are hardcoded in the uncompressRawPublicKey function.
        public const string P256_P = "115792089210356248762697446949407573530086143415290314195533631308867097853951";
        public const string P256_B = "5ac635d8aa3a93e7b3ebbd55769886bc651d06b0cc53b0f63bce3c3e27d2604b";
        public const string P256_A_OFFSET = "3"; // p - 3
    }
}
