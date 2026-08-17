namespace DockYarp.Tls;

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

/// <summary>A certificate together with any additional (chain) certificates that travel with it.</summary>
/// <param name="Leaf">The keyed leaf certificate.</param>
/// <param name="Additional">The additional certificates (typically intermediates) that must be sent alongside
/// <paramref name="Leaf"/> during a TLS handshake for a client to build a complete chain.</param>
public sealed record LoadedCertificate(X509Certificate2 Leaf, IReadOnlyList<X509Certificate2> Additional);
