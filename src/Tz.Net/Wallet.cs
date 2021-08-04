﻿using Chaos.NaCl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TezosSharp.Extensions;
using TezosSharp.Security;
using TezosSharp.Internal;

namespace TezosSharp
{
    public class Wallet
    {
        /// <summary>
        /// Create wallet in a non-deterministic fashion.
        /// </summary>
        public Wallet()
        {
            byte[] seed = new byte[32];

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                // Fill the array with a random seed
                rng.GetBytes(seed);
            }

            FromSeed(seed);
        }

        /// <summary>
        /// Create wallet in a deterministic fashion. Mnemonic is randomly generated and accessible.
        /// </summary>
        /// <param name="passphrase">Passphrase to generate seed</param>
        public Wallet(string passphrase)
        {
            string mnemonic = CryptoBase.GenerateMnemonic();
            FromMnemonic(mnemonic, passphrase);
        }

        /// <summary>
        /// Create wallet from mnemonic, email, and password with deterministic seed.
        /// </summary>
        /// <param name="mnemonic">15 word mnemonic</param>
        /// <param name="email">Email address</param>
        /// <param name="password">Wallet password</param>
        public Wallet(string mnemonic, string email, string password)
            : this(mnemonic, email + password)
        { }

        /// <summary>
        /// Create wallet from mnemonic and passphrase with deterministic seed.
        /// </summary>
        /// <param name="mnemonic">15 word mnemonic</param>
        /// <param name="passphrase">Passphrase to generate seed</param>
        public Wallet(string mnemonic, string passphrase)
        {
            FromMnemonic(mnemonic, passphrase);
        }

        /// <summary>
        /// Create wallet from mnemonic words, email, and password with deterministic seed.
        /// </summary>
        /// <param name="mnemonic">15 word mnemonic</param>
        /// <param name="email">Email address</param>
        /// <param name="password">Wallet password</param>
        public Wallet(IEnumerable<string> words, string email, string password)
            : this(words, email + password)
        { }

        /// <summary>
        /// Create wallet from mnemonic and passphrase with deterministic seed.
        /// </summary>
        /// <param name="mnemonic">15 word mnemonic</param>
        /// <param name="passphrase">Usually email + password.</param>
        public Wallet(IEnumerable<string> words, string passphrase)
        {
            FromMnemonic(words, passphrase);
        }

        /// <summary>
        /// Generate wallet from seed.
        /// </summary>
        /// <param name="seed">Seed to generate keys with.</param>
        public Wallet(byte[] seed)
        {
            FromSeed(seed);
        }

        private void FromMnemonic(string mnemonic, string passphrase)
        {
            if (string.IsNullOrEmpty(mnemonic))
            {
                throw new ArgumentException("Mnemonic must be 15 words", nameof(mnemonic));
            }

            string[] words = mnemonic.Split(' ');

            if (words.Length != 15)
            {
                throw new ArgumentException("Mnemonic must be 15 words", nameof(mnemonic));
            }

            FromMnemonic(words, passphrase);
        }

        private void FromMnemonic(IEnumerable<string> words, string passphrase)
        {
            if (words?.Any() == false)
            {
                throw new ArgumentException("Words required", nameof(words));
            }

            if (string.IsNullOrWhiteSpace(passphrase))
            {
                throw new ArgumentException("Passphrase required", nameof(passphrase));
            }

            Mnemonic = words;

            Passphrase = passphrase;

            Bip39 bip39 = new Bip39();

            byte[] seed = bip39.ToSeed(words, passphrase).CopyOfRange(0, 32);

            FromSeed(seed);
        }

        private void FromSeed(byte[] seed)
        {
            if (seed?.Any() == false)
            {
                throw new ArgumentException("Seed required", nameof(seed));
            }

            Seed = seed;

            // Create key pair (PK and SK) from a seed.
            byte[] pk = new byte[32];

            byte[] sk = new byte[64];

            try
            {
                Ed25519.KeyPairFromSeed(out pk, out sk, Seed);

                // Also creates the tz1 PK hash.
                Keys = new Keys(pk, sk);
            }
            finally
            {
                // Keys should clear this, but clear again for good measure.
                Array.Clear(pk, 0, pk.Length);
                Array.Clear(sk, 0, sk.Length);
            }
        }

        /// <summary>
        /// The seed used to generate the wallet keys.
        /// </summary>
        public byte[] Seed { get; internal set; }

        /// <summary>
        /// The mnemonic used to generate the seed.
        /// </summary>
        public IEnumerable<string> Mnemonic { get; internal set; }

        /// <summary>
        /// The passphrase used to generate the seed.
        /// </summary>
        public string Passphrase { get; internal set; }

        /// <summary>
        /// The encrypted public/private keys.
        /// </summary>
        public Keys Keys { get; internal set; }

        /// <summary>
        /// This wallet's public hash.
        /// </summary>
        public string PublicHash => Keys?.PublicHash;

        public static Wallet FromStringSeed(string seed, byte[] prefix = null)
        {
            return new Wallet(B58C.Decode(seed, prefix ?? Prefix.edsk));
        }
    }
}