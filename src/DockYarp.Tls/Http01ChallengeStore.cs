namespace DockYarp.Tls;

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

/// <summary>In-memory ACME HTTP-01 challenge store.</summary>
public sealed class Http01ChallengeStore : IHttp01ChallengeStore
{
    private readonly ConcurrentDictionary<string, string> responses = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Set(string token, string keyAuthorization) => responses[token] = keyAuthorization;

    /// <inheritdoc />
    public bool TryGet(string token, [MaybeNullWhen(false)] out string keyAuthorization) =>
        responses.TryGetValue(token, out keyAuthorization);

    /// <inheritdoc />
    public void Remove(string token) => responses.TryRemove(token, out _);
}
