// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using Dalamud.Plugin.Services;
using System;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Preserve the callback's exact place within RSR's plugin-scoped event. Adding
// a second framework handler after RSR would be too late to validate execution.
internal sealed class RsrSupportCallbackLease : IDisposable
{
    private readonly object framework;
    private readonly FieldInfo field;
    private readonly IFramework.OnUpdateDelegate original;
    private readonly IFramework.OnUpdateDelegate wrapper;
    private readonly Func<IDisposable?> enter;
    private readonly Action<Exception> failure;
    private bool disposed;

    private RsrSupportCallbackLease(object framework, FieldInfo field, IFramework.OnUpdateDelegate original,
        Func<IDisposable?> enter, Action<Exception> failure)
    {
        this.framework = framework;
        this.field = field;
        this.original = original;
        this.enter = enter;
        this.failure = failure;
        this.wrapper = this.Update;
    }

    public bool Owned => Contains(this.field.GetValue(this.framework) as Delegate, this.wrapper);

    internal static MethodInfo ResolveUpdate(Assembly plugin)
    {
        var updater = plugin.GetType("RotationSolver.Updaters.MajorUpdater", true)!;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        // 7.5.7.3 segmented callbacks; 7.5.7.6 consolidated observation,
        // selection and delivery into OnUpdate. Both bracket the original once.
        foreach (var name in new[] { "RSRActivatedCoreUpdate", "OnUpdate" })
        {
            var method = updater.GetMethod(name, flags, [typeof(IFramework)]);
            if (method?.ReturnType == typeof(void)) return method;
        }
        throw new MissingMethodException("RSR framework selection/execution callback changed");
    }

    public static RsrSupportCallbackLease Acquire(object framework, MethodInfo originalMethod,
        Func<IDisposable?> enter, Action<Exception> failure)
    {
        var field = framework.GetType().GetField("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("RSR scoped framework Update event");
        if (field.FieldType != typeof(IFramework.OnUpdateDelegate))
            throw new InvalidOperationException("RSR scoped framework event type changed");
        var original = originalMethod.CreateDelegate<IFramework.OnUpdateDelegate>();
        var lease = new RsrSupportCallbackLease(framework, field, original, enter, failure);
        var handlers = field.GetValue(framework) as Delegate;
        field.SetValue(framework, Replace(handlers, original, lease.wrapper));
        if (!lease.Owned) throw new InvalidOperationException("RSR callback was not installed");
        return lease;
    }

    private void Update(IFramework value)
    {
        IDisposable? scope = null;
        if (!this.disposed)
        {
            try { scope = this.enter(); }
            catch (Exception ex) { this.failure(ex); }
        }

        try
        {
            // Even a failed ownership acquisition must run the original once.
            this.original(value);
        }
        finally
        {
            try { scope?.Dispose(); }
            catch (Exception ex) { this.failure(ex); }
        }
    }

    public void Dispose()
    {
        this.disposed = true;
        var handlers = this.field.GetValue(this.framework) as Delegate;
        // If RSR's scoped framework has already been disposed, its event is
        // empty. Never resurrect an unloaded plugin's callback.
        if (Contains(handlers, this.wrapper))
            this.field.SetValue(this.framework, Replace(handlers, this.wrapper, this.original));
    }

    internal static Delegate? Replace(Delegate? handlers, Delegate original, Delegate replacement)
    {
        if (handlers == null) throw new InvalidOperationException("RSR callback list is empty");
        var invocation = handlers.GetInvocationList();
        var matches = 0;
        Delegate? result = null;
        foreach (var handler in invocation)
        {
            if (handler.Equals(original))
            {
                matches++;
                result = Delegate.Combine(result, replacement);
            }
            else result = Delegate.Combine(result, handler);
        }

        if (matches != 1) throw new InvalidOperationException("Expected exactly one RSR callback");
        return result;
    }

    private static bool Contains(Delegate? handlers, Delegate callback)
    {
        if (handlers == null) return false;
        foreach (var handler in handlers.GetInvocationList())
            if (handler.Equals(callback)) return true;
        return false;
    }
}
