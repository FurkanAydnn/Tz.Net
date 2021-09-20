using Chaos.NaCl;
using System;
using System.Collections.Generic;
using TezosSharp.Extensions;
using TezosSharp.Internal;

namespace TezosSharp.Security
{
    public abstract class CryptoBase
    {
        /// <summary>
        /// Checks that a tz1 address is valid.
        /// </summary>
        /// <param name="address">address to check for validity</param>
        /// <returns>True if valid address.</returns>
        public static bool CheckAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new Exception("Address is null");

            try
            {
                byte[] prefix;

                if (address.StartsWith("tz1"))
                    prefix = Prefix.tz1;

                else if (address.StartsWith("tz2"))
                    prefix = Prefix.tz2;

                else if (address.StartsWith("tz3"))
                    prefix = Prefix.tz3;

                else if (address.StartsWith("KT"))
                    prefix = Prefix.KT;

                else
                    throw new Exception("Not supported prefix");

                B58C.Decode(address, prefix);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generate a new 15-word mnemonic code.
        /// </summary>
        /// <returns>15 words</returns>
        public static string GenerateMnemonic()
        {
            Bip39 bip39 = new Bip39();

            byte[] bytes = new byte[20];

            new Random().NextBytes(bytes);

            List<string> code = bip39.ToMnemonic(bytes);

            return string.Join(" ", code);
        }
        
        /// <summary>
        /// Sign message with private key.
        /// </summary>
        /// <param name="bytes">Message to sign.</param>
        /// <param name="sk">Secret key used to sign.</param>
        /// <param name="watermark">Watermark</param>
        /// <returns>Signed message.</returns>
        public abstract SignedMessage Sign(string bytes, string watermark = null);

        /// <summary>
        /// Sign message with private key.
        /// </summary>
        /// <param name="bytes">Message to sign.</param>
        /// <param name="sk">Secret key used to sign.</param>
        /// <param name="watermark">Watermark</param>
        /// <returns>Signed message.</returns>
        public abstract SignedMessage Sign(string bytes, byte[] watermark = null);

        public abstract bool Verify(string bytes, byte[] sig, string pk);

        public abstract string DecryptPublicKey();
    }
}