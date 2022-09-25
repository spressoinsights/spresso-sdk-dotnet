using System;

namespace Spresso.Sdk.Core.Auth
{
    public struct TokenResponse
    {
        public string? Token { get; }
        public DateTimeOffset? ExpiresAt { get; }
        public bool IsSuccess => Error == AuthError.None;
        public AuthError Error { get; }

        public TokenResponse(string token, DateTimeOffset expiresAt)
        {
            Token = token;
            ExpiresAt = expiresAt;
            Error = AuthError.None;
        }

        public TokenResponse(AuthError authError)
        {
            if (authError == AuthError.None)
            {
                throw new ArgumentException(nameof(AuthError));
            }
            Error = authError;
            Token = null;
            ExpiresAt = null;
        }
    }
}