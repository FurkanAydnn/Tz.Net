using NBitcoin;
using System;
using TezosSharp.Extensions;
using TezosSharp.Internal;

namespace TezosSharp.Security
{
    public class CryptoSecp256k1 : CryptoBase
    {
        KeysSecp256k1 _keys;
        
        public CryptoSecp256k1(KeysSecp256k1 keys)
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

            byte[] hash = Hashing.Generic(32 * 8, bb);
            NBitcoin.Crypto.ECDSASignature signature = _keys.BitcoinPrivateKey.Sign(new uint256(hash));
            TezosECDSASignature cosmosSignature = new TezosECDSASignature(signature);
            byte[] sig = cosmosSignature.ByteSignature;

            string edsignature = B58C.Encode(sig, Prefix.spsig1);
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
        // TODO: Test this 
        public override bool Verify(string bytes, byte[] sig, string pk)
        {
            byte[] bb = bytes.HexToByteArray();

            byte[] sppk = B58C.Decode(pk, Prefix.sppk);

            return new PubKey(sppk).Verify(new uint256(bb), sig);
        }
    }
}