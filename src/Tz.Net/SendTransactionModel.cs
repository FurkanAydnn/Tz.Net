using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace TezosSharp
{
    public class SendTransactionModel
    {
        public string To { get; set; }
        public BigFloat AmountTez { get; set; }
        public BigFloat FeeMTez { get; set; }
        public BigFloat GasLimit { get; set; }
        public BigFloat StorageLimit { get; set; }
    }
}
