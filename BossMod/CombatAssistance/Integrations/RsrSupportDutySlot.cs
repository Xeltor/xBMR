// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace BossMod.CombatAssistance.Integrations;

// A callback-local adapter for RSR's existing GeneralAbility extension slot.
// All other selectors forward to the original duty instance. The adapter is
// never installed in RSR's rotation catalogue or retained across its updater.
internal sealed class RsrSupportDutySlot
{
    private readonly object adapter;
    private readonly PropertyInfo currentDuty;
    private object? original;
    private bool entered;

    public RsrSupportDutySlot(PropertyInfo currentDuty, Func<object?[], bool> jobGeneral,
        Func<object?[], bool> supportFallback, Func<object?[], bool>? generalGcdFallback = null)
    {
        this.currentDuty = currentDuty;
        var type = currentDuty.PropertyType;
        var selectors = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.IsVirtual && !method.IsFinal && method.ReturnType == typeof(bool) &&
                (method.Name.EndsWith("Ability", StringComparison.Ordinal) || method.Name.EndsWith("GCD", StringComparison.Ordinal)))
            .ToDictionary(method => method.Name);
        if (!selectors.ContainsKey("GeneralAbility")) throw new MissingMethodException("RSR GeneralAbility slot");
        var factory = CreateFactory(type, selectors.Values.ToArray());
        this.adapter = factory((name, arguments) =>
        {
            if (this.original != null && RsrSupportActionProxy.Forward(this.original, selectors[name], arguments) is true)
                return true;
            if (name == "GeneralGCD") return generalGcdFallback?.Invoke(arguments) == true;
            if (name != "GeneralAbility") return false;
            // These are adjacent calls in RSR's Ability method. Preserve its
            // original duty -> job ordering, then offer support only on false.
            return jobGeneral(arguments) || supportFallback(arguments);
        });
    }

    public void Enter()
    {
        if (this.entered) throw new InvalidOperationException("Nested RSR support duty scope");
        this.original = this.currentDuty.GetValue(null);
        this.entered = true;
        this.currentDuty.SetValue(null, this.adapter);
    }

    public void Exit()
    {
        if (!this.entered) return;
        if (ReferenceEquals(this.currentDuty.GetValue(null), this.adapter))
            this.currentDuty.SetValue(null, this.original);
        this.original = null;
        this.entered = false;
    }

    internal static Func<object?[], bool> RemainingAbilities(object current, Type dataCenter)
    {
        const BindingFlags instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags shared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var custom = current.GetType();
        while (custom.FullName != "RotationSolver.Basic.Rotations.CustomRotation")
            custom = custom.BaseType ?? throw new MissingMemberException("RSR CustomRotation base");
        var general = current.GetType().GetMethod("GeneralAbility", instance) ?? throw new MissingMethodException("GeneralAbility");
        var potion = custom.GetMethod("UseMpPotion", shared) ?? throw new MissingMethodException("UseMpPotion");
        var roleAbility = custom.GetMethod("GeneralUsingAbility", instance) ?? throw new MissingMethodException("GeneralUsingAbility");
        var speed = current.GetType().GetMethod("SpeedAbility", instance) ?? throw new MissingMethodException("SpeedAbility");
        var role = dataCenter.GetProperty("Role", shared) ?? throw new MissingMemberException("Role");
        var auto = dataCenter.GetProperty("AutoStatus", shared) ?? throw new MissingMemberException("AutoStatus");
        var speedFlag = (Enum)Enum.Parse(auto.PropertyType, "Speed");
        return arguments =>
        {
            if (RsrSupportActionProxy.Forward(current, general, arguments) is true || potion.Invoke(null, arguments) is true) return true;
            object?[] roleArguments = [role.GetValue(null), arguments[0], null];
            if (RsrSupportActionProxy.Forward(current, roleAbility, roleArguments) is true)
            { arguments[1] = roleArguments[2]; return true; }
            return auto.GetValue(null) is Enum status && status.HasFlag(speedFlag)
                && RsrSupportActionProxy.Forward(current, speed, arguments) is true;
        };
    }

    internal static Func<Func<string, object?[], bool>, object> CreateFactory(Type baseType, MethodInfo[] selectors)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BossMod.SupportSlot"), AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("SupportSlot");
        var builder = module.DefineType("SupportSlot", TypeAttributes.Public | TypeAttributes.Sealed, baseType);
        var callbackType = typeof(Func<string, object?[], bool>);
        var callback = builder.DefineField("Callback", callbackType, FieldAttributes.Public);
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        var invoke = callbackType.GetMethod("Invoke")!;
        foreach (var method in selectors)
        {
            var parameters = method.GetParameters();
            if (method.IsGenericMethod || method.ReturnType != typeof(bool) ||
                parameters.Any(parameter => parameter.ParameterType.IsPointer ||
                    (!parameter.IsOut && parameter.ParameterType.IsByRef)))
                throw new InvalidOperationException("Unsupported RSR duty selector signature: " + method.Name);
            var implementation = builder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(bool), parameters.Select(parameter => parameter.ParameterType).ToArray());
            var il = implementation.GetILGenerator();
            var arguments = il.DeclareLocal(typeof(object[]));
            var result = il.DeclareLocal(typeof(bool));
            il.Emit(OpCodes.Ldc_I4, parameters.Length);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc, arguments);
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].IsOut) continue;
                il.Emit(OpCodes.Ldloc, arguments);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);
                if (parameters[i].ParameterType.IsValueType) il.Emit(OpCodes.Box, parameters[i].ParameterType);
                il.Emit(OpCodes.Stelem_Ref);
            }
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, callback);
            il.Emit(OpCodes.Ldstr, method.Name);
            il.Emit(OpCodes.Ldloc, arguments);
            il.Emit(OpCodes.Callvirt, invoke);
            il.Emit(OpCodes.Stloc, result);
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!parameters[i].IsOut) continue;
                var element = parameters[i].ParameterType.GetElementType()!;
                il.Emit(OpCodes.Ldarg, i + 1);
                il.Emit(OpCodes.Ldloc, arguments);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(element.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, element);
                il.Emit(OpCodes.Stobj, element);
            }
            il.Emit(OpCodes.Ldloc, result);
            il.Emit(OpCodes.Ret);
            builder.DefineMethodOverride(implementation, method);
        }
        var generated = builder.CreateType()!;
        var field = generated.GetField("Callback")!;
        return handler =>
        {
            var instance = Activator.CreateInstance(generated)!;
            field.SetValue(instance, handler);
            return instance;
        };
    }
}
