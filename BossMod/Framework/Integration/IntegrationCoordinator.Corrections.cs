namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    private readonly IntegrationStackCorrection _stackCorrection = new();
    internal sealed record StackCorrectionRequest(IntegrationSession.Context Context, string OwnerToken, bool Enabled);
    internal string SetStackCarrierSeparation(string json)
    {
        var request = IntegrationSession.Parse<StackCorrectionRequest>(json);
        if (request == null || !Session.Matches(request.Context) || request.Context.Stage != "hints-ready"
            || !Session.IsOwnerActive(request.OwnerToken) || request.Enabled && (!Session.CanContribute || !ControlsAllowed()))
            return Unavailable("invalid-stack-correction-context");
        var scope = ControlScope();
        var module = bossmod.ActiveModule;
        var source = Session.CaptureObservationLiveness(request.OwnerToken);
        bool Live() => source?.Invoke() == true && ControlsAllowed() && ControlScope() == scope && ReferenceEquals(bossmod.ActiveModule, module);
        if (!_stackCorrection.Set(request.OwnerToken, request.Enabled, Live) && request.Enabled) return Unavailable("stack-correction-owner-retired-or-work-limit");
        return IntegrationSession.Serialize(new { accepted = true, context = request.Context, requested = request.Enabled,
            nextBuildEnabled = _stackCorrection.Enabled, currentBuildEnabled = hints.StackCarrierSeparationActive,
            sourceLifetimeMs = IntegrationSession.MaxFrameAgeMs,
            meaning = "owner opt-in takes effect at the next native hint build; base stack AI only; masks/settings/custom handlers remain unchanged" });
    }
}
