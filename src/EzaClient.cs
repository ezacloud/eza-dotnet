using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ezacloud.Sdk
{
    public enum ServiceType
    {
        PayEdge,
        Email
    }

    public class EzaClient : IDisposable
    {
        public const string DefaultEmailUrl = "https://email.eza.co.ke";
        public const string DefaultPayEdgeUrl = "https://payedge.eza.co.ke";

        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;

        public string? ApiKey { get; }
        public string? EmailKey { get; }
        public string EmailUrl { get; }
        public string PayEdgeUrl { get; }
        public int MaxRetries { get; }

        public PayEdgeClient PayEdge { get; }
        public EmailsClient Emails { get; }

        public EzaClient(
            string? apiKey = null,
            string? emailKey = null,
            string? emailUrl = null,
            string? payedgeUrl = null,
            int maxRetries = 2,
            HttpClient? httpClient = null)
        {
            ApiKey = apiKey ?? Environment.GetEnvironmentVariable("EZA_API_KEY");
            EmailKey = emailKey ?? Environment.GetEnvironmentVariable("EZA_EMAIL_KEY") ?? ApiKey;
            EmailUrl = (emailUrl ?? Environment.GetEnvironmentVariable("EZA_EMAIL_URL") ?? DefaultEmailUrl).TrimEnd('/');
            PayEdgeUrl = (payedgeUrl ?? Environment.GetEnvironmentVariable("EZA_PAYEDGE_URL") ?? DefaultPayEdgeUrl).TrimEnd('/');
            MaxRetries = maxRetries;

            if (httpClient == null)
            {
                _httpClient = new HttpClient();
                _disposeHttpClient = true;
            }
            else
            {
                _httpClient = httpClient;
                _disposeHttpClient = false;
            }

            PayEdge = new PayEdgeClient(this);
            Emails = new EmailsClient(this);
        }

        internal async Task<TResponse> SendJsonAsync<TRequest, TResponse>(
            HttpMethod method,
            string url,
            TRequest body,
            ServiceType service,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(body);
            return await ExecuteRequestAsync<TResponse>(method, url, json, service, idempotencyKey, cancellationToken);
        }

        internal async Task<TResponse> SendAsync<TResponse>(
            HttpMethod method,
            string url,
            ServiceType service,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteRequestAsync<TResponse>(method, url, null, service, null, cancellationToken);
        }

        private async Task<TResponse> ExecuteRequestAsync<TResponse>(
            HttpMethod method,
            string url,
            string? jsonBody,
            ServiceType service,
            string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            var authKey = service == ServiceType.Email ? EmailKey : ApiKey;
            if (string.IsNullOrEmpty(authKey))
            {
                var serviceName = service == ServiceType.Email ? "Email" : "PayEdge";
                var envVar = service == ServiceType.Email ? "EZA_EMAIL_KEY or EZA_API_KEY" : "EZA_API_KEY";
                throw new ConfigurationException($"No API key provided for {serviceName}. Provide via constructor or set {envVar}.");
            }

            if (method == HttpMethod.Post && string.IsNullOrEmpty(idempotencyKey))
            {
                idempotencyKey = Guid.NewGuid().ToString();
            }

            var attempts = 0;
            while (true)
            {
                attempts++;
                using var request = new HttpRequestMessage(method, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("User-Agent", "eza-dotnet/0.2.0");

                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    request.Headers.Add("Idempotency-Key", idempotencyKey);
                }

                if (jsonBody != null)
                {
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellationToken);
                }
                catch (Exception) when (attempts <= MaxRetries)
                {
                    await Task.Delay((int)(100 * Math.Pow(2, attempts - 1)), cancellationToken);
                    continue;
                }

                var statusCode = (int)response.StatusCode;

                // Handle 429 Rate Limit (honor Retry-After)
                if (statusCode == 429 && attempts <= MaxRetries)
                {
                    var delaySeconds = 0.2 * Math.Pow(2, attempts - 1);
                    if (response.Headers.RetryAfter?.Delta.HasValue == true)
                    {
                        delaySeconds = Math.Max(response.Headers.RetryAfter.Delta.Value.TotalSeconds, 0.05);
                    }
                    response.Dispose();
                    await Task.Delay((int)(delaySeconds * 1000), cancellationToken);
                    continue;
                }

                // Handle 503/504 Service Unavailable
                if ((statusCode == 503 || statusCode == 504) && attempts <= MaxRetries)
                {
                    response.Dispose();
                    await Task.Delay((int)(100 * Math.Pow(2, attempts - 1)), cancellationToken);
                    continue;
                }

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (typeof(TResponse) == typeof(JsonElement))
                    {
                        var doc = JsonDocument.Parse(string.IsNullOrEmpty(responseBody) ? "{}" : responseBody);
                        return (TResponse)(object)doc.RootElement;
                    }
                    return JsonSerializer.Deserialize<TResponse>(responseBody)!;
                }

                // Error response
                string? errorCode = null;
                string? errorMessage = null;
                string? requestId = null;
                List<FieldError>? fields = null;

                if (response.Headers.TryGetValues("eza-request-id", out var reqIds))
                {
                    foreach (var id in reqIds) { requestId = id; break; }
                }

                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("code", out var codeProp)) errorCode = codeProp.GetString();
                    else if (root.TryGetProperty("name", out var nameProp)) errorCode = nameProp.GetString();

                    if (root.TryGetProperty("message", out var msgProp)) errorMessage = msgProp.GetString();
                    else if (root.TryGetProperty("detail", out var detailProp)) errorMessage = detailProp.GetString();

                    if (root.TryGetProperty("request_id", out var rProp)) requestId ??= rProp.GetString();

                    if (root.TryGetProperty("errors", out var errsProp) && errsProp.ValueKind == JsonValueKind.Array)
                    {
                        fields = JsonSerializer.Deserialize<List<FieldError>>(errsProp.GetRawText());
                    }
                    else if (root.TryGetProperty("fields", out var fProp) && fProp.ValueKind == JsonValueKind.Array)
                    {
                        fields = JsonSerializer.Deserialize<List<FieldError>>(fProp.GetRawText());
                    }
                }
                catch
                {
                    errorMessage = responseBody;
                }

                throw new EzaException(
                    errorMessage ?? $"HTTP {statusCode}",
                    statusCode,
                    errorCode,
                    requestId,
                    fields);
            }
        }

        public void Dispose()
        {
            if (_disposeHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}
