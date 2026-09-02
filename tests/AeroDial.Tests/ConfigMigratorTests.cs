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
    public void Migrated_v1_document_deserializes_and_rewrites_legacy_icon_names()
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
        Assert.Equal("fluent:play", cfg.Menus[0].Items[0].Icon);
        Assert.Equal(ActionType.Media, cfg.Menus[0].Items[0].ActionType);
    }

    [Fact]
    public void V2_document_gets_icon_names_rewritten_but_paths_and_fluent_keys_untouched()
    {
        var root = Obj("""
        {
          "configVersion": 2,
          "menus": [ { "id": "m", "name": "M", "items": [
              { "label": "A", "icon": "vol_up" },
              { "label": "B", "icon": "C:\\Tools\\app.exe" },
              { "label": "C", "icon": "fluent:E8B7" },
              { "label": "D", "icon": "Trash" }
          ] } ]
        }
        """);
        Assert.Equal(2, ConfigMigrator.Migrate(root));
        var items = (JsonArray)root["menus"]![0]!["items"]!;
        Assert.Equal("fluent:volume_up", (string)items[0]!["icon"]!);
        Assert.Equal("C:\\Tools\\app.exe", (string)items[1]!["icon"]!);
        Assert.Equal("fluent:E8B7", (string)items[2]!["icon"]!);
        Assert.Equal("fluent:delete", (string)items[3]!["icon"]!); // aliases are case-insensitive
    }
}
