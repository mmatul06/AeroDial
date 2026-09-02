using System.Text.Json;
using System.Text.Json.Nodes;
using AeroDial.Config;

namespace AeroDial.Tests;

public class ConfigMigratorTests
{
    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void Document_without_version_is_treated_as_v1_and_stamped_current()
    {
        var root = Obj("""{ "activeMenuId": "default", "menus": [] }""");
        int from = ConfigMigrator.Migrate(root);
        Assert.Equal(1, from);
        Assert.Equal(ConfigMigrator.CurrentVersion, (int)root["configVersion"]!);
    }

    [Fact]
    public void Current_document_is_left_alone()
    {
        var root = Obj($$"""{ "configVersion": {{ConfigMigrator.CurrentVersion}}, "activeMenuId": "x" }""");
        int from = ConfigMigrator.Migrate(root);
        Assert.Equal(ConfigMigrator.CurrentVersion, from);
        Assert.Equal("x", (string)root["activeMenuId"]!);
    }

    [Fact]
    public void Garbage_version_is_treated_as_v1()
    {
        var root = Obj("""{ "configVersion": "old" }""");
        Assert.Equal(1, ConfigMigrator.Migrate(root));
    }

    [Fact]
    public void Migrated_v1_document_deserializes_with_legacy_icon_names_intact()
    {
        var root = Obj("""
        {
          "menus": [ { "id": "default", "name": "Main", "items": [ { "label": "Play", "icon": "play", "actionType": "media", "mediaAction": "playPause" } ] } ],
          "activeMenuId": "default"
        }
        """);
        ConfigMigrator.Migrate(root);

        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
        var cfg = root.Deserialize<AeroDialConfig>(opts)!;
        Assert.Equal(ConfigMigrator.CurrentVersion, cfg.ConfigVersion);
        Assert.Equal("play", cfg.Menus[0].Items[0].Icon);
        Assert.Equal(ActionType.Media, cfg.Menus[0].Items[0].ActionType);
    }
}
