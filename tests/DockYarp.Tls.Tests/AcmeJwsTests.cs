namespace DockYarp.Tls.Tests;

using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using DockYarp.Tls.Acme;

/// <summary>Structural and self-consistency tests for the hand-rolled JWS(ES256)/JWK/key-authorization
/// helpers — no external library or live ACME server involved. Mirrors <c>DnsUpdateMessageTests</c>'s own
/// approach: independently re-derive and verify each cryptographic value rather than asserting against an
/// official RFC test vector this codebase doesn't have a confidently-memorized copy of.</summary>
public sealed class AcmeJwsTests
{
    private static ECDsa NewKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    [Test]
    public void Jwk_FieldsMatchTheKeysOwnExportedCoordinates()
    {
        using ECDsa key = NewKey();
        ECParameters expected = key.ExportParameters(includePrivateParameters: false);

        AcmeJws.JwkFields jwk = AcmeJws.Jwk(key);

        Base64Url.DecodeFromChars(jwk.X).Should().BeEquivalentTo(expected.Q.X);
        Base64Url.DecodeFromChars(jwk.Y).Should().BeEquivalentTo(expected.Q.Y);
    }

    [Test]
    public void JwkThumbprint_IsStableForTheSameKeyAndDiffersAcrossKeys()
    {
        using ECDsa keyA = NewKey();
        using ECDsa keyB = NewKey();

        string first = AcmeJws.JwkThumbprint(keyA);
        string second = AcmeJws.JwkThumbprint(keyA);
        string other = AcmeJws.JwkThumbprint(keyB);

        first.Should().Be(second, "the thumbprint is a pure function of the key's public coordinates");
        first.Should().NotBe(other, "two different keys must not collide");
    }

    [Test]
    public void Dns01TxtValue_IsIndependentlyVerifiableAsSha256OfTheKeyAuthorization()
    {
        using ECDsa key = NewKey();
        const string token = "some-challenge-token";

        string keyAuthorization = AcmeJws.KeyAuthorization(token, key);
        keyAuthorization.Should().Be($"{token}.{AcmeJws.JwkThumbprint(key)}");

        string txtValue = AcmeJws.Dns01TxtValue(token, key);
        byte[] recomputed = SHA256.HashData(Encoding.UTF8.GetBytes(keyAuthorization));
        txtValue.Should().Be(Base64Url.EncodeToString(recomputed));
    }

    [Test]
    public void Sign_AccountCreation_ProtectedHeaderCarriesJwkNotKid()
    {
        using ECDsa key = NewKey();
        AcmeJws.JwsRequestContext context = new(key, "https://acme.example/new-account", "nonce-1", Kid: null);

        string jws = AcmeJws.Sign(context, (AcmeNewAccountRequest?)null, AcmeJsonContext.Default.AcmeNewAccountRequest);

        using JsonDocument protectedHeader = ParseProtectedHeader(jws);
        protectedHeader.RootElement.GetProperty("alg").GetString().Should().Be("ES256");
        protectedHeader.RootElement.TryGetProperty("jwk", out _).Should().BeTrue();
        protectedHeader.RootElement.TryGetProperty("kid", out _).Should().BeFalse();
    }

    [Test]
    public void Sign_AfterAccountExists_ProtectedHeaderCarriesKidNotJwk()
    {
        using ECDsa key = NewKey();
        AcmeJws.JwsRequestContext context = new(key, "https://acme.example/new-order", "nonce-2", Kid: "https://acme.example/acct/1");

        string jws = AcmeJws.Sign(context, (AcmeNewOrderRequest?)null, AcmeJsonContext.Default.AcmeNewOrderRequest);

        using JsonDocument protectedHeader = ParseProtectedHeader(jws);
        protectedHeader.RootElement.TryGetProperty("kid", out JsonElement kid).Should().BeTrue();
        kid.GetString().Should().Be("https://acme.example/acct/1");
        protectedHeader.RootElement.TryGetProperty("jwk", out _).Should().BeFalse();
    }

    [Test]
    public void Sign_PostAsGet_PayloadFieldIsEmptyString()
    {
        using ECDsa key = NewKey();
        AcmeJws.JwsRequestContext context = new(key, "https://acme.example/authz/1", "nonce-3", "https://acme.example/acct/1");

        string jws = AcmeJws.Sign(context, (AcmeNewOrderRequest?)null, AcmeJsonContext.Default.AcmeNewOrderRequest);

        using JsonDocument document = JsonDocument.Parse(jws);
        document.RootElement.GetProperty("payload").GetString().Should().BeEmpty();
    }

    [Test]
    public void Sign_SignatureVerifiesAgainstThePublicKeyOverTheSigningInput()
    {
        using ECDsa key = NewKey();
        AcmeJws.JwsRequestContext context = new(key, "https://acme.example/new-account", "nonce-4", Kid: null);

        string jws = AcmeJws.Sign(context, (AcmeNewAccountRequest?)null, AcmeJsonContext.Default.AcmeNewAccountRequest);

        using JsonDocument document = JsonDocument.Parse(jws);
        string protectedB64 = document.RootElement.GetProperty("protected").GetString()!;
        string payloadB64 = document.RootElement.GetProperty("payload").GetString()!;
        byte[] signature = Base64Url.DecodeFromChars(document.RootElement.GetProperty("signature").GetString());

        byte[] signingInput = Encoding.ASCII.GetBytes($"{protectedB64}.{payloadB64}");
        bool verified = key.VerifyData(
            signingInput, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        verified.Should().BeTrue("the signature must verify against the same public key that signed it");
    }

    private static JsonDocument ParseProtectedHeader(string jws)
    {
        using JsonDocument document = JsonDocument.Parse(jws);
        string protectedB64 = document.RootElement.GetProperty("protected").GetString()!;
        return JsonDocument.Parse(Base64Url.DecodeFromChars(protectedB64));
    }
}
