﻿using Chaos.NaCl;
using System;
using System.Collections.Generic;
using TezosSharp.Extensions;
using TezosSharp.Internal;

namespace TezosSharp.Security
{
    public class Crypto : CryptoBase
    {
        Keys _keys;
        
        public Crypto(Keys keys)
        {
            _keys = keys;   
        }

        public override string DecryptPublicKey()
        {
            return ((IKeys)_keys).DecryptPublicKey();
        }
    
        public override SignedMessage Sign(string bytes, string watermark = null)
        {
            return Sign(bytes, watermark?.HexToByteArray());
        }

        /// <summary>
        /// Sign message with private key.
        /// </summary>
        /// <param name="bytes">Message to sign.</param>
        /// <param name="sk">Secret key used to sign.</param>
        /// <param name="watermark">Watermark</param>
        /// <returns>Signed message.</returns>
        public override SignedMessage Sign(string bytes, byte[] watermark = null)
        {
            byte[] bb = bytes.HexToByteArray();

            if (watermark?.Length > 0)
            {
                byte[] bytesWithWatermark = new byte[bb.Length + watermark.Length];

                Array.Copy(watermark, 0, bytesWithWatermark, 0, watermark.Length);
                Array.Copy(bb, 0, bytesWithWatermark, watermark.Length, bb.Length);

                bb = bytesWithWatermark;
            }

            // TODO: See if there's a way to further reduce potential attack vectors.
            string sk = _keys.DecryptPrivateKey();

            byte[] dsk = B58C.Decode(sk, Prefix.edsk);
            byte[] hash = Hashing.Generic(32 * 8, bb);
            byte[] sig = Ed25519.Sign(hash, dsk);
            string edsignature = B58C.Encode(sig, Prefix.edsig);
            string sbytes = bytes + sig.ToHexString();

            return new SignedMessage
            {
                Bytes = bb,
                SignedHash = sig,
                EncodedSignature = edsignature,
                SignedBytes = sbytes
            };
        }

       
        /// <summary>
        /// Verify a signed message with public key.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="sig">Signed message to verify.</param>
        /// <param name="pk">Public key used for verification.</param>
        /// <returns></returns>
        public override bool Verify(string bytes, byte[] sig, string pk)
        {
            byte[] bb = bytes.HexToByteArray();

            byte[] edpk = B58C.Decode(pk, Prefix.edpk);

            return Ed25519.Verify(sig, bb, edpk);
        }
    }
}