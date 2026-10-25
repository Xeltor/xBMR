// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using System;
using System.Linq;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Intercepts only a successful native CanUse. Accepted selections retain their
// original concrete BaseAction identity, target and downstream execution checks.
internal sealed class RsrMitigationSelectionLease : IDisposable
{
    private readonly object rotation;
    private readonly FieldInfo field;
    private readonly object originalLazy;
    private readonly object ownedLazy;
    private readonly RsrMitigationSelectionProxy proxy;
    private readonly object action;
    private readonly PropertyInfo preview;
    private readonly PropertyInfo forced;
    private readonly Func<object, bool> defer;
    private readonly Action<Exception> failure;

    private RsrMitigationSelectionLease(object rotation, FieldInfo field, object originalLazy, object ownedLazy,
        RsrMitigationSelectionProxy proxy, object action, PropertyInfo preview, PropertyInfo forced,
        Func<object, bool> defer, Action<Exception> failure)
    {
        this.rotation = rotation; this.field = field; this.originalLazy = originalLazy; this.ownedLazy = ownedLazy;
        this.proxy = proxy; this.action = action; this.preview = preview; this.forced = forced;
        this.defer = defer; this.failure = failure;
    }

    public static RsrMitigationSelectionLease Acquire(object rotation, string propertyName, uint expectedId,
        Func<object, bool> defer, Action<Exception> failure)
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var property = rotation.GetType().GetProperty(propertyName, Instance) ?? throw new MissingMemberException(propertyName);
        FieldInfo? field = null;
        for (var type = rotation.GetType(); type != null && field == null; type = type.BaseType)
            field = type.GetField("_" + propertyName + "Creator", Instance | BindingFlags.DeclaredOnly);
        if (field == null || !property.PropertyType.IsInterface || field.FieldType != typeof(Lazy<>).MakeGenericType(property.PropertyType))
            throw new InvalidOperationException("Generated mitigation action factory changed");
        var originalLazy = field.GetValue(rotation) ?? throw new InvalidOperationException("Missing action factory");
        var action = property.GetValue(rotation) ?? throw new InvalidOperationException("Missing mitigation action");
        var preview = property.PropertyType.GetProperty("ActionPreview", Static);
        var forced = property.PropertyType.GetProperty("ForceEnable", Static);
        if (action.GetType().GetProperty("AdjustedID", Instance)?.GetValue(action) is not uint id || id != expectedId ||
            preview?.PropertyType != typeof(bool) || forced?.PropertyType != typeof(bool) ||
            property.PropertyType.GetMethod("CanUse")?.GetParameters() is not { Length: 12 } parameters || !parameters[0].IsOut)
            throw new InvalidOperationException("Mitigation CanUse contract changed");
        var proxy = (RsrMitigationSelectionProxy)DispatchProxy.Create(property.PropertyType, typeof(RsrMitigationSelectionProxy));
        proxy.Original = action;
        var factory = RsrActionFactory.Create(property.PropertyType, proxy);
        var ownedLazy = Activator.CreateInstance(field.FieldType, factory) ?? throw new InvalidOperationException("Missing mitigation factory");
        var lease = new RsrMitigationSelectionLease(rotation, field, originalLazy, ownedLazy, proxy, action, preview, forced, defer, failure);
        proxy.Intercept = lease.Invoke;
        try { field.SetValue(rotation, ownedLazy); return lease; }
        catch { lease.Dispose(); throw; }
    }

    private object? Invoke(MethodInfo method, object?[]? arguments)
    {
        var result = RsrSupportActionProxy.Forward(this.action, method, arguments);
        if (method.Name != "CanUse" || result is not true || arguments is not { Length: 12 } ||
            !ReferenceEquals(arguments[0], this.action)) return result;
        try
        {
            if (ReferenceEquals(this.field.GetValue(this.rotation), this.ownedLazy) &&
                this.preview.GetValue(null) is false && this.forced.GetValue(null) is false && this.defer(this.action))
            {
                arguments[0] = null;
                return false;
            }
        }
        catch (Exception ex) { this.failure(ex); }
        return result;
    }

    public void Dispose()
    {
        this.proxy.Intercept = null;
        if (ReferenceEquals(this.field.GetValue(this.rotation), this.ownedLazy)) this.field.SetValue(this.rotation, this.originalLazy);
    }
}

// The duty slot is reached in normal priority order AFTER emergency and healing.
// Forward the original duty first; only its false result enables the following
// native job DefenseArea selector. Every later selector cancels that scope.
internal sealed class RsrMitigationDutyScope
{
    private readonly PropertyInfo property;
    private readonly object adapter;
    private object? original;
    private bool entered;
    public bool NativeDefense { get; private set; }
    public RsrMitigationDutyScope(PropertyInfo property)
    {
        this.property = property;
        var selectors = property.PropertyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.IsVirtual && !method.IsFinal && method.ReturnType == typeof(bool) &&
                (method.Name.EndsWith("Ability", StringComparison.Ordinal) || method.Name.EndsWith("GCD", StringComparison.Ordinal))).ToDictionary(method => method.Name);
        if (!selectors.ContainsKey("DefenseAreaAbility")) throw new MissingMethodException("DefenseAreaAbility");
        this.adapter = RsrSupportDutySlot.CreateFactory(property.PropertyType, selectors.Values.ToArray())((name, arguments) =>
        {
            this.NativeDefense = false;
            if (this.original != null && RsrSupportActionProxy.Forward(this.original, selectors[name], arguments) is true) return true;
            this.NativeDefense = this.entered && name == "DefenseAreaAbility";
            return false;
        });
    }
    public void Enter()
    {
        if (this.entered) throw new InvalidOperationException("Nested mitigation scope");
        this.original = this.property.GetValue(null);
        this.entered = true;
        this.property.SetValue(null, this.adapter);
    }
    public void Exit()
    {
        this.NativeDefense = false;
        if (!this.entered) return;
        if (ReferenceEquals(this.property.GetValue(null), this.adapter)) this.property.SetValue(null, this.original);
        this.original = null;
        this.entered = false;
    }
}
