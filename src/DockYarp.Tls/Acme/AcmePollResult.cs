namespace DockYarp.Tls.Acme;

using System;

/// <summary>A polled ACME resource (authorization or order) alongside any <c>Retry-After</c> the CA suggested
/// for the next poll (RFC 7231 §7.1.3, already capped — see <see cref="AcmeHttpClient"/>).</summary>
/// <typeparam name="T">The polled resource type (<see cref="AcmeAuthorization"/> or <see cref="AcmeOrder"/>).</typeparam>
/// <param name="Resource">The polled resource.</param>
/// <param name="RetryAfter">The CA-suggested delay before the next poll, or <see langword="null"/> when the
/// response carried no <c>Retry-After</c> header.</param>
internal readonly record struct AcmePollResult<T>(T Resource, TimeSpan? RetryAfter);
