using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

internal sealed class RsrCastControlEligibility
{
    private Assembly? assembly;
    private Func<IBattleChara, bool>? attackable;
    private Func<IBattleChara, bool>? stunImmune;

    internal void Prepare(Assembly basic)
    {
        if (this.assembly == basic) return;
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var helper = basic.GetType("RotationSolver.Basic.Helpers.ObjectHelper", true)!;
        this.attackable = helper.GetMethod("IsAttackable", Flags)?.CreateDelegate<Func<IBattleChara, bool>>()
            ?? throw new MissingMethodException("RSR target immunity contract unavailable");
        this.stunImmune = helper.GetMethod("IsOCStunImmuneTarget", Flags)?.CreateDelegate<Func<IBattleChara, bool>>()
            ?? throw new MissingMethodException("RSR known stun immunity contract unavailable");
        this.assembly = basic;
    }

    internal bool Attackable(IBattleChara target) => this.attackable?.Invoke(target) == true;
    internal bool KnownStunImmune(IBattleChara target) => this.stunImmune?.Invoke(target) != false;
}
