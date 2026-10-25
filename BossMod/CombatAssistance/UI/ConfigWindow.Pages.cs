using BossMod.AI;
using BossMod.CombatAssistance.Config;
// Material layout and controls adapted from XelsCombatAI 4a0f7968; native settings ownership.
using Dalamud.Interface;
using System;

namespace BossMod.CombatAssistance.UI;

internal partial class ConfigWindow
{
    private void DrawGeneralTab()
    {
        this.DrawToggleSectionHeader("Enable AI", () => this.settings.MayAssist && this.manager.Beh != null, false, this.setEnabled,
            "Starts automatic movement and combat assistance for your character. Turn off to stop both. Manual movement always takes priority.");
        this.DrawSectionHeader("Status bar");
        this.Checkbox("Show AI status in DTR bar", () => this.NativeConfig.ShowDTR, NativeDefaults.ShowDTR,
            value => this.NativeChange(() => this.NativeConfig.ShowDTR = value));
        this.DrawSectionHeader("RotationSolver");
        this.Checkbox("Link RotationSolver to combat assistance", () => this.config.LinkRotationSolverToggle, this.defaultConfig.LinkRotationSolverToggle,
            v => this.config.LinkRotationSolverToggle = v,
            "Starts RotationSolver while native automatic movement and combat assistance are active.\nWhen that ends, stops only the mode started here. Your own mode changes are preserved.");
        this.DrawAvailability(() => this.config.LinkRotationSolverToggle ? this.rotationSolverUnavailable : null);
        this.DrawSectionHeader("Resurrection");
        this.Checkbox("Accept resurrection offers", () => this.config.AutoAcceptRaises, this.defaultConfig.AutoAcceptRaises,
            v => this.config.AutoAcceptRaises = v,
            "Accepts an available Raise offer. Does not choose Return.",
            icon: FontAwesomeIcon.SkullCrossbones,
            iconTooltip: "Accepting immediately may revive you in danger. Leave off to choose the timing yourself.");
        this.Spacing();

        this.DrawToggleSectionHeader(
            "Automatic camera", () =>
            this.config.CombatCamera,
            this.defaultConfig.CombatCamera,
            v => this.config.CombatCamera = v,
            "Frames the fight with smooth turns and viewing-height changes. Manual input pauses it briefly. Requires Enable AI.");
        this.DrawAvailability(() => !this.config.ManageMovement && this.config.CombatCamera ? "Turn on Enable AI in General." : null);
        var cameraDisabledTooltip = !this.config.CombatCamera ? "Requires Automatic camera." :
            !this.config.ManageMovement ? "Turn on Enable AI in General." : null;
        if (cameraDisabledTooltip != null)
            this.BeginDisabled();
        this.Checkbox("Zoom out for large fights", () => this.config.CameraAutoZoom, this.defaultConfig.CameraAutoZoom,
            v => this.config.CameraAutoZoom = v,
            "Gently widens the view when the boss or arena needs more room and the camera path is clear.\nUses the normal zoom limit and leaves the wider zoom in place. Manual input pauses it.",
            disabledTooltip: cameraDisabledTooltip);
        this.SliderFloat(
            "Panning speed (×)", () => this.config.CameraPanningSpeed, this.defaultConfig.CameraPanningSpeed,
            0.25f, 3f, v => this.config.CameraPanningSpeed = v, "%.2fx",
            "Lower values turn the camera more slowly; higher values turn it faster.",
            cameraDisabledTooltip);
        if (cameraDisabledTooltip != null)
            this.EndDisabled();
        this.Spacing();

        this.DrawSectionHeader("Chat");
        this.Checkbox("Print command messages in chat", () => this.NativeConfig.EchoToChat, NativeDefaults.EchoToChat,
            value => this.NativeChange(() => this.NativeConfig.EchoToChat = value));
        this.Spacing();

        this.DrawSectionHeader("Restore defaults");
        this.Button("Reset everything...", () => { this.confirmResetAll = true; });
        if (this.confirmResetAll)
        {
            this.Text("Restore AI preferences to defaults? AI stays in its current on/off state.");
            this.Button("Confirm reset everything", () =>
            {
                this.config.ResetAll();
                this.confirmResetAll = false;
                this.CommitChange();
            });
            this.Button("Cancel", () => { this.confirmResetAll = false; });
        }
    }

    private void DrawMovementTab()
    {
        this.sectionVisible = this.ActiveTab == 0;
        var movementDisabledTooltip = !this.config.ManageMovement ? "Turn on Enable AI in General." : null;
        if (!this.config.ManageMovement) this.BeginDisabled();
        this.Checkbox("Use Sprint", () => this.config.UseSprint, this.defaultConfig.UseSprint, v => this.config.UseSprint = v, "Uses Sprint for safety, attack range or dungeon pulls. Occult Sprint also needs Phantom movement abilities.", movementDisabledTooltip);

        this.DrawSectionHeader("Manual control");
        this.BeginDisabled();
        this.Checkbox("Pause when I move", () => true, true, _ => { },
            disabledTooltip: "Manual movement always takes priority over automatic movement.");
        this.EndDisabled();
        this.Checkbox(
            "Disable auto-face when I move", () =>
            this.config.DisableAutoFaceTargetDuringManualMovement,
            this.defaultConfig.DisableAutoFaceTargetDuringManualMovement,
            v => this.config.DisableAutoFaceTargetDuringManualMovement = v,
            "Temporarily disables the game’s Auto-face target option while you move manually.",
            movementDisabledTooltip);
        this.Spacing();
        this.DrawSectionHeader("Movement timing");
        this.Combo("Movement timing", () => this.config.CombatStyle, this.defaultConfig.CombatStyle, v => this.config.CombatStyle = v, "Safe first moves earlier. Greedy options allow more attacks before moving.", v => v switch
        {
            CombatStyle.Greed => "Greedy",
            CombatStyle.GreedGCD => "Greedy (GCD margin)",
            CombatStyle.GreedLastMoment => "Move at the last safe moment",
            _ => "Safe first"
        }, movementDisabledTooltip);
        this.Spacing();
        this.DrawSectionHeader("Interrupts & stuns");
        this.Checkbox("Manage interrupts and stuns", () => this.config.ManageCastControl, this.defaultConfig.ManageCastControl,
            v => this.config.ManageCastControl = v,
            tooltip: "Interrupts or stuns threatening casts when useful. Skips stunned or immune targets. Requires RotationSolver.",
            disabledTooltip: !this.config.ManageMovement ? "Turn on Enable AI in General." : null);
        this.sectionVisible = this.ActiveTab == 1;
        this.DrawSectionHeader("Knockbacks");
        this.Checkbox("Plan knockback landings", () => this.config.PlanKnockbackLandings, this.defaultConfig.PlanKnockbackLandings,
            v => this.config.PlanKnockbackLandings = v, tooltip: "Prepares for supported knockbacks with a safe landing near attack or healing range.",
            disabledTooltip: !this.config.ManageMovement ? "Turn on Enable AI in General." : null);
        this.Checkbox("Manage knockback immunity", () => this.config.ManageKnockback, this.defaultConfig.ManageKnockback,
            v => this.config.ManageKnockback = v, tooltip: "Uses Surecast or Arm’s Length when avoiding a knockback helps. Requires RotationSolver.",
            disabledTooltip: !this.config.ManageMovement ? "Turn on Enable AI in General." : null);
        this.sectionVisible = this.ActiveTab == 2;
        this.DrawToggleSectionHeader("Avoid danger zones", () => this.config.ManageForbiddenZoneDistance, this.defaultConfig.ManageForbiddenZoneDistance, v => this.config.ManageForbiddenZoneDistance = v, "Leaves extra room beside AoE edges when attacks and mechanics allow. BossMod still handles dodging.", disabledTooltip: movementDisabledTooltip);
        var forbiddenZoneDisabledTooltip = !this.config.ManageForbiddenZoneDistance ? "Requires Avoid danger zones." : movementDisabledTooltip;
        if (!this.config.ManageForbiddenZoneDistance)
            this.BeginDisabled();
        this.SliderFloat("Extra space (yalms)", () => this.config.PreferredForbiddenZoneDistance, this.defaultConfig.PreferredForbiddenZoneDistance, 0f, 3f, v => this.config.PreferredForbiddenZoneDistance = v, disabledTooltip: forbiddenZoneDisabledTooltip);
        if (!this.config.ManageForbiddenZoneDistance)
            this.EndDisabled();
        this.Spacing();

        this.DrawSectionHeader("Party support");
        this.Checkbox(
            "Stay in healer range", () =>
            this.config.ManageHealerCoverageZone,
            this.defaultConfig.ManageHealerCoverageZone,
            v => this.config.ManageHealerCoverageZone = v,
            "Keeps useful healing coverage. Supported automatic group heals can wait briefly for a safe coverage move; emergencies and manual actions keep priority.\nTanks keep their tanking position.",
            movementDisabledTooltip);
        this.Checkbox(
            "Stand in defensive ground effects", () =>
            this.config.ManageDefensiveGroundZonePositioning,
            this.defaultConfig.ManageDefensiveGroundZonePositioning,
            v => this.config.ManageDefensiveGroundZonePositioning = v,
            "Stands in effects such as Asylum or Sacred Soil when useful for healing or incoming damage.",
            movementDisabledTooltip);
        this.Checkbox(
            "Stand behind Passage of Arms", () =>
            this.config.ManagePassageOfArmsPositioning,
            this.defaultConfig.ManagePassageOfArmsPositioning,
            v => this.config.ManagePassageOfArmsPositioning = v,
            disabledTooltip: movementDisabledTooltip);
        this.Spacing();
        this.DrawSectionHeader("Position preferences");
        this.Checkbox(
            "Choose useful dodge landings", () => this.config.ManageNaturalDestinations,
            this.defaultConfig.ManageNaturalDestinations,
            v => this.config.ManageNaturalDestinations = v,
            "Prefers dodge landings that keep attacks and heals in range. BossMod controls the safe route and timing.",
            movementDisabledTooltip);
        this.Checkbox(
            "Give party members space", () =>
            this.config.ManageSocialSpacing,
            this.defaultConfig.ManageSocialSpacing,
            v => this.config.ManageSocialSpacing = v,
            "Gives nearby players space when attacks and mechanics allow. Still joins stack markers.",
            movementDisabledTooltip);
        this.Checkbox(
            "Avoid standing inside enemies", () =>
            this.config.AvoidStandingInsideEnemies,
            this.defaultConfig.AvoidStandingInsideEnemies,
            v => this.config.AvoidStandingInsideEnemies = v,
            "Avoids standing inside enemies when a useful safe position is available.",
            movementDisabledTooltip);
        this.Checkbox(
            "Avoid arena edge", () =>
            this.config.AvoidArenaEdge,
            this.defaultConfig.AvoidArenaEdge,
            v => this.config.AvoidArenaEdge = v,
            disabledTooltip: movementDisabledTooltip);
        this.Checkbox("Follow party facing during downtime", () => this.config.ManageSocialTurning, this.defaultConfig.ManageSocialTurning, v => this.config.ManageSocialTurning = v, "Faces the same way as nearby party members during downtime.");
        if (!this.config.ManageMovement) this.EndDisabled();
        this.sectionVisible = true;
    }

    private void DrawAoeAndTrashTab()
    {
        this.DrawSectionHeader("Dungeon pulls");
        var movementDisabledTooltip = !this.config.ManageMovement ? "Turn on Enable AI in General." : null;

        if (!this.config.ManageMovement)
            this.BeginDisabled();
        this.Checkbox("Keep up with dungeon pulls", () => this.config.MaintainPullContact, this.defaultConfig.MaintainPullContact, v => this.config.MaintainPullContact = v, "Keeps up with an observed moving tank gathering dungeon enemies. Stops when combat ends.", movementDisabledTooltip);
        if (!this.config.ManageMovement)
            this.EndDisabled();

        this.DrawAvailability(() => this.config.ManageAoePackPositioning || this.config.PickBetterAoeTarget ? this.ActionPredictionAvailability?.Invoke() : null);
        this.DrawSectionHeader("AoE coverage");
        this.Checkbox("Pick better AoE target", () => this.config.PickBetterAoeTarget, this.defaultConfig.PickBetterAoeTarget,
            v => this.config.PickBetterAoeTarget = v,
            "Targets more enemies with your next AoE without losing current hits. Works with movement assistance off. Respects manual target changes. Requires RotationSolver.");
        if (!this.config.ManageMovement)
            this.BeginDisabled();
        this.Checkbox("Move for better AoE hits", () => this.config.ManageAoePackPositioning, this.defaultConfig.ManageAoePackPositioning,
            v => this.config.ManageAoePackPositioning = v,
            "Moves to improve your next AoE when safe. Prefers a target change if it avoids moving. Requires RotationSolver.", movementDisabledTooltip);
        if (!this.config.ManageMovement)
            this.EndDisabled();
        this.Spacing();
    }

    private void DrawPositionalsTab()
    {
        this.DrawToggleSectionHeader("Move for positionals", () => this.config.ManagePositionals, this.defaultConfig.ManagePositionals, v => this.config.ManagePositionals = v);
        this.DrawAvailability(() => this.config.ManageTrueNorth ? this.rotationSolverUnavailable ?? this.trueNorthWarning() : null);
        var positionalsDisabledTooltip = !this.config.ManagePositionals ? "Requires Move for positionals." : null;
        if (!this.config.ManagePositionals)
            this.BeginDisabled();
        this.Checkbox(
            "Use True North", () =>
            this.config.ManageTrueNorth,
            this.defaultConfig.ManageTrueNorth,
            v =>
            {
                this.config.ManageTrueNorth = v;
                if (v)
                {
                    this.manageTrueNorthEnabled();
                }
            },
            "Uses True North when the correct rear/flank is unsafe or unreachable.",
            positionalsDisabledTooltip);
        if (!this.config.ManagePositionals)
            this.EndDisabled();
        this.Spacing();
    }

    private void DrawJobSpecificTab()
    {
        this.BeginJobSection("Party support");
        this.Checkbox("Avoid overlapping party mitigation", () => this.config.PreserveCoveredRangedMitigation,
            this.defaultConfig.PreserveCoveredRangedMitigation, value => this.config.PreserveCoveredRangedMitigation = value,
            "Saves Tactician, Troubadour or Shield Samba when equivalent party protection already covers the predicted hit.");
        if (this.config.HealRaisedNonPartyPlayers || this.config.ManageKardiaRecipient || this.config.ManageDancePartnerRecipient ||
            this.config.ManageFeyUnionRecipient || this.config.ManageAstCardRecipients)
            this.DrawAvailability(() => this.SupportRecipientStatus?.Invoke());
        this.Checkbox("Manage Kardia recipient", () => this.config.ManageKardiaRecipient, this.defaultConfig.ManageKardiaRecipient,
            v => this.config.ManageKardiaRecipient = v,
            "Chooses a party member who needs Kardia healing, usually the tank taking damage. Requires RotationSolver.");
        this.Checkbox("Heal raised non-party players to full", () => this.config.HealRaisedNonPartyPlayers, this.defaultConfig.HealRaisedNonPartyPlayers,
            v => this.config.HealRaisedNonPartyPlayers = v,
            "Heals a non-party player you just raised until they reach full HP. Party healing takes priority. Requires RotationSolver.");
        this.Checkbox("Manage Dance Partner", () => this.config.ManageDancePartnerRecipient, this.defaultConfig.ManageDancePartnerRecipient,
            v => this.config.ManageDancePartnerRecipient = v,
            "Chooses a damage dealer for Dance Partner. Keeps a partner you selected by name in RotationSolver.");
        this.Checkbox("Manage Fey Union recipient", () => this.config.ManageFeyUnionRecipient, this.defaultConfig.ManageFeyUnionRecipient,
            v => this.config.ManageFeyUnionRecipient = v,
            "Tethers the fairy to a party member who needs healing. Ends the tether when no longer useful. Requires RotationSolver.");
        this.Checkbox("Manage Astrologian cards", () => this.config.ManageAstCardRecipients, this.defaultConfig.ManageAstCardRecipients,
            v => this.config.ManageAstCardRecipients = v,
            "Chooses recipients for damage, healing and defensive cards. RotationSolver decides when to play them.");
        this.Spacing();

        this.BeginJobSection("Black Mage");
        this.Checkbox("Move at the last safe moment", () => this.config.BlackMageLateMovement, this.defaultConfig.BlackMageLateMovement,
            v => this.config.BlackMageLateMovement = v,
            "Delays movement to preserve casting time when a safe escape is available. Requires Black Mage movement abilities.",
            icon: FontAwesomeIcon.SkullCrossbones,
            iconTooltip: "Late movement gives less reaction time. Latency or a moving dash target can cause a hit.");
        this.DrawAvailability(() => !this.config.ManageMovement ? "Returns to Ley Lines require Enable AI." : null);
        this.Combo("Return to Ley Lines", () => this.config.LeyLinesReturn, this.defaultConfig.LeyLinesReturn,
            v => this.config.LeyLinesReturn = v,
            "Returns to Ley Lines when safe, using your chosen movement method.",
            v => v switch
            {
                LeyLinesReturnMode.AbilitiesOnly => "Abilities only",
                LeyLinesReturnMode.WalkingAndAbilities => "Walking and abilities",
                _ => "Off"
            });
        var leylinesDisabledTooltip = !this.config.ManageLeylines ? "Enable a Return to Ley Lines method." : null;
        if (!this.config.ManageLeylines)
            this.BeginDisabled();
        this.Checkbox("Choose Ley Lines placement", () => this.config.PlanLeyLinesPlacement, this.defaultConfig.PlanLeyLinesPlacement,
            v => this.config.PlanLeyLinesPlacement = v,
            "Chooses a nearby Ley Lines position with more casting time before the next mechanic. Requires RotationSolver.",
            disabledTooltip: leylinesDisabledTooltip);
        this.Checkbox("Leave Ley Lines at the last safe moment", () => this.config.GreedyLeyLines, this.defaultConfig.GreedyLeyLines,
            v => this.config.GreedyLeyLines = v,
            "Leaves Ley Lines late with Aetherial Manipulation, then returns when safe. Requires Black Mage movement abilities and Between the Lines.",
            disabledTooltip: leylinesDisabledTooltip, icon: FontAwesomeIcon.SkullCrossbones,
            iconTooltip: "Late movement gives less reaction time. Latency or a moving dash target can cause a hit.");
        this.Checkbox("Use Between the Lines", () => this.config.UseBetweenTheLines, this.defaultConfig.UseBetweenTheLines, v => this.config.UseBetweenTheLines = v, disabledTooltip: leylinesDisabledTooltip);
        this.Checkbox("Use Retrace", () => this.config.UseRetrace, this.defaultConfig.UseRetrace, v => this.config.UseRetrace = v,
            "Moves active Ley Lines to you when it improves your selected spell. Keeps its remaining duration and allows for the action lock.",
            disabledTooltip: leylinesDisabledTooltip);
        if (!this.config.ManageLeylines)
            this.EndDisabled();
        this.Spacing();

        this.BeginJobSection("Pictomancer");
        var movementDisabledTooltip = !this.config.ManageMovement ? "Requires Enable AI." : null;
        if (!this.config.ManageMovement)
        {
            this.BeginDisabled();
        }

        this.Checkbox(
            "Stay in Starry Muse", () =>
            this.config.ManagePictomancerStarryMuse,
            this.defaultConfig.ManagePictomancerStarryMuse,
            v => this.config.ManagePictomancerStarryMuse = v,
            "Returns to an observed spot in your landscape when it improves your current spell and remains safe. Does not place it.",
            movementDisabledTooltip);
        this.DrawAvailability(() => this.config.UsePictomancerStarryMuseSmudge && (!this.config.UseGapCloser || !this.config.GapCloserPCT)
            ? "Smudge requires Use movement abilities and Pictomancer in Movement abilities." : null);
        var starryDisabledTooltip = movementDisabledTooltip ?? (!this.config.ManagePictomancerStarryMuse ? "Requires Stay in Starry Muse." : null);
        if (!this.config.ManagePictomancerStarryMuse && this.config.ManageMovement)
        {
            this.BeginDisabled();
        }

        this.Checkbox(
            "Use Smudge to return", () =>
            this.config.UsePictomancerStarryMuseSmudge,
            this.defaultConfig.UsePictomancerStarryMuseSmudge,
            v => this.config.UsePictomancerStarryMuseSmudge = v,
            "Allows Smudge for a useful return to your landscape. Walking remains available.\nEnable Pictomancer in Movement abilities.",
            starryDisabledTooltip);
        if (!this.config.ManagePictomancerStarryMuse && this.config.ManageMovement)
        {
            this.EndDisabled();
        }

        if (!this.config.ManageMovement)
        {
            this.EndDisabled();
        }

        this.Spacing();

        this.BeginJobSection("Red Mage");
        this.DrawAvailability(() => this.config.UseRedMageMeleeComboMovement ? this.ActionPredictionAvailability?.Invoke() : null);
        if (!this.config.ManageMovement)
        {
            this.BeginDisabled();
        }

        this.Checkbox(
            "Move for melee combo", () =>
            this.config.UseRedMageMeleeComboMovement,
            this.defaultConfig.UseRedMageMeleeComboMovement,
            v => this.config.UseRedMageMeleeComboMovement = v,
            "Prepares a safe position for evidenced melee combos while preserving your selected spell.\nUses enabled movement abilities when useful. Requires RotationSolver.",
            movementDisabledTooltip);
        if (!this.config.ManageMovement)
        {
            this.EndDisabled();
        }

        this.Spacing();

        this.BeginJobSection("Tanks");
        this.DrawSectionHeader("Enmity & tank swaps");
        this.Checkbox(
            "Target nearby loose enemies", () =>
            this.config.TankTargetLostTrashAggro,
            this.defaultConfig.TankTargetLostTrashAggro,
            v => this.config.TankTargetLostTrashAggro = v,
            "Targets nearby enemies attacking party members so you can recover their aggro.");
        this.Checkbox(
            "Use ranged attacks to recover aggro", () =>
            this.config.TankUseRangedAggroRecovery,
            this.defaultConfig.TankUseRangedAggroRecovery,
            v => this.config.TankUseRangedAggroRecovery = v,
            "Uses ranged attacks to recover loose enemies. May use Provoke in duties; excludes FATEs.",
            icon: FontAwesomeIcon.SkullCrossbones,
            iconTooltip: "May spend Provoke or your next attack to recover aggro.");
        this.Checkbox(
            "Manage off-tank stance", () =>
            this.config.TankDropStanceWhenCoTankHasStance,
            this.defaultConfig.TankDropStanceWhenCoTankHasStance,
            v => this.config.TankDropStanceWhenCoTankHasStance = v,
            "Builds backup enmity, then turns stance off before overtaking the main tank. Keeps stance on while you hold enemies.",
            icon: FontAwesomeIcon.SkullCrossbones,
            iconTooltip: "Changes your tank stance automatically. Turn this off if you want to manage stance yourself.");
        this.Checkbox(
            "Take aggro after the tank dies", () =>
            this.config.TankRecoverAfterTankDeath,
            this.defaultConfig.TankRecoverAfterTankDeath,
            v => this.config.TankRecoverAfterTankDeath = v,
            "Enables stance and takes aggro after an enemy’s tank dies. Leaves enemies held by living tanks alone.",
            icon: FontAwesomeIcon.SkullCrossbones,
            iconTooltip: "May change stance, use Provoke and turn an enemy toward you.");
        this.Checkbox(
            "Follow BossMod tank swap requests", () =>
            this.config.TankFollowBossModSwaps,
            this.defaultConfig.TankFollowBossModSwaps,
            v => this.config.TankFollowBossModSwaps = v,
            "Takes aggro when BossMod requests a tank swap. Requires correct BossMod roles. Does not use Shirk.",
            icon: FontAwesomeIcon.SkullCrossbones,
            iconTooltip: "Takes enemy aggro automatically. Check your BossMod role assignments first.");
        this.Spacing();

        this.sectionVisible = true;
    }

    private void DrawDashesTab()
    {
        this.DrawToggleSectionHeader(
            "Use movement abilities", () =>
            this.config.UseGapCloser,
            this.defaultConfig.UseGapCloser,
            v => this.config.UseGapCloser = v,
            tooltip: "Uses permitted movement abilities for safety and attack range, including damaging gap closers.");
        var gapCloserDisabledTooltip = !this.config.UseGapCloser ? "Requires Use movement abilities." : null;
        if (!this.config.UseGapCloser)
        {
            this.BeginDisabled();
        }

        this.DrawSectionHeader("Allow movement abilities for");
        this.BeginJobGrid();
        {
            foreach (var toggle in AssistancePreferences.GapCloserJobToggles)
            {
                this.JobCheckbox(
                    toggle.Label, () =>
                    toggle.Get(this.config),
                    toggle.Get(this.defaultConfig),
                    value => toggle.Set(this.config, value),
                    gapCloserDisabledTooltip);
            }

            this.EndJobGrid();
        }

        this.DrawSectionHeader("Occult Crescent");
        this.Checkbox(
            "Use Phantom movement abilities", () =>
            this.config.UsePhantomGapClosers,
            this.defaultConfig.UsePhantomGapClosers,
            v => this.config.UsePhantomGapClosers = v,
            "Allows Phantom Job movement in Occult Crescent. Occult Sprint also needs Use Sprint in Movement.",
            gapCloserDisabledTooltip);
        if (!this.config.UseGapCloser)
        {
            this.EndDisabled();
        }

        this.Spacing();
    }

    private void DrawBmrFixesTab()
    {
        this.sectionVisible = this.ActiveTab == 0;
        this.DrawSectionHeader("Movement fixes");
        this.Checkbox("Choose safe forced march headings", () => this.config.FixBmrForcedMarchHeading,
            this.defaultConfig.FixBmrForcedMarchHeading, v => this.config.FixBmrForcedMarchHeading = v,
            "Chooses a safe forced-march direction. Requires Protect forced march facing.");
        this.Checkbox("Protect forced march facing", () => this.config.FixBmrForcedMarchFacing,
            this.defaultConfig.FixBmrForcedMarchFacing, v => this.config.FixBmrForcedMarchFacing = v,
            "Briefly pauses RotationSolver attacks to preserve your forced-march direction, then resumes your previous mode.");
        this.Checkbox(
            "Use normal movement in Dawntrail hallways", () =>
            this.config.FixBmrDawntrailHallwayModules,
            this.defaultConfig.FixBmrDawntrailHallwayModules,
            v => this.config.FixBmrDawntrailHallwayModules = v,
            "Uses normal dungeon-trash movement in supported Dawntrail hallways. Restores the previous module settings when disabled. Boss encounters stay enabled.");
        this.Checkbox(
            "Keep stack markers apart", () =>
            this.config.FixBmrStackSeparation,
            this.defaultConfig.FixBmrStackSeparation,
            v => this.config.FixBmrStackSeparation = v,
            "Keeps simultaneous circle-stack markers apart while BossMod handles the mechanic.");
        this.Checkbox("Keep FATE terrain checks aligned", () => this.config.FixBmrFateArenaFrame,
            this.defaultConfig.FixBmrFateArenaFrame, v => this.config.FixBmrFateArenaFrame = v,
            "Keeps terrain checks aligned with the search window in supported FATEs. Encounter arenas and mechanic avoidance stay under BossMod control.");
        this.DrawSectionHeader("Quest compatibility");
        this.Checkbox("Restore missing Questionable quest targeting", () => this.config.FixQuestionableQuestTargeting,
            this.defaultConfig.FixQuestionableQuestTargeting, v => this.config.FixQuestionableQuestTargeting = v,
            "Restores the target selector if it is missing from Questionable's solo-duty preset. Existing target settings and quest movement are preserved.");
        this.sectionVisible = true;
    }

    private void DrawTroubleshootingTab()
    {
        this.sectionVisible = this.ActiveTab == 0;
        this.DrawSectionHeader("Plugin connections");
        this.DrawConnections();
        this.Spacing();

        this.DrawSectionHeader("RotationSolver compatibility");
        this.Checkbox("Restore ranged attacks after dashes", () => this.config.FixRsrMeleeRangedAfterMovement,
            this.defaultConfig.FixRsrMeleeRangedAfterMovement, v => this.config.FixRsrMeleeRangedAfterMovement = v,
            "Fixes ranged attacks staying blocked after dashes on Reaper, Dragoon, Ninja and Samurai. Disable to compare normal RotationSolver behavior.");
        this.Spacing();

        this.sectionVisible = this.ActiveTab == 1;
        this.DrawSectionHeader("Overlay");
        this.Checkbox(
            "Show decision overlay", () =>
            this.config.ShowDecisionOverlay,
            this.defaultConfig.ShowDecisionOverlay,
            v => this.config.ShowDecisionOverlay = v,
            "Shows in-world markers for movement, attack range, positionals and party support.");
        this.Combo(
            "Overlay detail", () =>
            this.config.DecisionOverlayDensity,
            this.defaultConfig.DecisionOverlayDensity,
            v => this.config.DecisionOverlayDensity = v,
            "Minimal: main destination and path.\nNormal: adds nearby decisions.\nDetailed: adds troubleshooting markers.",
            v => v switch
            {
                OverlayDensity.Minimal => "Minimal",
                OverlayDensity.Normal => "Normal",
                OverlayDensity.Detailed => "Detailed",
                _ => v.ToString()
            },
            this.config.ShowDecisionOverlay ? null : "Requires Show decision overlay.");
        this.Spacing();

        this.DrawSectionHeader("Get help");
        this.Button("Copy troubleshooting report", () =>
        {
            this.copyToClipboard(this.debugState());
            this.copiedDebugStateUntil = DateTime.UtcNow.AddSeconds(2);
        });
        this.DynamicText(() => DateTime.UtcNow < this.copiedDebugStateUntil ? "Copied." : string.Empty);
        this.sectionVisible = true;
    }

}
