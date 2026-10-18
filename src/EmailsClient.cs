using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ezacloud.Sdk
{
    public class EmailsClient
    {
        private readonly EzaClient _client;

        internal EmailsClient(EzaClient client)
        {
            _client = client;
        }

        public async Task<EmailResponse> SendAsync(SendEmailRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrEmpty(request.From)) throw new ArgumentException("From is required", nameof(request));
            if (request.To == null || request.To.Count == 0) throw new ArgumentException("To recipients are required", nameof(request));
            if (string.IsNullOrEmpty(request.Subject)) throw new ArgumentException("Subject is required", nameof(request));
            if (string.IsNullOrEmpty(request.Text) && string.IsNullOrEmpty(request.Html))
                throw new ArgumentException("Either Text or Html must be provided", nameof(request));

            var url = $"{_client.EmailUrl}/emails";
            return await _client.SendJsonAsync<SendEmailRequest, EmailResponse>(
                HttpMethod.Post,
                url,
                request,
                service: ServiceType.Email,
                idempotencyKey: idempotencyKey,
                cancellationToken: cancellationToken);
        }

        public async Task<JsonElement> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            var escaped = Uri.EscapeDataString(id);
            var url = $"{_client.EmailUrl}/emails/{escaped}";
            return await _client.SendAsync<JsonElement>(
                HttpMethod.Get,
                url,
                service: ServiceType.Email,
                cancellationToken: cancellationToken);
        }
    }
}
