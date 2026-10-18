using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ezacloud.Sdk
{
    public static class Webhook
    {
        public const int DefaultToleranceSeconds = 300;

        public static WebhookEvent Verify(
            string rawBody,
            string? signatureHeader,
            string? secret,
            int toleranceSeconds = DefaultToleranceSeconds,
            DateTimeOffset? now = null)
        {
            if (string.IsNullOrEmpty(rawBody))
                throw new SignatureException("raw body cannot be empty");
            if (string.IsNullOrEmpty(signatureHeader))
                throw new SignatureException("signature header cannot be empty");
            if (string.IsNullOrEmpty(secret))
                throw new SignatureException("secret cannot be empty");

            long? timestamp = null;
            string? signature = null;

            foreach (var part in signatureHeader.Split(','))
            {
                var kv = part.Trim().Split(new[] { '=' }, 2);
                if (kv.Length == 2)
                {
                    if (kv[0] == "t" && long.TryParse(kv[1], out var t))
                        timestamp = t;
                    else if (kv[0] == "v1")
                        signature = kv[1];
                }
            }

            if (!timestamp.HasValue || string.IsNullOrEmpty(signature))
                throw new SignatureException("malformed signature header: missing timestamp or v1 signature");

            var currentTime = now ?? DateTimeOffset.UtcNow;
            var currentTimestamp = currentTime.ToUnixTimeSeconds();
            if (Math.Abs(currentTimestamp - timestamp.Value) > toleranceSeconds)
                throw new SignatureException($"timestamp outside tolerance threshold ({toleranceSeconds}s)");

            var signedPayload = $"t.{timestamp.Value}.{rawBody}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
            var computedHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex),
                Encoding.UTF8.GetBytes(signature)))
            {
                throw new SignatureException("signature hash mismatch");
            }

            var evt = JsonSerializer.Deserialize<WebhookEvent>(rawBody);
            if (evt == null)
                throw new SignatureException("failed to parse event json");

            return evt;
        }
    }
}
