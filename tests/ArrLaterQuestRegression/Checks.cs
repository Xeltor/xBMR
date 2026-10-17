using System.Numerics;
using BossMod;
using BossMod.Components;
using Inferno = BossMod.RealmReborn.Quest.MSQ.LordOfTheInferno.LordOfTheInferno;
using Company = BossMod.RealmReborn.Quest.MSQ.TheCompanyYouKeepMaelstrom.TheCompanyYouKeepMaelstrom;
using Brother = BossMod.RealmReborn.Quest.MSQ.BrotherlyLove.BrotherlyLove;
using Nouveau = BossMod.RealmReborn.Quest.MSQ.NouveauRiche.NouveauRiche;
using Trouble = BossMod.RealmReborn.Quest.MSQ.BigTroubleInLittleAlaMhigo.BigTroubleInLittleAlaMhigo;

static class Checks
{
    private static int assertions;
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }

    private static Actor Create(WorldState ws, ulong id, uint oid, WPos pos, float hitbox, bool ally = false)
    {
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, ActorType.Enemy, Class.None, 29,
            new Vector4(pos.X, 0, pos.Z, 0), hitbox, new(100, 100, 0, 0, 0), !ally, ally, 0, 0, 0));
        return ws.Actors.Find(id)!;
    }

    private sealed record Case(ushort Zone, ushort Duty, uint Anchor, uint Enemy, uint Action, float Hitbox, float Length, float HalfWidth, float HalfAngle, Func<WorldState, Actor, BossModule> Factory);

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        Case[] cases = [
            new(275, 339, 0x3C3, 0x3C7, 146, 1.2f, 5f, 0, -1, (w, a) => new Inferno(w, a)),
            new(279, 342, 0x406, 0x3FE, 146, 0.5f, 5f, 0, -1, (w, a) => new Company(w, a)),
            new(303, 350, 0x633, 0x630, 146, 0.5f, 5f, 0, -1, (w, a) => new Brother(w, a)),
            new(275, 339, 0x3C3, 0x3C8, 490, 1.2f, 4.2f, 1.5f, 0, (w, a) => new Inferno(w, a)),
            new(279, 342, 0x406, 0x3F8, 526, 2.8f, 8.8f, 2.5f, 0, (w, a) => new Company(w, a)),
            new(279, 342, 0x406, 0x3F8, 525, 2.8f, 32.8f, 2.5f, 0, (w, a) => new Company(w, a)),
            new(303, 350, 0x633, 0x631, 604, 3.5f, 15.5f, 0, 60, (w, a) => new Brother(w, a)),
            new(303, 350, 0x633, 0x631, 606, 3.5f, 9.5f, 4, 0, (w, a) => new Brother(w, a)),
            new(232, 311, 0x66E, 0x66A, 866, 0.5f, 3.5f, 1.5f, 0, (w, a) => new Nouveau(w, a)),
            new(232, 311, 0x66E, 0x66D, 720, 0.5f, 6.5f, 0, 45, (w, a) => new Nouveau(w, a)),
            new(232, 311, 0x66E, 0x66D, 1135, 0.5f, 30.5f, 1, 0, (w, a) => new Nouveau(w, a)),
            new(306, 353, 0x7BE, 0x7BD, 517, 2.7f, 8.7f, 0, 60, (w, a) => new Trouble(w, a)),
            new(306, 353, 0x7BE, 0x7BA, 490, 1.2f, 4.2f, 1.5f, 0, (w, a) => new Trouble(w, a)),
            new(306, 353, 0x7BE, 0x6C1, 865, 0.5f, 3.5f, 1.5f, 0, (w, a) => new Trouble(w, a)),
            new(306, 353, 0x7BE, 0x6C2, 866, 0.55f, 3.55f, 1.5f, 0, (w, a) => new Trouble(w, a))
        ];
        foreach (var c in cases)
        {
            var ws = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = c.Zone, CurrentCFCID = c.Duty };
            var anchor = Create(ws, 1, c.Anchor, default, 0.5f, true);
            var first = Create(ws, 2, c.Enemy, new(0, 0), c.Hitbox);
            var second = Create(ws, 3, c.Enemy, new(20, 0), c.Hitbox);
            var friendly = Create(ws, 4, c.Enemy, new(40, 0), c.Hitbox, true);
            first.InCombat = second.InCombat = true;
            ActorCastInfo Cast(Actor a) => new() { Action = new(ActionType.Spell, c.Action), Location = a.PosRot.AsVector3(), TotalTime = 2.2f };
            ws.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(first)));
            ws.Execute(new ActorState.OpCastInfo(second.InstanceID, Cast(second)));
            ws.Execute(new ActorState.OpCastInfo(friendly.InstanceID, Cast(friendly)));
            using var module = c.Factory(ws, anchor);
            module.Update();
            Check(BossModuleRegistry.FindByOID(c.Anchor)?.ModuleType == module.GetType(), "generated module registration");
            Check(module.StateMachine.ActiveState != null, "untargetable ally anchor activates with enemy combat");
            var aoes = module.Components.OfType<GenericAOEs>().Single();
            var active = aoes.ActiveAOEs(0, first);
            Check(active.Length == 2, "preexisting simultaneous casts bootstrap; allied cast excluded");
            Check(Math.Abs((active[0].Activation - Now).TotalSeconds - 2.5) < 0.001, "native NPC correction retained");
            var a = active[0];
            first.CastInfo!.ElapsedTime = first.CastInfo.TotalTime + .1f;
            Check(aoes.ActiveAOEs(0, first)[0].Activation == a.Activation, "corrected deadline survives raw timer's final 0.3 second tail");
            first.CastInfo.ElapsedTime = 0;
            Check(a.Check(new(0, c.Length - 0.02f)), "inner front boundary dangerous");
            Check(!a.Check(new(0, c.Length + 0.02f)), "outer front boundary safe from this attack");
            Check(a.Check(new(0, -1)) == (c.HalfAngle < 0), "circle covers rear; frontal hazards do not");
            if (c.HalfWidth > 0)
            {
                Check(a.Check(new(c.HalfWidth - 0.02f, 1)), "inside half-width dangerous");
                Check(!a.Check(new(c.HalfWidth + 0.02f, 1)), "outside half-width safe from this attack");
            }
            else if (c.HalfAngle > 0)
            {
                Check(a.Check(new WPos(0, 0) + (c.HalfAngle - 0.2f).Degrees().ToDirection() * 2), "inside cone angle dangerous");
                Check(!a.Check(new WPos(0, 0) + (c.HalfAngle + 0.2f).Degrees().ToDirection() * 2), "outside cone angle safe from this attack");
            }
            var hints = new AIHints();
            hints.PotentialTargets.Add(new(first, 0, false));
            hints.PotentialTargets.Add(new(second, -1, false));
            module.CalculateAIHints(0, first, default, hints);
            Check(hints.ForbiddenZones.Count == 2, "AI receives every simultaneous hazard");
            var expectedPriority = c.Enemy switch { 0x3C7 or 0x631 => -2, 0x3C8 or 0x630 or 0x6C2 => 3, 0x3FE or 0x66A or 0x7BD => 4, 0x66D or 0x6C1 => 2, 0x7BA => 5, _ => 1 };
            Check(hints.PotentialTargets[0].Priority == expectedPriority, "guide-backed target priority applied");
            Check(hints.PotentialTargets[1].Priority == -1, "pending-death target priority preserved");
            hints.PotentialTargets[0].Priority = AIHints.Enemy.PriorityForbidden;
            module.CalculateAIHints(0, first, default, hints);
            Check(hints.PotentialTargets[0].Priority == AIHints.Enemy.PriorityForbidden, "preexisting forbidden target is not reenabled");
            ws.Execute(new ActorState.OpCastInfo(first.InstanceID, null));
            Check(aoes.ActiveAOEs(0, first).Length == 1, "cancellation removes only its caster");
            ws.Execute(new ActorState.OpDead(second.InstanceID, true));
            Check(aoes.ActiveAOEs(0, first).Length == 0, "death removes cast without waiting for future event");
            module.Update();
            Check(module.StateMachine.ActiveState != null, "individual enemy death does not end wave encounter");
            first.InCombat = second.InCombat = false;
            Check(!module.CheckReset(), "reinforcement gap does not reset module");
            ws.CurrentZone = 0;
            Check(module.CheckReset(), "leaving duty resets module");
            ws.CurrentZone = c.Zone;
            ws.Execute(new ActorState.OpDestroy(second.InstanceID));
            var recreated = Create(ws, 3, c.Enemy, new(20, 0), c.Hitbox);
            Check(aoes.ActiveAOEs(0, first).Length == 0, "recreated actor does not retain prior cast");
            ws.Execute(new ActorState.OpCastInfo(recreated.InstanceID, Cast(recreated)));
            Check(aoes.ActiveAOEs(0, first).Length == 1, "recreated caster accepted independently");
            ws.Execute(new ActorState.OpDestroy(recreated.InstanceID));
            Check(aoes.ActiveAOEs(0, first).Length == 0, "destroyed active caster clears immediately");
            anchor.IsDead = true;
            module.Update();
            Check(module.Components.Count == 0, "anchor death clears phase on failure or final victory");
        }
        foreach (var c in cases.Where(c => c.Zone != 275).DistinctBy(c => c.Zone))
        {
            var ws = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = c.Zone, CurrentCFCID = c.Duty };
            var anchor = Create(ws, 1, c.Anchor, default, .5f, true);
            var oid = c.Zone switch { 279 => 0x3FEu, 303 => 0x630u, 232 => 0x66Bu, _ => 0x7B9u };
            var caster = Create(ws, 2, oid, default, .5f);
            var second = Create(ws, 3, oid, new(20, 0), .5f);
            var ally = Create(ws, 4, oid, new(40, 0), .5f, true);
            var target = Create(ws, 10, 0xFFFF, new(7, 9), .5f, true);
            caster.InCombat = true;
            ActorCastInfo Cast() => new() { Action = new(ActionType.Spell, 147), TargetID = target.InstanceID, Location = caster.PosRot.AsVector3(), TotalTime = 2.7f };
            ws.Execute(new ActorState.OpCastInfo(caster.InstanceID, Cast()));
            ws.Execute(new ActorState.OpCastInfo(second.InstanceID, Cast()));
            ws.Execute(new ActorState.OpCastInfo(ally.InstanceID, Cast()));
            using var module = c.Factory(ws, anchor);
            module.Update();
            var fire = module.Components.OfType<BaitAwayCast>().Single();
            Check(fire.CurrentBaits.Count == 2, "Fire II bootstrap retains independent hostile sources, excludes friendly same OID");
            Check(Math.Abs((fire.CurrentBaits[0].Activation - Now).TotalSeconds - 3) < .001, "splash uses corrected nominal cast deadline");
            var bait = fire.CurrentBaits[0];
            Check(fire.BaitOrigin(ref bait) == target.Position, "splash centered on target rather than caster cast location");
            Check(bait.Shape.Check(target.Position + new WDir(0, 4.95f), target.Position, default), "splash inner radius dangerous");
            Check(!bait.Shape.Check(target.Position + new WDir(0, 5.05f), target.Position, default), "splash outer radius safe from this attack");
            ws.Execute(new ActorState.OpMove(target.InstanceID, new Vector4(13, 0, 15, 0)));
            Check(fire.BaitOrigin(ref bait) == target.Position, "splash follows moving target");
            fire.OnCastStarted(caster, caster.CastInfo!);
            Check(fire.CurrentBaits.Count == 2, "duplicate callback is idempotent");
            var hints = new AIHints();
            fire.AddAIHints(0, target, default, hints);
            Check(hints.ForbiddenZones.Count == 0, "target is not asked to dodge its own following circle");
            hints = new();
            fire.AddAIHints(0, caster, default, hints);
            Check(hints.ForbiddenZones.Count == 2, "other actors receive both moving splash constraints");
            ws.Execute(new ActorState.OpCastInfo(caster.InstanceID, null));
            Check(fire.CurrentBaits.Count == 1, "cancel removes only matching splash");
            ws.Execute(new ActorState.OpDestroy(second.InstanceID));
            Check(fire.CurrentBaits.Count == 0, "source destruction removes following splash");
            ws.Execute(new ActorState.OpCastInfo(caster.InstanceID, Cast()));
            Check(fire.CurrentBaits.Count == 1, "new splash activates after cancellation");
            ws.Execute(new ActorState.OpDestroy(target.InstanceID));
            Check(fire.CurrentBaits.Count == 0, "target destruction removes following splash");
        }
        Console.WriteLine($"ARR later quest synthetic regression: {assertions} checks passed.");
    }
}
