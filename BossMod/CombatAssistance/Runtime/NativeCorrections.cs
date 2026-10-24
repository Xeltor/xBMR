using BossMod.CombatAssistance.Config;
using BossMod.Components;

namespace BossMod.CombatAssistance.Runtime;

internal sealed class NativeCorrections(BossModuleManager modules) : IDisposable
{
    private readonly Dictionary<uint, OwnedModuleEnablement.LocalScope> _hallways = [];
    private static readonly ConcurrentDictionary<Type, bool> InheritedFateFrames = new();
    private static readonly ConcurrentDictionary<Type, bool> BaseStackHandlers = new();

    internal void UpdateHallways(bool enabled)
    {
        if (!enabled)
        {
            Dispose();
            return;
        }
        foreach (var entry in DawntrailHallwayModules.Entries)
        {
            // Keep an invalidated scope recorded until the feature is disabled.
            // Otherwise an explicit user change could be reacquired next frame.
            if (_hallways.ContainsKey(entry.ObjectId)
                || BossModuleRegistry.FindByOID(entry.ObjectId)?.ModuleType.FullName != entry.TypeName
                || modules.LoadedModules.Any(m => m.PrimaryActor.OID == entry.ObjectId && !m.CanTemporarilyDisable()))
                continue;
            if (BossModuleManager.Config.AcquireModuleScope(entry.ObjectId, false) is { } scope)
                _hallways.Add(entry.ObjectId, scope);
        }
    }

    public void Dispose()
    {
        foreach (var scope in _hallways.Values)
            scope.Dispose();
        _hallways.Clear();
    }

    // Only called by the inherited SimpleBossModule hint calculation. Authored
    // encounter bounds and component constraints remain untouched.
    internal static void AlignDefaultFateFrame(SimpleBossModule module, Actor actor, AIHints hints)
    {
        if (!hints.NativeFateFrameAlignmentRequested || module is not OpenWorldFate
            || !actor.InCombat || !ReferenceEquals(hints.PathfindMapBounds, AIHints.DefaultBounds))
            return;
        // One metadata check per concrete module type preserves authored overrides.
        // This is inheritance detection, not access to another plugin's private state.
        if (!InheritedFateFrames.GetOrAdd(module.GetType(), type =>
            type.GetMethod("UpdateModule", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.DeclaringType == typeof(SimpleBossModule)
            && type.GetMethod("CalculateModuleAIHints", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.DeclaringType == typeof(SimpleBossModule)))
            return;
        var center = hints.PathfindMapCenter;
        var (entry, bitmap) = module.Obstacles.Find(new(center.X, actor.PosRot.Y, center.Z));
        if (entry == null || bitmap == null || bitmap.PixelSize != hints.PathfindMapBounds.MapResolution
            || !ReferenceEquals(bitmap, hints.PathfindMapObstacles.Bitmap))
            return;
        if (AlignTerrainFrame(center, entry.Origin, bitmap.PixelSize, hints.PathfindMapBounds.Radius) is not { } alignment)
            return;
        hints.PathfindMapCenter = alignment.Center;
        hints.PathfindMapObstacles = new(bitmap, new(alignment.X - alignment.Half, alignment.Z - alignment.Half,
            alignment.X + alignment.Half, alignment.Z + alignment.Half));
    }

    internal static (WPos Center, int X, int Z, int Half)? AlignTerrainFrame(WPos center, WPos origin, float resolution, float radius)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Z) || !float.IsFinite(origin.X) || !float.IsFinite(origin.Z)
            || !float.IsFinite(resolution) || resolution <= 0 || !float.IsFinite(radius) || radius <= 0)
            return null;
        var local = (center - origin) / resolution;
        var half = radius / resolution;
        // Integer representability, not the old serialized query's coordinate/map cap.
        if (!float.IsFinite(local.X) || !float.IsFinite(local.Z) || !float.IsFinite(half)
            || half > int.MaxValue / 2 || Math.Abs((double)local.X) + half >= int.MaxValue
            || Math.Abs((double)local.Z) + half >= int.MaxValue)
            return null;
        var x = (int)local.X;
        var z = (int)local.Z;
        return (origin + new WDir(x, z) * resolution, x, z, (int)half);
    }

    internal static bool SeparateStackCarriers(GenericStackSpread component, Actor local, GenericStackSpread.Stack other)
    {
        // Preserve the previous exclusion of custom handlers, including overrides
        // that call base. Cache method ownership once rather than inspect each stack.
        if (other.Target == local || !component.EnableHints || !component.IncludeDeadTargets && other.Target.IsDead
            || !BaseStackHandlers.GetOrAdd(component.GetType(), type =>
                type.GetMethod(nameof(GenericStackSpread.AddAIHints))?.DeclaringType == typeof(GenericStackSpread)))
            return false;
        var module = component.Module;
        bool Participant(GenericStackSpread.Stack stack) => module.ActorMatchesArenaProjectionLayer(local,
            stack.ResolveArenaProjectionLayer(module), stack.RestrictToArenaProjectionLayer);
        return Participant(other) && component.Stacks.Any(own => own.Target == local
            && (component.IncludeDeadTargets || !own.Target.IsDead) && Participant(own)
            && Math.Abs((own.Activation - other.Activation).TotalSeconds) <= 1);
    }
}
