// Settings and legacy migrations adapted from XCAI 4a0f7968. Preserve existing field names
// for one-time import. No standalone plugin lifecycle or plugin config writer is retained.
using Newtonsoft.Json;
using System;

namespace BossMod.CombatAssistance.Config;

internal sealed class AssistancePreferences
{
    public const float InternalMeleeUptimeRange = 2.6f;
    public const float InternalRangedUptimeRange = 25f;
    public const float InternalDisabledUptimeRange = 30f;
    public const float DefaultPreferredForbiddenZoneDistance = 2f;
    private const float PreviousDefaultPreferredForbiddenZoneDistance = 1f;
    private const float DefaultMinimumGapCloserDistance = 8f;

    internal static readonly GapCloserJobToggle[] GapCloserJobToggles =
    [
        new("PLD", config => config.GapCloserPLD, (config, value) => config.GapCloserPLD = value),
        new("WAR", config => config.GapCloserWAR, (config, value) => config.GapCloserWAR = value),
        new("DRK", config => config.GapCloserDRK, (config, value) => config.GapCloserDRK = value),
        new("GNB", config => config.GapCloserGNB, (config, value) => config.GapCloserGNB = value),
        new("MNK", config => config.GapCloserMNK, (config, value) => config.GapCloserMNK = value),
        new("DRG", config => config.GapCloserDRG, (config, value) => config.GapCloserDRG = value),
        new("NIN", config => config.GapCloserNIN, (config, value) => config.GapCloserNIN = value),
        new("SMN", config => config.GapCloserSMN, (config, value) => config.GapCloserSMN = value),
        new("BLU", config => config.GapCloserBLU, (config, value) => config.GapCloserBLU = value),
        new("SAM", config => config.GapCloserSAM, (config, value) => config.GapCloserSAM = value),
        new("BRD", config => config.GapCloserBRD, (config, value) => config.GapCloserBRD = value),
        new("DNC", config => config.GapCloserDNC, (config, value) => config.GapCloserDNC = value),
        new("RPR", config => config.GapCloserRPR, (config, value) => config.GapCloserRPR = value),
        new("BST", config => config.GapCloserBST, (config, value) => config.GapCloserBST = value),
        new("VPR", config => config.GapCloserVPR, (config, value) => config.GapCloserVPR = value),
        new("WHM", config => config.GapCloserWHM, (config, value) => config.GapCloserWHM = value),
        new("BLM", config => config.GapCloserBLM, (config, value) => config.GapCloserBLM = value),
        new("RDM", config => config.GapCloserRDM, (config, value) => config.GapCloserRDM = value),
        new("SGE", config => config.GapCloserSGE, (config, value) => config.GapCloserSGE = value),
        new("PCT", config => config.GapCloserPCT, (config, value) => config.GapCloserPCT = value)
    ];

    public int Version { get; set; } = 30;

    public string[]? AcknowledgedPatchNotes { get; set; }

    public string? PatchNotesBaselineVersion { get; set; }

    public System.Numerics.Vector2? SettingsWindowPosition { get; set; }

    // Enables assistance only; native AI activation and movement authority remain separate.
    public bool Enabled { get; set; } = true;
    public bool ShareFightRecordings { get; set; } = false;
    public bool LinkRotationSolverToggle { get; set; }
    public bool AutoAcceptRaises { get; set; }
    public bool FixRsrMeleeRangedAfterMovement { get; set; } = true;
    public bool ManageMovement { get; set; } = true;
    public bool FixBmrDawntrailHallwayModules { get; set; } = true;
    // Persisted recovery journal; full reset must retain it until BMR settings are restored.
    public System.Collections.Generic.List<uint> BmrHallwayModulesToRestore { get; set; } = [];
    public System.Collections.Generic.Dictionary<uint, long> BmrHallwayRestoreUserRevisions { get; set; } = [];
    public string? BmrHallwayRestoreGeneration { get; set; }
    public bool FixBmrFateArenaFrame { get; set; } = true;
    // Retained only for saved-setting compatibility; encounter corrections now belong to xBMR.
    public bool EnableEncounterFixes { get; set; } = true;
    public System.Collections.Generic.HashSet<string> DisabledEncounterFixes { get; set; } = new();
    public bool FixBmrStackSeparation { get; set; } = true;
    public bool FixBmrForcedMarchHeading { get; set; } = true;
    public bool FixBmrForcedMarchFacing { get; set; } = true;
    public bool FixBmrDefaultTankFacing { get; set; } = true;
    public bool FixQuestionableQuestTargeting { get; set; } = true;
    public bool UseSprint { get; set; } = true;
    public bool CombatCamera { get; set; }
    public bool CameraAutoZoom { get; set; } = true;

    public float CameraPanningSpeed { get; set; } = 1f;

    public bool RespectManualMovement { get; set; } = true;
    public bool DisableAutoFaceTargetDuringManualMovement { get; set; } = true;
    public bool ManageSocialTurning { get; set; } = true;
    public bool ManageSocialSpacing { get; set; } = true;
    public bool ManageNaturalDestinations { get; set; } = true;
    // Optional comfort only. Melee does not use BMR's global hazard score penalty.
    public bool ManageForbiddenZoneDistance { get; set; } = true;
    public bool ManagePositionals { get; set; } = true;
    public bool ManageTrueNorth { get; set; } = true;
    public bool ManageKardiaRecipient { get; set; }
    public bool HealRaisedNonPartyPlayers { get; set; }
    public bool ManageDancePartnerRecipient { get; set; }
    public bool ManageFeyUnionRecipient { get; set; }
    public bool ManageAstCardRecipients { get; set; }
    public bool PreserveCoveredRangedMitigation { get; set; } = true;
    public bool ManageCastControl { get; set; } = true;
    public bool ManageKnockback { get; set; } = true;
    public bool PlanKnockbackLandings { get; set; } = true;
    public bool ManageLeylines { get; set; } = true;
    public bool BlackMageLateMovement { get; set; } = true;
    public bool GreedyLeyLines { get; set; }
    public bool PlanLeyLinesPlacement { get; set; } = true;
    public bool UseBetweenTheLines { get; set; } = true;
    public bool UseRetrace { get; set; } = true;
    public bool ReturnToLeylines { get; set; } = true;
    public bool UseRedMageMeleeComboMovement { get; set; } = true;
    public bool ManagePictomancerStarryMuse { get; set; } = true;
    public bool UsePictomancerStarryMuseSmudge { get; set; } = true;
    public bool UseGapCloser { get; set; } = true;
    public bool UsePhantomGapClosers { get; set; } = true;
    public bool GapCloserPLD { get; set; } = true;
    public bool GapCloserWAR { get; set; } = true;
    public bool GapCloserDRK { get; set; } = true;
    public bool GapCloserGNB { get; set; } = true;
    public bool GapCloserMNK { get; set; } = true;
    public bool GapCloserDRG { get; set; } = true;
    public bool GapCloserNIN { get; set; } = true;
    public bool GapCloserSMN { get; set; } = true;
    public bool GapCloserBLU { get; set; } = true;
    public bool GapCloserSAM { get; set; } = true;
    public bool GapCloserBRD { get; set; } = true;
    public bool GapCloserDNC { get; set; } = true;
    public bool GapCloserRPR { get; set; } = true;
    public bool GapCloserBST { get; set; } = true;
    public bool GapCloserVPR { get; set; } = true;
    public bool GapCloserWHM { get; set; } = true;
    public bool GapCloserBLM { get; set; } = true;
    public bool GapCloserRDM { get; set; } = true;
    public bool GapCloserSGE { get; set; } = true;
    public bool GapCloserPCT { get; set; } = true;
    public bool EchoStatusToChat { get; set; } = true;
    public CombatStyle CombatStyle { get; set; } = CombatStyle.Greed;
    public float PreferredForbiddenZoneDistance { get; set; } = DefaultPreferredForbiddenZoneDistance;
    public float MinimumGapCloserDistance { get; set; } = DefaultMinimumGapCloserDistance;
    public bool MaintainPullContact { get; set; } = true;
    public bool ManageAoePackPositioning { get; set; } = true;
    public bool ManageHealerCoverageZone { get; set; } = true;
    public bool ManageDefensiveGroundZonePositioning { get; set; } = true;
    public bool ManagePassageOfArmsPositioning { get; set; } = true;
    public bool PickBetterAoeTarget { get; set; } = true;
    public bool KeepTrashTargetSelected { get; set; } = true;
    public bool TankIgnoreFrontConeMovement { get; set; } = false;
    public bool TankKeepFrontConeAwayFromParty { get; set; } = false;
    public bool TankTargetLostTrashAggro { get; set; } = false;
    public bool TankUseRangedAggroRecovery { get; set; } = false;
    public bool TankRecoverAfterTankDeath { get; set; } = false;
    public bool TankFollowBossModSwaps { get; set; } = false;
    public bool TankDropStanceWhenCoTankHasStance { get; set; } = false;
    public bool AvoidStandingInsideEnemies { get; set; } = true;
    public bool AvoidArenaEdge { get; set; } = true;
    public bool ShowDecisionOverlay { get; set; } = false;
    public OverlayDensity DecisionOverlayDensity { get; set; } = OverlayDensity.Normal;
    private bool? manageSurvivabilityZonePositioningCompatibility;
    private bool? manageMultiTargetTargetingCompatibility;
    private bool? aoePackPositioningControlRsrTargetCompatibility;
    private bool? manageTrueNorthInRsrCompatibility;
    private bool? manageHealerAoePositioningCompatibility;
    private bool? useEscapeGapCloserCompatibility;
    private bool? escapeGapCloserMNKCompatibility;
    private bool? escapeGapCloserDRGCompatibility;
    private bool? escapeGapCloserNINCompatibility;
    private bool? escapeGapCloserSAMCompatibility;
    private bool? escapeGapCloserBRDCompatibility;
    private bool? escapeGapCloserDNCCompatibility;
    private bool? escapeGapCloserRPRCompatibility;
    private bool? escapeGapCloserVPRCompatibility;
    private bool? escapeGapCloserWHMCompatibility;
    private bool? escapeGapCloserBLMCompatibility;
    private bool? escapeGapCloserRDMCompatibility;
    private bool? escapeGapCloserSGECompatibility;
    private bool? escapeGapCloserPCTCompatibility;

    [JsonProperty("ManageSurvivabilityZonePositioning")]
    private bool ManageSurvivabilityZonePositioningCompatibility
    {
        set
        {
            this.manageSurvivabilityZonePositioningCompatibility = value;
            this.ManageDefensiveGroundZonePositioning = value;
            this.ManagePassageOfArmsPositioning = value;
        }
    }

    [JsonProperty("ManageMultiTargetTargeting")]
    private bool ManageMultiTargetTargetingCompatibility
    {
        set
        {
            this.manageMultiTargetTargetingCompatibility = value;
            this.KeepTrashTargetSelected = value;
        }
    }

    [JsonProperty("AoePackPositioningControlRsrTarget")]
    private bool AoePackPositioningControlRsrTargetCompatibility
    {
        set
        {
            this.aoePackPositioningControlRsrTargetCompatibility = value;
            this.PickBetterAoeTarget = value;
        }
    }

    [JsonProperty("MoveCloserToTrashPacks")]
    private bool MoveCloserToTrashPacksCompatibility
    {
        set { }
    }

    [JsonProperty("AoePackPositioningAoeCombatControl")]
    private bool AoePackPositioningAoeCombatControlCompatibility
    {
        set
        {
            if (!value)
                return;

            this.PickBetterAoeTarget = true;
            this.KeepTrashTargetSelected = true;
        }
    }

    [JsonProperty("ManageTrueNorthInRsr")]
    private bool ManageTrueNorthInRsrCompatibility
    {
        set
        {
            this.manageTrueNorthInRsrCompatibility = value;
            this.ManageTrueNorth = value;
        }
    }

    [JsonProperty("ManageRange")]
    private bool ManageRangeCompatibility
    {
        set { }
    }

    [JsonProperty("HealerPartyCoverage")]
    private bool HealerPartyCoverageCompatibility
    {
        set { }
    }

    [JsonProperty("ManageHealerAoePositioning")]
    private bool ManageHealerAoePositioningCompatibility
    {
        set
        {
            this.manageHealerAoePositioningCompatibility = value;
            this.ManageHealerCoverageZone = value;
        }
    }

    [JsonProperty("UseEscapeGapCloser")]
    private bool UseEscapeGapCloserCompatibility
    {
        set => this.useEscapeGapCloserCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserMNK")]
    private bool EscapeGapCloserMNKCompatibility
    {
        set => this.escapeGapCloserMNKCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserDRG")]
    private bool EscapeGapCloserDRGCompatibility
    {
        set => this.escapeGapCloserDRGCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserNIN")]
    private bool EscapeGapCloserNINCompatibility
    {
        set => this.escapeGapCloserNINCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserSAM")]
    private bool EscapeGapCloserSAMCompatibility
    {
        set => this.escapeGapCloserSAMCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserBRD")]
    private bool EscapeGapCloserBRDCompatibility
    {
        set => this.escapeGapCloserBRDCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserDNC")]
    private bool EscapeGapCloserDNCCompatibility
    {
        set => this.escapeGapCloserDNCCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserRPR")]
    private bool EscapeGapCloserRPRCompatibility
    {
        set => this.escapeGapCloserRPRCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserVPR")]
    private bool EscapeGapCloserVPRCompatibility
    {
        set => this.escapeGapCloserVPRCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserWHM")]
    private bool EscapeGapCloserWHMCompatibility
    {
        set => this.escapeGapCloserWHMCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserBLM")]
    private bool EscapeGapCloserBLMCompatibility
    {
        set => this.escapeGapCloserBLMCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserRDM")]
    private bool EscapeGapCloserRDMCompatibility
    {
        set => this.escapeGapCloserRDMCompatibility = value;
    }

    [JsonProperty("EscapeGapCloserSGE")]
    private bool EscapeGapCloserSGECompatibility
    {
        set => this.escapeGapCloserSGECompatibility = value;
    }

    [JsonProperty("EscapeGapCloserPCT")]
    private bool EscapeGapCloserPCTCompatibility
    {
        set => this.escapeGapCloserPCTCompatibility = value;
    }

    [JsonProperty("MinimumReengageGapCloserDistance")]
    private float MinimumReengageGapCloserDistanceCompatibility
    {
        set { }
    }

    [JsonProperty("MinimumEscapeGapCloserDistance")]
    private float MinimumEscapeGapCloserDistanceCompatibility
    {
        set { }
    }

    internal void Migrate()
    {
        // The encounter now has one toggle. Preserve any previous Ogre opt-out
        // and consume the temporary per-mechanic keys so enabling it works cleanly.
        this.DisabledEncounterFixes ??= new();
        var ogreTeleportDisabled = this.DisabledEncounterFixes.Remove("ogre-teleport-preparation");
        var ogreFireDisabled = this.DisabledEncounterFixes.Remove("ogre-fire-clearance");
        if (ogreTeleportDisabled || ogreFireDisabled)
            this.DisabledEncounterFixes.Add("ogre-wisp-deadlines");

        if (this.Version < 13)
        {
            this.ManageTrueNorth = false;
            this.GapCloserSAM = true;
            this.GapCloserPLD = true;
            this.GapCloserWAR = true;
            this.GapCloserDRK = true;
            this.GapCloserGNB = true;
            this.GapCloserRPR = true;
            this.GapCloserDNC = true;
            this.GapCloserWHM = true;
            this.RespectManualMovement = true;
            this.ManageAoePackPositioning = true;
            this.ManageHealerCoverageZone = true;
            this.ManageDefensiveGroundZonePositioning = true;
            this.ManagePassageOfArmsPositioning = true;
            this.PickBetterAoeTarget = false;
            this.KeepTrashTargetSelected = true;
            this.AvoidStandingInsideEnemies = true;
            this.AvoidArenaEdge = true;
            this.ShowDecisionOverlay = false;
            this.ApplyCompatibilityValues();
            this.Version = 13;
        }

        if (this.Version < 14)
        {
            this.Version = 14;
        }

        if (this.Version < 15)
        {
            this.Version = 15;
        }

        if (this.Version < 16)
        {
            this.Version = 16;
        }

        if (this.Version < 17)
        {
            this.ManageSocialTurning = true;
            this.UseRedMageMeleeComboMovement = false;
            this.MigrateUnifiedGapCloserSettings();
            this.Version = 17;
        }

        if (this.Version < 18)
        {
            this.Version = 18;
        }

        if (this.Version < 19)
        {
            this.UsePhantomGapClosers = false;
            this.Version = 19;
        }

        if (this.Version < 20)
        {
            this.DisableAutoFaceTargetDuringManualMovement = false;
            this.Version = 20;
        }

        if (this.Version < 21)
        {
            this.TankIgnoreFrontConeMovement = false;
            this.TankKeepFrontConeAwayFromParty = false;
            this.TankTargetLostTrashAggro = false;
            this.TankUseRangedAggroRecovery = false;
            this.TankRecoverAfterTankDeath = false;
            this.TankFollowBossModSwaps = false;
            this.TankDropStanceWhenCoTankHasStance = false;
            this.Version = 21;
        }

        if (this.Version < 22)
        {
            this.ManageSocialSpacing = true;
            this.Version = 22;
        }

        if (this.Version < 23)
            this.Version = 23;

        if (this.Version < 24)
        {
            this.ManagePictomancerStarryMuse = true;
            this.UsePictomancerStarryMuseSmudge = true;
            this.Version = 24;
        }

        if (this.Version < 25)
            this.Version = 25;

        if (this.Version < 26)
            this.Version = 26;

        if (this.Version < 27)
        {
            this.DecisionOverlayDensity = OverlayDensity.Normal;
            this.Version = 27;
        }

        if (this.Version < 28)
        {
            this.UseGapCloser = true;
            this.Version = 28;
        }

        if (this.Version < 29)
            this.Version = 29;

        if (this.Version < 30)
        {
            if (MathF.Abs(this.PreferredForbiddenZoneDistance - PreviousDefaultPreferredForbiddenZoneDistance) < 0.001f)
                this.PreferredForbiddenZoneDistance = DefaultPreferredForbiddenZoneDistance;
            this.Version = 30;
        }
    }

    internal void Clamp()
    {
        this.CameraPanningSpeed = float.IsFinite(this.CameraPanningSpeed) ? Math.Clamp(this.CameraPanningSpeed, 0.25f, 3f) : 1f;
        this.PreferredForbiddenZoneDistance = float.IsFinite(this.PreferredForbiddenZoneDistance)
            ? Math.Clamp(this.PreferredForbiddenZoneDistance, 0f, 3f) : DefaultPreferredForbiddenZoneDistance;
        this.CombatStyle = Enum.IsDefined(this.CombatStyle) ? this.CombatStyle : CombatStyle.Normal;
        this.DecisionOverlayDensity = Enum.IsDefined(this.DecisionOverlayDensity) ? this.DecisionOverlayDensity : OverlayDensity.Normal;
    }

    [JsonIgnore]
    internal LeyLinesReturnMode LeyLinesReturn
    {
        get => !this.ManageLeylines ? LeyLinesReturnMode.Off
            : this.ReturnToLeylines ? LeyLinesReturnMode.WalkingAndAbilities : LeyLinesReturnMode.AbilitiesOnly;
        set
        {
            this.ManageLeylines = value != LeyLinesReturnMode.Off;
            this.ReturnToLeylines = value == LeyLinesReturnMode.WalkingAndAbilities;
        }
    }

    internal void ResetMovementSettings()
    {
        var defaults = new AssistancePreferences();
        this.ManageMovement = defaults.ManageMovement;
        this.UseSprint = defaults.UseSprint;
        this.ManageSocialTurning = defaults.ManageSocialTurning;
        this.RespectManualMovement = defaults.RespectManualMovement;
        this.DisableAutoFaceTargetDuringManualMovement = defaults.DisableAutoFaceTargetDuringManualMovement;
        this.CombatStyle = defaults.CombatStyle;
        this.ManageForbiddenZoneDistance = defaults.ManageForbiddenZoneDistance;
        this.PreferredForbiddenZoneDistance = defaults.PreferredForbiddenZoneDistance;
        this.ManageHealerCoverageZone = defaults.ManageHealerCoverageZone;
        this.ManageDefensiveGroundZonePositioning = defaults.ManageDefensiveGroundZonePositioning;
        this.ManagePassageOfArmsPositioning = defaults.ManagePassageOfArmsPositioning;
        this.ManageSocialSpacing = defaults.ManageSocialSpacing;
        this.ManageNaturalDestinations = defaults.ManageNaturalDestinations;
        this.AvoidStandingInsideEnemies = defaults.AvoidStandingInsideEnemies;
        this.AvoidArenaEdge = defaults.AvoidArenaEdge;
    }

    internal void ResetAll()
    {
        this.Enabled = true;
        this.ShareFightRecordings = false;
        this.LinkRotationSolverToggle = false;
        this.AutoAcceptRaises = false;
        this.FixRsrMeleeRangedAfterMovement = true;
        this.ManageMovement = true;
        this.FixBmrDawntrailHallwayModules = true;
        this.FixBmrFateArenaFrame = true;
        this.EnableEncounterFixes = true;
        this.DisabledEncounterFixes = new();
        this.FixBmrStackSeparation = true;
        this.FixBmrForcedMarchHeading = true;
        this.FixBmrForcedMarchFacing = true;
        this.FixBmrDefaultTankFacing = true;
        this.FixQuestionableQuestTargeting = true;
        this.UseSprint = true;
        this.RespectManualMovement = true;
        this.DisableAutoFaceTargetDuringManualMovement = true;
        this.ManageSocialTurning = true;
        this.ManageSocialSpacing = true;
        this.ManageNaturalDestinations = true;
        this.ManageForbiddenZoneDistance = true;
        this.ManagePositionals = true;
        this.ManageTrueNorth = true;
        this.ManageKardiaRecipient = false;
        this.HealRaisedNonPartyPlayers = false;
        this.ManageDancePartnerRecipient = false;
        this.ManageFeyUnionRecipient = false;
        this.ManageAstCardRecipients = false;
        this.PreserveCoveredRangedMitigation = true;
        this.ManageCastControl = true;
        this.ManageKnockback = true;
        this.PlanKnockbackLandings = true;
        this.ManageLeylines = true;
        this.BlackMageLateMovement = true;
        this.GreedyLeyLines = false;
        this.PlanLeyLinesPlacement = true;
        this.UseBetweenTheLines = true;
        this.UseRetrace = true;
        this.ReturnToLeylines = true;
        this.UseRedMageMeleeComboMovement = true;
        this.ManagePictomancerStarryMuse = true;
        this.UsePictomancerStarryMuseSmudge = true;
        this.UseGapCloser = true;
        this.UsePhantomGapClosers = true;
        this.SetAllGapCloserJobs(true);
        this.EchoStatusToChat = true;
        this.CombatStyle = CombatStyle.Greed;
        this.CombatCamera = false;
        this.CameraAutoZoom = true;
        this.CameraPanningSpeed = 1f;
        this.MaintainPullContact = true;
        this.ManageAoePackPositioning = true;
        this.ManageHealerCoverageZone = true;
        this.ManageDefensiveGroundZonePositioning = true;
        this.ManagePassageOfArmsPositioning = true;
        this.PickBetterAoeTarget = true;
        this.KeepTrashTargetSelected = true;
        this.TankIgnoreFrontConeMovement = false;
        this.TankKeepFrontConeAwayFromParty = false;
        this.TankTargetLostTrashAggro = false;
        this.TankUseRangedAggroRecovery = false;
        this.TankRecoverAfterTankDeath = false;
        this.TankFollowBossModSwaps = false;
        this.TankDropStanceWhenCoTankHasStance = false;
        this.AvoidStandingInsideEnemies = true;
        this.AvoidArenaEdge = true;
        this.ShowDecisionOverlay = false;
        this.DecisionOverlayDensity = OverlayDensity.Normal;
        this.ResetMovementSettings();
    }

    internal void SetAllGapCloserJobs(bool enabled)
    {
        foreach (var toggle in GapCloserJobToggles)
        {
            toggle.Set(this, enabled);
        }
    }

    internal bool IsGapCloserJobEnabled(uint classJobId)
    {
        return classJobId switch
        {
            1 or 19 => this.GapCloserPLD,
            3 or 21 => this.GapCloserWAR,
            32 => this.GapCloserDRK,
            37 => this.GapCloserGNB,
            2 or 20 => this.GapCloserMNK,
            4 or 22 => this.GapCloserDRG,
            5 or 23 => this.GapCloserBRD,
            25 => this.GapCloserBLM,
            29 or 30 => this.GapCloserNIN,
            27 => this.GapCloserSMN,
            36 => this.GapCloserBLU,
            34 => this.GapCloserSAM,
            38 => this.GapCloserDNC,
            39 => this.GapCloserRPR,
            24 => this.GapCloserWHM,
            35 => this.GapCloserRDM,
            40 => this.GapCloserSGE,
            43 => this.GapCloserBST,
            41 => this.GapCloserVPR,
            42 => this.GapCloserPCT,
            _ => false
        };
    }

    internal bool IsPhantomGapCloserJobEnabled(uint classJobId)
    {
        if (!IsCombatClassOrJob(classJobId))
        {
            return false;
        }

        return HasGapCloserJobToggle(classJobId)
            ? this.IsGapCloserJobEnabled(classJobId)
            : JobRoles.GetRangeRole(classJobId) switch
            {
                RangeRole.Melee => this.GapCloserMNK || this.GapCloserDRG || this.GapCloserNIN || this.GapCloserSAM || this.GapCloserRPR || this.GapCloserVPR || this.GapCloserBST,
                RangeRole.PhysicalRanged => this.GapCloserBRD || this.GapCloserDNC,
                RangeRole.Healer => this.GapCloserWHM || this.GapCloserSGE,
                RangeRole.MagicRanged => this.GapCloserBLM || this.GapCloserRDM || this.GapCloserPCT,
                _ => false
            };
    }

    private static bool HasGapCloserJobToggle(uint classJobId)
    {
        return classJobId is
            1 or 19 or
            3 or 21 or
            32 or
            37 or
            2 or 20 or
            4 or 22 or
            5 or 23 or
            25 or
            29 or 30 or
            27 or 36 or
            34 or
            38 or
            39 or
            24 or
            35 or
            40 or
            41 or
            42 or
            43;
    }

    private static bool IsCombatClassOrJob(uint classJobId)
    {
        return classJobId is >= 1 and <= 7 or >= 19 and <= 43;
    }

    private void SetTankGapCloserJobs(bool enabled)
    {
        this.GapCloserPLD = enabled;
        this.GapCloserWAR = enabled;
        this.GapCloserDRK = enabled;
        this.GapCloserGNB = enabled;
    }

    private void MigrateUnifiedGapCloserSettings()
    {
        var oldReengageEnabled = this.UseGapCloser;
        var oldEscapeEnabled = this.useEscapeGapCloserCompatibility ?? false;

        if (oldEscapeEnabled && !oldReengageEnabled)
        {
            this.SetTankGapCloserJobs(false);
            this.ApplyEscapeGapCloserJobs(replace: true);
        }
        else if (oldEscapeEnabled)
        {
            this.ApplyEscapeGapCloserJobs(replace: false);
        }
        else if (oldReengageEnabled)
        {
            this.GapCloserBRD = false;
            this.GapCloserBLM = false;
            this.GapCloserRDM = false;
            this.GapCloserSGE = false;
            this.GapCloserPCT = false;
        }

        this.UseGapCloser = oldReengageEnabled || oldEscapeEnabled;
    }

    private void ApplyEscapeGapCloserJobs(bool replace)
    {
        if (replace)
        {
            this.GapCloserMNK = LegacyEscapeGapCloserEnabled(this.escapeGapCloserMNKCompatibility);
            this.GapCloserDRG = LegacyEscapeGapCloserEnabled(this.escapeGapCloserDRGCompatibility);
            this.GapCloserNIN = LegacyEscapeGapCloserEnabled(this.escapeGapCloserNINCompatibility);
            this.GapCloserSAM = LegacyEscapeGapCloserEnabled(this.escapeGapCloserSAMCompatibility);
            this.GapCloserBRD = LegacyEscapeGapCloserEnabled(this.escapeGapCloserBRDCompatibility);
            this.GapCloserDNC = LegacyEscapeGapCloserEnabled(this.escapeGapCloserDNCCompatibility);
            this.GapCloserRPR = LegacyEscapeGapCloserEnabled(this.escapeGapCloserRPRCompatibility);
            this.GapCloserVPR = LegacyEscapeGapCloserEnabled(this.escapeGapCloserVPRCompatibility);
            this.GapCloserWHM = LegacyEscapeGapCloserEnabled(this.escapeGapCloserWHMCompatibility);
            this.GapCloserBLM = LegacyEscapeGapCloserEnabled(this.escapeGapCloserBLMCompatibility);
            this.GapCloserRDM = LegacyEscapeGapCloserEnabled(this.escapeGapCloserRDMCompatibility);
            this.GapCloserSGE = LegacyEscapeGapCloserEnabled(this.escapeGapCloserSGECompatibility);
            this.GapCloserPCT = LegacyEscapeGapCloserEnabled(this.escapeGapCloserPCTCompatibility);
            return;
        }

        this.GapCloserMNK |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserMNKCompatibility);
        this.GapCloserDRG |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserDRGCompatibility);
        this.GapCloserNIN |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserNINCompatibility);
        this.GapCloserSAM |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserSAMCompatibility);
        this.GapCloserBRD |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserBRDCompatibility);
        this.GapCloserDNC |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserDNCCompatibility);
        this.GapCloserRPR |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserRPRCompatibility);
        this.GapCloserVPR |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserVPRCompatibility);
        this.GapCloserWHM |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserWHMCompatibility);
        this.GapCloserBLM |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserBLMCompatibility);
        this.GapCloserRDM |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserRDMCompatibility);
        this.GapCloserSGE |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserSGECompatibility);
        this.GapCloserPCT |= LegacyEscapeGapCloserEnabled(this.escapeGapCloserPCTCompatibility);
    }

    private static bool LegacyEscapeGapCloserEnabled(bool? value) => value ?? true;

    private void ApplyCompatibilityValues()
    {
        if (this.manageSurvivabilityZonePositioningCompatibility.HasValue)
        {
            this.ManageDefensiveGroundZonePositioning = this.manageSurvivabilityZonePositioningCompatibility.Value;
            this.ManagePassageOfArmsPositioning = this.manageSurvivabilityZonePositioningCompatibility.Value;
        }

        if (this.manageMultiTargetTargetingCompatibility.HasValue)
        {
            this.KeepTrashTargetSelected = this.manageMultiTargetTargetingCompatibility.Value;
        }

        if (this.aoePackPositioningControlRsrTargetCompatibility.HasValue)
        {
            this.PickBetterAoeTarget = this.aoePackPositioningControlRsrTargetCompatibility.Value;
        }

        if (this.manageTrueNorthInRsrCompatibility.HasValue)
        {
            this.ManageTrueNorth = this.manageTrueNorthInRsrCompatibility.Value;
        }

        if (this.manageHealerAoePositioningCompatibility.HasValue)
        {
            this.ManageHealerCoverageZone = this.manageHealerAoePositioningCompatibility.Value;
        }
    }


}

internal sealed record GapCloserJobToggle(
    string Label,
    Func<AssistancePreferences, bool> Get,
    Action<AssistancePreferences, bool> Set);
