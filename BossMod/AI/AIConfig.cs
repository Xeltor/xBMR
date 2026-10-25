namespace BossMod.AI;

[ConfigDisplay(Name = "Automovement", Order = 7)]
sealed class AIConfig : ConfigNode
{
    public bool ShowDTR = false;

    public bool DrawUI = false;

    public bool FocusTargetMaster = false;

    public bool BroadcastToSlaves = false;

    public int FollowSlot = 0;

    public bool ForbidActions = false;

    public bool ManualTarget = false;

    public bool ForbidMovement = false;

    public bool FollowDuringCombat = true;

    public bool FollowDuringActiveBossModule = true;

    public bool FollowOutOfCombat = false;

    public bool FollowTarget = true;

    public Positional DesiredPositional = Positional.Any;

    public float MaxDistanceToSlot = 1f;

    public float MaxDistanceToTarget = 2.6f;

    public bool UseCombatPositioningRange = true;

    public float MinDistance = 0f;

    public float PreferredDistance = 0f;

    public bool AutoAFK = false;

    public float AFKModeTimer = 10f;

    public bool DisableObstacleMaps = false;

    public double MoveDelay = default;

    public bool ForbidAIMovementMounted = false;

    public bool EchoToChat = true;

    public bool FollowRSRDesiredPositional = true;

    public string? AIAutorotPresetName;
    public override void DrawCustom(UITree tree, WorldState ws)
    {
        if (Dalamud.Bindings.ImGui.ImGui.Button("Open AI settings"))
            AIManager.Instance?.OpenSettings();
    }

    // The default leader is self. Hidden controls are corrected when AI starts.
    public override void Deserialize(System.Text.Json.JsonElement j, System.Text.Json.JsonSerializerOptions ser)
    {
        try { base.Deserialize(j, ser); }
        finally { FollowSlot = PartyState.PlayerSlot; }
    }
}
