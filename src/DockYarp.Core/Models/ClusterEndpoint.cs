namespace DockYarp.Core.Models;

using System.Globalization;

/// <summary>A single backend destination within a <see cref="Cluster"/>.</summary>
/// <remarks>
/// <paramref name="Id"/> is a stable identity for the destination (for example a Docker container id),
/// used to add or remove the right endpoint when the underlying container changes. <paramref name="Address"/>
/// is absolute and carries the backend scheme; use <see cref="Create"/> to build it from a
/// <see cref="BackendScheme"/>.
/// </remarks>
/// <param name="Id">Stable identity of the destination.</param>
/// <param name="Address">Absolute destination address (for example <c>http://10.0.0.1:8080</c>).</param>
public sealed record ClusterEndpoint(string Id, string Address)
{
    /// <summary>Creates an endpoint whose absolute address targets the host using the given scheme.</summary>
    /// <param name="id">Stable identity of the destination.</param>
    /// <param name="scheme">The backend transport scheme.</param>
    /// <param name="host">The backend host or IP address.</param>
    /// <param name="port">The backend port.</param>
    /// <returns>An endpoint whose address is <c>{scheme}://{host}:{port}</c>.</returns>
    public static ClusterEndpoint Create(string id, BackendScheme scheme, string host, int port)
    {
        string uriScheme = scheme == BackendScheme.Https ? "https" : "http";
        string address = string.Create(CultureInfo.InvariantCulture, $"{uriScheme}://{host}:{port}");
        return new ClusterEndpoint(id, address);
    }
}
