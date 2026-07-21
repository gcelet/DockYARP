namespace DockYarp.Core.Models;

/// <summary>A single backend destination within a <see cref="Cluster"/>.</summary>
/// <remarks>
/// <paramref name="Id"/> is a stable identity for the destination (for example a Docker container id),
/// used to add or remove the right endpoint when the underlying container changes.
/// </remarks>
/// <param name="Id">Stable identity of the destination.</param>
/// <param name="Address">Absolute destination address (for example <c>http://10.0.0.1:8080</c>).</param>
public sealed record ClusterEndpoint(string Id, string Address);
