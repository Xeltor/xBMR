// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// No party membership, action settings, hard target or persistent cache entries
// are changed. The native all-object range filter validates the external actor;
// GetCanTargets and CanUse still own visibility, LoS and action legality.
internal sealed class RsrExternalHealTargetScope : IDisposable
{
    private readonly Dictionary<float, List<IBattleChara>> cache;
    private readonly float key;
    private readonly bool existed;
    private readonly List<IBattleChara>? original;
    public bool Eligible { get; }

    public RsrExternalHealTargetScope(Assembly basic, Type dataCenter, object action, IBattleChara recipient)
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var info = action.GetType().GetProperty("TargetInfo", Instance)!.GetValue(action)!;
        var range = (float)info.GetType().GetProperty("Range", Instance)!.GetValue(info)!;
        this.cache = dataCenter.GetProperty("TargetsByRange", Static)!.GetValue(null) as Dictionary<float, List<IBattleChara>>
            ?? throw new InvalidOperationException("RSR range cache contract changed");
        this.key = MathF.Round(range, 0) + 1000f;
        this.existed = this.cache.TryGetValue(this.key, out this.original);
        var helper = basic.GetType("RotationSolver.Basic.Helpers.TargetHelper", true)!;
        var candidates = helper.GetMethod("GetTargetsByRange", Static)!.Invoke(null, [range, null]) as IEnumerable<IBattleChara>;
        this.Eligible = candidates?.Any(actor => actor.GameObjectId == recipient.GameObjectId) == true;
        if (this.Eligible) this.cache[this.key] = [recipient];
    }

    public void Dispose()
    {
        if (!this.Eligible) return;
        if (this.existed) this.cache[this.key] = this.original!;
        else this.cache.Remove(this.key);
    }
}
