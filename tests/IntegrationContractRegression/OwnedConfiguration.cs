using BossMod;
using System.Reflection;
using System.Text.Json;

static partial class Checks
{
    private const BindingFlags InternalInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Property(object value, string property) => value.GetType().GetProperty(property)!.GetValue(value)!;
    private static (ConfigRoot Root, ActionTweaksConfig Node, object Field) ConfigFixture()
    {
        var root = new ConfigRoot();
        var node = new ActionTweaksConfig();
        typeof(ConfigRoot).GetMethod("RegisterNode", InternalInstance)!.Invoke(root, [typeof(ActionTweaksConfig), node]);
        var metadata = typeof(ConfigRoot).Assembly.GetType("BossMod.GeneratedConfigMetadata", true)!;
        var typeMetadata = metadata.GetMethod("Get", BindingFlags.Static | BindingFlags.Public, [typeof(ConfigNode)])!.Invoke(null, [node])!;
        var fields = (System.Collections.IDictionary)typeMetadata.GetType().GetField("FieldsByName")!.GetValue(typeMetadata)!;
        return (root, node, fields[nameof(ActionTweaksConfig.PreventMovingWhileCasting)]!);
    }
    private static object ReadSetting(ConfigRoot root, ConfigNode node, object field)
        => typeof(ConfigRoot).GetMethod("ReadOwnedField", InternalInstance)!.Invoke(root, [node, field])!;
    private static object SetSetting(ConfigRoot root, ConfigNode node, object field, string lease, object expected, bool value)
        => typeof(ConfigRoot).GetMethod("SetOwnedField", InternalInstance)!.Invoke(root,
            [node, field, lease, Property(expected, "Revision"), Property(expected, "Value"), value])!;
    private static bool ReleaseSetting(ConfigRoot root, ConfigNode node, object field, string lease, long revision)
        => (bool)typeof(ConfigRoot).GetMethod("ReleaseOwnedField", InternalInstance)!.Invoke(root, [node, field, lease, revision])!;

    private static void OwnedConfiguration()
    {
        var (root, node, field) = ConfigFixture();
        var persistentNotifications = 0;
        var runtimeNotifications = 0;
        using var saved = root.Modified.Subscribe(() => ++persistentNotifications);
        using var updated = node.Modified.Subscribe(() => ++runtimeNotifications);
        var initial = ReadSetting(root, node, field);
        var applied = SetSetting(root, node, field, "lease-a", initial, true);
        Check((bool)Property(applied, "Accepted") && node.PreventMovingWhileCasting, "owned setting is applied through native generated metadata");
        var revision = (long)Property(applied, "Revision");
        Check(persistentNotifications == 0 && runtimeNotifications == 1, "borrowed settings notify runtime without causing a preference save");
        var foreign = SetSetting(root, node, field, "lease-b", ReadSetting(root, node, field), false);
        Check(!(bool)Property(foreign, "Accepted") && node.PreventMovingWhileCasting, "another owner cannot overwrite a leased setting");
        Check(root.ClearTransient() == 0 && node.PreventMovingWhileCasting, "legacy global clear cannot restore an independent owner's field");
        Check(root.SetTransient(["ActionTweaksConfig", "PreventMovingWhileCasting", "false"]).Count > 0 && node.PreventMovingWhileCasting,
            "legacy transient cannot steal an owner's field");
        Check(!ReleaseSetting(root, node, field, "lease-b", revision) && !ReleaseSetting(root, node, field, "lease-a", revision - 1),
            "restore requires exact owner and revision");

        var directory = Path.Combine(Path.GetTempPath(), "xbmr-owned-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var file = new FileInfo(Path.Combine(directory, "config.json"));
            root.SaveToFile(file);
            using (var json = JsonDocument.Parse(File.ReadAllText(file.FullName)))
                Check(!json.RootElement.GetProperty("Payload").GetProperty(typeof(ActionTweaksConfig).FullName!).GetProperty("PreventMovingWhileCasting").GetBoolean(),
                    "save while leased persists the user's original setting");
            node.CancelCastOnDeadTarget = true;
            node.Modified.Fire();
            Check((string?)Property(ReadSetting(root, node, field), "LeaseId") == "lease-a", "an unrelated user change does not discard this setting lease");
            Parallel.For(0, 8, _ => root.SaveToFile(file));
            using (var json = JsonDocument.Parse(File.ReadAllText(file.FullName)))
            {
                var payload = json.RootElement.GetProperty("Payload").GetProperty(typeof(ActionTweaksConfig).FullName!);
                Check(payload.GetProperty("CancelCastOnDeadTarget").GetBoolean() && !payload.GetProperty("PreventMovingWhileCasting").GetBoolean(),
                    "concurrent user saves preserve unrelated changes and original borrowed preferences");
            }
            Check(ReleaseSetting(root, node, field, "lease-a", revision) && !node.PreventMovingWhileCasting, "owned setting restores its original value");
            Check(persistentNotifications == 1, "owned restoration does not create an extra preference save");

            applied = SetSetting(root, node, field, "lease-a", ReadSetting(root, node, field), true);
            revision = (long)Property(applied, "Revision");
            root.ConsoleCommand(["ActionTweaksConfig", "PreventMovingWhileCasting", "true"]);
            Check(!ReleaseSetting(root, node, field, "lease-a", revision) && node.PreventMovingWhileCasting,
                "explicit user choice equal to the borrowed value still revokes restoration ownership");
            root.SaveToFile(file);
            using (var json = JsonDocument.Parse(File.ReadAllText(file.FullName)))
                Check(json.RootElement.GetProperty("Payload").GetProperty(typeof(ActionTweaksConfig).FullName!).GetProperty("PreventMovingWhileCasting").GetBoolean(),
                    "equal-value user intent is persisted after lease revocation");

            root.ConsoleCommand(["ActionTweaksConfig", "PreventMovingWhileCasting", "false"]);
            applied = SetSetting(root, node, field, "lease-a", ReadSetting(root, node, field), true);
            revision = (long)Property(applied, "Revision");
            node.PreventMovingWhileCasting = false; node.Modified.Fire();
            node.PreventMovingWhileCasting = true; node.Modified.Fire();
            Check(!ReleaseSetting(root, node, field, "lease-a", revision) && node.PreventMovingWhileCasting,
                "user change away and back cannot be mistaken for continued lease ownership");
        }
        finally { Directory.Delete(directory, true); }

        var (legacyRoot, legacyNode, legacyField) = ConfigFixture();
        Check(legacyRoot.SetTransient(["ActionTweaksConfig", "PreventMovingWhileCasting", "true"]).Count == 0, "legacy transient setting remains compatible");
        var blocked = SetSetting(legacyRoot, legacyNode, legacyField, "new-lease", ReadSetting(legacyRoot, legacyNode, legacyField), false);
        Check(!(bool)Property(blocked, "Accepted") && (string)Property(blocked, "Reason") == "legacy-transient-active",
            "owned acquisition cannot absorb a legacy client's restoration state");
        Check(legacyRoot.ClearTransient() == 1 && !legacyNode.PreventMovingWhileCasting, "legacy clear still restores legacy overrides");

        var (reentrantRoot, reentrantNode, reentrantField) = ConfigFixture();
        var changed = false;
        var persisted = 0;
        using var persist = reentrantRoot.Modified.Subscribe(() => ++persisted);
        using var react = reentrantNode.Modified.Subscribe(() =>
        {
            if (changed) return;
            changed = true;
            reentrantRoot.ConsoleCommand(["ActionTweaksConfig", "PreventMovingWhileCasting", "true"]);
        });
        var rejected = SetSetting(reentrantRoot, reentrantNode, reentrantField, "reentrant", ReadSetting(reentrantRoot, reentrantNode, reentrantField), true);
        Check(!(bool)Property(rejected, "Accepted") && reentrantNode.PreventMovingWhileCasting && persisted > 0,
            "user intervention during a runtime notification is retained and still triggers persistence");
    }
}
