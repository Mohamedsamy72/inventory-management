using System.Text.Json;
using System.Text.Json.Nodes;

namespace Inventory.Infrastructure.Audit;

/// <summary>
/// Task 4.12 - strips known-secret fields from an audit value before it is serialized into
/// <c>old_values</c>/<c>new_values</c> (docs/14 §1 point 2). Works on the serialized JSON tree,
/// not on the source type, so it catches a secret field on ANY object shape a future handler
/// passes in - including one nested inside a child object - rather than trusting every future
/// caller to remember to omit it themselves.
/// </summary>
internal static class AuditSanitizer
{
    /// <summary>Matched by substring, case-insensitively, against every property name at every
    /// nesting depth. Deliberately broad (docs/14 §1: "strict sanitization, zero secret
    /// leakage") - a false-positive redaction of a harmless field is a formatting nuisance; a
    /// missed secret is a compliance incident.</summary>
    private static readonly string[] SecretMarkers =
    [
        "password", "passwordhash", "otp", "securitystamp", "token", "connectionstring", "secret",
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string? Sanitize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        JsonNode? node = JsonSerializer.SerializeToNode(value, value.GetType(), SerializerOptions);
        Redact(node);
        return node?.ToJsonString(SerializerOptions);
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (string propertyName in obj.Select(static kvp => kvp.Key).ToList())
            {
                if (IsSecretField(propertyName))
                {
                    obj[propertyName] = "[REDACTED]";
                }
                else
                {
                    Redact(obj[propertyName]);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                Redact(item);
            }
        }
    }

    private static bool IsSecretField(string propertyName) =>
        SecretMarkers.Any(marker => propertyName.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
