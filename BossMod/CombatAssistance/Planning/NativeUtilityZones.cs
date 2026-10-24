using BossMod.CombatAssistance.Config;

namespace BossMod.CombatAssistance.Planning;

// Current effects only. Native cast events supply placement and original expiry;
// recipient buffs do not establish a new ground location or renew a lifetime.
internal sealed class NativeUtilityZones : IDisposable
{
    private sealed record Placed(uint Action, ulong Owner, WPos Center, float Height, DateTime EndsAt, uint Sequence = 0);
    private readonly WorldState _world;
    private readonly IDisposable _events;
    private readonly Dictionary<(ulong Owner, uint Action), Placed> _placed = [];
    private ushort _zone;
    private ulong _player;
    internal ImmutableArray<PlanningZone> Current { get; private set; } = [];

    internal NativeUtilityZones(WorldState world)
    {
        _world = world;
        _events = world.Actors.CastEvent.Subscribe(Observe);
    }

    private void ResetIfChanged()
    {
        var player = _world.Party.Player()?.InstanceID ?? 0;
        if (_zone != _world.CurrentZone || _player != player)
        {
            _placed.Clear();
            Current = [];
            _zone = _world.CurrentZone;
            _player = player;
        }
    }

    private void Observe(Actor source, ActorCastEvent cast)
    {
        ResetIfChanged();
        var owner = source.OwnerID != 0 ? source.OwnerID : source.InstanceID;
        if (cast.Action.Type != ActionType.Spell || source.IsDead || source.IsDestroyed
            || !_world.Party.WithoutSlot(excludeAlliance: true).Any(a => a.InstanceID == owner)) return;
        Prune();
        var id = cast.Action.ID;
        if (id is 8324 or 7440 or 7441)
        {
            _placed.Remove((owner, 7439));
            return;
        }
        if (owner != source.InstanceID) return;
        if (id == 36988)
        {
            if (_placed.TryGetValue((source.InstanceID, 3573), out var old))
                _placed[(source.InstanceID, 3573)] = old with { Center = source.Position, Height = source.PosRot.Y };
            return;
        }
        var seconds = id switch { 34675 => 30, 3569 => 24, 188 => 15, 3573 or 7439 => 20, _ => 0 };
        if (seconds == 0) return;
        // Self-centered actions have authoritative caster coordinates, including
        // legitimate (0,0,0). Ground actions use the actual event target position.
        var position = id is 3573 or 34675 ? source.PosRot.XYZ() : cast.TargetPos;
        _placed[(source.InstanceID, id)] = new(id, source.InstanceID, new(position.X, position.Z), position.Y,
            _world.CurrentTime.AddSeconds(seconds), cast.GlobalSequence);
    }

    internal ObservedStarryMuse? StarryMuse(Actor player)
    {
        ResetIfChanged();
        Prune();
        return player.Class == Class.PCT && _placed.TryGetValue((player.InstanceID, 34675), out var placed)
            && placed.Sequence != 0 && Math.Abs(placed.Height - player.PosRot.Y) <= 2
            ? new(placed.Owner, placed.Sequence, placed.Center, placed.Height, placed.EndsAt) : null;
    }

    internal ImmutableArray<PlanningZone> Capture(Actor player, AIHints hints, AssistancePreferences settings, bool enabled, ObservedLeyLines? leyLines)
    {
        ResetIfChanged();
        Prune();
        if (!enabled || !player.InCombat || player.IsDead)
            return Current = [];
        // The native owner status and owned object also recover a live Ley Lines
        // after reload. Retrace never resets the status's original expiration.
        if (leyLines != null)
        {
            var expiry = leyLines.ExpiresAt;
            if (_placed.TryGetValue((player.InstanceID, 3573), out var existing) && existing.EndsAt < expiry) expiry = existing.EndsAt;
            _placed[(player.InstanceID, 3573)] = new(3573, player.InstanceID, leyLines.Center, leyLines.Height, expiry);
        }
        var result = ImmutableArray.CreateBuilder<PlanningZone>();
        var healingUseful = player.HPMP.CurHP < player.HPMP.MaxHP || hints.PredictedDamage.Any(d => d.Players[PartyState.PlayerSlot]);
        foreach (var placed in _placed.Values.OrderBy(p => p.Owner).ThenBy(p => p.Action))
        {
            if (Math.Abs(placed.Height - player.PosRot.Y) > 2) continue;
            var remaining = (float)(placed.EndsAt - _world.CurrentTime).TotalSeconds;
            if (placed.Action == 3573 && placed.Owner == player.InstanceID && player.Class == Class.BLM
                && settings.ManageLeylines && settings.ReturnToLeylines)
                Add(placed, "Ley Lines", 3, PlanningZoneBenefit.PersonalCasting, remaining);
            if (placed.Action == 34675 && placed.Owner == player.InstanceID && player.Class == Class.PCT
                && settings.ManagePictomancerStarryMuse && player.FindStatus(3688) is { } hyperphantasia)
            {
                var expiry = Math.Min(remaining, (float)(hyperphantasia.ExpireAt - _world.CurrentTime).TotalSeconds);
                // Thirty yalms describes the party damage buff, not proof of the
                // Inspiration footprint. Until that footprint is corroborated,
                // use the placed center and the currently observed covered pose.
                result.Add(new(34675, placed.Owner, "Starry Muse center", placed.Center, placed.Height, 0,
                    expiry, PlanningZoneBenefit.PersonalCasting, CompleteFootprint: false));
                if (player.FindStatus(3689) != null && player.Position != placed.Center)
                    result.Add(new(34675, placed.Owner, "Inspiration", player.Position, player.PosRot.Y, 0,
                        expiry, PlanningZoneBenefit.PersonalCasting, CompleteFootprint: false));
            }
            if (settings.ManageDefensiveGroundZonePositioning)
                switch (placed.Action)
                {
                    case 188: Add(placed, "Sacred Soil", 15, PlanningZoneBenefit.Mitigation, remaining); break;
                    case 3569 when healingUseful: Add(placed, "Asylum", 15, PlanningZoneBenefit.Healing, remaining); break;
                    case 7439 when healingUseful: Add(placed, "Earthly Star", 20, PlanningZoneBenefit.Healing, remaining); break;
                }
        }
        foreach (var ally in _world.Party.WithoutSlot(excludeAlliance: true).OrderBy(a => a.InstanceID))
        {
            if (ally.InstanceID == player.InstanceID || ally.IsDead || ally.IsDestroyed || Math.Abs(ally.PosRot.Y - player.PosRot.Y) > 2) continue;
            if (settings.ManagePassageOfArmsPositioning && ally.Class == Class.PLD && ally.FindStatus(1175) is { } passage)
                result.Add(new(7385, ally.InstanceID, "Passage of Arms", ally.Position, ally.PosRot.Y, 8,
                    (float)(passage.ExpireAt - _world.CurrentTime).TotalSeconds, PlanningZoneBenefit.Mitigation,
                    (ally.Rotation + 180.Degrees()).Normalized(), 60.Degrees().Rad));
            if (settings.ManageDefensiveGroundZonePositioning && ally.Class == Class.AST
                && (ally.FindStatus(847) ?? ally.FindStatus(848)) is { } collective)
            {
                var remaining = (float)(collective.ExpireAt - _world.CurrentTime).TotalSeconds;
                result.Add(new(3613, ally.InstanceID, "Collective Unconscious", ally.Position, ally.PosRot.Y, 30,
                    remaining, PlanningZoneBenefit.Mitigation));
                if (healingUseful)
                    result.Add(new(3613, ally.InstanceID, "Wheel of Fortune", ally.Position, ally.PosRot.Y, 8,
                        remaining, PlanningZoneBenefit.Healing));
            }
        }
        return Current = result.Where(z => z.Valid).ToImmutableArray();

        void Add(Placed placed, string name, float radius, PlanningZoneBenefit benefit, float remaining)
            => result.Add(new(placed.Action, placed.Owner, name, placed.Center, placed.Height, radius, remaining, benefit));
    }

    private void Prune()
    {
        var party = _world.Party.WithoutSlot(excludeAlliance: true).Select(a => a.InstanceID).ToHashSet();
        foreach (var key in _placed.Where(e => e.Value.EndsAt <= _world.CurrentTime || !party.Contains(e.Value.Owner)
            || _world.Actors.Find(e.Value.Owner) is not { IsDead: false, IsDestroyed: false }).Select(e => e.Key).ToArray())
            _placed.Remove(key);
    }

    public void Dispose()
    {
        _events.Dispose();
        _placed.Clear();
        Current = [];
    }
}
