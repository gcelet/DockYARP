namespace DockYarp.AdminApi;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>Source-generated, AOT/trim-safe metadata for every type the admin API serializes.</summary>
/// <remarks>
/// Registered via <c>ConfigureHttpJsonOptions</c> in <c>ObservabilityServiceCollectionExtensions</c> (for
/// <c>Results.BadRequest</c>/<c>NotFound</c>, which have no overload accepting a context directly) AND
/// passed explicitly to each <c>Results.Json(value, AdminApiJsonContext.Default)</c> call (the DI-registered
/// ambient options alone do not change which overload the trim analyzer sees at the call site — confirmed
/// empirically, not assumed). The <c>JsonSourceGenerationOptions</c> camelCase naming below must match
/// ASP.NET Core minimal APIs' own default naming policy exactly — passing the context directly to
/// <c>Results.Json</c> uses the context's own options, not the ambient ones, so a mismatch here is a real,
/// silent response-shape regression (caught live by <c>AdminApiIntegrationTests</c> during this change).
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AdminApiModels.VersionView))]
[JsonSerializable(typeof(IReadOnlyList<AdminApiModels.RouteView>))]
[JsonSerializable(typeof(IReadOnlyList<AdminApiModels.ClusterView>))]
[JsonSerializable(typeof(IReadOnlyList<AdminApiModels.CertView>))]
[JsonSerializable(typeof(AdminApiModels.ResolveView))]
[JsonSerializable(typeof(AdminApiModels.HealthView))]
[JsonSerializable(typeof(AdminApiModels.ErrorView))]
[JsonSerializable(typeof(AdminApiModels.ResolveNotFoundView))]
public sealed partial class AdminApiJsonContext : JsonSerializerContext;
