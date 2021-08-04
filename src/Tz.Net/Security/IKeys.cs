using System;
using System.Runtime.InteropServices;
using System.Security;
using TezosSharp.Internal;

namespace TezosSharp.Security
{
    public interface IKeys
    {
        string DecryptPublicKey();
    }
}