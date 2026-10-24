using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Planning;
using Dalamud.Bindings.ImGui;

namespace BossMod.CombatAssistance.UI;

internal sealed partial class DecisionOverlayController(Func<AssistancePreferences> preferences)
{
    private AssistancePreferences config => preferences();
    private DateTime _nextError;

    internal void Draw(NativeMovementHost host, Actor player)
    {
        if (!config.ShowDecisionOverlay)
            return;
        try
        {
            var draw = ImGui.GetBackgroundDrawList();
            if (host.Assistance?.TargetDecision is { Published: true } targeting
                && DateTime.UtcNow - targeting.ObservedAt < TimeSpan.FromMilliseconds(250))
            {
                const string label = "Action target";
                DrawContextMarker(draw, new(OverlayVisualKind.ContextMarker, OverlayVisualProminence.Preview,
                    DecisionOverlaySource.TargetUptime, DecisionOverlayState.Active, targeting.Position, null, null,
                    new(DecisionOverlayState.Active, targeting.Position, 0.45f, label), label, 0, 1));
            }
            if (host.Assistance?.TankRecoveryDecision is { } recovery
                && DateTime.UtcNow - recovery.ObservedAt < TimeSpan.FromMilliseconds(250))
            {
                var state = recovery.Submitted ? DecisionOverlayState.Active : DecisionOverlayState.Candidate;
                var label = recovery.Submitted ? "Aggro recovery submitted" : "Tank recovery";
                DrawContextMarker(draw, new(OverlayVisualKind.ContextMarker, OverlayVisualProminence.Preview,
                    DecisionOverlaySource.TankBehavior, state, recovery.Position, null, null,
                    new(state, recovery.Position, 0.45f, label), label, 0, 1));
            }
            if (host.Assistance?.TankStanceDecision is { } stance
                && DateTime.UtcNow - stance.ObservedAt < TimeSpan.FromMilliseconds(250))
            {
                var state = stance.Submitted ? DecisionOverlayState.Active : DecisionOverlayState.Candidate;
                DrawBadgeGroup(draw, player.PosRot.XYZ(), [new(state, DecisionOverlaySource.TankBehavior,
                    stance.Enable ? "Enable tank stance" : "Release tank stance")]);
            }
            if (host.Assistance?.CastControlDecision is { } control
                && DateTime.UtcNow - control.ObservedAt < TimeSpan.FromMilliseconds(250))
            {
                var state = control.Choice == Control.CastControlChoice.Submitted ? DecisionOverlayState.Active
                    : control.Action != 0 ? DecisionOverlayState.Candidate : DecisionOverlayState.Rejected;
                var label = control.Choice == Control.CastControlChoice.Interrupt ? "Interrupt"
                    : control.Choice == Control.CastControlChoice.Stun ? "Stun"
                    : control.Choice == Control.CastControlChoice.Submitted ? "Control submitted" : "Dodge";
                DrawContextMarker(draw, new(OverlayVisualKind.ContextMarker, OverlayVisualProminence.Preview,
                    DecisionOverlaySource.CastControl, state, control.Position, null, null,
                    new DecisionOverlayMarker(state, control.Position, 0.45f, label), label, 0, 1));
            }
            if (host.Assistance?.SupportSnapshot is { Position: { } recipient } support
                && DateTime.UtcNow - support.ObservedAt < TimeSpan.FromSeconds(1))
            {
                var state = support.RequestSubmitted ? DecisionOverlayState.Active : DecisionOverlayState.Candidate;
                var marker = new DecisionOverlayMarker(state, recipient, 0.45f, support.Action);
                DrawContextMarker(draw, new(OverlayVisualKind.ContextMarker, OverlayVisualProminence.Preview,
                    DecisionOverlaySource.SupportRecipient, state, recipient, null, null, marker, support.Action, 0, 1));
                if (config.DecisionOverlayDensity == OverlayDensity.Detailed)
                    DrawPathLine(draw, player.PosRot.XYZ(), recipient, ColorFor(state, DecisionOverlaySource.SupportRecipient), 1.8f, true);
            }
            if (host.CurrentPlan is not { } plan || !NativePlanOverlay.CanDisplay(plan))
                return;
            var frame = NativePlanOverlay.Capture(plan, host.Execution, host.StageIndex, host.RejectedMobility, player.PosRot.Y);
            if (plan.ComboRecovery is { } combo && config.DecisionOverlayDensity != OverlayDensity.Minimal)
            {
                var label = combo.Kind == ComboIntentKind.Continuation ? "Combo recovery" : "Melee preparation";
                DrawContextMarker(draw, new(OverlayVisualKind.ContextMarker, OverlayVisualProminence.Preview,
                    DecisionOverlaySource.TargetUptime, DecisionOverlayState.Candidate, combo.Center.ToVec3(player.PosRot.Y), null, null,
                    new(DecisionOverlayState.Candidate, combo.Center.ToVec3(player.PosRot.Y), 0.45f, label), label, 0, 1));
            }
            if (plan.PartyContact is { } contact && config.DecisionOverlayDensity != OverlayDensity.Minimal)
            {
                DrawContextMarker(draw, new(OverlayVisualKind.ContextMarker, OverlayVisualProminence.Preview,
                    DecisionOverlaySource.PullContact, DecisionOverlayState.Candidate, contact.Center.ToVec3(player.PosRot.Y), null, null,
                    new(DecisionOverlayState.Candidate, contact.Center.ToVec3(player.PosRot.Y), 0.45f, "Tank contact"), "Tank contact", 0, 1));
            }
            if (config.DecisionOverlayDensity != OverlayDensity.Minimal)
                foreach (var zone in plan.UsefulZones)
                {
                    var source = zone.ActionId switch
                    {
                        3573 => DecisionOverlaySource.LeyLines,
                        34675 => DecisionOverlaySource.StarryMuse,
                        7385 => DecisionOverlaySource.PassageOfArms,
                        _ => DecisionOverlaySource.SurvivabilityZone
                    };
                    var state = frame.Preview ? DecisionOverlayState.Candidate : DecisionOverlayState.Active;
                    var shape = new DecisionOverlayShape(zone.Facing == null ? DecisionOverlayShapeKind.Circle : DecisionOverlayShapeKind.Cone,
                        state, new(zone.Center.X, zone.Height, zone.Center.Z), zone.Radius, zone.HalfAngle, 0, zone.Facing?.Rad ?? 0, zone.Name);
                    if (zone.Radius > 0)
                        DrawShape(draw, shape, source, ColorFor(state, source));
                    else
                        DrawContextMarker(draw, new(OverlayVisualKind.ContextMarker, OverlayVisualProminence.Preview,
                            source, state, shape.Origin, null, null, new(state, shape.Origin, 0.35f, zone.Name), zone.Name, 0, 1));
                    if (config.DecisionOverlayDensity == OverlayDensity.Detailed)
                        DrawLabel(draw, shape.Origin, zone.Name, ColorFor(state, source));
                }
            foreach (var line in frame.Lines)
                DrawPathLine(draw, line.From, line.To, ColorFor(line.State, DecisionOverlaySource.FinalMovement), 2.8f,
                    line.State != DecisionOverlayState.Active);
            DrawDestinationPad(draw, new(frame.Preview ? OverlayVisualKind.PreviewDestination : OverlayVisualKind.ActiveDestination,
                frame.Preview ? OverlayVisualProminence.Preview : OverlayVisualProminence.Active,
                DecisionOverlaySource.FinalMovement, frame.Preview ? DecisionOverlayState.Candidate : DecisionOverlayState.Active, frame.Destination, null, null, null,
                frame.DestinationLabel, 0, 1));
            foreach (var marker in frame.Markers)
            {
                if (marker.State == DecisionOverlayState.Rejected)
                    DrawBlockedMarker(draw, marker.Position, DecisionOverlaySource.GapCloser, 1);
                else
                    DrawContextMarker(draw, new(OverlayVisualKind.ContextMarker, OverlayVisualProminence.Preview,
                        DecisionOverlaySource.GapCloser, marker.State, marker.Position, null, null, marker, marker.Label, 0, 1));
                if (config.DecisionOverlayDensity == OverlayDensity.Detailed && marker.State == DecisionOverlayState.Rejected)
                    DrawLabel(draw, marker.Position, marker.Label ?? "Movement unavailable", ColorFor(marker.State, DecisionOverlaySource.GapCloser));
            }
            if (config.DecisionOverlayDensity != OverlayDensity.Minimal)
                DrawBadgeGroup(draw, frame.Destination, [new(frame.Preview ? DecisionOverlayState.Candidate : DecisionOverlayState.Active, DecisionOverlaySource.FinalMovement, frame.Status)]);
        }
        catch (Exception error)
        {
            if (DateTime.UtcNow >= _nextError)
            {
                Service.Logger.Verbose(error, "Native movement overlay unavailable.");
                _nextError = DateTime.UtcNow.AddSeconds(10);
            }
        }
    }
}

internal sealed record NativePlanOverlay(Vector3 Destination, string DestinationLabel, string Status, bool Preview,
    ImmutableArray<DecisionOverlayLine> Lines, ImmutableArray<DecisionOverlayMarker> Markers)
{
    internal static bool CanDisplay(MovementPlan plan) => plan.HasVerifiedRoute
        || plan.Status == MovementSearchStatus.NativeRecovery && plan.NativeRecovery?.Publication is { WitnessComplete: true };

    internal static NativePlanOverlay Capture(MovementPlan plan, MovementExecution? execution, int stage,
        ImmutableArray<RejectedMobility> rejected, float height)
    {
        var preview = plan.Status == MovementSearchStatus.Searching || plan.OptimizationPending
            || plan.Status != MovementSearchStatus.NativeRecovery && execution == null;
        Vector3 World(WPos point) => new(point.X, height, point.Z);
        var lines = ImmutableArray.CreateBuilder<DecisionOverlayLine>();
        var markers = ImmutableArray.CreateBuilder<DecisionOverlayMarker>();
        for (var i = stage; i < plan.Stages.Length; ++i)
        {
            var item = plan.Stages[i];
            var state = preview ? DecisionOverlayState.Candidate : i == stage ? DecisionOverlayState.Active : DecisionOverlayState.Future;
            if (item.Forced is { } forced)
            {
                foreach (var leg in forced.Legs)
                    if (leg.From != leg.To) lines.Add(new(state, World(leg.From), World(leg.To)));
                markers.Add(new(state, World(forced.Landing), 0.45f, forced.Knockback == null ? "Forced-march landing" : "Knockback landing"));
            }
            else if (item.Transit is { } transit)
            {
                lines.Add(new(state, World(item.From), World(transit.Entrance)));
                lines.Add(new(state, World(transit.Entrance), World(transit.Exit)));
                markers.Add(new(state, World(transit.Entrance), 0.45f, "Teleporter entry"));
                markers.Add(new(state, World(transit.Exit), 0.45f, "Teleporter landing"));
            }
            else if (item.From != item.To)
                lines.Add(new(state, World(item.From), World(item.To)));
            if (item.Kind == MovementStageKind.Knockback && item.PreventedByActionId != 0)
                markers.Add(new(state, World(item.From), 0.45f, "Immunity position"));
            else if (item.Kind == MovementStageKind.Knockback)
            {
                markers.Add(new(state, World(item.From), 0.45f, "Knockback launch"));
                markers.Add(new(state, World(item.To), 0.45f, "Knockback landing"));
            }
            if (item.Kind == MovementStageKind.FieldPlacement)
                markers.Add(new(state, World(item.To), PlanningFieldPlacement.Radius, "Ley Lines placement"));
            if (item.Kind == MovementStageKind.Target)
                markers.Add(new(state, World(item.From), 0.35f, "Target setup"));
            if (item.Kind == MovementStageKind.UtilityAction)
                markers.Add(new(state, World(item.To), 0.35f, item.ActionId switch
                { 7548 => "Arm's Length", 7559 => "Surecast", 7546 => "True North", 3 => "Sprint", 41646 => "Occult Sprint", 36988 => "Retrace", _ => "Planned utility" }));
            if (item.Kind == MovementStageKind.Cast && item.To != plan.UsefulDestination)
                markers.Add(new(state, World(item.To), 0.35f, "Action stop"));
            if (item.Kind is MovementStageKind.TargetDash or MovementStageKind.DirectionalMovement or MovementStageKind.GroundMovement or MovementStageKind.Return)
                markers.Add(new(state, World(item.To), 0.45f, item.Kind switch
                {
                    MovementStageKind.TargetDash => "Target dash",
                    MovementStageKind.DirectionalMovement => item.ActionId == 34684 ? "Smudge" : "Directional movement",
                    MovementStageKind.Return => "Return",
                    _ => "Ground movement"
                }));
        }
        if (plan.Status == MovementSearchStatus.NativeRecovery && plan.NativeRecovery?.Publication is { } publication)
        {
            // Use the actual native recovery witness, including its detours.
            if (plan.Bound == MovementSearchBound.NativeDisplacement)
                markers.Add(new(preview ? DecisionOverlayState.Candidate : DecisionOverlayState.Active,
                    World(plan.UsefulDestination), 0.45f, "Native knockback recovery"));
            var witness = publication.CopyWitness();
            for (var i = 1; i < witness.Length; ++i)
                lines.Add(new(preview ? DecisionOverlayState.Candidate : DecisionOverlayState.Active, World(witness[i - 1]), World(witness[i])));
        }
        foreach (var failure in rejected)
            markers.Add(new(DecisionOverlayState.Rejected, World(failure.To), 0.52f, failure.Reason));
        if (execution is { Kind: MovementExecutionKind.Face, Facing: { } direction, PresentationFacing: true })
        {
            var end = plan.UsefulDestination + 2 * direction.ToDirection();
            lines.Add(new(DecisionOverlayState.Active, World(plan.UsefulDestination), World(end)));
            markers.Add(new(DecisionOverlayState.Active, World(end), 0.35f, "Party facing"));
        }
        var status = preview ? "Proposed route" : plan.Status switch
        {
            MovementSearchStatus.Complete => "Search complete",
            MovementSearchStatus.Bounded => "Best found · search bounded",
            MovementSearchStatus.NativeRecovery => "Native recovery",
            _ => "Planning"
        };
        var label = execution?.Kind switch
        {
            MovementExecutionKind.Wait => "Planned wait",
            MovementExecutionKind.Face => "Prepare facing",
            MovementExecutionKind.Action => "Movement action",
            MovementExecutionKind.Arrived => "Useful destination",
            _ => "Useful destination"
        };
        return new(World(plan.UsefulDestination), label, status, preview, lines.ToImmutable(), markers.ToImmutable());
    }
}
