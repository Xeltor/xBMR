using Dalamud.Plugin.Services;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// One owner for RSR's original selection/delivery callback. Native policies
// register independent synchronous scopes; none installs a second handler or
// changes callback ordering. An inactive owner is transparent and cannot start AI.
internal sealed class RsrSelectionScopeOwner(RsrRuntimeAccess runtime, Func<bool> enabled, IPluginLog log) : IDisposable
{
    private readonly List<Registration> _registrations = [];
    private RsrSupportCallbackLease? _lease;
    private Assembly? _provider;
    private Assembly? _yieldedProvider;
    private DateTime _nextLog;
    private bool _disposed;
    internal string Status { get; private set; } = "inactive";
    internal bool Available => !_disposed && _lease?.Owned == true && enabled();

    internal Registration Register(Func<IDisposable?> enter, Action<Exception> failure, int order = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var registration = new Registration(this, enter, failure, order);
        _registrations.Add(registration);
        _registrations.Sort((a, b) => a.Order.CompareTo(b.Order));
        return registration;
    }

    internal void Refresh()
    {
        if (_disposed || !enabled() || _registrations.Count == 0)
        {
            Release(); _yieldedProvider = null; Status = "inactive"; return;
        }
        Assembly? attemptedProvider = null;
        try
        {
            if (!runtime.TryGetSupportContext(out var plugin, out _, out _))
            {
                Release(); _yieldedProvider = null; Status = "waiting for RSR"; return;
            }
            attemptedProvider = plugin;
            if (ReferenceEquals(plugin, _provider) && _lease?.Owned == true) return;
            if (ReferenceEquals(plugin, _provider) && _lease != null)
            {
                // A later event owner wins. Do not repeatedly replace a foreign
                // callback while the same plugin instance remains active.
                _yieldedProvider = plugin;
                Release();
            }
            if (ReferenceEquals(plugin, _yieldedProvider))
            {
                Status = "RSR callback ownership changed; native policies yielded";
                return;
            }
            Release();
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var ecommons = RsrRuntimeAccess.ResolveReferencedAssemblyInLoadContext(plugin, "ECommons")
                ?? throw new MissingMemberException("RSR scoped ECommons assembly");
            var services = ecommons.GetType("ECommons.DalamudServices.Svc", true)!;
            var framework = services.GetProperty("Framework", flags)?.GetValue(null)
                ?? services.GetField("Framework", flags)?.GetValue(null)
                ?? throw new MissingMemberException("RSR scoped framework");
            _lease = RsrSupportCallbackLease.Acquire(framework, RsrSupportCallbackLease.ResolveUpdate(plugin), Enter, Failed);
            _provider = plugin;
            _yieldedProvider = null;
            Status = "original RSR callback owned; independent native scopes ready";
        }
        catch (Exception error)
        {
            _yieldedProvider = attemptedProvider;
            Release();
            Failed(error);
        }
    }

    private IDisposable? Enter() => !_disposed && enabled()
        ? EnterAll(_registrations.Where(r => !r.Disposed).Select(r => (r.Enter, r.Failure)).ToArray()) : null;

    internal static IDisposable? EnterAll(IEnumerable<(Func<IDisposable?> Enter, Action<Exception> Failure)> registrations)
    {
        var entered = new List<(IDisposable Scope, Action<Exception> Failure)>();
        try
        {
            foreach (var registration in registrations)
            {
                try
                {
                    if (registration.Enter() is { } scope) entered.Add((scope, registration.Failure));
                }
                catch (Exception error) { registration.Failure(error); }
            }
            return entered.Count == 0 ? null : new EnteredScopes(entered);
        }
        catch
        {
            // Even a failing failure reporter must unwind preceding writes.
            new EnteredScopes(entered).Dispose();
            throw;
        }
    }

    private sealed class EnteredScopes(List<(IDisposable Scope, Action<Exception> Failure)> scopes) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            List<Exception>? reportingFailures = null;
            for (var i = scopes.Count - 1; i >= 0; --i)
            {
                try { scopes[i].Scope.Dispose(); }
                catch (Exception error)
                {
                    try { scopes[i].Failure(error); }
                    catch (Exception reportingError) { (reportingFailures ??= []).Add(reportingError); }
                }
            }
            if (reportingFailures != null) throw new AggregateException("Native RSR scope failure reporting failed", reportingFailures);
        }
    }

    private void Failed(Exception error)
    {
        Status = "RSR callback integration unavailable; original rotation retained";
        if (DateTime.UtcNow < _nextLog) return;
        _nextLog = DateTime.UtcNow.AddSeconds(10);
        log.Verbose(error, Status);
    }

    private void Release()
    {
        var lease = _lease; _lease = null; _provider = null;
        lease?.Dispose();
    }

    private void Remove(Registration registration)
    {
        _registrations.Remove(registration);
        if (_registrations.Count == 0)
        {
            try { Release(); }
            catch (Exception error) { Failed(error); }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        try { Release(); }
        catch (Exception error) { Failed(error); }
        _registrations.Clear();
        _yieldedProvider = null;
        Status = "disposed";
    }

    internal sealed class Registration(RsrSelectionScopeOwner owner, Func<IDisposable?> enter,
        Action<Exception> failure, int order) : IDisposable
    {
        internal Func<IDisposable?> Enter { get; } = enter;
        internal Action<Exception> Failure { get; } = failure;
        internal int Order { get; } = order;
        internal bool Disposed { get; private set; }
        internal bool Owned => !Disposed && !owner._disposed && owner._lease?.Owned == true;
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            owner.Remove(this);
        }
    }
}
