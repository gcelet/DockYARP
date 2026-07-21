namespace DockYarp.Core.Configuration;

/// <summary>A diagnostic produced while merging configuration contributions.</summary>
/// <remarks>Core does not log; callers (which own an <c>ILogger</c>) translate these into log entries.</remarks>
/// <param name="Severity">Severity of the diagnostic.</param>
/// <param name="Code">Stable, machine-readable code (for example <c>route.conflict</c>).</param>
/// <param name="Message">Human-readable description.</param>
public sealed record MergeDiagnostic(MergeSeverity Severity, string Code, string Message);
