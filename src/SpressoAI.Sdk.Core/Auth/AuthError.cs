namespace SpressoAI.Sdk.Core.Auth
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