using System;
using System.Collections.Generic;
using TezosSharp.Security;

namespace TezosSharp
{
    
    public interface IDWallet
    {
        Keys GetAccount();
        Keys GetAccount(uint accountNumber);
        IEnumerable<WalletAddress> GetAddress(int from, int size);
    }

    public class DWallet : IDWallet
    {
        private readonly string _mnemonic;
        private readonly string _passphrase;

        public DWallet(string mnemonic, string passphrase)
        {
            if (string.IsNullOrEmpty(mnemonic)) throw new ArgumentNullException(nameof(mnemonic));

            _mnemonic = mnemonic;
            _passphrase = passphrase;
        }

        Keys IDWallet.GetAccount()
        {
            var account = new Wallet(_mnemonic, _passphrase);
            return account.Keys;
        }

        Keys IDWallet.GetAccount(uint accountNumber)
        {
            var account = new Wallet(_mnemonic, _passphrase + accountNumber);
            return account.Keys;
        }

        IEnumerable<WalletAddress> IDWallet.GetAddress(int from, int size)
        {
            for (int i = 0; i < size; i++)
            {
                var index = from + i;
                var account = new Wallet(_mnemonic, (_passphrase + index));

                WalletAddress address = new WalletAddress
                {
                    Address = account.PublicHash,
                    Index = (uint)index
                };

                yield return address;
            }
        }
    }
}
