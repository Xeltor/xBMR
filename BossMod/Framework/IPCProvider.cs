using BossMod.AI;
using BossMod.Autorotation;
using BossMod.Pathfinding;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BossMod;

sealed class IPCProvider : IDisposable
{
    private Action? _disposeActions;

    public IPCProvider(BossModuleManager bossmod, AIHints hints, RotationModuleManager autorotation, ActionManagerEx amex, MovementOverride movement, AIManager ai, ObstacleMapManager obstacles, Integration.IntegrationCoordinator integration)
    {
        Register("Integration.V1.Capabilities", integration.Capabilities);
        Register("Integration.V1.Register", (string request, Action<string> callback) => Integration.IntegrationSession.Serialize(integration.Session.Register(Integration.IntegrationSession.Parse<Integration.IntegrationSession.Registration>(request), callback)));
        Register("Integration.V1.Renew", (string request) => Integration.IntegrationSession.Serialize(integration.Session.Renew(Integration.IntegrationSession.Parse<Integration.IntegrationSession.Lease>(request))));
        Register("Integration.V1.Unregister", (string request) => Integration.IntegrationSession.Serialize(integration.Session.Unregister(Integration.IntegrationSession.Parse<Integration.IntegrationSession.Lease>(request))));
        Register("Goals.V1.Submit", (string request, Func<Vector2, float> score) => Integration.IntegrationSession.Serialize(integration.Session.Submit(Integration.IntegrationSession.Parse<Integration.IntegrationSession.GoalRequest>(request), score)));
        Register("Goals.V1.SubmitNoLoss", (string request, Func<Vector2, float> score) => Integration.IntegrationSession.Serialize(integration.Session.SubmitNoLoss(Integration.IntegrationSession.Parse<Integration.IntegrationSession.NoLossRequest>(request), score)));
        Register("Goals.V1.SubmitNoLossBatch", (string request, Func<Vector2, float>[] scores) => Integration.IntegrationSession.Serialize(integration.Session.SubmitNoLossBatch(Integration.IntegrationSession.Parse<Integration.IntegrationSession.NoLossBatch>(request), scores)));
        Register("Goals.V1.Clear", (string request) => Integration.IntegrationSession.Serialize(integration.Session.Clear(Integration.IntegrationSession.Parse<Integration.IntegrationSession.Lease>(request))));
        Register("Goals.V1.EvaluateNativeDetails", (string request) => integration.EvaluateNativeGoalDetails(request));
        Register("Goals.V1.EvaluateNative", (string request) => integration.EvaluateNativeGoals(request));
        Register("Arena.V1.ApplyFateFrame", (string request) => integration.ApplyFateFrame(request));
        Register("Movement.V1.ApplyDutyIngress", (string request) => integration.ApplyDutyIngress(request));
        Register("Movement.V1.GetPhysicalStep", (string request) => integration.GetPhysicalStep(request));
        Register("Movement.V1.RequestPace", (string request) => integration.RequestPace(request));
        Register("Movement.V1.RequestDepartureHold", (string request) => integration.RequestDepartureHold(request));
        Register("Movement.V1.RequestArrivalHold", (string request) => integration.RequestArrivalHold(request));
        Register("Movement.V1.RequestFollowArrivalHold", (string request) => integration.RequestFollowArrivalHold(request));
        Register("Movement.V1.RequestFollowPhysicalBrake", (string request) => integration.RequestFollowPhysicalBrake(request));
        Register("Movement.V1.RequestPhysicalBrake", (string request) => integration.RequestPhysicalBrake(request));
        Register("Movement.V1.ClearDepartureHold", (string request) => integration.ClearPace(request));
        Register("Movement.V1.ClearPace", (string request) => integration.ClearPace(request));
        Register("Movement.V1.GetSnapshot", (string request) => integration.GetSnapshot(request));
        Register("Movement.V1.Latest", integration.GetLatestSnapshot);
        Register("Hints.V1.CaptureConstraintDistances", (string request, Action<string, Func<Vector2, float>[]> receive) => integration.CaptureConstraintDistances(request, receive));
        Register("Mechanics.V1.CaptureForecastDistances", (string request, Action<string, Func<Vector2, float>[]> receive) => integration.CaptureForecastDistances(request, receive));
        Register("Mechanics.V1.EvaluateForcedMarchHeadings", (string request) => integration.EvaluateForcedMarchHeadings(request));
        Register("Directions.V1.ConstrainForcedMarchHeading", (string request) => integration.ConstrainForcedMarchHeading(request));
        Register("Hints.V1.SetStackCarrierSeparation", (string request) => integration.SetStackCarrierSeparation(request));
        Register("Mechanics.V1.ProjectNativeKnockbackPoses", (string request) => integration.ProjectNativeKnockbackPoses(request));
        Register("Knockback.V1.StartSafetySnapshot", (string request) => integration.StartSafetySnapshot(request));
        Register("Knockback.V1.PollSafetySnapshot", (string request) => integration.PollSafetySnapshot(request));
        Register("Knockback.V1.GetSafetyWindow", (string request) => integration.GetSafetyWindow(request));
        Register("Knockback.V1.StartSafetyRoute", (string request) => integration.StartSafetyRoute(request));
        Register("Knockback.V1.PollSafetyRoute", (string request) => integration.PollSafetyRoute(request));
        Register("Diagnostics.V1.CheckControlBackend", (string request) => integration.CheckControlBackend(request));
        Register("Diagnostics.V1.CheckSafetyBackend", (string request) => integration.CheckSafetyBackend(request));
        Register("Knockback.V1.EvaluateSafetySegments", (string request) => integration.EvaluateSafetySegments(request));
        Register("Constraints.V1.Submit", (string request, Func<Vector2, float> distance) => integration.SubmitConstraint(request, distance));
        Register("Constraints.V1.SubmitCells", (string request) => integration.SubmitConstraintCells(request));
        Register("Constraints.V1.Poll", (string request) => integration.PollConstraint(request));
        Register("Constraints.V1.Clear", (string request) => Integration.IntegrationSession.Serialize(integration.Session.Clear(Integration.IntegrationSession.Parse<Integration.IntegrationSession.Lease>(request))));
        Register("Hints.V1.GetCastControl", (string request) => integration.GetCastControl(request));
        Register("Hints.V1.GetTargets", (string request) => integration.GetTargets(request));
        Register("Hints.V1.GetPredictedDamage", (string request) => integration.GetPredictedDamage(request));
        Register("World.V1.GetActionEffects", (string request) => integration.GetActionEffects(request));
        Register("Arena.V1.GetSnapshot", (string request) => integration.GetArenaSnapshot(request));
        Register("Arena.V1.QueryPoints", (string request) => integration.QueryArena(request));
        Register("Navigation.V1.StartSearch", (string request) => integration.StartSearch(request));
        Register("Navigation.V1.PollSearch", (string request) => integration.PollSearch(request));
        Register("Navigation.V1.GetSnapshot", (string request) => integration.GetNavigationSnapshot(request));
        Register("Navigation.V1.GetLocalMap", (string request) => integration.GetNavigationMap(request));
        Register("Navigation.V1.EvaluateRoutes", (string request) => integration.EvaluateRoutes(request));
        Register("Follow.V1.GetSnapshot", (string request) => integration.GetFollow(request));
        Register("Follow.V1.Stop", (string request) => integration.StopFollow(request));
        Register("Mechanics.V1.GetSnapshot", (string request) => integration.GetMechanics(request));
        Register("Mechanics.V1.EvaluateMovement", (string request) => integration.EvaluateMechanicMovement(request));
        Register("Mechanics.V1.ProjectPoses", (string request) => integration.ProjectMechanicPoses(request));
        Register("Mechanics.V1.EvaluateLandings", (string request) => integration.EvaluateKnockbackLandings(request));
        Register("Registry.V1.GetSnapshot", (string request) => integration.GetStaticRegistry(request));
        Register("Replay.V1.DecodeEvidence", (string request, Func<string, System.Threading.Tasks.Task> receive, System.Threading.CancellationToken cancellation) => integration.DecodeEvidence(request, receive, cancellation));
        Register("Modules.V1.GetSnapshot", (string request) => integration.GetModules(request));
        Register("Modules.V1.GetEnablement", (string request) => integration.GetModuleEnablement(request));
        Register("Modules.V1.ApplyEnablement", (string request) => integration.ApplyModuleEnablement(request));
        Register("Strategies.V1.GetSnapshot", (string request) => integration.GetStrategy(request));
        Register("Strategies.V1.Apply", (string request) => integration.ApplyStrategy(request));
        Register("Control.V1.GetSettings", (string request) => integration.GetSettings(request));
        Register("Control.V1.ApplySetting", (string request) => integration.ApplySetting(request));
        Register("Control.V1.Renew", (string request) => integration.RenewControl(request));
        Register("Control.V1.Release", (string request) => integration.ReleaseControl(request));
        Register("Control.V1.WithDirectionalAngle", (string request, Func<bool> execute) => integration.WithDirectionalAngle(request, execute));
        _disposeActions += integration.Dispose;

        Register("HasModuleByDataId", static (uint dataId) => BossModuleRegistry.FindByOID(dataId) != null);

        // Timeline IPC endpoints for external plugin integration (e.g. RotationSolverReborn)
        Register("HasActiveModule", () => bossmod.ActiveModule?.StateMachine.ActiveState != null);
        Register("ActiveModuleName", () => bossmod.ActiveModule?.PrimaryActor.Name.ToString());

        // Debug endpoint: walks the state machine and reports what it finds
        Register("Debug.TimelineWalk", () =>
        {
            var module = bossmod.ActiveModule;
            if (module == null)
            {
                return "No active module";
            }

            var sm = module.StateMachine;
            var activestate = sm.ActiveState;
            if (activestate == null)
            {
                return "ActiveState is null";
            }

            var sb = new StringBuilder();
            sb.Append($"Phase={sm.ActivePhaseIndex} State={activestate.ID:X}({activestate.Name}) Dur={activestate.Duration:F1}s Hint={activestate.EndHint}");
            var count = 0;
            var next = activestate;
            var foundRW = false;
            var foundTB = false;
            while (next != null && count < 20)
            {
                if (!foundRW && (next.EndHint & StateMachine.StateHint.Raidwide) != 0)
                {
                    foundRW = true;
                    sb.Append($" | RW@{next.ID:X}({next.Name})");
                }
                if (!foundTB && (next.EndHint & StateMachine.StateHint.Tankbuster) != 0)
                {
                    foundTB = true;
                    sb.Append($" | TB@{next.ID:X}({next.Name})");
                }
                next = next.NextStates?.Length == 1 ? next.NextStates[0] : null;
                ++count;
            }
            if (!foundRW)
            {
                sb.Append(" | RW=NONE");
            }

            if (!foundTB)
            {
                sb.Append(" | TB=NONE");
            }

            if (next == null && count < 20)
            {
                sb.Append($" | Chain ended at {count} states");
            }

            if (count >= 20)
            {
                sb.Append(" | Walked 20+ states");
            }

            return sb.ToString();
        });

        Register("Timeline.NextRaidwideIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
            {
                return float.MaxValue;
            }

            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.Raidwide);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextTankbusterIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
            {
                return float.MaxValue;
            }

            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.Tankbuster);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextKnockbackIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
            {
                return float.MaxValue;
            }

            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.Knockback);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextDowntimeIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
            {
                return float.MaxValue;
            }

            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.DowntimeStart);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextDowntimeEndIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
            {
                return float.MaxValue;
            }

            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.DowntimeEnd);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextVulnerableIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
            {
                return float.MaxValue;
            }

            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.VulnerableStart);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextVulnerableEndIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
            {
                return float.MaxValue;
            }

            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.VulnerableEnd);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Hints.NextDamageIn", () =>
        {
            var predicted = hints.PredictedDamage;
            return predicted.Count == 0 ? float.MaxValue : (float)(predicted[0].Activation - DateTime.Now).TotalSeconds;
        });

        Register("Hints.NextDamageType", () =>
        {
            var predicted = hints.PredictedDamage;
            return predicted.Count == 0 ? 0 : (int)predicted[0].Type;
        });

        Register("Hints.MaxCastTime", () => hints.MaxCastTime);

        Register("Hints.ForceCancelCastMechanic", () => hints.ForceCancelCastMechanic);
        Register("Hints.ForceCancelCastOther", () => hints.ForceCancelCastOther);

        Register("Hints.ForceCancelCastMechanicAI", () => ai.Controller.ForceCancelCastMechanicAI);
        Register("Hints.ForceCancelCastOtherAI", () => ai.Controller.ForceCancelCastOtherAI);

        Register("Movement.IsMoving", () => hints.EffectiveForcedMovement != null || hints.HoldCasts);
        Register("Movement.IsMoveRequested", movement.IsMoveRequested);
        Register("Hints.ForbiddenZonesCount", () => hints.ForbiddenZones.Count);
        Register("Hints.ForbiddenZonesNextActivation", () => hints.ForbiddenZones.Count == 0 ? float.MaxValue : (float)(hints.ForbiddenZones[0].activation - DateTime.Now).TotalSeconds);
        Register("Hints.ArenaCenter", () => new Vector2(hints.PathfindMapCenter.X, hints.PathfindMapCenter.Z));
        Register("Hints.ArenaRadius", () => hints.PathfindMapBounds.Radius);
        Register("Hints.PredictedDamagePlayers", () => hints.PredictedDamage.Count == 0 ? 0ul : hints.PredictedDamage[0].Players.Raw);
        Register("Hints.ForbiddenDirectionsCount", () => hints.ForbiddenDirections.Count);
        Register("Hints.ShouldCleansePlayers", () => hints.ShouldCleanse.Raw);
        Register("Hints.InteractWithTargetOID", () => hints.InteractWithTarget?.InstanceID ?? 0ul);
        Register("Hints.RecommendedPositional", () => (int)hints.RecommendedPositional.Pos);
        Register("AI.PauseMovement", static (bool pause) => Service.Config.Get<AIConfig>().ForbidMovement = pause);
        Register("AI.NaviTargetPos", () =>
        {
            var pos = ai.Controller.NaviTargetPos;
            return pos.HasValue ? new Vector3(pos.Value.X, 0, pos.Value.Z) : (Vector3?)null;
        });
        Register("AI.IsNavigating", () => ai.Controller.NaviTargetPos != null);
        Register("AI.PlayerSpeed", () => ai.WorldState.Client.MoveSpeed);
        // ---------------------------------

        // Type-specific damage prediction endpoints — search ALL entries for the first matching type
        Register("Hints.NextRaidwideDamageIn", () =>
        {
            var predicted = hints.PredictedDamage;
            var now = DateTime.Now;
            for (var i = 0; i < predicted.Count; ++i)
            {
                if (predicted[i].Type == AIHints.PredictedDamageType.Raidwide)
                {
                    return (float)(predicted[i].Activation - now).TotalSeconds;
                }
            }
            return float.MaxValue;
        });

        Register("Hints.NextTankbusterDamageIn", () =>
        {
            var predicted = hints.PredictedDamage;
            var now = DateTime.Now;
            for (var i = 0; i < predicted.Count; ++i)
            {
                if (predicted[i].Type == AIHints.PredictedDamageType.Tankbuster)
                {
                    return (float)(predicted[i].Activation - now).TotalSeconds;
                }
            }
            return float.MaxValue;
        });

        Register("Hints.SpecialModeIn", () =>
        {
            return hints.ImminentSpecialMode == default
                ? float.MaxValue
                : (float)(hints.ImminentSpecialMode.activation - DateTime.Now).TotalSeconds;
        });

        Register("Hints.SpecialModeType", () =>
        {
            return hints.ImminentSpecialMode == default ? 0 : (int)hints.ImminentSpecialMode.mode;
        });

        // Returns true if the destination position is safe (within arena bounds, not in a forbidden zone or temporary obstacle).
        // 'from' is the player's current world position (XZ used); 'to' is the intended destination (XZ used).
        // Use this to validate movement-ability targets before executing them.
        Register("Hints.IsPositionSafe", (Vector3 to) =>
        {
            var player = bossmod.WorldState.Party.Player();
            return player != null && ActionPredicate.IsDashSafe(player.Position, new WPos(to.X, to.Z), hints);
        });

        // Same as Hints.IsPositionSafe but with an explicit source position, useful when the dash origin differs from the player's current position.
        Register("Hints.IsDashSafe", (Vector3 from, Vector3 to) =>
            ActionPredicate.IsDashSafe(new WPos(from.X, from.Z), new WPos(to.X, to.Z), hints));

        // Returns true if dashing forward (or backward) by 'range' yalms from the player's current position/rotation is safe.
        // Mirrors DashFixedDistanceCheck: dest = playerPos + playerRotation * range (negated when backwards=true).
        Register("Hints.IsFixedDashSafe", (float range, bool backwards) =>
        {
            var player = bossmod.WorldState.Party.Player();
            if (player == null)
            {
                return false;
            }

            var dest = player.Position + player.Rotation.ToDirection() * range * (backwards ? -1f : 1f);
            return ActionPredicate.IsDashSafe(player.Position, dest, hints);
        });

        // Returns true if backdashing 'range' yalms directly away from 'enemyPos' is safe.
        // Mirrors BackdashCheck: dir = normalize(playerPos - enemyPos), dest = playerPos + dir * range.
        Register("Hints.IsBackdashSafe", (Vector3 enemyPos, float range) =>
        {
            var player = bossmod.WorldState.Party.Player();
            if (player == null)
            {
                return false;
            }

            var dir = (player.Position - new WPos(enemyPos.X, enemyPos.Z)).Normalized();
            var dest = player.Position + dir * range;
            return ActionPredicate.IsDashSafe(player.Position, dest, hints);
        });

        // VBM clients use IReadOnlyList (including collection expressions); older BMR clients use List.
        // Keep returning a concrete List so both subscriber return types remain compatible.
        Register("Configuration", (IReadOnlyList<string> args, bool save) => IPCConfiguration.Execute(Service.Config, args, save));
        // same arguments as Configuration, but the change is never saved; ClearTransient restores the user's values
        Register("Configuration.SetTransient", (List<string> args) => Service.Config.SetTransient(args.AsSpan()));
        Register("Configuration.ClearTransient", () => Service.Config.ClearTransient());

        var lastModified = DateTime.Now;
        Service.Config.Modified.Subscribe(() => lastModified = DateTime.Now);
        Register("Configuration.LastModified", () => lastModified);

        Register("Rotation.ActionQueue.HasEntries", () =>
        {
            var entries = CollectionsMarshal.AsSpan(autorotation.Hints.ActionsToExecute.Entries);
            var len = entries.Length;
            for (var i = 0; i < len; ++i)
            {
                ref readonly var e = ref entries[i];
                if (!e.Manual)
                {
                    return true;
                }
            }
            return false;
        });

        Register("Presets.Get", (string name) =>
        {
            var preset = autorotation.Database.Presets.FindPresetByName(name);
            return preset != null ? JsonSerializer.Serialize(preset, Serialization.BuildSerializationOptions()) : null;
        });
        Register("Presets.Create", (string presetSerialized, bool overwrite) =>
        {
            var node = JsonNode.Parse(presetSerialized, documentOptions: new JsonDocumentOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (node == null)
            {
                return false;
            }

            // preset converter operates on array of presets; plan converter doesn't but expects an `Encounter` key in the object
            node = new JsonArray(node);

            var version = 0;
            var finfo = new FileInfo("<in-memory preset>");

            foreach (var conv in PlanPresetConverter.PresetSchema.Converters)
            {
                node = conv(node, version, finfo);
            }

            var p = node.AsArray()[0].Deserialize<Preset>(Serialization.BuildSerializationOptions());
            if (p == null)
            {
                return false;
            }

            var index = autorotation.Database.Presets.UserPresets.FindIndex(x => x.Name == p.Name);
            if (index >= 0 && !overwrite)
            {
                return false;
            }

            autorotation.Database.Presets.Modify(index, p);
            return true;
        });
        Register("Presets.Delete", (string name) =>
        {
            var index = autorotation.Database.Presets.UserPresets.FindIndex(x => x.Name == name);
            if (index < 0)
            {
                return false;
            }

            autorotation.Database.Presets.Modify(index, null);
            return true;
        });

        Register("Presets.GetActive", () => autorotation.Preset?.Name);
        Register("Presets.SetActive", (string name) =>
        {
            var preset = autorotation.Database.Presets.FindPresetByName(name);
            if (preset == null)
            {
                return false;
            }

            autorotation.Preset = preset;
            return true;
        });
        Register("Presets.ClearActive", () =>
        {
            if (autorotation.Preset == null)
            {
                return false;
            }

            autorotation.Preset = null;
            return true;
        });
        Register("Presets.GetForceDisabled", () => autorotation.Preset == RotationModuleManager.ForceDisable);
        Register("Presets.SetForceDisabled", () =>
        {
            if (autorotation.Preset == RotationModuleManager.ForceDisable)
            {
                return false;
            }

            autorotation.Preset = RotationModuleManager.ForceDisable;
            return true;
        });

        bool addTransientStrategy(string presetName, string moduleTypeName, string trackName, string value, StrategyTarget target = StrategyTarget.Automatic, int targetParam = 0)
        {
            var mt = RotationModuleRegistry.FindType(moduleTypeName);
            if (mt == null || !RotationModuleRegistry.Modules.TryGetValue(mt, out var md))
            {
                return false;
            }

            var iTrack = md.Definition.Configs.FindIndex(td => td.InternalName == trackName);
            if (iTrack < 0)
            {
                return false;
            }

            StrategyValue tempValue;

            switch (md.Definition.Configs[iTrack])
            {
                case StrategyConfigTrack tr:
                    var iOpt = tr.Options.FindIndex(od => od.InternalName == value);
                    if (iOpt < 0)
                    {
                        return false;
                    }

                    tempValue = new StrategyValueTrack() { Option = iOpt, Target = target, TargetParam = targetParam };
                    break;
                case StrategyConfigFloat sc:
                    tempValue = new StrategyValueFloat() { Value = Math.Clamp(float.Parse(value), sc.MinValue, sc.MaxValue) };
                    break;
                case StrategyConfigInt si:
                    tempValue = new StrategyValueInt() { Value = Math.Clamp(long.Parse(value), si.MinValue, si.MaxValue) };
                    break;
                case var x:
                    throw new ArgumentException($"unhandled config type {x.GetType()}");
            }

            var ms = autorotation.Database.Presets.FindPresetByName(presetName)?.Modules.Find(m => m.Type == mt);
            if (ms == null)
            {
                return false;
            }

            if (ms.HasOwnedStrategy(iTrack))
                return false; // a legacy client cannot steal a V1 owner

            var setting = new Preset.ModuleSetting(default, iTrack, tempValue);
            var index = ms.TransientSettings.FindIndex(s => s.Track == iTrack);
            if (index < 0)
            {
                ms.TransientSettings.Add(setting);
            }
            else
            {
                ms.TransientSettings[index] = setting;
            }

            return true;
        }
        Register("Presets.AddTransientStrategy", (string presetName, string moduleTypeName, string trackName, string value) => addTransientStrategy(presetName, moduleTypeName, trackName, value));
        Register("Presets.AddTransientStrategyTargetEnemyOID", (string presetName, string moduleTypeName, string trackName, string value, int oid) => addTransientStrategy(presetName, moduleTypeName, trackName, value, StrategyTarget.EnemyByOID, oid));

        Register("Presets.ClearTransientStrategy", (string presetName, string moduleTypeName, string trackName) =>
        {
            var mt = RotationModuleRegistry.FindType(moduleTypeName);
            if (mt == null || !RotationModuleRegistry.Modules.TryGetValue(mt, out var md))
            {
                return false;
            }

            var iTrack = md.Definition.Configs.FindIndex(td => td.InternalName == trackName);
            if (iTrack < 0)
            {
                return false;
            }

            var ms = autorotation.Database.Presets.FindPresetByName(presetName)?.Modules.Find(m => m.Type == mt);
            if (ms == null)
            {
                return false;
            }

            var index = ms.TransientSettings.FindIndex(s => s.Track == iTrack);
            if (index < 0)
            {
                return false;
            }

            ms.TransientSettings.RemoveAt(index);
            return true;
        });
        Register("Presets.ClearTransientModuleStrategies", (string presetName, string moduleTypeName) =>
        {
            var mt = RotationModuleRegistry.FindType(moduleTypeName);
            if (mt == null || !RotationModuleRegistry.Modules.TryGetValue(mt, out var md))
            {
                return false;
            }

            var ms = autorotation.Database.Presets.FindPresetByName(presetName)?.Modules.Find(m => m.Type == mt);
            if (ms == null)
            {
                return false;
            }

            ms.TransientSettings.Clear();
            return true;
        });
        Register("Presets.ClearTransientPresetStrategies", (string presetName) =>
        {
            var preset = autorotation.Database.Presets.FindPresetByName(presetName);
            if (preset == null)
            {
                return false;
            }

            foreach (var ms in preset.Modules)
            {
                ms.TransientSettings.Clear();
            }

            return true;
        });

        Register("AI.SetPreset", (string name) =>
        {
            Preset? found = null;
            foreach (var p in autorotation.Database.Presets.AllPresets)
            {
                if (p.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    found = p;
                    break;
                }
            }

            ai.SetAIPreset(found);
        });
        Register("AI.GetPreset", () => ai.GetAIPreset);

        Register("AI.SetPreset", (string name) =>
        {
            Preset? found = null;
            foreach (var p in autorotation.Database.Presets.AllPresets)
            {
                if (p.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    found = p;
                    break;
                }
            }

            ai.SetAIPreset(found);
        });
        Register("AI.GetPreset", () => ai.GetAIPreset);

        Register("ObstacleMap.Generate", (Vector3 centerWorld, float radius, bool writeToFile) => obstacles.GenerateMap(centerWorld, radius, writeToFile, false));
        Register("ObstacleMap.GetGenerationStatus", () => obstacles.GenerationStatus);
        Register("ObstacleMap.HasTempMap", obstacles.HasTempMap);
        Register("ObstacleMap.ClearTempMap", obstacles.ClearTempMap);
        Register("ObstacleMap.EvaluateTempMapQuality", obstacles.EvaluateTempMapQuality);

        // Cooldown Planner IPC endpoints for external plugin integration (RSR)
        // returns a JSON-serialized array of PlanExecution.PlannedAction entries, resolved along the currently active plan branch
        Register("Plan.GetUpcomingActions", (float lookAheadSeconds) =>
        {
            var planner = autorotation.Planner;
            if (planner == null)
                return "[]";

            var actions = planner.GetUpcomingPlannedActions(bossmod.WorldState, autorotation.PlayerSlot, lookAheadSeconds);
            return JsonSerializer.Serialize(actions);
        });

        // push notification: fired whenever the active plan changes
        var plannedActionsChangedProvider = Service.PluginInterface.GetIpcProvider<object>("BossMod.Plan.ActionsChanged");
        void OnPlannedActionsChanged() => plannedActionsChangedProvider.SendMessage();
        autorotation.PlannedActionsChanged += OnPlannedActionsChanged;
        _disposeActions += () => autorotation.PlannedActionsChanged -= OnPlannedActionsChanged;
    }

    public void Dispose() => _disposeActions?.Invoke();

    private void Register<TRet>(string name, Func<TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, TRet>(string name, Func<T1, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, T2, TRet>(string name, Func<T1, T2, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, T2, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, T2, T3, TRet>(string name, Func<T1, T2, T3, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, T2, T3, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, T2, T3, T4, TRet>(string name, Func<T1, T2, T3, T4, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, T2, T3, T4, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, T2, T3, T4, T5, TRet>(string name, Func<T1, T2, T3, T4, T5, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, T2, T3, T4, T5, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    //private void Register(string name, Action func)
    //{
    //    var p = Service.PluginInterface.GetIpcProvider<object>("BossMod." + name);
    //    p.RegisterAction(func);
    //    _disposeActions += p.UnregisterAction;
    //}

    private void Register<T1>(string name, Action<T1> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, object>("BossMod." + name);
        p.RegisterAction(func);
        _disposeActions += p.UnregisterAction;
    }
}
