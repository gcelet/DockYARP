namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>Source-generated JSON contract for every ACME wire type — passed explicitly at each
/// (de)serialization call site so the AOT/trim analyzer recognizes the trim-safe path (registering a
/// context via DI alone does not achieve this — see design.md).</summary>
[JsonSerializable(typeof(AcmeDirectory))]
[JsonSerializable(typeof(AcmeNewAccountRequest))]
[JsonSerializable(typeof(AcmeNewOrderRequest))]
[JsonSerializable(typeof(AcmeOrder))]
[JsonSerializable(typeof(AcmeFinalizeRequest))]
[JsonSerializable(typeof(AcmeAuthorization))]
[JsonSerializable(typeof(AcmeProblemDetails))]
[JsonSerializable(typeof(object))]
internal sealed partial class AcmeJsonContext : JsonSerializerContext;
