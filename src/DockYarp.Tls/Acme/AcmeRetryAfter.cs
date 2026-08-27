namespace DockYarp.Tls.Acme;

using System;
using System.Net.Http.Headers;

/// <summary>Parses and caps a CA-supplied <c>Retry-After</c> header (RFC 7231 §7.1.3), used both for
/// <see cref="AcmeHttpClient"/>'s bounded <c>rateLimited</c> retry and for <see cref="AcmePollResult{T}"/>'s
/// status-polling delay.</summary>
internal static class AcmeRetryAfter
{
    /// <summary>The maximum duration a CA-supplied <c>Retry-After</c> is honored for — matches this codebase's
    /// existing ~60s polling-loop order of magnitude, and keeps a misbehaving CA value from stalling a
    /// provisioning attempt indefinitely.</summary>
    internal static readonly TimeSpan Cap = TimeSpan.FromSeconds(60);

    /// <summary>Parses a response's <c>Retry-After</c> header, capped at <see cref="Cap"/>.</summary>
    /// <param name="headers">The response headers to read <c>Retry-After</c> from.</param>
    /// <returns>The capped duration, or <see langword="null"/> when the header is absent.</returns>
    internal static TimeSpan? Capped(HttpResponseHeaders headers)
    {
        TimeSpan? retryAfter = Parse(headers);
        return retryAfter is { } value && value > Cap ? Cap : retryAfter;
    }

    /// <summary>Parses a response's <c>Retry-After</c> header, uncapped.</summary>
    /// <param name="headers">The response headers to read <c>Retry-After</c> from.</param>
    /// <returns>The parsed duration (clamped to a minimum of <see cref="TimeSpan.Zero"/>), or
    /// <see langword="null"/> when the header is absent.</returns>
    /// <remarks><see cref="HttpResponseHeaders.RetryAfter"/> already parses both RFC 7231 §7.1.3 forms
    /// (delay-seconds via <see cref="RetryConditionHeaderValue.Delta"/>, HTTP-date via
    /// <see cref="RetryConditionHeaderValue.Date"/>) — no hand-rolled parsing is needed. A <c>Date</c> already
    /// in the past (clock skew, or a fast-expiring hint) clamps to zero rather than going negative.</remarks>
    internal static TimeSpan? Parse(HttpResponseHeaders headers)
    {
        RetryConditionHeaderValue? retryAfter = headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (retryAfter.Date is { } date)
        {
            TimeSpan remaining = date - DateTimeOffset.UtcNow;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        return null;
    }
}
