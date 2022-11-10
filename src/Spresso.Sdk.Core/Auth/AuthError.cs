namespace Spresso.Sdk.Core.Auth
{
    public enum AuthError : byte
    {
        None,
        InvalidCredentials,
        Timeout,
        InvalidScopes,
        Unknown
    }
}