using System;
using System.Collections.Generic;
using NBitcoin;
using TezosSharp.Internal;
using TezosSharp.Security;

namespace TezosSharp
{
    public class WalletAddress
    {
        public string Address { get; set; }
        public uint Index { get; set; }
    }

    public class Account 
    {
        public Key PrivateKey { get; set;}
        public PubKey PublicKey { get; set;}
        public string DecryptedPublicKey { get => this._key.DecryptPublicKey(); }
        public WalletAddress WalletAddress { get; set;}

        KeysSecp256k1 _key;
        CryptoSecp256k1 _crypto;

        public Account(uint accountNumber, ExtKey extKey)
        {
            this.PrivateKey = extKey.PrivateKey;
            this.PublicKey = extKey.PrivateKey.PubKey;
            
            _key = new KeysSecp256k1(this.PublicKey, this.PrivateKey);
            _crypto = new CryptoSecp256k1(_key);

            this.WalletAddress = new WalletAddress(){
                Address = _key.PublicHash,
                Index = accountNumber
            };
        }

        public SignedMessage Sign(string message, byte[] watermark)
        {
            return _crypto.Sign(message, watermark );
        }
    }

    public interface IHdWallet
    {
        /// <summary>
        /// Returns number of 'size' addresses, including 'from'
        /// </summary>
        /// <param name="from"></param>
        /// <param name="size"></param>
        /// <returns>List of Tezos wallet addresses</returns>
        IEnumerable<WalletAddress> GetAddress(uint from, int size);
        Account GetAccount(uint accountNumber);
    }

    public class HdWallet : IHdWallet
    {
        private readonly byte[] COSMOS_PREFIX = Prefix.tz2;
        private readonly string TEZOS_PATH =  "m/44'/1729'/0'/0/x";

        private ExtKey _masterKey;

        public static HdWallet FromMasterKey(string masterKey)
        {
            BitcoinExtKey bitcoinExtKey = new BitcoinExtKey(masterKey);
            return new HdWallet(bitcoinExtKey);
        }

        public static HdWallet FromMnemonic(string mnemonic)
        {
            Mnemonic mneumonic = new Mnemonic(mnemonic);
            return new HdWallet(mneumonic);
        }

        public static HdWallet FromMnemonic(string mnemonic, string passphrase)
        {
            Mnemonic mneumonic = new Mnemonic(mnemonic);
            return new HdWallet(mneumonic, passphrase);
        }

        HdWallet(BitcoinExtKey bitcoinExtKey)
        {
            _masterKey = bitcoinExtKey.ExtKey;
        }
        
        HdWallet(Mnemonic mneumonic)
        {
            byte[] seed = mneumonic.DeriveSeed("");
            _masterKey = new ExtKey(seed);
        }

        HdWallet(Mnemonic mneumonic, string passphrase)
        {
            byte[] seed = mneumonic.DeriveSeed(passphrase);
            _masterKey = new ExtKey(seed);
        }

        IEnumerable<WalletAddress> IHdWallet.GetAddress(uint from, int size)
        {
            for (uint i = 0; i < size; i++)
            {
                var index = from + i;
                var acc = ((IHdWallet)this).GetAccount(index);
                yield return acc.WalletAddress;
            }
        }

        Account IHdWallet.GetAccount(uint accountNumber)
        {
            var path = TEZOS_PATH.Replace("x", accountNumber.ToString()); 
            ExtKey key = _masterKey.Derive(new KeyPath(path));
            return new Account(accountNumber, key);
        }
    }
}