using System;

namespace Moderyo
{
    /// <summary>
    /// Base exception for all Moderyo SDK errors
    /// </summary>
    public class ModeryoException : Exception
    {
        public string Code { get; }
        public int? StatusCode { get; }
        public string RequestId { get; }

        public ModeryoException(string message, string code = "MODERYO_ERROR", int? statusCode = null, string requestId = null)
            : base(message)
        {
            Code = code;
            StatusCode = statusCode;
            RequestId = requestId;
        }

        public ModeryoException(string message, Exception innerException, string code = "MODERYO_ERROR")
            : base(message, innerException)
        {
            Code = code;
        }
    }

    /// <summary>
    /// Thrown when API key is invalid
    /// </summary>
    public class AuthenticationException : ModeryoException
    {
        public AuthenticationException(string message = "Invalid or missing API key", string requestId = null)
            : base(message, "AUTHENTICATION_ERROR", 401, requestId)
        {
        }
    }

    /// <summary>
    /// Thrown when rate limit is exceeded
    /// </summary>
    public class RateLimitException : ModeryoException
    {
        public float RetryAfter { get; }
        public int Limit { get; }
        public int Remaining { get; }

        public RateLimitException(
            string message = "Rate limit exceeded",
            float retryAfter = 60f,
            int limit = 0,
            int remaining = 0,
            string requestId = null)
            : base(message, "RATE_LIMIT_ERROR", 429, requestId)
        {
            RetryAfter = retryAfter;
            Limit = limit;
            Remaining = remaining;
        }
    }

    /// <summary>
    /// Thrown when request validation fails
    /// </summary>
    public class ValidationException : ModeryoException
    {
        public string Field { get; }

        public ValidationException(string message, string field = null, string requestId = null)
            : base(message, "VALIDATION_ERROR", 400, requestId)
        {
            Field = field;
        }
    }

    /// <summary>
    /// Thrown when quota is exceeded
    /// </summary>
    public class QuotaExceededException : ModeryoException
    {
        public long CurrentUsage { get; }
        public long Limit { get; }

        public QuotaExceededException(
            string message = "Monthly quota exceeded",
            long currentUsage = 0,
            long limit = 0,
            string requestId = null)
            : base(message, "QUOTA_EXCEEDED_ERROR", 402, requestId)
        {
            CurrentUsage = currentUsage;
            Limit = limit;
        }
    }

    /// <summary>
    /// Thrown when network error occurs
    /// </summary>
    public class NetworkException : ModeryoException
    {
        public bool IsTimeout { get; }

        public NetworkException(string message = "Network error occurred", bool isTimeout = false, Exception innerException = null)
            : base(message, innerException, isTimeout ? "TIMEOUT_ERROR" : "NETWORK_ERROR")
        {
            IsTimeout = isTimeout;
        }
    }
}
