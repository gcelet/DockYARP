namespace DockYarp.Core.Models;

/// <summary>Client-affinity ("sticky session") policy for a cluster.</summary>
public enum SessionAffinityPolicy
{
    /// <summary>No affinity — destinations are selected purely by the load-balancing policy.</summary>
    None,

    /// <summary>Deterministic client-IP hash, stateless (no cookie/header). The nginx-proxy <c>ip_hash</c>
    /// parity mechanism; needs no Data Protection.</summary>
    ClientIpHash,

    /// <summary>YARP's built-in encrypted cookie policy. Requires Data Protection.</summary>
    Cookie,

    /// <summary>YARP's built-in encrypted custom-header policy. Requires Data Protection.</summary>
    CustomHeader,
}
