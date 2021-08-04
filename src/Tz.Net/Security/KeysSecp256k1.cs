using System;
using System.Runtime.InteropServices;
using System.Security;
using NBitcoin;
using TezosSharp.Internal;

namespace TezosSharp.Security
{
    public sealed class KeysSecp256k1 : IKeys
    {
        private const int PKHASH_BIT_SIZE = 20 * 8;

        public SecureString PublicKey { get; internal set; }
        public SecureString PrivateKey { get; internal set; }
        public string PublicHash { get; internal set; }
        
        public NBitcoin.Key BitcoinPrivateKey{ get; }
        public PubKey BitcoinPublicKey { get; }

        public KeysSecp256k1(PubKey pubKey, NBitcoin.Key privateKey)
        {
            BitcoinPrivateKey = privateKey;
            BitcoinPublicKey = pubKey;
            
            CrateKeys(Prefix.tz2);
        }

        private void CrateKeys(byte[] prefix) 
        {
            var sk = this.BitcoinPrivateKey.ToBytes();
            var pk = this.BitcoinPublicKey.ToBytes();

            PublicHash = B58C.Encode(Hashing.Generic(PKHASH_BIT_SIZE, pk), prefix);

            PublicKey = new SecureString();
            PrivateKey = new SecureString();

            string encodedPK = B58C.Encode(pk, Prefix.sppk);
            foreach (char c in encodedPK)
            {
                PublicKey.AppendChar(c);
            }

            string encodedSK = B58C.Encode(sk, Prefix.spsk);
            foreach (char c in encodedSK)
            {
                PrivateKey.AppendChar(c);
            }
            
            // Quickly zero out the unneeded key arrays so it doesn't linger in memory before the GC can sweep it up.
            Array.Clear(pk, 0, pk.Length);
            Array.Clear(sk, 0, sk.Length);
        }

        /// <summary>
        /// Do not store this result on the heap!
        /// </summary>
        /// <returns>Decrypted public key</returns>
        public string DecryptPublicKey()
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(PublicKey);
                return Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        /// <summary>
        /// Do not store this result on the heap!
        /// </summary>
        /// <returns>Decrypted private key</returns>
        string DecryptPrivateKey()
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(PrivateKey);
                return Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }
    }
}