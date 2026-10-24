using System.Globalization;
using System.IO;

namespace BossMod.ReplayAnalysis.Evidence;

// The archive schema stays stable while all observations now use native types.
// Renames or field changes fail compilation rather than silently losing evidence.
internal sealed partial class ReplayEvidenceDecoder
{
    private static object Position(Vector4 p) => new { x = p.X, y = p.Y, z = p.Z, facing = p.W };
    private static object? Position(Vector3? p, Angle facing = default)
        => p is { } v ? new { x = v.X, y = v.Y, z = v.Z, facing = facing.Rad } : null;

    private Dictionary<string, object?>? SharedIdentity(WorldState.Operation op, int sequence, long ticks)
    {
        var identity = new Dictionary<string, object?> { ["seq"] = sequence, ["ticks"] = ticks };
        switch (op)
        {
            case ActorState.OpCreate created:
                AddName(created.InstanceID, created.Name);
                break;
            case ActorState.OpRename renamed:
                AddName(renamed.InstanceID, renamed.Name);
                break;
            case PartyState.OpModify party:
                if (party.Slot is < 0 or >= PartyState.MaxAllies) throw new InvalidDataException("identity_slot_limit");
                identity["actor"] = Actor(party.Member.InstanceId);
                identity["slot"] = (long)party.Slot;
                identity["content_id"] = party.Member.ContentId.ToString(CultureInfo.InvariantCulture);
                break;
            default: return null;
        }
        return identity;

        void AddName(ulong id, string name)
        {
            if (name.Length > 128) throw new InvalidDataException("identity_name_limit");
            identity["actor"] = Actor(id);
            identity["name"] = name;
        }
    }

    private Dictionary<string, object?>? Project(WorldState.Operation op, int sequence, long ticks)
    {
        string? kind = op switch
        {
            ActorState.OpCreate => "create", PartyState.OpModify when projectionSchema == 3 => "party",
            ActorState.OpRename when projectionSchema == 3 => "rename", ActorState.OpDestroy => "destroy",
            ActorState.OpMove => "move", ActorState.OpSizeChange => "size",
            ActorState.OpCastInfo cast => cast.Value == null ? "cast_end" : "cast_start",
            ActorState.OpCastEvent => "action", ActorState.OpStatus status => status.Value.ID == 0 ? "status_loss" : "status_gain",
            ActorState.OpTether => "tether", ActorState.OpIcon => "icon", WorldState.OpMapEffect => "map_effect",
            WorldState.OpDirectorUpdate => "director", ActorState.OpEventObjectStateChange => "event_state",
            ActorState.OpEventObjectAnimation => "event_animation", ActorState.OpPlayActionTimelineEvent => "timeline",
            ActorState.OpEventState => "actor_event_state", ActorState.OpVFX => "vfx", ActorState.OpEffectResult => "effect_result",
            ActorState.OpTarget => "target", ActorState.OpTargetable => "targetable", ActorState.OpCombat => "combat",
            ActorState.OpDead => "dead", ActorState.OpModelState => "model", WorldState.OpZoneChange => "zone",
            WaymarkState.OpWaymarkChange => "waymark", _ => null
        };
        if (kind == null) return null;
        var actor = Actor(op is ActorState.Operation actorOp ? actorOp.InstanceID : 0);
        var row = new Dictionary<string, object?> { ["seq"] = sequence, ["ticks"] = ticks, ["kind"] = kind, ["actor"] = actor };
        var parameters = new List<long>();
        switch (op)
        {
            case ActorState.OpCreate create:
                row["oid"] = (long)create.OID;
                row["position"] = Position(create.PosRot);
                row["hitbox_radius"] = (double)create.HitboxRadius;
                parameters.AddRange([(long)create.Type, (long)create.Class, create.Level]);
                break;
            case PartyState.OpModify party:
                row["actor"] = Actor(party.Member.InstanceId);
                parameters.Add(party.Slot);
                break;
            case ActorState.OpRename rename: parameters.Add(rename.NameID); break;
            case ActorState.OpMove move: row["position"] = Position(move.PosRot); break;
            case ActorState.OpSizeChange size: row["hitbox_radius"] = (double)size.HitboxRadius; break;
            case ActorState.OpCastInfo { Value: { } cast }:
                AddAction(cast.Action, cast.TargetID, cast.Location, cast.Rotation);
                row["cast_elapsed"] = (double)cast.ElapsedTime;
                row["cast_total"] = (double)cast.TotalTime;
                row["duration"] = Math.Max(0, (double)cast.TotalTime - cast.ElapsedTime);
                casts[actor] = (cast.Action.ID, (long)cast.Action.Type);
                break;
            case ActorState.OpCastInfo:
                if (casts.Remove(actor, out var ended)) { row["action"] = ended.Id; row["action_type"] = ended.Type; }
                break;
            case ActorState.OpCastEvent action:
                var value = action.Value;
                AddAction(value.Action, value.MainTargetID, value.TargetPos, value.Rotation);
                parameters.AddRange([value.GlobalSequence, value.SourceSequence, value.MaxTargets]);
                // 64 targets and 8 effects are the recorded game action-event layout.
                if (value.Targets.Count > 64) throw new InvalidDataException("target_limit");
                row["targets"] = value.Targets.Select(t => Actor(t.ID)).ToList();
                var effects = new List<ulong>();
                foreach (var target in value.Targets)
                    for (var i = 0; i < ActionEffects.MaxCount; ++i) effects.Add(target.Effects[i]);
                row["effects"] = effects;
                break;
            case ActorState.OpStatus status:
                var key = (actor, (long)status.Index);
                var previous = statuses.GetValueOrDefault(key);
                row["status"] = status.Value.ID == 0 ? previous : (long)status.Value.ID;
                row["target"] = Actor(status.Value.SourceID);
                parameters.AddRange([status.Index, status.Value.Extra, previous]);
                if (status.Value.ID == 0) statuses.Remove(key); else statuses[key] = status.Value.ID;
                row["duration"] = Math.Clamp((status.Value.ExpireAt.Ticks - ticks - currentOrigin) / 10000000.0, 0, 86400);
                break;
            case ActorState.OpTether tether:
                row["target"] = Actor(tether.Value.Target); parameters.Add(tether.Value.ID); break;
            case ActorState.OpTarget target: row["target"] = Actor(target.Value); break;
            case ActorState.OpTargetable targetable: parameters.Add(targetable.Value ? 1 : 0); break;
            case ActorState.OpCombat combat: parameters.Add(combat.Value ? 1 : 0); break;
            case ActorState.OpDead dead: parameters.Add(dead.Value ? 1 : 0); break;
            case WaymarkState.OpWaymarkChange mark:
                row["position"] = Position(mark.Pos); parameters.Add((long)mark.ID); break;
            case ActorState.OpIcon icon: parameters.Add(icon.IconID); row["target"] = Actor(icon.TargetID); break;
            case ActorState.OpVFX vfx: parameters.Add(vfx.VfxID); row["target"] = Actor(vfx.TargetID); break;
            case WorldState.OpMapEffect map: parameters.AddRange([map.Index, map.State]); break;
            case WorldState.OpDirectorUpdate director:
                // Historical projection order is part of archive compatibility.
                parameters.AddRange([director.Param1, director.Param2, director.Param3, director.Param4, director.DirectorID, director.UpdateID]); break;
            case ActorState.OpEventObjectStateChange state: parameters.Add(state.State); break;
            case ActorState.OpEventObjectAnimation animation: parameters.AddRange([animation.Param1, animation.Param2]); break;
            case ActorState.OpPlayActionTimelineEvent timeline: parameters.Add(timeline.ActionTimelineID); break;
            case ActorState.OpEffectResult result: parameters.AddRange([result.Seq, result.TargetIndex]); break;
            case ActorState.OpEventState state: parameters.Add(state.Value); break;
            case ActorState.OpModelState model: parameters.AddRange([model.Value.ModelState, model.Value.AnimState1, model.Value.AnimState2]); break;
            case WorldState.OpZoneChange zone: parameters.AddRange([zone.Zone, zone.CFCID]); break;
        }
        row["params"] = parameters;
        return row;

        void AddAction(ActionID action, ulong target, Vector3 position, Angle facing)
        {
            row["action"] = (long)action.ID;
            row["action_type"] = (long)action.Type;
            row["target"] = Actor(target);
            row["position"] = Position(position, facing);
        }
    }
}
