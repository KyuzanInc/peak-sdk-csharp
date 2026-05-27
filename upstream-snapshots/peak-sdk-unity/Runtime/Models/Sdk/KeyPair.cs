using System;

namespace Peak.Models.Sdk
{
    /// <summary>
    /// Represents a public/private key pair for P256 cryptography.
    /// </summary>
    [Serializable]
    public class KeyPair
    {
        /// <summary>
        /// The public key in hex format
        /// </summary>
        public string publicKey;

        /// <summary>
        /// The private key in hex format
        /// </summary>
        public string privateKey;

        /// <summary>
        /// Default constructor for Unity serialization
        /// </summary>
        public KeyPair() { }

        /// <summary>
        /// Creates a new KeyPair with the specified keys
        /// </summary>
        public KeyPair(string publicKey, string privateKey)
        {
            this.publicKey = publicKey;
            this.privateKey = privateKey;
        }
    }
}
