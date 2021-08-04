using Newtonsoft.Json.Linq;

namespace TezosSharp.Internal.OperationResultHandlers
{
    internal interface IOperationHandler
    {
        string HandlesOperation { get; }
        OperationResult ParseApplyOperationsResult(JToken appliedOp);
    }
}