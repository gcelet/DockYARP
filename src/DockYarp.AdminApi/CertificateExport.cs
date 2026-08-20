namespace DockYarp.AdminApi;

/// <summary>A stored certificate's exportable PEM material.</summary>
/// <param name="CertificatePem">The full-chain certificate PEM text (leaf plus any additional certificates).</param>
/// <param name="PrivateKeyPem">The private key PEM text.</param>
public sealed record CertificateExport(string CertificatePem, string PrivateKeyPem);
