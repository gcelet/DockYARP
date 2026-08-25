namespace DockYarp.Tls.Acme;

using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>JWS(ES256) request signing (RFC 8555 §6.2) and the JWK/key-authorization helpers ACME's
/// challenge types build on (RFC 7638, RFC 8555 §8.1/§8.4). DockYarp only ever uses a single P-256 (ES256)
/// key per order, so this deliberately does not support any other JWS algorithm.</summary>
internal static class AcmeJws
{
    /// <summary>Builds the canonical JWK for an EC P-256 public key (RFC 7518 §6.2.1 field order).</summary>
    /// <param name="key">The ES256 key whose public part is exposed as a JWK.</param>
    /// <returns>The JWK as <c>{crv, kty, x, y}</c>, alphabetically ordered per RFC 7638 §3.2's canonical form.</returns>
    public static JwkFields Jwk(ECDsa key)
    {
        ECParameters parameters = key.ExportParameters(includePrivateParameters: false);
        return new JwkFields(
            Base64Url.EncodeToString(parameters.Q.X!),
            Base64Url.EncodeToString(parameters.Q.Y!));
    }

    /// <summary>Computes the RFC 7638 JWK thumbprint: SHA-256 of the canonical JWK JSON, base64url-encoded.</summary>
    /// <param name="key">The ES256 key whose public part is thumbprinted.</param>
    public static string JwkThumbprint(ECDsa key)
    {
        JwkFields jwk = Jwk(key);
        string canonical = $$"""{"crv":"P-256","kty":"EC","x":"{{jwk.X}}","y":"{{jwk.Y}}"}""";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Base64Url.EncodeToString(hash);
    }

    /// <summary>Builds the HTTP-01 key authorization (RFC 8555 §8.1): the challenge token joined to the
    /// account key's thumbprint.</summary>
    /// <param name="token">The challenge token.</param>
    /// <param name="accountKey">The account key the challenge is bound to.</param>
    public static string KeyAuthorization(string token, ECDsa accountKey) =>
        $"{token}.{JwkThumbprint(accountKey)}";

    /// <summary>Builds the DNS-01 TXT record value (RFC 8555 §8.4): SHA-256 of the key authorization,
    /// base64url-encoded.</summary>
    /// <param name="token">The challenge token.</param>
    /// <param name="accountKey">The account key the challenge is bound to.</param>
    public static string Dns01TxtValue(string token, ECDsa accountKey)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(KeyAuthorization(token, accountKey)));
        return Base64Url.EncodeToString(hash);
    }

    /// <summary>Signs a JWS request body (RFC 8555 §6.2) — either key-authenticated (account creation, no
    /// <see cref="JwsRequestContext.Kid"/> yet) or key-ID-authenticated (every request after).</summary>
    /// <param name="context">The signing key, target URL, replay-nonce, and (once known) account key ID.</param>
    /// <param name="payload">The request payload, or <see langword="null"/> for a POST-as-GET (RFC 8555
    /// §6.3), whose JWS payload is the empty string, not an empty JSON object.</param>
    /// <param name="jsonTypeInfo">The source-generated contract for <paramref name="payload"/>'s runtime type.</param>
    public static string Sign<T>(JwsRequestContext context, T? payload, JsonTypeInfo<T> jsonTypeInfo)
        where T : class
    {
        string protectedHeader = context.Kid is null
            ? BuildJwkProtectedHeader(context.SigningKey, context.Url, context.Nonce)
            : BuildKidProtectedHeader(context.Kid, context.Url, context.Nonce);
        string protectedB64 = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(protectedHeader));

        string payloadB64 = payload is null
            ? string.Empty
            : Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload, jsonTypeInfo));

        byte[] signingInput = Encoding.ASCII.GetBytes($"{protectedB64}.{payloadB64}");
        byte[] signature = context.SigningKey.SignData(
            signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        string signatureB64 = Base64Url.EncodeToString(signature);

        return $$"""{"protected":"{{protectedB64}}","payload":"{{payloadB64}}","signature":"{{signatureB64}}"}""";
    }

    /// <summary>The signing key, target URL, replay-nonce, and (once known) account key ID a JWS request needs.</summary>
    /// <param name="SigningKey">The account's ES256 key.</param>
    /// <param name="Url">The request's target URL (bound into the signed protected header).</param>
    /// <param name="Nonce">The replay-nonce to consume.</param>
    /// <param name="Kid">The account URL, once known; <see langword="null"/> only for account creation.</param>
    public readonly record struct JwsRequestContext(ECDsa SigningKey, string Url, string Nonce, string? Kid);

    private static string BuildJwkProtectedHeader(ECDsa signingKey, string url, string nonce)
    {
        JwkFields jwk = Jwk(signingKey);
        return $$"""{"alg":"ES256","jwk":{"crv":"P-256","kty":"EC","x":"{{jwk.X}}","y":"{{jwk.Y}}"},"nonce":"{{nonce}}","url":"{{url}}"}""";
    }

    private static string BuildKidProtectedHeader(string kid, string url, string nonce) =>
        $$"""{"alg":"ES256","kid":"{{kid}}","nonce":"{{nonce}}","url":"{{url}}"}""";

    /// <summary>The base64url-encoded <c>x</c>/<c>y</c> coordinates of an EC P-256 public key.</summary>
    public readonly record struct JwkFields(string X, string Y);
}
