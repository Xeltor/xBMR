using IntegrationExample;
using System.Text.Json;

static partial class Checks
{
    private static void MovementProvenanceConsumer()
    {
        var reader = new BossModV1MovementProvenance();
        string Snapshot(long frame, string stage = "movement-applied", long? assignment = null, long? input = null,
            long inputAssignment = 1, long inputFrame = 1, string instance = "provider", string source = "decision",
            double age = 0.01, bool manual = false, bool automatic = true, string? inputSource = "decision")
            => JsonSerializer.Serialize(new
            {
                available = true,
                context = new { schemaVersion = 1, instanceGeneration = instance, frameId = frame, stage },
                overrideAssignment = assignment == null ? null : new
                {
                    instanceGeneration = instance, frameId = frame, generation = assignment.Value,
                    controllerDecisionId = source, controllerOwner = "bmr-follow"
                },
                controllerDecision = new { id = source, instanceGeneration = instance, frameId = frame,
                    owner = "bmr-follow", wroteMovement = true, matchesEffectiveMovement = true },
                lastInputRead = input == null ? null : new
                {
                    generation = input.Value, kind = "walk-input-result", automatic, followPath = false, misdirection = false,
                    ageSeconds = age, userDirection = new[] { manual ? 1f : 0, 0 }, outputDirection = new[] { 0.5f, 0 },
                    matchingAssignment = new { instanceGeneration = instance, frameId = inputFrame, generation = inputAssignment,
                        controllerDecisionId = inputSource, controllerOwner = "bmr-follow" }
                }
            });
        BossModV1MovementProvenance.Observation? Observe(string s) { using var json = JsonDocument.Parse(s); return reader.Observe(json.RootElement); }
        Check(Observe(Snapshot(1, assignment: 1)) == null, "an applied override cannot fabricate an input read");
        Check(Observe(Snapshot(2, input: 10).Replace("\"schemaVersion\":1", "\"schemaVersion\":2")) == null
            && Observe(Snapshot(2, input: 10).Replace("\"available\":true", "\"available\":false")) == null,
            "compiled provenance consumer rejects incompatible schemas and unavailable snapshots");
        var result = Observe(Snapshot(2, "hints-ready", input: 10));
        Check(result is { AssignmentFrame: 1, AssignmentGeneration: 1, InputGeneration: 10, ControllerOwner: "bmr-follow" },
            "compiled consumer joins a later frame's input to the original applied controller assignment");
        Check(Observe(Snapshot(2, "hints-ready", input: 10)) == null, "retained input generation is not observed twice");
        Check(Observe(Snapshot(2, input: 11, inputAssignment: 2)) == null, "unremembered assignment cannot borrow a previous matching direction");
        Check(Observe(Snapshot(2, input: 11, inputSource: "foreign")) == null, "controller decision identity must match the remembered native source");
        Check(Observe(Snapshot(2, input: 11, inputFrame: 2)) == null, "same assignment number cannot move to another frame");
        Check(Observe(Snapshot(2, input: 11, age: 0.25)) == null, "expired input cannot become fresh evidence through a new callback");
        Check(Observe(Snapshot(2, input: 11, manual: true)) == null && Observe(Snapshot(2, input: 11, automatic: false)) == null,
            "manual or nonautomatic reads cannot be presented as native automatic execution");
        for (var f = 2; f <= 10; ++f) _ = Observe(Snapshot(f, assignment: f));
        Check(Observe(Snapshot(11, input: 11)) == null, "compiled consumer retains at most eight native assignment identities");
        Check(Observe(Snapshot(11, input: 12, inputAssignment: 10, inputFrame: 10, instance: "reloaded")) == null,
            "provider reload clears all remembered assignments and input generations");
    }
}
