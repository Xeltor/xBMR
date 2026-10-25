using BossMod.CombatAssistance.Integrations;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

public static partial class Checks
{
    private static void RsrFactoryReuse()
    {
        var selectors = typeof(ConcurrentFactoryDuty).GetMethods().Where(m => m.DeclaringType == typeof(ConcurrentFactoryDuty)).ToArray();
        var before = GeneratedDutyAssemblies();
        var factories = new Func<Func<string, object?[], bool>, object>[16];
        Parallel.For(0, factories.Length, i => factories[i] = RsrSupportDutySlot.CreateFactory(typeof(ConcurrentFactoryDuty), selectors));
        Check(factories.All(factory => ReferenceEquals(factory, factories[0])) && GeneratedDutyAssemblies() == before + 1,
            "Concurrent adapter setup emits exactly one metadata factory per provider base type.");
        Check(ReferenceEquals(factories[0], RsrSupportDutySlot.CreateFactory(typeof(ConcurrentFactoryDuty), selectors.Reverse().ToArray())),
            "Equivalent selector sets reuse the same factory regardless of reflection enumeration order.");
        var instances = factories.Select((factory, index) => (ConcurrentFactoryDuty)factory((_, args) =>
        { args[1] = index; return true; })).ToArray();
        for (var i = 0; i < instances.Length; ++i)
            Check(instances[i].GeneralAbility(null, out var value) && Equals(value, i)
                && instances[i].GetType() == instances[0].GetType() && (i == 0 || !ReferenceEquals(instances[i], instances[0])),
                "A shared emitted type retains separate adapter objects and independent callbacks.");
        Check(instances[0].GetType().Assembly.IsCollectible, "Cached adapter code remains collectible with its provider.");
        var mismatched = false;
        try { _ = RsrSupportDutySlot.CreateFactory(typeof(ConcurrentFactoryDuty), selectors[..1]); }
        catch (InvalidOperationException) { mismatched = true; }
        Check(mismatched && ReferenceEquals(factories[0], RsrSupportDutySlot.CreateFactory(typeof(ConcurrentFactoryDuty), selectors)),
            "A changed selector contract cannot borrow or corrupt a cached factory.");

        var unsupported = false;
        before = GeneratedDutyAssemblies();
        try { _ = RsrSupportDutySlot.CreateFactory(typeof(UnsupportedFactoryDuty), [typeof(UnsupportedFactoryDuty).GetMethod("GeneralAbility")!]); }
        catch (InvalidOperationException) { unsupported = true; }
        Check(unsupported && GeneratedDutyAssemblies() == before, "Unsupported by-ref contracts fail before emitting an assembly.");
        var recovered = RsrSupportDutySlot.CreateFactory(typeof(UnsupportedFactoryDuty), [typeof(UnsupportedFactoryDuty).GetMethod("ValidAbility")!]);
        Check(recovered != null, "A failed contract check does not poison a subsequent supported factory.");

        var property = typeof(RsrSupportDutyTests).GetProperty(nameof(RsrSupportDutyTests.CurrentDuty))!;
        var warmup = new RsrSupportDutySlot(property, _ => false, _ => false);
        warmup.Enter();
        var firstType = RsrSupportDutyTests.CurrentDuty!.GetType();
        warmup.Exit();
        var clock = Stopwatch.StartNew();
        before = GeneratedDutyAssemblies();
        for (var i = 0; i < 128; ++i)
        {
            var instanceValue = i;
            var slot = new RsrSupportDutySlot(property, args => { args[1] = instanceValue; return true; }, _ => false);
            slot.Enter();
            var current = RsrSupportDutyTests.CurrentDuty!;
            Check(current.GetType() == firstType && current.GeneralAbility(null!, out var value) && Equals(value, instanceValue),
                "Release/reacquire reuses code without retaining a prior adapter callback.");
            slot.Exit();
            Check(RsrSupportDutyTests.CurrentDuty == null, "Each reacquired adapter restores its own original duty value.");
        }
        Check(GeneratedDutyAssemblies() == before, "128 reacquisitions emit no additional duty assemblies.");
        Console.WriteLine($"RSR duty adapter: 128 reacquisitions in {clock.Elapsed.TotalMilliseconds:f3}ms, no additional emitted assemblies; synthetic only.");

        var first = new RsrMitigationSelectionTests.Action();
        var second = new RsrMitigationSelectionTests.Action();
        var factoryA = (Func<RsrMitigationSelectionTests.IAction>)RsrActionFactory.Create(typeof(RsrMitigationSelectionTests.IAction), first);
        var factoryB = (Func<RsrMitigationSelectionTests.IAction>)RsrActionFactory.Create(typeof(RsrMitigationSelectionTests.IAction), second);
        Check(ReferenceEquals(new Lazy<RsrMitigationSelectionTests.IAction>(factoryA).Value, first)
            && ReferenceEquals(new Lazy<RsrMitigationSelectionTests.IAction>(factoryB).Value, second)
            && !ReferenceEquals(factoryA.Target, factoryB.Target), "Typed Lazy factories keep exact per-lease action identities.");
        Check(factoryA.Method.Module == typeof(RsrActionFactory).Module && !factoryA.Method.Module.Assembly.IsDynamic,
            "Constant Lazy delegates execute existing compiled code, with no per-lease expression compilation.");

        var weak = CollectibleFactory();
        for (var i = 0; i < 10 && (weak.Provider.IsAlive || weak.Adapter.IsAlive); ++i)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Check(!weak.Provider.IsAlive && !weak.Adapter.IsAlive, "The weak factory cache does not retain an unloaded provider or its generated adapter.");
    }

    private static int GeneratedDutyAssemblies() => AppDomain.CurrentDomain.GetAssemblies().Count(a => a.IsDynamic && a.GetName().Name == "BossMod.SupportSlot");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Provider, WeakReference Adapter) CollectibleFactory()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CollectibleRsrFactoryFixture"), AssemblyBuilderAccess.RunAndCollect);
        var builder = assembly.DefineDynamicModule("Fixture").DefineType("FixtureDuty", TypeAttributes.Public);
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        var method = builder.DefineMethod("GeneralAbility", MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(bool), [typeof(object), typeof(object).MakeByRefType()]);
        method.DefineParameter(2, ParameterAttributes.Out, "action");
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Stind_Ref); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ret);
        var type = builder.CreateType()!;
        var factory = RsrSupportDutySlot.CreateFactory(type, [type.GetMethod("GeneralAbility")!]);
        var instance = factory(static (_, args) => { args[1] = null; return false; });
        return (new(type), new(instance.GetType()));
    }
}

public class ConcurrentFactoryDuty
{
    public virtual bool GeneralAbility(object? gcd, out object? action) { action = null; return false; }
    public virtual bool DefenseAreaAbility(object? gcd, out object? action) { action = null; return false; }
}

public class UnsupportedFactoryDuty
{
    public virtual bool GeneralAbility(ref object? action) => false;
    public virtual bool ValidAbility(out object? action) { action = null; return false; }
}
