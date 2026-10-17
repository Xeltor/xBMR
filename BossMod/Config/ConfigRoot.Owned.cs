namespace BossMod;

public sealed partial class ConfigRoot
{
    internal sealed record OwnedFieldState(object? Value, long Revision, string? LeaseId);
    internal sealed record OwnedFieldWrite(bool Accepted, string Reason, long Revision);
    private sealed record FieldWatch(object? Value, long Revision);
    private readonly Dictionary<(ConfigNode node, ConfigFieldMetadata field), FieldWatch> _ownedFieldWatches = [];
    private long _ownedRevision;
    private long _ownedExternalRevision;
    [ThreadStatic] private static HashSet<ConfigRoot>? _ownedNotifications;
    private readonly object _saveGate = new();

    // Exact generated metadata, never a consumer-supplied reflected member or shortened name.
    internal bool TryResolveOwnedField(string nodeName, string fieldName, out ConfigNode? node, out ConfigFieldMetadata? field)
    {
        node = _nodes.Values.SingleOrDefault(n => n.GetType().FullName == nodeName);
        field = node == null ? null : GeneratedConfigMetadata.Get(node).FieldsByName.GetValueOrDefault(fieldName);
        return field is { Serializable: true };
    }

    internal OwnedFieldState ReadOwnedField(ConfigNode node, ConfigFieldMetadata field)
    {
        lock (_transient)
        {
            var watch = ObserveOwnedField(node, field);
            return new(watch.Value, watch.Revision, _transient.GetValueOrDefault((node, field)).Owner);
        }
    }

    private FieldWatch ObserveOwnedField(ConfigNode node, ConfigFieldMetadata field)
    {
        var key = (node, field);
        var value = field.Getter(node);
        if (!_ownedFieldWatches.TryGetValue(key, out var watch))
            return _ownedFieldWatches[key] = new(value, ++_ownedRevision);
        if (!Equals(value, watch.Value))
        {
            _ownedFieldWatches[key] = watch = new(value, ++_ownedRevision);
            ++_ownedExternalRevision;
            if (_transient.TryGetValue(key, out var entry) && entry.Owner != null && !Equals(value, entry.Value))
            {
                _transient.Remove(key);
                ++_transientVersion;
            }
        }
        return watch;
    }

    private void RefreshOwnedFields(ConfigNode node)
    {
        foreach (var key in _ownedFieldWatches.Keys.Where(k => k.node == node).ToArray())
            ObserveOwnedField(key.node, key.field);
    }

    private void ExplicitOwnedFieldIntent(ConfigNode node, ConfigFieldMetadata field)
    {
        ++_ownedExternalRevision;
        if (_ownedFieldWatches.ContainsKey((node, field)))
            _ownedFieldWatches[(node, field)] = new(field.Getter(node), ++_ownedRevision);
    }

    internal OwnedFieldWrite SetOwnedField(ConfigNode node, ConfigFieldMetadata field, string leaseId,
        long expectedRevision, object? expectedValue, object value)
    {
        long revision;
        lock (_transient)
        {
            var watch = ObserveOwnedField(node, field);
            if (watch.Revision != expectedRevision || !Equals(watch.Value, expectedValue))
                return new(false, "changed-setting", watch.Revision);
            var key = (node, field);
            var exists = _transient.TryGetValue(key, out var entry);
            if (exists && entry.Owner != leaseId)
                return new(false, entry.Owner == null ? "legacy-transient-active" : "other-owner", watch.Revision);
            if (!field.FieldType.IsInstanceOfType(value) || string.IsNullOrEmpty(leaseId) || !exists && _transient.Count >= 64)
                return new(false, "invalid-value-or-limit", watch.Revision);
            var original = exists ? entry.Original : watch.Value;
            field.Setter(node, value);
            revision = ++_ownedRevision;
            _ownedFieldWatches[key] = new(value, revision);
            _transient[key] = new(original, value, leaseId, revision);
            ++_transientVersion;
        }
        NotifyOwnedRuntimeChange(node);
        return OwnsField(node, field, leaseId, revision) ? new(true, "applied", revision)
            : new(false, "changed-during-notification", ReadOwnedField(node, field).Revision);
    }

    internal bool OwnsField(ConfigNode node, ConfigFieldMetadata field, string leaseId, long revision)
    {
        lock (_transient)
        {
            ObserveOwnedField(node, field);
            return _transient.TryGetValue((node, field), out var entry) && entry.Owner == leaseId && entry.Revision == revision;
        }
    }

    internal bool ReleaseOwnedField(ConfigNode node, ConfigFieldMetadata field, string leaseId, long revision)
    {
        lock (_transient)
        {
            ObserveOwnedField(node, field);
            var key = (node, field);
            if (!_transient.TryGetValue(key, out var entry) || entry.Owner != leaseId || entry.Revision != revision)
                return false;
            if (!Equals(field.Getter(node), entry.Value))
                return false;
            field.Setter(node, entry.Original);
            _transient.Remove(key);
            _ownedFieldWatches[key] = new(entry.Original, ++_ownedRevision);
            ++_transientVersion;
        }
        NotifyOwnedRuntimeChange(node);
        return true;
    }

    // Runtime consumers receive their usual notification. A borrowed value must not trigger a
    // redundant disk write; any unrelated user save still serializes the original value above.
    internal void NotifyOwnedRuntimeChange(ConfigNode node)
    {
        long before;
        lock (_transient) before = _ownedExternalRevision;
        var notifications = _ownedNotifications ??= [];
        var added = notifications.Add(this);
        try { node.Modified.Fire(); }
        finally
        {
            if (added)
                notifications.Remove(this);
        }
        bool externalChange;
        lock (_transient)
        {
            RefreshOwnedFields(node);
            externalChange = before != _ownedExternalRevision;
        }
        if (added && externalChange)
            Modified.Fire(); // a user/foreign write during notification must still be persisted
    }

    private void NotifyPersistentChange()
    {
        if (_ownedNotifications?.Contains(this) != true)
            Modified.Fire();
    }
}
