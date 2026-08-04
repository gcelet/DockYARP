namespace DockYarp.Tls;

using System.Net;

/// <summary>A parsed PROXY protocol header.</summary>
/// <param name="SourceEndPoint">
/// The real client endpoint the header carries, or <see langword="null"/> when it carries no usable client
/// address (<c>UNKNOWN</c> in v1; <c>LOCAL</c>, an unspecified family, or a non-stream transport in v2).
/// </param>
public readonly record struct ProxyProtocolHeader(IPEndPoint? SourceEndPoint);
