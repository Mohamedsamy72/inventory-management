using System.Text.Json;
using System.Text.Json.Serialization;

namespace Inventory.Api.Serialization;

/// <summary>
/// AC-31-9 (docs/31 §7): strips U+202E (Right-to-Left Override) from every JSON string value
/// deserialized from a request body, applied globally rather than per-DTO so no future field can
/// be added without this protection - left in place, U+202E enables spoofed filenames and
/// display-order attacks in tables and file lists. Serialization (writing responses) is left
/// untouched: an already-stored value from before this converter existed should still round-trip
/// visibly rather than silently vanish from a read.
/// </summary>
public sealed class RightToLeftOverrideStrippingConverter : JsonConverter<string>
{
    private const char RightToLeftOverride = '‮';

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        return value is null || value.IndexOf(RightToLeftOverride) < 0
            ? value
            : value.Replace(RightToLeftOverride.ToString(), string.Empty, StringComparison.Ordinal);
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
