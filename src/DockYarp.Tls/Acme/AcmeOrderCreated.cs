namespace DockYarp.Tls.Acme;

/// <summary>A newly-created order together with its own URL (from the response's <c>Location</c> header),
/// needed to poll it again after finalizing (RFC 8555 §7.4).</summary>
/// <param name="OrderUrl">The order's own URL.</param>
/// <param name="Order">The parsed order body.</param>
internal readonly record struct AcmeOrderCreated(string OrderUrl, AcmeOrder Order);
