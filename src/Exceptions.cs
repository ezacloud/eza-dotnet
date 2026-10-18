using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ezacloud.Sdk
{
    public class EzaException : Exception
    {
        public int Status { get; }
        public string? ErrorCode { get; }
        public string? RequestId { get; }
        public IReadOnlyList<FieldError>? Fields { get; }

        public EzaException(
            string message,
            int status = 0,
            string? errorCode = null,
            string? requestId = null,
            IReadOnlyList<FieldError>? fields = null,
            Exception? innerException = null)
            : base(FormatMessage(message, status, errorCode, requestId), innerException)
        {
            Status = status;
            ErrorCode = errorCode;
            RequestId = requestId;
            Fields = fields;
        }

        private static string FormatMessage(string message, int status, string? errorCode, string? requestId)
        {
            var prefix = string.IsNullOrEmpty(errorCode) ? "eza:" : $"eza [{errorCode}]:";
            var statusStr = status > 0 ? $" (status {status})" : "";
            var reqStr = !string.IsNullOrEmpty(requestId) ? $" (request_id: {requestId})" : "";
            return $"{prefix} {message}{statusStr}{reqStr}".Trim();
        }
    }

    public class FieldError
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class SignatureException : Exception
    {
        public SignatureException(string message) : base($"eza webhook signature: {message}") { }
    }

    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base($"eza configuration: {message}") { }
    }
}
