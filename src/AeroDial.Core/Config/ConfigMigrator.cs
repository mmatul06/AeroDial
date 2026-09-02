// AeroDial — ConfigMigrator.cs
// Upgrades older config.json documents to the current schema before
// deserialization. Works on the raw JSON tree so renamed or removed fields
// can be rewritten without the model having to know about old shapes.
//
// Version history:
//   1  v1.0 - v2.0 (no configVersion field)
//   2  v3.0: configVersion field introduced; no structural changes yet

using System.Text.Json.Nodes;

namespace AeroDial.Config;

internal static class ConfigMigrator
{
    public const int CurrentVersion = 2;

    private const string VersionField = "configVersion";

    /// <summary>
    /// Migrates <paramref name="root"/> in place to <see cref="CurrentVersion"/>.
    /// Returns the version the document was at before migration.
    /// </summary>
    public static int Migrate(JsonObject root)
    {
        int from = ReadVersion(root);

        for (int v = from; v < CurrentVersion; v++)
        {
            switch (v)
            {
                case 1: MigrateV1ToV2(root); break;
            }
        }

        root[VersionField] = CurrentVersion;
        return from;
    }

    private static int ReadVersion(JsonObject root)
    {
        if (root.TryGetPropertyValue(VersionField, out var node) &&
            node is JsonValue value && value.TryGetValue<int>(out var version) && version > 0)
            return version;
        return 1;
    }

    // v1 -> v2: only stamps the version. Kept as an explicit step so later
    // migrations have a template to follow.
    private static void MigrateV1ToV2(JsonObject root) { }
}
