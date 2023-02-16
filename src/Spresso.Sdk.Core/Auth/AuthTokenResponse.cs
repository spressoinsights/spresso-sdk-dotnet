using System;

namespace Spresso.Sdk.Core.Auth
{
    public sealed class AuthTokenResponse
    {
        public string? Token { get; }
        public DateTimeOffset? ExpiresAt { get; }
        public bool IsSuccess => Error == AuthError.None;
        public AuthError Error { get; }

        public AuthTokenResponse(string token, DateTimeOffset expiresAt)
        {
            Token = token;
            ExpiresAt = expiresAt;
            Error = AuthError.None;
        }

        public AuthTokenResponse(AuthError authError)
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