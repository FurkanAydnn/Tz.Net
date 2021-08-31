using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Threading.Tasks;
using TezosSharp.Extensions;
using TezosSharp.Internal;
using TezosSharp.Internal.OperationResultHandlers;
using TezosSharp.Security;

namespace TezosSharp
{
    public class RpcWCrypto
    {
        private static readonly HttpClient _client = new HttpClient();
        private static readonly Dictionary<string, IOperationHandler> OpHandlers = new Dictionary<string, IOperationHandler>
        {
            {  Operations.ActivateAccount, new ActivateAccountOperationHandler() },
            {  Operations.Transaction, new TransactionOperationHandler() },
            {  Operations.Reveal, new RevealOperationHandler() }
        };

        private readonly string _provider;
        private readonly string _chain;
        private readonly CryptoBase _crypto;

        public RpcWCrypto(string provider, CryptoBase crypto, string chain = "main")
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider), "Provider required");
            _chain = chain ?? throw new ArgumentNullException(nameof(chain), "Chain required");
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        }

        #region Rpc methods

        public async Task<JObject> Describe()
        {
            // There is curently a weird situation in alpha where the RPC will not honor any request without a recurse=true arg. // 8 Aug 2018
            return await QueryJ<JObject>("describe?recurse=true");
        }

        public async Task<JObject> GetMempool()
        {
            return await QueryJ<JObject>($"chains/{_chain}/mempool/pending_operations");
        }

        public async Task<JObject> GetHead()
        {
            return await QueryJ<JObject>($"chains/{_chain}/blocks/head");
        }

        public async Task<JObject> GetHeader()
        {
            return await QueryJ<JObject>($"chains/{_chain}/blocks/head/header");
        }

        public async Task<JObject> GetBlockById(ulong id)
        {
            return await QueryJ<JObject>($"chains/{_chain}/blocks/{id}");
        }

        public async Task<JArray> GetOperationsByBlockId(ulong id)
        {
            return await QueryJ<JArray>($"chains/{_chain}/blocks/{id}/operations");
        }

        public async Task<JArray> GetTransactionHashListByBlockId(ulong id)
        {
            return await QueryJ<JArray>($"chains/{_chain}/blocks/{id}/operation_hashes/3");
        }

        public async Task<JArray> GetTransactionsListByBlockId(ulong id)
        {
            return await QueryJ<JArray>($"chains/{_chain}/blocks/{id}/operations/3");
        }

        public async Task<JObject> GetAccountForBlock(string blockHash, string address)
        {
            return await QueryJ<JObject>($"chains/{_chain}/blocks/{blockHash}/context/contracts/{address}");
        }

        public async Task<BigFloat> GetBalance(string address)
        {
            JToken response = await QueryJ($"chains/{_chain}/blocks/head/context/contracts/{address}/balance");

            return new BigFloat(response.ToString());
        }

        public async Task<JObject> GetNetworkStat()
        {
            return await QueryJ<JObject>("network/stat");
        }

        public async Task<int> GetCounter(string address)
        {
            JToken counter = await QueryJ($"chains/{_chain}/blocks/head/context/contracts/{address}/counter");
            return Convert.ToInt32(counter.ToString());
        }

        public async Task<JToken> GetManagerKey(string address)
        {
            return await QueryJ($"chains/{_chain}/blocks/head/context/contracts/{address}/manager_key");
        }
        #endregion

        public async Task<ActivateAccountOperationResult> Activate(string address, string secret)
        {
            JObject activateOp = new JObject();

            activateOp["kind"] = Operations.ActivateAccount;
            activateOp["pkh"] = address;
            activateOp["secret"] = secret;

            List<OperationResult> sendResults = await SendOperations(activateOp, null);

            return sendResults.LastOrDefault() as ActivateAccountOperationResult;
        }

        public async Task<SendTransactionOperationResult> SendTransaction(string from, string to, BigFloat amount, BigFloat fee, BigFloat gasLimit = null, BigFloat storageLimit = null, JObject param = null)
        {
            gasLimit ??= 200;
            storageLimit ??= 0;

            JObject head = await GetHeader();
            JObject account = await GetAccountForBlock(head["hash"].ToString(), from);

            int counter = int.Parse(account["counter"].ToString());

            JArray operations = new JArray();

            JToken managerKey = await GetManagerKey(from);

            string gas = gasLimit.ToString();
            string storage = storageLimit.ToString();

            if (string.IsNullOrEmpty(managerKey.ToString()))
            {
                JObject revealOp = new JObject();
                operations.AddFirst(revealOp);

                revealOp["kind"] = "reveal";
                revealOp["fee"] = "0";
                revealOp["public_key"] = _crypto.DecryptPublicKey();
                revealOp["source"] = from;
                revealOp["storage_limit"] = storage;
                revealOp["gas_limit"] = gas;
                revealOp["counter"] = (++counter).ToString();
            }

            JObject transaction = new JObject();
            transaction["kind"] = Operations.Transaction;
            transaction["source"] = from;
            transaction["fee"] = fee.ToString();
            transaction["counter"] = (++counter).ToString();
            transaction["gas_limit"] = gas;
            transaction["storage_limit"] = storage;
            // Convert to microtez, truncate at 6 digits, round up
            transaction["amount"] = new BigFloat(amount.ToMicroTez().ToString(6)).Round().ToString();
            transaction["destination"] = to;

            if (param != null)
                transaction["parameters"] = param;

            operations.Add(transaction);

            List<OperationResult> sendResults = await SendOperations(operations, head);

            return sendResults.LastOrDefault() as SendTransactionOperationResult;
        }

        private async Task<List<OperationResult>> SendOperations(JToken operations, JObject head = null)
        {
            if (head == null)
            {
                head = await GetHeader();
            }

            JArray arrOps = operations as JArray;
            if (arrOps == null)
            {
                arrOps = new JArray(operations);
            }

            JToken forgedOpGroup = await ForgeOperations(head, arrOps);

            SignedMessage signedOpGroup = _crypto.Sign(forgedOpGroup.ToString(), Watermark.Generic);

            List<OperationResult> opResults = await PreApplyOperations(head, arrOps, signedOpGroup.EncodedSignature);

            //deleting too big contractCode from response
            foreach (var opResult in opResults)
            {
                if (opResult.Data?["metadata"]?["operation_result"]?["status"]?.ToString() == "failed")
                {
                    foreach (JObject error in opResult.Data["metadata"]["operation_result"]["errors"])
                    {
                        if (error["contractCode"]?.ToString().Length > 1000)
                            error["contractCode"] = "";
                    }
                }
            }

            string opHash = "";

            if (!opResults.All(op => op.Succeeded)) return opResults;

            JToken injectedOperation = await InjectOperations(signedOpGroup.SignedBytes);
            opHash = injectedOperation.ToString();
            opResults.LastOrDefault().Data["op_hash"] = opHash;

            return opResults;
        }

        private async Task<JToken> ForgeOperations(JObject blockHead, JArray operations)
        {
            JObject contents = new JObject();

            contents["branch"] = blockHead["hash"];
            contents["contents"] = operations;

            return await QueryJ($"chains/{_chain}/blocks/head/helpers/forge/operations", contents);
        }

        private async Task<List<OperationResult>> PreApplyOperations(JObject head, JArray operations, string signature)
        {
            JArray payload = new JArray();
            JObject jsonObject = new JObject();
            payload.Add(jsonObject);

            jsonObject["protocol"] = head["protocol"];
            jsonObject["branch"] = head["hash"];
            jsonObject["contents"] = operations;
            jsonObject["signature"] = signature;

            JArray result = await QueryJ<JArray>($"chains/{_chain}/blocks/head/helpers/preapply/operations", payload);

            return ParseApplyOperationsResult(result);
        }

        private async Task<JToken> InjectOperations(string signedBytes)
        {
            return await QueryJ<JValue>($"injection/operation?chain={_chain}", new JRaw($"\"{signedBytes}\""));
        }

        private List<OperationResult> ParseApplyOperationsResult(JArray appliedOps)
        {
            List<OperationResult> operationResults = new List<OperationResult>();

            if (!(appliedOps?.Count > 0)) return operationResults;

            if (!(appliedOps.First["contents"] is JArray contents)) return operationResults;

            foreach (JToken content in contents)
            {
                string kind = content["kind"].ToString();

                if (!string.IsNullOrWhiteSpace(kind))
                {
                    IOperationHandler handler = OpHandlers[kind];

                    if (handler != null)
                    {
                        OperationResult opResult = handler.ParseApplyOperationsResult(content);

                        if (opResult != null)
                        {
                            operationResults.Add(opResult);
                        }
                    }
                }
            }

            return operationResults;
        }

        private async Task<JToken> QueryJ(string ep, JToken data = null)
        {
            return await QueryJ<JToken>(ep, data);
        }

        private async Task<JType> QueryJ<JType>(string ep, JToken data = null)
            where JType : JToken
        {
            return (JType)JToken.Parse(await Query(ep, data?.ToString(Formatting.None)));
        }

        private async Task<string> Query(string ep, object data = null)
        {
            bool get = data == null;

            HttpRequestMessage request = new HttpRequestMessage(get ? HttpMethod.Get : HttpMethod.Post, $"{_provider}/{ep}")
            {
                Version = HttpVersion.Version11 // Tezos node does not like the default v2.
            };

            if (!get)
            {
                request.Content = new StringContent(data.ToString());
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            HttpResponseMessage response = await _client.SendAsync(request);

            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode) return responseBody;

            // If failed, throw the body as the exception message.
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                throw new HttpRequestException(responseBody);
            }
            else
            {
                // Otherwise, throw a generic exception.
                response.EnsureSuccessStatusCode();
            }

            return responseBody;
        }
    }
}