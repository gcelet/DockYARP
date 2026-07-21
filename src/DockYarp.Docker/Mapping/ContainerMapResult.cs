namespace DockYarp.Docker.Mapping;

using System.Collections.Immutable;

using DockYarp.Core.Configuration;

/// <summary>The outcome of mapping containers to a dynamic configuration contribution.</summary>
/// <param name="Contribution">The dynamic contribution (routes + clusters) built from the containers.</param>
/// <param name="Warnings">Reasons for containers that were skipped during mapping.</param>
public sealed record ContainerMapResult(ConfigContribution Contribution, ImmutableArray<string> Warnings);
