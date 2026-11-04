namespace BossMod.Shadowbringers.Alliance.A24TheCompound2P;

sealed class ThreePartsDisdainStack(BossModule module) : Components.GenericStackSpread(module)
{
    private Actor? _caster;
    private ActorCastInfo? _cast;
    private uint _nextAction;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.ThreePartsDisdain1 || _cast == spell || WorldState.Actors.Find(spell.TargetID) is not { } target)
            return;
        Stacks.Clear();
        Stacks.Add(new(target, 6f, 24, 24, Module.CastFinishAt(spell)));
        _caster = caster;
        _cast = spell;
        _nextAction = spell.Action.ID;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        var action = spell.Action.ID;
        if (caster != _caster || Stacks.Count == 0 || action < _nextAction || action > (uint)AID.ThreePartsDisdain3)
            return;
        if (action == (uint)AID.ThreePartsDisdain3)
        {
            Clear();
            return;
        }
        _nextAction = action + 1;
        Stacks.Ref(0).Activation = WorldState.FutureTime(NextHitDelay(action));
    }

    // Retained 20891 -> 20892 and 20892 -> 20893 intervals in the recorded sequence.
    public static double NextHitDelay(uint action) => action == (uint)AID.ThreePartsDisdain1 ? 1.09 : 1.38;

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_cast == spell && !spell.EventHappened && _nextAction == (uint)AID.ThreePartsDisdain1)
            Clear();
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == _caster || Stacks.Count != 0 && Stacks[0].Target == actor)
            Clear();
    }

    public override void Update()
    {
        if (Stacks.Count != 0 && (Stacks[0].Target.IsDeadOrDestroyed || _caster!.IsDeadOrDestroyed
            || WorldState.CurrentTime > Stacks[0].Activation.AddSeconds(2)))
            Clear();
    }

    private void Clear()
    {
        Stacks.Clear();
        _caster = null;
        _cast = null;
    }

    public (Actor Caster, Stack Stack, int Stage)? Pending()
        => _caster is { IsDeadOrDestroyed: false } caster && Stacks.Count != 0 && !Stacks[0].Target.IsDeadOrDestroyed
            ? (caster, Stacks[0], (int)(_nextAction - (uint)AID.ThreePartsDisdain1)) : null;
}

sealed class ThreePartsDisdainKnockback(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly float[] Distances = [8f, 8f, 12f];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (Module.FindComponent<ThreePartsDisdainStack>()?.Pending() is not { } pending || !pending.Stack.IsInside(actor))
            return [];
        var (caster, stack, stage) = pending;
        var direction = Angle.FromDirection(stack.Target.Position - caster.Position);
        var knockbacks = new Knockback[Distances.Length - stage];
        var activation = stack.Activation;
        for (var i = stage; i < Distances.Length; ++i)
        {
            knockbacks[i - stage] = new(caster.Position, Distances[i], activation, direction: direction,
                kind: Kind.DirForward, actorID: caster.InstanceID, ignoreImmunes: true);
            activation = activation.AddSeconds(ThreePartsDisdainStack.NextHitDelay((uint)AID.ThreePartsDisdain1 + (uint)i));
        }
        return knockbacks;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Module.FindComponent<ThreePartsDisdainStack>()?.Pending() is not { } pending)
            return;
        var (caster, stack, stage) = pending;
        if (actor == stack.Target)
        {
            // Preserve the one-yalm arena inset for the carrier's whole sequence.
            var remainingDistance = stage == 0 ? 28f : stage == 1 ? 20f : 12f;
            hints.AddForbiddenZone(new SDKnockbackInAABBSquareAwayFromOrigin(Arena.Center, caster.Position, remainingDistance, 29f), stack.Activation);
        }
        else if (stack.IsInside(actor))
        {
            var direction = Angle.FromDirection(stack.Target.Position - caster.Position).ToDirection();
            hints.AddForbiddenZone(new SDKnockbackInAABBSquareFixedDirection(Arena.Center, Distances[stage] * direction, 29f), stack.Activation);
        }
    }
}
