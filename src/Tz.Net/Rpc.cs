using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using TezosSharp.Extensions;
using TezosSharp.Internal;
using TezosSharp.Internal.OperationResultHandlers;
using TezosSharp.Security;

namespace TezosSharp
{
    public class Rpc
    {
        private readonly HttpClient _client;
        private static readonly Dictionary<string, IOperationHandler> OpHandlers = new Dictionary<string, IOperationHandler>
        {
            {  Operations.ActivateAccount, new ActivateAccountOperationHandler() },
            {  Operations.Transaction, new TransactionOperationHandler() },
            {  Operations.Reveal, new RevealOperationHandler() }
        };

        private readonly string _provider;
        private readonly string _chain;

        private readonly IHdWallet _wallet;

        public Rpc(HttpClient client, string provider, IHdWallet wallet, string chain = "main")
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider), "Provider required");
            _chain = chain ?? throw new ArgumentNullException(nameof(chain), "Chain required");
            _wallet = wallet;
            _client = client;
        }

        #region Rpc methods

        public async Task<JObject> Describe(CancellationToken stoppingToken)
        {
            // There is curently a weird situation in alpha where the RPC will not honor any request without a recurse=true arg. // 8 Aug 2018
            return await QueryJ<JObject>("describe?recurse=true", stoppingToken);
        }

        public async Task<JObject> GetMempool(CancellationToken stoppingToken)
        {
            return await QueryJ<JObject>($"chains/{_chain}/mempool/pending_operations", stoppingToken);
        }

        public async Task<JObject> GetHead(CancellationToken stoppingToken)
        {
            return await QueryJ<JObject>($"chains/{_chain}/blocks/head", stoppingToken);
        }

        public async Task<JObject> GetHeader(CancellationToken stoppingToken)
        {
            return await QueryJ<JObject>($"chains/{_chain}/blocks/head/header", stoppingToken);
        }

        public async Task<JObject> GetBlockById(ulong id, CancellationToken stoppingToken)
        {
            return await QueryJ<JObject>($"chains/{_chain}/blocks/{id}", stoppingToken);
        }

        public async Task<JArray> GetOperationsByBlockId(ulong id, CancellationToken stoppingToken)
        {
            return await QueryJ<JArray>($"chains/{_chain}/blocks/{id}/operations", stoppingToken);
        }

        public async Task<JArray> GetTransactionHashListByBlockId(ulong id, CancellationToken stoppingToken)
        {
            return await QueryJ<JArray>($"chains/{_chain}/blocks/{id}/operation_hashes/3", stoppingToken);
        }

        public async Task<JArray> GetTransactionsListByBlockId(ulong id, CancellationToken stoppingToken)
        {
            return await QueryJ<JArray>($"chains/{_chain}/blocks/{id}/operations/3", stoppingToken);
        }

        public async Task<JObject> GetAccountForBlock(string blockHash, string address, CancellationToken stoppingToken)
        {
            return await QueryJ<JObject>($"chains/{_chain}/blocks/{blockHash}/context/contracts/{address}", stoppingToken);
        }

        public async Task<BigFloat> GetBalance(string address, CancellationToken stoppingToken)
        {
            JToken response = await QueryJ($"chains/{_chain}/blocks/head/context/contracts/{address}/balance", stoppingToken);

            return new BigFloat(response.ToString());
        }

        public async Task<JObject> GetNetworkStat(CancellationToken stoppingToken)
        {
            return await QueryJ<JObject>("network/stat", stoppingToken);
        }

        public async Task<ulong> GetCounter(string address, CancellationToken stoppingToken)
        {
            JToken counter = await QueryJ($"chains/{_chain}/blocks/head/context/contracts/{address}/counter", stoppingToken);
            return ulong.Parse(counter.ToString());
        }

        public async Task<JToken> GetManagerKey(string address, CancellationToken stoppingToken)
        {
            return await QueryJ($"chains/{_chain}/blocks/head/context/contracts/{address}/manager_key", stoppingToken);
        }
        #endregion

        // TODO: 
        // public async Task<ActivateAccountOperationResult> Activate(string address, string secret)
        // {
        //     JObject activateOp = new JObject();

        //     activateOp["kind"] = Operations.ActivateAccount;
        //     activateOp["pkh"] = address;
        //     activateOp["secret"] = secret;

        //     List<OperationResult> sendResults = await SendOperations(activateOp, null);

        //     return sendResults.LastOrDefault() as ActivateAccountOperationResult;
        // }

        public async Task<SendTransactionOperationResult> SendTransaction(uint index, string to, BigFloat amountTez, BigFloat feeMTez, CancellationToken stoppingToken, BigFloat gasLimit = null, BigFloat storageLimit = null, JObject param = null)
        {
            if (_wallet == null) throw new NullReferenceException(nameof(_wallet));

            Account hdWalletAccount = _wallet.GetAccount(index);
            string fromAddress = hdWalletAccount.WalletAddress.Address;

            gasLimit ??= 200;
            storageLimit ??= 0;

            JObject head = await GetHeader(stoppingToken);
            JObject account = await GetAccountForBlock(head["hash"].ToString(), fromAddress, stoppingToken);

            ulong counter = ulong.Parse(account["counter"].ToString());

            JArray operations = new JArray();

            JToken managerKey = await GetManagerKey(fromAddress, stoppingToken);

            string gas = gasLimit.ToString();
            string storage = storageLimit.ToString();

            if (string.IsNullOrEmpty(managerKey.ToString()))
            {
                JObject revealOp = new JObject();
                operations.AddFirst(revealOp);

                revealOp["kind"] = "reveal";
                revealOp["fee"] = "0";
                revealOp["public_key"] = hdWalletAccount.DecryptedPublicKey;
                revealOp["source"] = fromAddress;
                revealOp["storage_limit"] = storage;
                revealOp["gas_limit"] = gas;
                revealOp["counter"] = (++counter).ToString();
            }

            JObject transaction = new JObject();
            transaction["kind"] = Operations.Transaction;
            transaction["source"] = fromAddress;
            transaction["fee"] = feeMTez.ToString();
            transaction["counter"] = (++counter).ToString();
            transaction["gas_limit"] = gas;
            transaction["storage_limit"] = storage;
            // Convert to microtez, truncate at 6 digits, round up
            transaction["amount"] = new BigFloat(amountTez.ToMicroTez().ToString(6)).Round().ToString();
            transaction["destination"] = to;

            if (param != null)
                transaction["parameters"] = param;

            //else
            //{
            //    JObject parameters = new JObject();
            //    transaction["parameters"] = parameters;
            //    parameters["prim"] = "Pair";
            //    parameters["args"] = new JArray(new JProperty("string", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture))); // No args for this contract.
            //}

            operations.Add(transaction);

            List<OperationResult> sendResults = await SendOperations(hdWalletAccount, stoppingToken, operations, head);

            return sendResults.LastOrDefault() as SendTransactionOperationResult;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="paymentTransactions"></param>
        /// <param name="storageLimit"></param>
        /// <param name="gasLimit"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public async Task<List<SendTransactionOperationResult>> SendMultipleTransaction(uint index, List<SendTransactionModel> paymentTransactions, BigFloat gasLimit, BigFloat storageLimit, CancellationToken stoppingToken)
        {
            if (_wallet == null) throw new NullReferenceException(nameof(_wallet));

            gasLimit ??= 200;
            storageLimit ??= 0;

            Account hdWalletAccount = _wallet.GetAccount(index);
            string fromAddress = hdWalletAccount.WalletAddress.Address;

            JObject head = await GetHeader(stoppingToken);
            JObject account = await GetAccountForBlock(head["hash"].ToString(), fromAddress, stoppingToken);
            ulong counter = ulong.Parse(account["counter"].ToString());

            JArray operations = new JArray();

            JToken managerKey = await GetManagerKey(fromAddress, stoppingToken);

            if (string.IsNullOrEmpty(managerKey.ToString()))
            {
                JObject revealOp = new JObject();
                operations.AddFirst(revealOp);

                revealOp["kind"] = "reveal";
                revealOp["fee"] = "0";
                revealOp["public_key"] = hdWalletAccount.DecryptedPublicKey;
                revealOp["source"] = fromAddress;
                revealOp["storage_limit"] = storageLimit.ToString();
                revealOp["gas_limit"] = gasLimit.ToString();
                revealOp["counter"] = (++counter).ToString();
            }

            foreach (var paymentTransaction in paymentTransactions)
            {
                paymentTransaction.GasLimit ??= 200;
                paymentTransaction.StorageLimit ??= 0;

                string gas = paymentTransaction.GasLimit.ToString();
                string storage = paymentTransaction.StorageLimit.ToString();

                JObject transaction = new JObject
                {
                    ["kind"] = Operations.Transaction,
                    ["source"] = fromAddress,
                    ["fee"] = paymentTransaction.FeeMTez.ToString(),
                    ["counter"] = (++counter).ToString(),
                    ["gas_limit"] = gas,
                    ["storage_limit"] = storage,
                    // Convert to microtez, truncate at 6 digits, round up
                    ["amount"] = new BigFloat(paymentTransaction.AmountTez.ToMicroTez().ToString(6)).Round().ToString(),
                    ["destination"] = paymentTransaction.To
                };

                operations.Add(transaction);
            }

            List<OperationResult> sendResults = await SendOperations(hdWalletAccount, stoppingToken, operations, head);

            return sendResults?.Select(x => x as SendTransactionOperationResult).ToList();
        }

        private async Task<List<OperationResult>> SendOperations(Account hdWalletAccount, CancellationToken stoppingToken, JToken operations, JObject head = null)
        {
            if (head == null)
            {
                head = await GetHeader(stoppingToken);
            }

            JArray arrOps = operations as JArray;
            if (arrOps == null)
            {
                arrOps = new JArray(operations);
            }

            JToken forgedOpGroup = await ForgeOperations(head, arrOps, stoppingToken);

            SignedMessage signedOpGroup = hdWalletAccount.Sign(forgedOpGroup.ToString(), Watermark.Generic);

            List<OperationResult> opResults = await PreApplyOperations(head, arrOps, signedOpGroup.EncodedSignature, stoppingToken);

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

            JToken injectedOperation = await InjectOperations(signedOpGroup.SignedBytes, stoppingToken);
            opHash = injectedOperation.ToString();
            opResults.LastOrDefault().Data["op_hash"] = opHash;

            return opResults;
        }

        private async Task<JToken> ForgeOperations(JObject blockHead, JArray operations, CancellationToken stoppingToken)
        {
            JObject contents = new JObject();

            contents["branch"] = blockHead["hash"];
            contents["contents"] = operations;

            return await QueryJ($"chains/{_chain}/blocks/head/helpers/forge/operations", stoppingToken, contents);
        }

        private async Task<List<OperationResult>> PreApplyOperations(JObject head, JArray operations, string signature, CancellationToken stoppingToken)
        {
            JArray payload = new JArray();
            JObject jsonObject = new JObject();
            payload.Add(jsonObject);

            jsonObject["protocol"] = head["protocol"];
            jsonObject["branch"] = head["hash"];
            jsonObject["contents"] = operations;
            jsonObject["signature"] = signature;

            JArray result = await QueryJ<JArray>($"chains/{_chain}/blocks/head/helpers/preapply/operations", stoppingToken, payload);

            return ParseApplyOperationsResult(result);
        }

        private async Task<JToken> InjectOperations(string signedBytes, CancellationToken stoppingToken)
        {
            return await QueryJ<JValue>($"injection/operation?chain={_chain}", stoppingToken, new JRaw($"\"{signedBytes}\""));
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

        private async Task<JToken> QueryJ(string ep, CancellationToken stoppingToken, JToken data = null)
        {
            return await QueryJ<JToken>(ep, stoppingToken, data);
        }

        private async Task<JType> QueryJ<JType>(string ep, CancellationToken stoppingToken, JToken data = null)
            where JType : JToken
        {
            return (JType)JToken.Parse(await Query(ep, stoppingToken, data?.ToString(Formatting.None)));
        }

        private async Task<string> Query(string ep, CancellationToken stoppingToken, object data = null)
        {
            bool get = data == null;

            HttpRequestMessage request = new HttpRequestMessage(get ? HttpMethod.Get : HttpMethod.Post, $"{_provider}/{ep}")
            {
                Version = HttpVersion.Version11, // Tezos node does not like the default v2.
            };

            if (!get)
            {
                request.Content = new StringContent(data.ToString());
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            HttpResponseMessage response = await _client.SendAsync(request, stoppingToken);

            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                response.Content.Headers.TryGetValues("Content-Type", out IEnumerable<string> contentTypes);
                return contentTypes.Any(c => c != null && c.StartsWith("text/plain"))
                    ? JsonConvert.SerializeObject(responseBody)
                    : responseBody;
            }

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