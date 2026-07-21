namespace DockYarp.Core.Configuration;

/// <summary>Severity of a <see cref="MergeDiagnostic"/>.</summary>
public enum MergeSeverity
{
    /// <summary>Informational message.</summary>
    Information,

    /// <summary>A condition the operator should be aware of (conflict, skipped entry).</summary>
    Warning,
}
