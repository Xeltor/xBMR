namespace BossMod.CombatAssistance.Integrations;

// A separate holder belongs to every lease. The factory executes compiled
// generic code instead of compiling a new constant expression on reacquisition.
internal static class RsrActionFactory
{
    private sealed class Constant<T>
    {
        private readonly T _value;
        public Constant(object value) => _value = (T)value;
        public T Read() => _value;
    }

    internal static Delegate Create(Type actionType, object action)
    {
        if (!actionType.IsInterface || actionType.ContainsGenericParameters || !actionType.IsInstanceOfType(action))
            throw new ArgumentException("RSR action factory requires the exact closed action interface.", nameof(actionType));
        var holderType = typeof(Constant<>).MakeGenericType(actionType);
        var holder = Activator.CreateInstance(holderType, action)!;
        return holderType.GetMethod(nameof(Constant<object>.Read))!
            .CreateDelegate(typeof(Func<>).MakeGenericType(actionType), holder);
    }
}
