// AeroDial — ThemeJson.cs
// The one JsonSerializerOptions used for theme files, for reading and writing alike.
//
// It exists because those two used to differ: themes were read with a camelCase naming
// policy but written with a bare options object, so every theme saved from the editor
// came back with PascalCase keys that the reader did not match. Nothing threw. The theme
// simply deserialized into all-default values, took the default name "Custom", and the
// real theme vanished from the list on the next start. Keep both directions on this
// object so the two can never drift apart again.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AeroDial.Themes;

public static class ThemeJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Reads the PascalCase files written by the versions that had the bug above,
        // so themes saved back then still load instead of silently becoming "Custom".
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
