using System;

namespace BossMod.CombatAssistance.Integrations;

// Sharing is explicitly limited to synchronous work on the owning thread.
// Nested observations invalidate the parent instead of restoring an older value.
internal sealed class ScopedObservation<T>(Func<T> read) where T : class
{
    private int depth;
    private int ownerThread;
    private T? value;

    public Scope Begin()
    {
        var thread = Environment.CurrentManagedThreadId;
        if (this.depth != 0 && this.ownerThread != thread)
            throw new InvalidOperationException("An observation scope cannot move between threads.");
        this.ownerThread = thread;
        this.depth++;
        this.value = null;
        return new(this);
    }

    public T Read()
    {
        if (this.depth == 0 || this.ownerThread != Environment.CurrentManagedThreadId)
            return read();
        return this.value ??= read();
    }

    public readonly struct Scope(ScopedObservation<T> owner) : IDisposable
    {
        public void Dispose()
        {
            owner.value = null;
            owner.depth--;
        }
    }
}
