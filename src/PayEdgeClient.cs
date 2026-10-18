using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ezacloud.Sdk
{
    public class PayEdgeClient
    {
        private readonly EzaClient _client;

        internal PayEdgeClient(EzaClient client)
        {
            _client = client;
        }

        public async Task<Transaction> StkPushAsync(StkPushRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.AmountKes < 1) throw new ArgumentException("AmountKes must be at least 1 KES", nameof(request));
            if (string.IsNullOrEmpty(request.Phone)) throw new ArgumentException("Phone is required", nameof(request));
            if (string.IsNullOrEmpty(request.AccountReference)) throw new ArgumentException("AccountReference is required", nameof(request));

            var url = $"{_client.PayEdgeUrl}/v1/stk-push";
            return await _client.SendJsonAsync<StkPushRequest, Transaction>(
                HttpMethod.Post,
                url,
                request,
                service: ServiceType.PayEdge,
                idempotencyKey: request.IdempotencyKey,
                cancellationToken: cancellationToken);
        }

        public async Task<Transaction> GetTransactionAsync(string reference, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(reference)) throw new ArgumentNullException(nameof(reference));

            var escaped = Uri.EscapeDataString(reference);
            var url = $"{_client.PayEdgeUrl}/v1/transactions/{escaped}";
            return await _client.SendAsync<Transaction>(
                HttpMethod.Get,
                url,
                service: ServiceType.PayEdge,
                cancellationToken: cancellationToken);
        }
    }
}
