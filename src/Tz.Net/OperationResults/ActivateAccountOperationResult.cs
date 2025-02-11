﻿using Newtonsoft.Json.Linq;
using System.Numerics;

namespace TezosSharp
{
    public class ActivateAccountOperationResult : OperationResult
    {
        public ActivateAccountOperationResult()
        { }

        public ActivateAccountOperationResult(JToken data)
            : base(data)
        { }

        public BigFloat Change { get; internal set; }
    }
}