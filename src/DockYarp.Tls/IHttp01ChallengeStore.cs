namespace DockYarp.Tls;

using System.Diagnostics.CodeAnalysis;

/// <summary>Holds pending ACME HTTP-01 challenge responses (token → key authorization).</summary>
public interface IHttp01ChallengeStore
{
    /// <summary>Stores the key authorization for a challenge token.</summary>
    /// <param name="token">The challenge token.</param>
    /// <param name="keyAuthorization">The expected key authorization response.</param>
    void Set(string token, string keyAuthorization);

    /// <summary>Gets the key authorization for a token.</summary>
    /// <param name="token">The challenge token.</param>
    /// <param name="keyAuthorization">The key authorization when found.</param>
    /// <returns><see langword="true"/> when the token is known.</returns>
    bool TryGet(string token, [MaybeNullWhen(false)] out string keyAuthorization);

    /// <summary>Removes a challenge token.</summary>
    /// <param name="token">The challenge token.</param>
    void Remove(string token);
}
