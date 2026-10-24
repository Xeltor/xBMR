// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace BossMod.CombatAssistance.Integrations;

// DispatchProxy supplies the installed RSR interface without a compile-time
// reference to RSR. Only the selected instance's generated action getter is
// leased; no method body, assembly file, or global action registry is patched.
internal class RsrSupportActionProxy : DispatchProxy
{
    private object original = null!;
    private Func<MethodInfo, object?[]?, object?>? intercept;
    internal bool Active { get; set; }

    public RsrSupportActionProxy() { }

    internal void Initialize(object originalAction, Func<MethodInfo, object?[]?, object?> callback)
    {
        this.original = originalAction;
        this.intercept = callback;
        this.Active = true;
    }

    internal void Release() => this.intercept = null;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null) throw new MissingMethodException("RSR support action invocation");
        var result = this.Active && this.intercept is { } callback
            ? callback(targetMethod, args)
            : Forward(this.original, targetMethod, args);

        // BaseAction.CanUse normally returns itself through the out argument.
        // Retain the proxy so RSR's later Use call reaches our execution guard.
        if (this.Active && this.intercept != null && targetMethod.Name == "CanUse" && result is true &&
            args is { Length: > 0 } && ReferenceEquals(args[0], this.original)) args[0] = this;
        return result;
    }

    internal static object? Forward(object original, MethodInfo method, object?[]? arguments)
    {
        try
        {
            return method.Invoke(original, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}

internal sealed class RsrSupportActionLease : IDisposable
{
    private readonly object rotation;
    private readonly FieldInfo field;
    private readonly object originalLazy;
    private readonly object ownedLazy;
    private readonly RsrSupportActionProxy proxy;
    private bool disposed;

    private RsrSupportActionLease(object rotation, FieldInfo field, object originalLazy, object ownedLazy,
        RsrSupportActionProxy proxy, object originalAction)
    {
        this.rotation = rotation;
        this.field = field;
        this.originalLazy = originalLazy;
        this.ownedLazy = ownedLazy;
        this.proxy = proxy;
        this.OriginalAction = originalAction;
    }

    public object OriginalAction { get; }
    public object Action => this.proxy;
    public bool Owned => ReferenceEquals(this.field.GetValue(this.rotation), this.ownedLazy);

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (!ReferenceEquals(this.field.GetValue(this.rotation), this.originalLazy))
            throw new InvalidOperationException("RSR support action ownership changed");
        this.field.SetValue(this.rotation, this.ownedLazy);
        this.proxy.Active = true;
    }

    public void Suspend()
    {
        this.proxy.Active = false;
        if (this.Owned) this.field.SetValue(this.rotation, this.originalLazy);
    }

    public static RsrSupportActionLease Acquire(object rotation, string propertyName,
        Func<object, MethodInfo, object?[]?, object?> intercept)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var property = rotation.GetType().GetProperty(propertyName, Flags)
            ?? throw new MissingMemberException(rotation.GetType().FullName, propertyName);
        FieldInfo? field = null;
        for (var type = rotation.GetType(); type != null && field == null; type = type.BaseType)
            field = type.GetField("_" + propertyName + "Creator", Flags | BindingFlags.DeclaredOnly);

        if (field == null || !field.FieldType.IsGenericType ||
            field.FieldType.GetGenericTypeDefinition() != typeof(Lazy<>) ||
            field.FieldType.GenericTypeArguments[0] != property.PropertyType || !property.PropertyType.IsInterface)
            throw new MissingMemberException("RSR generated Lazy action contract changed: " + propertyName);

        var originalLazy = field.GetValue(rotation) ?? throw new InvalidOperationException("Missing RSR action factory");
        var originalAction = property.GetValue(rotation) ?? throw new InvalidOperationException("Missing RSR action");
        var proxy = (RsrSupportActionProxy)DispatchProxy.Create(property.PropertyType, typeof(RsrSupportActionProxy));
        proxy.Initialize(originalAction, (method, args) => intercept(originalAction, method, args));
        var factoryType = typeof(Func<>).MakeGenericType(property.PropertyType);
        var factory = Expression.Lambda(factoryType, Expression.Convert(Expression.Constant(proxy), property.PropertyType)).Compile();
        var ownedLazy = Activator.CreateInstance(field.FieldType, factory)
            ?? throw new InvalidOperationException("Could not construct RSR action lease");
        var lease = new RsrSupportActionLease(rotation, field, originalLazy, ownedLazy, proxy, originalAction);
        try
        {
            field.SetValue(rotation, ownedLazy);
            if (!ReferenceEquals(property.GetValue(rotation), proxy))
                throw new InvalidOperationException("RSR action getter did not adopt the leased action");
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        // Previously selected proxies can outlive the getter lease. They must
        // immediately become transparent when ownership ends.
        this.disposed = true;
        this.proxy.Release();
        if (this.Owned) this.field.SetValue(this.rotation, this.originalLazy);
    }
}
