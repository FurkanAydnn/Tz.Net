using Newtonsoft.Json.Linq;

namespace TezosSharp
{
    public class OperationResult
    {
        public OperationResult()
        { }

        public OperationResult(JToken data)
        {
            Data = data;
        }

        public JToken Data { get; internal set; }
        public bool Succeeded { get; internal set; }
    }
}