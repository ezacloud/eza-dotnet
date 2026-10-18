using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ezacloud.Sdk;

namespace Ezacloud.Sdk.Tests
{
    public class ConformanceTests
    {
        // This test suite maps 1:1 with sdks/conformance/fixtures.json
        public static void RunAll()
        {
            TestWebhookConformance();
            TestWebhookInvalidCases();
            Console.WriteLine("All .NET SDK conformance tests passed.");
        }

        private static void TestWebhookConformance()
        {
            var secret = "whsec_test_0123456789abcdef";
            var timestamp = 1790538762L;
            var header = "t=1790538762,v1=5ed06d16182620e9aee7fd9d3b02a369038ccbdd656de90a45b004c9fc51c52b";
            var body = "{\"id\":\"evt_01\",\"type\":\"payment.completed\",\"livemode\":false,\"data\":{\"amount_kes\":1500}}";

            var now = DateTimeOffset.FromUnixTimeSeconds(timestamp + 30);
            var evt = Webhook.Verify(body, header, secret, toleranceSeconds: 300, now: now);

            if (evt.Id != "evt_01") throw new Exception($"Expected evt_01, got {evt.Id}");
            if (evt.Type != "payment.completed") throw new Exception($"Expected payment.completed, got {evt.Type}");
        }

        private static void TestWebhookInvalidCases()
        {
            var validSecret = "whsec_test_0123456789abcdef";
            var validHeader = "t=1790538762,v1=5ed06d16182620e9aee7fd9d3b02a369038ccbdd656de90a45b004c9fc51c52b";
            var validBody = "{\"id\":\"evt_01\",\"type\":\"payment.completed\",\"livemode\":false,\"data\":{\"amount_kes\":1500}}";

            // 1. Tampered body
            AssertThrows(() => Webhook.Verify("{\"tampered\":true}", validHeader, validSecret, now: DateTimeOffset.FromUnixTimeSeconds(1790538792)));

            // 2. Wrong secret
            AssertThrows(() => Webhook.Verify(validBody, validHeader, "whsec_test_other", now: DateTimeOffset.FromUnixTimeSeconds(1790538792)));

            // 3. Expired timestamp
            AssertThrows(() => Webhook.Verify(validBody, validHeader, validSecret, now: DateTimeOffset.FromUnixTimeSeconds(1790539362)));

            // 4. Malformed header
            AssertThrows(() => Webhook.Verify(validBody, "v1=abc", validSecret, now: DateTimeOffset.FromUnixTimeSeconds(1790538792)));
        }

        private static void AssertThrows(Action action)
        {
            try
            {
                action();
                throw new Exception("Expected SignatureException, but none was thrown.");
            }
            catch (SignatureException)
            {
                // Expected
            }
        }
    }
}
