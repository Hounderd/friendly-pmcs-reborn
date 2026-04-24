#if SPT_CLIENT
using EFT;
using EFT.InventoryLogic;
using FriendlyPMC.CoreFollowers.Models;
using FriendlyPMC.CoreFollowers.Services;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace FriendlyPMC.CoreFollowers.Modules;

internal sealed class BotOwnerFollowerRuntimeHandle : IFollowerRuntimeHandle
{
    private const float OrderReviewIntervalSeconds = 0.75f;
    private static readonly CustomFollowerRuntimeCadenceSettings CustomBrainCadenceSettings = new(
        DecisionReviewIntervalSeconds: 0.15f,
        PathRefreshIntervalSeconds: 0.6f,
        RecoveryRefreshIntervalSeconds: 0.3f);

    private readonly BotOwner botOwner;
    private readonly FollowerSnapshotDto identitySnapshot;
    private readonly FriendlyPmcCoreFollowersPlugin? plugin;
    private readonly FollowerRuntimeCompatibilityController runtimeCompatibilityController;
    private FollowerCommand? activeCommand;
    private CustomFollowerNavigationProgressState customNavigationProgressState;
    private CustomFollowerMovementDispatchState customMovementDispatchState;
    private CustomFollowerCombatStallState customCombatStallState;
    private CustomFollowerCombatAssistState customCombatAssistState;
    private CustomFollowerFormationTargetState customFormationTargetState;
    private bool customMovementYieldedToCombatPressure;
    private bool shouldBreakCustomCombatStall;
    private bool isCustomPatrolSuppressed;
    private float nextOrderReviewTime;
    private float lastExplicitCommandTimeSeconds;
    private float lastHitTimeSeconds;
    private float lastThreatStimulusTimeSeconds;
    private string? lastThreatStimulusType;
    private string? lastThreatStimulusAttackerProfileId;
    private string? lastCommandSummary;
    private string? lastTargetBiasSummary;
    private string? lastCombatAssistSummary;
    private string? lastCombatAssistTraceKey;
    private string? lastCleanupSummary;
    private string? lastUnblockSummary;
    private string? observedRequestType;
    private float observedRequestSinceTimeSeconds;
    private FollowerThreatStimulusGateState threatStimulusGateState;
    private bool sainAccuracyTuningChecked;
    private static readonly EBodyPart[] HealCommandBodyParts =
    {
        EBodyPart.Head,
        EBodyPart.Chest,
        EBodyPart.Stomach,
        EBodyPart.LeftArm,
        EBodyPart.RightArm,
        EBodyPart.LeftLeg,
        EBodyPart.RightLeg,
    };

    public BotOwnerFollowerRuntimeHandle(BotOwner botOwner, FollowerSnapshotDto? identitySnapshot = null)
    {
        this.botOwner = botOwner;
        this.identitySnapshot = identitySnapshot ?? CaptureLiveSnapshot();
        plugin = FriendlyPmcCoreFollowersPlugin.Instance;
        runtimeCompatibilityController = new FollowerRuntimeCompatibilityController(
            () => FollowerLootingRuntimeDisabler.DisableForFollower(
                botOwner,
                message =>
                {
                    plugin?.LogPluginInfo(message);
                }),
            () => UnityEngine.Time.time);
        if (botOwner.GetPlayer is not null)
        {
            botOwner.GetPlayer.BeingHitAction += OnBeingHit;
        }
    }

    public string Aid => identitySnapshot.Aid;

    public string RuntimeProfileId => botOwner.ProfileId;

    public bool IsOperational => botOwner is not null && !botOwner.IsDead;

    public int HealthPercent
    {
        get
        {
            var health = botOwner.HealthController.GetBodyPartHealth(EBodyPart.Common, true);
            if (health.Maximum <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp((int)Math.Round((health.Current / health.Maximum) * 100f), 0, 100);
        }
    }

    public BotDebugWorldPoint CurrentPosition => BotDebugSnapshotMapper.GetWorldPoint(botOwner);

    public BotDebugWorldPoint GetPlateAnchorPoint()
    {
        var neck = botOwner.GetPlayer?.PlayerBones?.Neck;
        var verticalOffset = plugin?.PlateSettings.VerticalOffsetWorld ?? FollowerPlateSettings.DefaultVerticalOffsetWorld;
        var position = neck is not null
            ? neck.position + new Vector3(0f, verticalOffset, 0f)
            : (botOwner.GetPlayer?.Transform.position ?? Vector3.zero) + new Vector3(0f, 1.8f + verticalOffset, 0f);

        return new BotDebugWorldPoint(position.x, position.y, position.z);
    }

    public FollowerSnapshotDto CaptureSnapshot()
    {
        var liveSnapshot = CaptureLiveSnapshot();
        return liveSnapshot with
        {
            Aid = identitySnapshot.Aid,
            Nickname = identitySnapshot.Nickname,
            Side = identitySnapshot.Side,
        };
    }

    public BotDebugSnapshot CaptureDebugSnapshot(FollowerCommand? activeOrder, BotDebugWorldPoint localPlayerPosition)
    {
        var controlPath = plugin?.Registry.TryGetControlPathRuntime(Aid, out var controlPathRuntime) == true
            ? controlPathRuntime.ActivePath.ToString()
            : null;
        var customBrainSession = plugin?.Registry.TryGetCustomBrainSession(Aid, out var resolvedCustomBrainSession) == true
            ? resolvedCustomBrainSession
            : null;
        var customDebugState = customBrainSession is not null
            ? customBrainSession.CurrentDebugState
            : (CustomFollowerDebugState?)null;
        var now = UnityEngine.Time.time;
        var followCooldownActive = customBrainSession?.Receiver.IsInFollowCombatSuppressionCooldown(now) == true;
        var followCooldownRemainingSeconds = followCooldownActive
            ? MathF.Max(0f, CustomFollowerBrainPolicy.FollowCombatSuppressionCooldownSeconds - (now - customBrainSession!.Receiver.LastFollowCommandTimestamp))
            : -1f;
        var distanceToHoldAnchor = plugin?.Registry.TryGetHoldAnchor(Aid, out var holdAnchor) == true
            ? CurrentPosition.DistanceTo(holdAnchor)
            : -1f;
        var activeCommandAgeSeconds = activeCommand.HasValue && lastExplicitCommandTimeSeconds > 0f
            ? now - lastExplicitCommandTimeSeconds
            : -1f;
        var currentRequestAgeSeconds = ObserveCurrentRequestAge(now);
        var requester = GamePlayerOwner.MyPlayer;
        var enemyDebugState = CaptureEnemyDebugState(requester);

        return BotDebugSnapshotMapper.CreateFriendlyFollowerSnapshot(
            botOwner,
            identitySnapshot,
            activeOrder,
            localPlayerPosition,
            controlPath,
            customDebugState?.Mode.ToString(),
            customDebugState?.NavigationIntent.ToString(),
            enemyDebugState.IsUnderFire,
            enemyDebugState.GoalEnemyHaveSeen,
            enemyDebugState.GoalEnemyLastSeenAgeSeconds,
            enemyDebugState.HasActionableEnemy,
            enemyDebugState.DistanceToNearestActionableEnemyMeters,
            enemyDebugState.DistanceToGoalEnemyMeters,
            enemyDebugState.KnownEnemiesSummary,
            enemyDebugState.RecentThreatStimulus,
            enemyDebugState.RecentThreatAgeSeconds,
            enemyDebugState.RecentThreatAttackerProfileId,
            followCooldownActive,
            followCooldownRemainingSeconds,
            distanceToHoldAnchor,
            activeCommandAgeSeconds,
            currentRequestAgeSeconds,
            customMovementYieldedToCombatPressure,
            lastCommandSummary,
            lastTargetBiasSummary,
            lastCombatAssistSummary,
            lastCleanupSummary,
            lastUnblockSummary);
    }

    public void Execute(FollowerCommand command)
    {
        if (command == FollowerCommand.Attention)
        {
            ApplyAttention();
            return;
        }

        if (command == FollowerCommand.Heal)
        {
            ApplyHeal();
            return;
        }

        if (command == FollowerCommand.Loot)
        {
            activeCommand = command;
            lastExplicitCommandTimeSeconds = UnityEngine.Time.time;
            nextOrderReviewTime = 0f;
            ApplyLoot();
            return;
        }

        activeCommand = command;
        lastExplicitCommandTimeSeconds = UnityEngine.Time.time;
        if (command is FollowerCommand.Hold or FollowerCommand.TakeCover)
        {
            plugin?.Registry.SetHoldAnchor(Aid, CurrentPosition);
        }

        nextOrderReviewTime = 0f;
        ApplyCommand(command, isRefresh: false);
    }

    private void ApplyAttention()
    {
        var requester = GamePlayerOwner.MyPlayer;
        if (requester is null || botOwner is null || botOwner.IsDead)
        {
            plugin?.LogPluginInfo(
                $"Skipped command {FollowerCommand.Attention} for {botOwner?.Profile?.Info?.Nickname ?? identitySnapshot.Nickname}: requester/controller unavailable or bot dead");
            return;
        }

        botOwner.BotRequestController?.TryStopCurrent(requester, false);
        ClearQueuedRequests(requester);

        var goalEnemy = botOwner.Memory?.GoalEnemy;
        var resetDecision = FollowerAttentionResetPolicy.Evaluate(
            botOwner.Memory?.HaveEnemy == true,
            ReadGoalEnemyBool(goalEnemy, "IsVisible"),
            ReadGoalEnemyBool(goalEnemy, "CanShoot"),
            ResolveGoalEnemyLastSeenAgeSeconds(goalEnemy));
        if (resetDecision.ShouldClearGoalEnemy && botOwner.Memory is not null)
        {
            if (goalEnemy?.Person is not null)
            {
                botOwner.Memory.DeleteInfoAboutEnemy(goalEnemy.Person);
            }

            botOwner.Memory.GoalEnemy = null;
            botOwner.Memory.LastEnemy = null;
        }

        lastThreatStimulusTimeSeconds = 0f;
        lastThreatStimulusType = null;
        lastThreatStimulusAttackerProfileId = null;
        shouldBreakCustomCombatStall = false;
        nextOrderReviewTime = 0f;

        var commandToRefresh = GetEffectiveCommand() ?? FollowerCommand.Follow;
        ApplyCommand(commandToRefresh, isRefresh: true);
        plugin?.LogPluginInfo(
            $"Follower attention reset: follower={identitySnapshot.Nickname}, aid={Aid}, resumed={commandToRefresh}, clearedGoal={resetDecision.ShouldClearGoalEnemy}");
    }

    public void TickOrder()
    {
        runtimeCompatibilityController.Tick();
        TryApplySainAccuracyTuning();

        CustomFollowerBrainRuntimeSession? customBrainSession = null;
        if (plugin?.Registry.TryGetCustomBrainSession(Aid, out var resolvedCustomBrainSession) == true)
        {
            customBrainSession = resolvedCustomBrainSession;
        }
        var effectiveCommand = activeCommand ?? customBrainSession?.Receiver.CurrentState.Command;

        if (!effectiveCommand.HasValue)
        {
            return;
        }

        if (activeCommand == FollowerCommand.Loot)
        {
            UpdateCustomPatrolSuppression(false);
            return;
        }

        if (GamePlayerOwner.MyPlayer is { } requester)
        {
            TryClearDeadEnemyMemory();
            var targetBiasResult = FollowerPlayerTargetBiasRuntimeService.TryApplyPreferredTarget(botOwner, requester, effectiveCommand.Value);
            lastTargetBiasSummary =
                $"shot={FollowerPlayerShotMemory.TryGetRecentShot(out _)};applied={targetBiasResult.AppliedTargetBias};boot={targetBiasResult.BootstrappedThreat};assist={targetBiasResult.ShouldActivateCombatAssist};target={targetBiasResult.PreferredTargetProfileId ?? "None"}";
            ApplyPlayerTargetBiasResult(targetBiasResult, effectiveCommand.Value);
            if (customBrainSession is not null)
            {
                var customTickResult = TickCustomBrainSession(customBrainSession, requester, effectiveCommand.Value);
                if (TryMaintainCustomBrain(customBrainSession, requester, customTickResult))
                {
                    return;
                }
            }
            else
            {
                UpdateCustomPatrolSuppression(false);
                TryApplyAmbientCombatAssist(requester, effectiveCommand.Value);
            }
        }

        if (!activeCommand.HasValue)
        {
            return;
        }

        if (UnityEngine.Time.time < nextOrderReviewTime)
        {
            return;
        }

        MaintainActiveOrder(activeCommand.Value);
    }

    private CustomFollowerBrainTickResult TickCustomBrainSession(
        CustomFollowerBrainRuntimeSession session,
        Player requester,
        FollowerCommand command)
    {
        var followerPosition = CurrentPosition;
        var distanceToPlayer = followerPosition.DistanceTo(BotDebugSnapshotMapper.GetWorldPoint(requester));
        var distanceToHoldAnchor = plugin?.Registry.TryGetHoldAnchor(Aid, out var holdAnchor) == true
            ? followerPosition.DistanceTo(holdAnchor)
            : 0f;
        var navigationStuckResult = CustomFollowerNavigationStuckPolicy.Evaluate(
            UnityEngine.Time.time,
            customNavigationProgressState,
            session.CurrentDebugState.NavigationIntent,
            followerPosition,
            distanceToPlayer,
            botOwner.Mover?.IsMoving ?? false);
        customNavigationProgressState = navigationStuckResult.NextState;
        var combatStallResult = CustomFollowerCombatStallPolicy.Evaluate(
            UnityEngine.Time.time,
            customCombatStallState,
            command,
            session.CurrentDebugState.NavigationIntent,
            BotDebugBrainInspector.GetActiveLayerName(botOwner),
            BotDebugBrainInspector.GetActiveLogicName(botOwner),
            botOwner.Mover?.IsMoving ?? false,
            distanceToPlayer);
        customCombatStallState = combatStallResult.NextState;
        shouldBreakCustomCombatStall = combatStallResult.ShouldBreakStall;
        var hasPreferredTarget = command is FollowerCommand.Follow or FollowerCommand.Combat or FollowerCommand.TakeCover
            && FollowerPlayerShotMemory.TryGetRecentShot(out _)
            && botOwner.Memory?.GoalEnemy is not null;
        var hasActionableEnemy = HasActionableEnemy(requester);
        var nearestActionableEnemyDistance = ComputeNearestActionableEnemyDistance(requester, followerPosition);
        var goalEnemy = botOwner.Memory?.GoalEnemy;
        var distanceToGoalEnemy = TryResolveGoalEnemyDistance(goalEnemy, followerPosition) ?? float.MaxValue;
        var isUnderFire = ReadBoolValue(botOwner.Memory, "IsUnderFire");
        var goalEnemyHaveSeen = ReadGoalEnemyBool(goalEnemy, "HaveSeen");
        var goalEnemyLastSeenAgeSeconds = ResolveGoalEnemyLastSeenAgeSeconds(goalEnemy);

        return session.Tick(
            UnityEngine.Time.time,
            distanceToPlayer,
            distanceToHoldAnchor,
            hasActionableEnemy,
            nearestActionableEnemyDistance,
            distanceToGoalEnemy,
            isUnderFire,
            goalEnemyHaveSeen,
            goalEnemyLastSeenAgeSeconds,
            hasPreferredTarget,
            isNavigationStuck: navigationStuckResult.IsStuck || combatStallResult.ShouldBreakStall,
            plugin?.ModeSettings ?? new FollowerModeSettings(),
            CustomBrainCadenceSettings);
    }

    private bool TryMaintainCustomBrain(
        CustomFollowerBrainRuntimeSession session,
        Player requester,
        CustomFollowerBrainTickResult tickResult)
    {
        var directive = CustomFollowerMaintenancePolicy.Resolve(tickResult);
        var combatPressure = ResolveCombatPressureContext(requester, CurrentPosition);
        var movementControlDecision = FriendlyFollowerMovementControlPolicy.Evaluate(
            plugin?.Registry.TryGetControlPathRuntime(Aid, out var controlPathRuntime) == true
                ? controlPathRuntime.ActivePath
                : (DebugSpawnFollowerControlPath?)null,
            tickResult.DebugState.Command,
            tickResult.DebugState.Mode,
            combatPressure.HasActionableEnemy,
            combatPressure.DistanceToNearestActionableEnemyMeters,
            combatPressure.IsUnderFire,
            legacyShouldControlMovement: false);
        TryStopStaleCombatRequest(
            session,
            requester,
            tickResult.DebugState);
        TryClearStaleEnemyMemory(
            requester,
            tickResult.DebugState);
        UpdateCustomPatrolSuppression(
            CustomFollowerPatrolSuppressionPolicy.ShouldSuppress(tickResult.DebugState.Mode));
        TryUnblockImmediateCombatPressure(
            requester,
            tickResult.DebugState,
            combatPressure);
        TryApplyCombatAssist(session, requester);
        UpdateCustomMovementCombatYieldState(
            movementControlDecision,
            tickResult.DebugState,
            combatPressure);
        if (!directive.SuppressLegacyMaintenance)
        {
            return false;
        }

        if (directive.NavigationIntent.HasValue && movementControlDecision.YieldedToCombatPressure)
        {
            return false;
        }

        if (shouldBreakCustomCombatStall)
        {
            shouldBreakCustomCombatStall = false;
            ApplyCommand(session.Receiver.CurrentState.Command, isRefresh: true);
        }

        if (directive.NavigationIntent.HasValue)
        {
            if (plugin?.Registry.TryGetControlPathRuntime(Aid, out var currentControlPathRuntime) == true
                && currentControlPathRuntime.ActivePath == DebugSpawnFollowerControlPath.CustomBrain
                && movementControlDecision.ShouldControlMovement)
            {
                return true;
            }

            var settings = plugin?.ModeSettings ?? new FollowerModeSettings();
            var distanceToPlayer = CurrentPosition.DistanceTo(BotDebugSnapshotMapper.GetWorldPoint(requester));
            var plan = CustomFollowerMovementExecutionPolicy.Resolve(
                session.Receiver.CurrentState.Command,
                directive.NavigationIntent.Value,
                distanceToPlayer,
                settings);
            var formationSlotIndex = ResolveFormationSlotIndex();
            var desiredTargetPoint = CustomFollowerMovementTargetPointPolicy.Resolve(
                requester,
                session.Receiver.CurrentState.Command,
                plan.MovementIntent,
                formationSlotIndex);
            var targetPoint = ResolveCustomTargetPoint(
                requester,
                plan,
                desiredTargetPoint);
            var dispatchResult = CustomFollowerMovementDispatchPolicy.Evaluate(
                UnityEngine.Time.time,
                customMovementDispatchState,
                directive.NavigationIntent.Value,
                targetPoint,
                distanceToPlayer);

            if ((tickResult.ReviewedDecision || tickResult.RefreshedPath)
                && (dispatchResult.ShouldDispatch || plan.ForcePathRefresh))
            {
                customMovementDispatchState = dispatchResult.NextState;
                CustomFollowerMovementExecutor.TryExecutePlan(
                    botOwner,
                    requester,
                    plan,
                    targetPoint);
            }
            else if (plan.ShouldMove)
            {
                CustomFollowerMovementExecutor.MaintainMotionState(botOwner, plan);
            }
        }
        else if (directive.ShouldRefreshHold && tickResult.ReviewedDecision)
        {
            ApplyCommand(FollowerCommand.Hold, isRefresh: true);
        }

        return true;
    }

    private void UpdateCustomMovementCombatYieldState(
        FriendlyFollowerMovementControlDecision decision,
        CustomFollowerDebugState debugState,
        FollowerCombatPressureContext combatPressure)
    {
        if (customMovementYieldedToCombatPressure == decision.YieldedToCombatPressure)
        {
            return;
        }

        customMovementYieldedToCombatPressure = decision.YieldedToCombatPressure;
        plugin?.LogPluginInfo(
            $"Follower custom movement {(decision.YieldedToCombatPressure ? "yielded" : "resumed")} due to combat pressure: follower={identitySnapshot.Nickname}, aid={Aid}, command={debugState.Command}, mode={debugState.Mode}, actionable={combatPressure.HasActionableEnemy}, underFire={combatPressure.IsUnderFire}, enemyDistance={(combatPressure.DistanceToNearestActionableEnemyMeters < float.MaxValue ? combatPressure.DistanceToNearestActionableEnemyMeters.ToString("0.0") : "None")}");
    }

    private void TryApplyCombatAssist(
        CustomFollowerBrainRuntimeSession session,
        Player requester)
    {
        TryApplyCombatAssistAction(
            requester,
            session.CurrentDebugState.Command,
            session.CurrentDebugState.Mode,
            source: "custom");
    }

    private BotDebugWorldPoint ResolveCustomTargetPoint(
        Player requester,
        CustomFollowerMovementExecutionPlan plan,
        BotDebugWorldPoint desiredTargetPoint)
    {
        if (plan.MovementIntent != FollowerMovementIntent.MoveToFormation)
        {
            customFormationTargetState = default;
            return desiredTargetPoint;
        }

        var result = CustomFollowerFormationTargetPolicy.Resolve(
            customFormationTargetState,
            BotDebugSnapshotMapper.GetWorldPoint(requester),
            desiredTargetPoint);
        customFormationTargetState = result.NextState;
        return result.TargetPoint;
    }

    private void UpdateCustomPatrolSuppression(bool shouldSuppress)
    {
        if (botOwner.PatrollingData is null || isCustomPatrolSuppressed == shouldSuppress)
        {
            return;
        }

        if (shouldSuppress)
        {
            botOwner.PatrollingData.Pause();
        }
        else
        {
            botOwner.PatrollingData.Unpause();
        }

        isCustomPatrolSuppressed = shouldSuppress;
    }

    private void TryApplySainAccuracyTuning()
    {
        if (sainAccuracyTuningChecked || botOwner?.Settings?.Current is null)
        {
            return;
        }

        sainAccuracyTuningChecked = SainFollowerAccuracyRuntimeTuner.TryApply(
            botOwner,
            identitySnapshot.Nickname,
            message => plugin?.LogPluginInfo(message));
    }

    private int ResolveFormationSlotIndex()
    {
        if (plugin?.Registry is null)
        {
            return 0;
        }

        var orderedAids = plugin.Registry.RuntimeFollowers
            .Where(follower => follower.IsOperational)
            .Select(follower => follower.Aid)
            .OrderBy(aid => aid, StringComparer.Ordinal)
            .ToArray();
        var index = Array.IndexOf(orderedAids, Aid);

        return Math.Max(0, index);
    }

    private void OnBeingHit(DamageInfoStruct damageInfo, EBodyPart bodyPart, float damageReducedByArmor)
    {
        var attacker = damageInfo.Player?.iPlayer;
        if (attacker is null)
        {
            return;
        }

        var currentTargetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory?.GoalEnemy);
        var currentTargetVisible = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "IsVisible");
        var currentTargetCanShoot = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "CanShoot");
        var currentTargetLastSeenAgeSeconds = ResolveGoalEnemyLastSeenAgeSeconds(botOwner.Memory?.GoalEnemy);
        var registeredFollowerProfileIds = plugin?.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId)
            ?? Array.Empty<string>();
        var localPlayerProfileId = GamePlayerOwner.MyPlayer?.ProfileId;
        var attackerIsProtected = FollowerProtectionPolicy.ShouldProtectPlayer(
            botOwner.ProfileId,
            registeredFollowerProfileIds,
            localPlayerProfileId,
            attacker.ProfileId);
        var currentTargetIsProtected = FollowerProtectionPolicy.ShouldProtectPlayer(
            botOwner.ProfileId,
            registeredFollowerProfileIds,
            localPlayerProfileId,
            currentTargetProfileId);
        var reaction = FollowerUnderFireReactionPolicy.Evaluate(
            attacker.ProfileId,
            attackerIsProtected,
            currentTargetProfileId,
            currentTargetIsProtected);

        if (!reaction.ShouldSetUnderFire)
        {
            return;
        }

        if (reaction.ShouldBreakHealing)
        {
            TryInterruptCurrentHealing($"under-fire attacker={attacker.ProfileId}");
        }

        var bootstrapped = FollowerUnderFireThreatBootstrapper.TryBootstrapThreat(
            botOwner,
            attacker,
            reaction.ShouldSetUnderFire,
            reaction.ShouldPromoteAttackerAsGoalEnemy,
            shouldMarkAttackerVisible: true,
            awarenessPosition: attacker.Transform.position);
        TryForceThreatGoalReplacement(
            attacker,
            bootstrapped,
            currentTargetProfileId,
            currentTargetVisible,
            currentTargetCanShoot,
            currentTargetLastSeenAgeSeconds,
            currentTargetIsProtected);
        TryActivateThreatCombatAssist(bootstrapped);
        lastHitTimeSeconds = UnityEngine.Time.time;
        lastThreatStimulusAttackerProfileId = attacker.ProfileId;
        plugin?.LogPluginInfo(
            $"Follower under-fire bootstrap: follower={identitySnapshot.Nickname}, aid={Aid}, attacker={attacker.ProfileId}, promoted={reaction.ShouldPromoteAttackerAsGoalEnemy}, bootstrapped={bootstrapped}");
    }

    internal void HandleThreatStimulus(IPlayer attacker, FollowerThreatStimulusType stimulusType, Vector3 stimulusPosition)
    {
        if (attacker is null)
        {
            return;
        }

        var now = UnityEngine.Time.time;
        var effectiveCommand = GetEffectiveCommand() ?? FollowerCommand.Follow;
        var isInFollowCombatSuppressionCooldown =
            plugin?.Registry.TryGetCustomBrainSession(Aid, out var customBrainSession) == true
            && customBrainSession.Receiver.IsInFollowCombatSuppressionCooldown(now);
        var distanceToStimulus = Vector3.Distance(
            botOwner.GetPlayer?.Transform.position ?? Vector3.zero,
            stimulusPosition);
        var gateDecision = FollowerThreatStimulusGatePolicy.Evaluate(
            now,
            threatStimulusGateState,
            effectiveCommand,
            isInFollowCombatSuppressionCooldown,
            stimulusType,
            attacker.ProfileId,
            distanceToStimulus);
        threatStimulusGateState = gateDecision.NextState;
        if (!gateDecision.ShouldProcessStimulus)
        {
            return;
        }

        var registeredFollowerProfileIds = plugin?.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId)
            ?? Array.Empty<string>();
        var localPlayerProfileId = GamePlayerOwner.MyPlayer?.ProfileId;
        var attackerIsProtected = FollowerProtectionPolicy.ShouldProtectPlayer(
            botOwner.ProfileId,
            registeredFollowerProfileIds,
            localPlayerProfileId,
            attacker.ProfileId);
        var currentTargetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory?.GoalEnemy);
        var currentTargetVisible = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "IsVisible");
        var currentTargetCanShoot = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "CanShoot");
        var currentTargetLastSeenAgeSeconds = ResolveGoalEnemyLastSeenAgeSeconds(botOwner.Memory?.GoalEnemy);
        var playerIsActivelyEngaged = FollowerPlayerShotMemory.TryGetRecentShot(out _);
        var currentTargetIsProtected = FollowerProtectionPolicy.ShouldProtectPlayer(
            botOwner.ProfileId,
            registeredFollowerProfileIds,
            localPlayerProfileId,
            currentTargetProfileId);
        var reaction = FollowerThreatStimulusPolicy.Evaluate(
            stimulusType,
            attacker.ProfileId,
            attackerIsProtected,
            currentTargetProfileId,
            currentTargetVisible,
            currentTargetCanShoot,
            currentTargetLastSeenAgeSeconds,
            currentTargetIsProtected,
            playerIsActivelyEngaged,
            distanceToStimulus);
        if (!reaction.ShouldAttemptThreatBootstrap)
        {
            return;
        }

        if (reaction.ShouldBreakHealing)
        {
            TryInterruptCurrentHealing($"stimulus={stimulusType} attacker={attacker.ProfileId}");
        }

        var bootstrapped = FollowerUnderFireThreatBootstrapper.TryBootstrapThreat(
            botOwner,
            attacker,
            reaction.ShouldSetUnderFire,
            reaction.ShouldPromoteAttackerAsGoalEnemy,
            reaction.ShouldMarkAttackerVisible,
            stimulusPosition);
        TryForceThreatGoalReplacement(
            attacker,
            bootstrapped,
            currentTargetProfileId,
            currentTargetVisible,
            currentTargetCanShoot,
            currentTargetLastSeenAgeSeconds,
            currentTargetIsProtected);
        TryActivateThreatCombatAssist(bootstrapped);
        lastThreatStimulusTimeSeconds = now;
        lastThreatStimulusType = stimulusType.ToString();
        lastThreatStimulusAttackerProfileId = attacker.ProfileId;
        plugin?.LogPluginInfo(
            $"Follower threat stimulus bootstrap: follower={identitySnapshot.Nickname}, aid={Aid}, stimulus={stimulusType}, attacker={attacker.ProfileId}, distance={distanceToStimulus:F1}, promoted={reaction.ShouldPromoteAttackerAsGoalEnemy}, visibleHint={reaction.ShouldMarkAttackerVisible}, bootstrapped={bootstrapped}");
    }

    private void ApplyHeal()
    {
        var requester = GamePlayerOwner.MyPlayer;
        var player = botOwner?.GetPlayer;
        if (botOwner is null || botOwner.IsDead || player?.ActiveHealthController is null)
        {
            plugin?.LogPluginInfo(
                $"Skipped command {FollowerCommand.Heal} for {botOwner?.Profile?.Info?.Nickname ?? identitySnapshot.Nickname}: bot unavailable or dead");
            return;
        }

        TryInterruptCurrentHealing("heal-command");
        foreach (var part in HealCommandBodyParts)
        {
            if (player.ActiveHealthController.IsBodyPartBroken(part))
            {
                player.ActiveHealthController.RemoveNegativeEffects(part);
            }

            if (player.ActiveHealthController.IsBodyPartDestroyed(part))
            {
                player.ActiveHealthController.RestoreBodyPart(part, 0f);
            }
        }

        player.ActiveHealthController.RestoreFullHealth();
        if (botOwner.Memory is not null)
        {
            botOwner.Memory.LastTimeHit = 0f;
            botOwner.Memory.UnderFireTime = 0f;
        }
        if (requester is not null)
        {
            botOwner.BotRequestController?.TryStopCurrent(requester, false);
            ClearQueuedRequests(requester);
        }

        botOwner.WeaponManager?.Selector?.TakePrevWeapon();
        var weaponSelector = botOwner.WeaponManager?.Selector;
        if (weaponSelector is not null
            && weaponSelector.LastEquipmentSlot != EquipmentSlot.FirstPrimaryWeapon)
        {
            weaponSelector.TryChangeToMain();
        }

        lastHitTimeSeconds = 0f;
        lastThreatStimulusTimeSeconds = 0f;
        lastThreatStimulusType = null;
        lastThreatStimulusAttackerProfileId = null;
        shouldBreakCustomCombatStall = false;
        nextOrderReviewTime = 0f;

        var commandToRefresh = GetEffectiveCommand() ?? FollowerCommand.Follow;
        ApplyCommand(commandToRefresh, isRefresh: true);
        plugin?.LogPluginInfo(
            $"Follower heal command applied: follower={identitySnapshot.Nickname}, aid={Aid}, resumed={commandToRefresh}");
    }

    private void ApplyLoot()
    {
        if (botOwner is null || botOwner.IsDead)
        {
            plugin?.LogPluginInfo(
                $"Skipped command {FollowerCommand.Loot} for {botOwner?.Profile?.Info?.Nickname ?? identitySnapshot.Nickname}: bot unavailable or dead");
            return;
        }

        if (!LootingBotsInteropBridge.IsLootingBotsLoaded())
        {
            plugin?.LogPluginInfo(
                $"Skipped command {FollowerCommand.Loot} for {identitySnapshot.Nickname}: Looting Bots is not loaded");
            return;
        }

        if (LootingBotsInteropBridge.CheckIfInventoryFull(botOwner))
        {
            plugin?.LogPluginInfo(
                $"Skipped command {FollowerCommand.Loot} for {identitySnapshot.Nickname}: inventory full");
            return;
        }

        if (GamePlayerOwner.MyPlayer is { } requester)
        {
            botOwner.BotRequestController?.TryStopCurrent(requester, false);
            ClearQueuedRequests(requester);
        }

        var releasedSuppression = LootingBotsInteropBridge.TryAllowBotToLoot(botOwner);
        var forced = LootingBotsInteropBridge.TryForceBotToScanLoot(botOwner);
        plugin?.LogPluginInfo(
            forced
                ? $"Follower loot scan requested: follower={identitySnapshot.Nickname}, aid={Aid}, releasedSuppression={releasedSuppression}"
                : $"Skipped command {FollowerCommand.Loot} for {identitySnapshot.Nickname}: Looting Bots runtime unavailable, releasedSuppression={releasedSuppression}");
    }

    private void TryInterruptCurrentHealing(string reason)
    {
        var medecine = botOwner?.Medecine;
        if (medecine is null)
        {
            return;
        }

        var interrupted = false;
        if (medecine.FirstAid?.Using == true)
        {
            medecine.FirstAid.CancelCurrent();
            interrupted = true;
        }

        if (medecine.SurgicalKit?.Using == true)
        {
            medecine.SurgicalKit.CancelCurrent();
            interrupted = true;
        }

        if (medecine.Stimulators?.Using == true)
        {
            medecine.Stimulators.CancelCurrent();
            interrupted = true;
        }

        if (interrupted)
        {
            plugin?.LogPluginInfo(
                $"Follower healing interrupted: follower={identitySnapshot.Nickname}, aid={Aid}, reason={reason}");
        }
    }

    private void TryForceThreatGoalReplacement(
        IPlayer attacker,
        bool bootstrapped,
        string? currentTargetProfileId,
        bool currentTargetVisible,
        bool currentTargetCanShoot,
        float currentTargetLastSeenAgeSeconds,
        bool currentTargetIsProtected)
    {
        var attackerInfo = botOwner.EnemiesController?.EnemyInfos?.Values?
            .FirstOrDefault(enemy =>
                enemy is not null
                && string.Equals(enemy.ProfileId, attacker.ProfileId, StringComparison.Ordinal));
        if (attackerInfo is null)
        {
            return;
        }

        var replacementDecision = FollowerThreatGoalReplacementPolicy.Evaluate(
            bootstrapped,
            currentTargetProfileId,
            currentTargetVisible,
            currentTargetCanShoot,
            currentTargetLastSeenAgeSeconds,
            currentTargetIsProtected,
            attacker.ProfileId);
        if (!replacementDecision.ShouldForceReplaceGoal)
        {
            return;
        }

        botOwner.Memory.GoalEnemy = attackerInfo;
        plugin?.LogPluginInfo(
            $"Follower threat bootstrap replaced stale goal: follower={identitySnapshot.Nickname}, aid={Aid}, oldTarget={currentTargetProfileId ?? "None"}, newTarget={attacker.ProfileId}");
    }

    private void TryActivateThreatCombatAssist(bool bootstrapped)
    {
        var effectiveCommand = GetEffectiveCommand();
        if (!effectiveCommand.HasValue || GamePlayerOwner.MyPlayer is not { } requester)
        {
            return;
        }

        var followCombatSuppressionCooldown = plugin?.Registry.TryGetCustomBrainSession(Aid, out var customBrainSession) == true
            && customBrainSession.Receiver.IsInFollowCombatSuppressionCooldown(UnityEngine.Time.time);
        var currentRequestType = botOwner.BotRequestController?.CurRequest?.BotRequestType.ToString() ?? "None";
        var activeLayerName = BotDebugBrainInspector.GetActiveLayerName(botOwner);
        var targetVisible = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "IsVisible");
        var canShoot = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "CanShoot");
        var enemyDistance = ComputeNearestActionableEnemyDistance(requester, CurrentPosition);
        var assistDecision = FollowerThreatCombatAssistPolicy.Evaluate(
            bootstrapped,
            effectiveCommand.Value,
            activeLayerName,
            currentRequestType,
            followCombatSuppressionCooldown,
            targetVisible,
            canShoot,
            enemyDistance);
        UpdateCombatAssistSummary(
            source: "threat",
            effectiveCommand.Value,
            currentMode: null,
            activeLayerName,
            currentRequestType,
            followCombatSuppressionCooldown,
            botOwner.Memory?.HaveEnemy == true,
            targetVisible,
            canShoot,
            BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory?.GoalEnemy),
            enemyDistance,
            assistDecision.ShouldActivateSuppression,
            assistDecision.ShouldActivateAttackClose,
            assistDecision.ShouldActivateTakeCover,
            activated: false);
        if (!assistDecision.ShouldActivateSuppression
            && !assistDecision.ShouldActivateAttackClose
            && !assistDecision.ShouldActivateTakeCover)
        {
            return;
        }

        var activated = assistDecision.ShouldActivateTakeCover
            ? FollowerCombatRequestActivator.TryActivateTakeCoverRequest(botOwner, requester)
            : assistDecision.ShouldActivateAttackClose
                ? FollowerCombatRequestActivator.TryActivateAttackCloseRequest(botOwner, requester)
                : FollowerCombatRequestActivator.TryActivateSuppressionRequest(botOwner, requester);
        UpdateCombatAssistSummary(
            source: "threat",
            effectiveCommand.Value,
            currentMode: null,
            activeLayerName,
            currentRequestType,
            followCombatSuppressionCooldown,
            botOwner.Memory?.HaveEnemy == true,
            targetVisible,
            canShoot,
            BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory?.GoalEnemy),
            enemyDistance,
            assistDecision.ShouldActivateSuppression,
            assistDecision.ShouldActivateAttackClose,
            assistDecision.ShouldActivateTakeCover,
            activated);
        if (activated)
        {
            plugin?.LogPluginInfo(
                $"Follower threat stimulus activated {(assistDecision.ShouldActivateTakeCover ? "take-cover" : assistDecision.ShouldActivateAttackClose ? "attack-close" : "suppression")}: follower={identitySnapshot.Nickname}, aid={Aid}, command={effectiveCommand.Value}, target={BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory?.GoalEnemy) ?? "None"}");
        }
    }

    private void ApplyPlayerTargetBiasResult(
        FollowerPlayerTargetBiasResult result,
        FollowerCommand command)
    {
        if (result.ClearedProtectedTarget)
        {
            plugin?.LogPluginInfo(
                $"Follower protected target cleared: follower={identitySnapshot.Nickname}, aid={Aid}, command={command}");
            lastTargetBiasSummary =
                $"shot={FollowerPlayerShotMemory.TryGetRecentShot(out _)};applied={result.AppliedTargetBias};boot={result.BootstrappedThreat};assist={result.ShouldActivateCombatAssist};activated=False;target=None;clearedProtected=True";
        }

        if (!result.AppliedTargetBias
            || string.IsNullOrWhiteSpace(result.PreferredTargetProfileId))
        {
            return;
        }

        if (result.ShouldInterruptHealing)
        {
            TryInterruptCurrentHealing($"player-combat target={result.PreferredTargetProfileId}");
        }

        var now = UnityEngine.Time.time;
        lastThreatStimulusTimeSeconds = now;
        lastThreatStimulusType = "PlayerCombatAssist";
        lastThreatStimulusAttackerProfileId = result.PreferredTargetProfileId;

        var activatedSuppression = false;
        if (result.ShouldActivateCombatAssist
            && GamePlayerOwner.MyPlayer is { } requester)
        {
            var followCombatSuppressionCooldown =
                plugin?.Registry.TryGetCustomBrainSession(Aid, out var customBrainSession) == true
                && customBrainSession.Receiver.IsInFollowCombatSuppressionCooldown(now);
            var currentRequestType = botOwner.BotRequestController?.CurRequest?.BotRequestType.ToString() ?? "None";
            var activeLayerName = BotDebugBrainInspector.GetActiveLayerName(botOwner);
            var targetVisible = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "IsVisible");
            var canShoot = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "CanShoot");
            var enemyDistance = ComputeNearestActionableEnemyDistance(requester, CurrentPosition);
            var assistDecision = FollowerThreatCombatAssistPolicy.Evaluate(
                bootstrapSucceeded: true,
                command,
                activeLayerName,
                currentRequestType,
                followCombatSuppressionCooldown,
                targetVisible,
                canShoot,
                enemyDistance);
            UpdateCombatAssistSummary(
                source: "player-target",
                command,
                currentMode: null,
                activeLayerName,
                currentRequestType,
                followCombatSuppressionCooldown,
                botOwner.Memory?.HaveEnemy == true,
                targetVisible,
                canShoot,
                result.PreferredTargetProfileId,
                enemyDistance,
                assistDecision.ShouldActivateSuppression,
                assistDecision.ShouldActivateAttackClose,
                assistDecision.ShouldActivateTakeCover,
                activatedSuppression);
            if (assistDecision.ShouldActivateSuppression || assistDecision.ShouldActivateAttackClose || assistDecision.ShouldActivateTakeCover)
            {
                activatedSuppression = assistDecision.ShouldActivateTakeCover
                    ? FollowerCombatRequestActivator.TryActivateTakeCoverRequest(botOwner, requester)
                    : assistDecision.ShouldActivateAttackClose
                        ? FollowerCombatRequestActivator.TryActivateAttackCloseRequest(botOwner, requester)
                        : FollowerCombatRequestActivator.TryActivateSuppressionRequest(botOwner, requester);
                UpdateCombatAssistSummary(
                    source: "player-target",
                    command,
                    currentMode: null,
                    activeLayerName,
                    currentRequestType,
                    followCombatSuppressionCooldown,
                    botOwner.Memory?.HaveEnemy == true,
                    targetVisible,
                    canShoot,
                    result.PreferredTargetProfileId,
                    enemyDistance,
                    assistDecision.ShouldActivateSuppression,
                    assistDecision.ShouldActivateAttackClose,
                    assistDecision.ShouldActivateTakeCover,
                    activatedSuppression);
                if (activatedSuppression)
                {
                    plugin?.LogPluginInfo(
                        $"Follower player combat assist activated {(assistDecision.ShouldActivateTakeCover ? "take-cover" : assistDecision.ShouldActivateAttackClose ? "attack-close" : "suppression")}: follower={identitySnapshot.Nickname}, aid={Aid}, command={command}, target={result.PreferredTargetProfileId}");
                }
            }
        }

        if (plugin?.EnableCombatTraceDiagnostics == true)
        {
            plugin.LogPluginInfo(
                $"Follower player combat target assist: follower={identitySnapshot.Nickname}, aid={Aid}, command={command}, target={result.PreferredTargetProfileId}, bootstrapped={result.BootstrappedThreat}, activatedSuppression={activatedSuppression}");
        }
        lastTargetBiasSummary =
            $"shot={FollowerPlayerShotMemory.TryGetRecentShot(out _)};applied={result.AppliedTargetBias};boot={result.BootstrappedThreat};assist={result.ShouldActivateCombatAssist};activated={activatedSuppression};target={result.PreferredTargetProfileId ?? "None"}";
    }

    private void ApplyCommand(FollowerCommand command, bool isRefresh)
    {
        var requester = GamePlayerOwner.MyPlayer;
        if (requester is null || botOwner is null || botOwner.IsDead)
        {
            if (!isRefresh)
            {
                plugin?.LogPluginInfo(
                    $"Skipped command {command} for {botOwner?.Profile?.Info?.Nickname ?? identitySnapshot.Nickname}: requester/controller unavailable or bot dead");
            }
            return;
        }

        var plan = FollowerCommandExecutionPolicy.Resolve(command);
        var requestController = botOwner.BotsGroup?.RequestsController;
        if (requestController is null)
        {
            if (!isRefresh)
            {
                plugin?.LogPluginInfo(
                    $"Skipped command {command} for {identitySnapshot.Nickname}: group request controller unavailable");
            }
            return;
        }

        if (isRefresh && ShouldDeferRefresh(command))
        {
            nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
            return;
        }

        if (plan.ClearCurrentRequest)
        {
            botOwner.BotRequestController?.TryStopCurrent(requester, false);
        }

        if (plan.ClearQueuedRequests)
        {
            ClearQueuedRequests(requester);
        }

        bool accepted = plan.Mode switch
        {
            FollowerCommandExecutionMode.ActivatePlayerFollowState => TryActivatePlayerFollowState(requester, command == FollowerCommand.Regroup),
            FollowerCommandExecutionMode.ActivateHoldPosition => requestController.TryActivateWait(requester, botOwner),
            FollowerCommandExecutionMode.ActivateTakeCover => FollowerCombatRequestActivator.TryActivateTakeCoverRequest(botOwner, requester),
            FollowerCommandExecutionMode.ActivateCombatMode => true,
            _ => false,
        };

        var currentRequest = botOwner.BotRequestController?.CurRequest;
        var activeRequestType = currentRequest?.BotRequestType.ToString() ?? "None";
        var groupRequestCount = botOwner.BotRequestController?.GroupRequestController?.RequestsCount ?? -1;
        var playerAskRequestCount = requester.AIData?.AskRequests?.RequestsCount ?? -1;
        var haveEnemy = botOwner.Memory?.HaveEnemy ?? false;
        var haveBoss = botOwner.BotFollower?.HaveBoss ?? false;
        var patrolStatus = botOwner.PatrollingData?.Status.ToString() ?? "Unknown";
        nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
        lastCommandSummary =
            $"cmd={command};mode={plan.Mode};accepted={accepted};refresh={isRefresh};req={activeRequestType};enemy={haveEnemy};boss={haveBoss};patrol={patrolStatus}";

        plugin?.LogPluginInfo(
            $"Follower command result: follower={identitySnapshot.Nickname}, aid={Aid}, command={command}, mode={plan.Mode}, refresh={isRefresh}, accepted={accepted}, haveBoss={haveBoss}, haveEnemy={haveEnemy}, patrolStatus={patrolStatus}, currentRequest={activeRequestType}, groupRequests={groupRequestCount}, playerAskRequests={playerAskRequestCount}");
    }

    private void MaintainActiveOrder(FollowerCommand command)
    {
        if (GamePlayerOwner.MyPlayer is not { } requester || botOwner.IsDead)
        {
            return;
        }

        switch (command)
        {
            case FollowerCommand.Follow:
                MaintainFollowOrder(requester, regroup: false);
                break;
            case FollowerCommand.Regroup:
                MaintainFollowOrder(requester, regroup: true);
                break;
            case FollowerCommand.Hold:
                MaintainHoldOrder();
                break;
            case FollowerCommand.TakeCover:
                MaintainTakeCoverOrder();
                break;
            case FollowerCommand.Combat:
                MaintainCombatOrder(requester);
                break;
        }
    }

    private void MaintainFollowOrder(Player requester, bool regroup)
    {
        var disposition = GetModeDisposition(regroup ? FollowerCommand.Regroup : FollowerCommand.Follow, requester);
        if (!FollowerRuntimeModeEnforcementPolicy.ShouldDriveMovement(FollowerCommand.Follow, disposition))
        {
            nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
            return;
        }

        var distanceToPlayer = CurrentPosition.DistanceTo(BotDebugSnapshotMapper.GetWorldPoint(requester));
        var movementIntent = FollowerCatchUpPolicy.ResolveMovementIntent(
            regroup ? FollowerCommand.Regroup : FollowerCommand.Follow,
            distanceToPlayer,
            plugin?.ModeSettings ?? new FollowerModeSettings());

        FollowerMovementStateApplier.TryDriveMovementOrder(
            botOwner,
            requester,
            regroup ? FollowerCommand.Regroup : FollowerCommand.Follow,
            movementIntent,
            forceRefresh: true);
        nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
    }

    private void MaintainCombatOrder(Player requester)
    {
        var disposition = GetModeDisposition(FollowerCommand.Combat, requester);
        if (!FollowerRuntimeModeEnforcementPolicy.ShouldDriveMovement(FollowerCommand.Combat, disposition))
        {
            nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
            return;
        }

        var distanceToPlayer = CurrentPosition.DistanceTo(BotDebugSnapshotMapper.GetWorldPoint(requester));
        var movementIntent = FollowerCatchUpPolicy.ResolveMovementIntent(
            FollowerCommand.Combat,
            distanceToPlayer,
            plugin?.ModeSettings ?? new FollowerModeSettings());

        FollowerMovementStateApplier.TryDriveMovementOrder(
            botOwner,
            requester,
            FollowerCommand.Combat,
            movementIntent,
            forceRefresh: true);
        nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
    }

    private void MaintainHoldOrder()
    {
        if (GamePlayerOwner.MyPlayer is not { } requester)
        {
            nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
            return;
        }

        var disposition = GetModeDisposition(FollowerCommand.Hold, requester);
        if (!FollowerRuntimeModeEnforcementPolicy.ShouldRefreshHold(FollowerCommand.Hold, disposition))
        {
            nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
            return;
        }

        var currentRequestType = botOwner.BotRequestController?.CurRequest?.BotRequestType;
        var activeLayerName = BotDebugBrainInspector.GetActiveLayerName(botOwner);
        var hasWaitRequest = currentRequestType == BotRequestType.wait;
        var isHoldLayerActive = hasWaitRequest
            && !FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName);

        if (!FollowerHoldStatePolicy.ShouldRefreshHold(disposition, hasWaitRequest, isHoldLayerActive))
        {
            nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
            return;
        }

        ApplyCommand(FollowerCommand.Hold, isRefresh: true);
    }

    private void MaintainTakeCoverOrder()
    {
        if (GamePlayerOwner.MyPlayer is not { } requester)
        {
            nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
            return;
        }

        var disposition = GetModeDisposition(FollowerCommand.TakeCover, requester);
        if (!FollowerRuntimeModeEnforcementPolicy.ShouldRefreshHold(FollowerCommand.TakeCover, disposition))
        {
            nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
            return;
        }

        var currentRequestType = botOwner.BotRequestController?.CurRequest?.BotRequestType;
        var activeLayerName = BotDebugBrainInspector.GetActiveLayerName(botOwner);
        var hasCoverRequest = currentRequestType == BotRequestType.getInCover;
        var isCoverLayerActive = hasCoverRequest
            && !FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName);

        if (!FollowerHoldStatePolicy.ShouldRefreshHold(disposition, hasCoverRequest, isCoverLayerActive))
        {
            nextOrderReviewTime = UnityEngine.Time.time + OrderReviewIntervalSeconds;
            return;
        }

        ApplyCommand(FollowerCommand.TakeCover, isRefresh: true);
    }

    private bool ShouldDeferRefresh(FollowerCommand command)
    {
        if (GamePlayerOwner.MyPlayer is not { } requester)
        {
            return false;
        }

        if (command is FollowerCommand.Follow or FollowerCommand.Regroup)
        {
            return false;
        }

        if (command is FollowerCommand.Hold or FollowerCommand.TakeCover)
        {
            var disposition = GetModeDisposition(command, requester);
            return !FollowerRuntimeModeEnforcementPolicy.ShouldRefreshHold(command, disposition);
        }

        return command == FollowerCommand.Combat;
    }

    private bool HasActionableEnemy(Player requester)
    {
        return ResolveCombatPressureContext(requester, CurrentPosition).HasActionableEnemy;
    }

    private float ComputeNearestActionableEnemyDistance(Player requester, BotDebugWorldPoint followerPosition)
    {
        return ResolveCombatPressureContext(requester, followerPosition).DistanceToNearestActionableEnemyMeters;
    }

    private FollowerCombatPressureContext ResolveCombatPressureContext(Player requester, BotDebugWorldPoint followerPosition)
    {
        var recentThreatAgeSeconds = lastThreatStimulusTimeSeconds > 0f
            ? UnityEngine.Time.time - lastThreatStimulusTimeSeconds
            : -1f;
        return FollowerCombatPressureContextResolver.Resolve(
            botOwner,
            requester.ProfileId,
            plugin?.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId) ?? Array.Empty<string>(),
            followerPosition,
            lastThreatStimulusAttackerProfileId,
            recentThreatAgeSeconds);
    }

    private static bool ReadGoalEnemyBool(object? goalEnemy, string memberName)
    {
        if (goalEnemy is null)
        {
            return false;
        }

        var property = goalEnemy.GetType().GetProperty(memberName);
        if (property?.GetValue(goalEnemy) is bool propertyValue)
        {
            return propertyValue;
        }

        var field = goalEnemy.GetType().GetField(memberName);
        return field?.GetValue(goalEnemy) as bool? ?? false;
    }

    private static float? TryResolveGoalEnemyDistance(object? goalEnemy, BotDebugWorldPoint followerPosition)
    {
        if (goalEnemy is null)
        {
            return null;
        }

        if (TryReadVector3(goalEnemy, "CurrPosition", out var currentPosition)
            || TryReadVector3(goalEnemy, "EnemyLastPosition", out currentPosition))
        {
            return followerPosition.DistanceTo(new BotDebugWorldPoint(currentPosition.x, currentPosition.y, currentPosition.z));
        }

        var enemyPlayer = TryReadReference(goalEnemy, "Person")
            ?? TryReadReference(goalEnemy, "EnemyPlayer");
        if (enemyPlayer is not null
            && TryReadTransformPosition(enemyPlayer, out currentPosition))
        {
            return followerPosition.DistanceTo(new BotDebugWorldPoint(currentPosition.x, currentPosition.y, currentPosition.z));
        }

        return null;
    }

    private static bool TryReadVector3(object? instance, string memberName, out Vector3 vector)
    {
        vector = default;
        if (instance is null)
        {
            return false;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property?.GetValue(instance) is Vector3 propertyVector)
        {
            vector = propertyVector;
            return true;
        }

        var field = instance.GetType().GetField(memberName);
        if (field?.GetValue(instance) is Vector3 fieldVector)
        {
            vector = fieldVector;
            return true;
        }

        return false;
    }

    private static object? TryReadReference(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property is not null)
        {
            return property.GetValue(instance);
        }

        var field = instance.GetType().GetField(memberName);
        return field?.GetValue(instance);
    }

    private static bool TryReadTransformPosition(object instance, out Vector3 position)
    {
        position = default;
        var transform = TryReadReference(instance, "Transform");
        return transform is not null
            && TryReadVector3(transform, "position", out position);
    }

    private FollowerEnemyDebugState CaptureEnemyDebugState(Player? requester)
    {
        var followerPosition = CurrentPosition;
        var goalEnemy = botOwner.Memory?.GoalEnemy;
        var goalEnemyHaveSeen = ReadGoalEnemyBool(goalEnemy, "HaveSeen");
        var goalEnemyLastSeenAgeSeconds = ResolveGoalEnemyLastSeenAgeSeconds(goalEnemy);
        var pressureContext = requester is not null
            ? ResolveCombatPressureContext(requester, followerPosition)
            : default;
        var underFire = pressureContext.IsUnderFire || ReadBoolValue(botOwner.Memory, "IsUnderFire");
        var actionableEnemy = pressureContext.HasActionableEnemy;
        var nearestActionableEnemyDistance = requester is not null
            ? pressureContext.DistanceToNearestActionableEnemyMeters
            : -1f;
        var goalEnemyDistance = TryResolveGoalEnemyDistance(goalEnemy, followerPosition) ?? -1f;
        var knownEnemiesSummary = requester is null ? "None" : BuildKnownEnemiesSummary(requester);
        var recentThreatAgeSeconds = lastThreatStimulusTimeSeconds > 0f
            ? UnityEngine.Time.time - lastThreatStimulusTimeSeconds
            : -1f;
        var recentThreatStimulus = lastThreatStimulusType;
        if (lastHitTimeSeconds > 0f && UnityEngine.Time.time - lastHitTimeSeconds <= 5f)
        {
            recentThreatStimulus = recentThreatStimulus is null
                ? "BeingHit"
                : $"{recentThreatStimulus}|BeingHit";
            recentThreatAgeSeconds = recentThreatAgeSeconds < 0f
                ? UnityEngine.Time.time - lastHitTimeSeconds
                : MathF.Min(recentThreatAgeSeconds, UnityEngine.Time.time - lastHitTimeSeconds);
        }

        return new FollowerEnemyDebugState(
            underFire,
            goalEnemyHaveSeen,
            goalEnemyLastSeenAgeSeconds,
            actionableEnemy,
            nearestActionableEnemyDistance,
            goalEnemyDistance,
            knownEnemiesSummary,
            recentThreatStimulus,
            recentThreatAgeSeconds,
            lastThreatStimulusAttackerProfileId);
    }

    private string BuildKnownEnemiesSummary(Player requester)
    {
        var enemyInfos = botOwner.EnemiesController?.EnemyInfos?.Values;
        if (enemyInfos is null)
        {
            return "None";
        }

        var followerPosition = CurrentPosition;
        var summaries = enemyInfos
            .Where(enemy => enemy is not null && !string.IsNullOrWhiteSpace(enemy.ProfileId))
            .Select(enemy => enemy!)
            .Where(enemy => !FollowerProtectionPolicy.ShouldProtectPlayer(
                botOwner.ProfileId,
                plugin?.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId) ?? Array.Empty<string>(),
                requester.ProfileId,
                enemy.ProfileId))
            .OrderByDescending(enemy => enemy.IsVisible)
            .ThenByDescending(enemy => enemy.CanShoot)
            .ThenBy(enemy => followerPosition.DistanceTo(new BotDebugWorldPoint(
                enemy.CurrPosition.x,
                enemy.CurrPosition.y,
                enemy.CurrPosition.z)))
            .Take(3)
            .Select(enemy =>
            {
                var distance = followerPosition.DistanceTo(new BotDebugWorldPoint(
                    enemy.CurrPosition.x,
                    enemy.CurrPosition.y,
                    enemy.CurrPosition.z));
                return $"{enemy.ProfileId[..Math.Min(6, enemy.ProfileId.Length)]}:v={enemy.IsVisible},s={enemy.CanShoot},d={distance:0}";
            })
            .ToArray();

        return summaries.Length == 0 ? "None" : string.Join("|", summaries);
    }

    private static float ResolveGoalEnemyLastSeenAgeSeconds(object? goalEnemy)
    {
        if (goalEnemy is null)
        {
            return -1f;
        }

        var property = goalEnemy.GetType().GetProperty("PersonalLastSeenTime");
        if (property?.GetValue(goalEnemy) is float lastSeenTime)
        {
            return UnityEngine.Time.time - lastSeenTime;
        }

        var field = goalEnemy.GetType().GetField("PersonalLastSeenTime");
        if (field?.GetValue(goalEnemy) is float fieldValue)
        {
            return UnityEngine.Time.time - fieldValue;
        }

        return -1f;
    }

    private static bool ReadBoolValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return false;
        }

        var property = instance.GetType().GetProperty(memberName);
        if (property?.GetValue(instance) is bool propertyValue)
        {
            return propertyValue;
        }

        var field = instance.GetType().GetField(memberName);
        return field?.GetValue(instance) as bool? ?? false;
    }

    private static bool IsPlayerOrEnemyDead(object? instance)
    {
        if (instance is null)
        {
            return false;
        }

        if (ReadBoolValue(instance, "IsDead"))
        {
            return true;
        }

        var healthController = instance.GetType().GetProperty("HealthController")?.GetValue(instance)
            ?? instance.GetType().GetProperty("ActiveHealthController")?.GetValue(instance);
        if (healthController is null)
        {
            return false;
        }

        var isAliveProperty = healthController.GetType().GetProperty("IsAlive");
        return isAliveProperty?.GetValue(healthController) as bool? == false;
    }

    private FollowerModeDisposition GetModeDisposition(FollowerCommand command, Player requester)
    {
        var followerPosition = CurrentPosition;
        var distanceToPlayer = followerPosition.DistanceTo(BotDebugSnapshotMapper.GetWorldPoint(requester));
        var distanceToHoldAnchor = plugin?.Registry.TryGetHoldAnchor(Aid, out var holdAnchor) == true
            ? followerPosition.DistanceTo(holdAnchor)
            : 0f;

        return FollowerModePolicy.ResolveDisposition(
            command,
            HasActionableEnemy(requester),
            distanceToPlayer,
            distanceToHoldAnchor,
            plugin?.ModeSettings ?? new FollowerModeSettings());
    }

    private void TryApplyAmbientCombatAssist(Player requester, FollowerCommand command)
    {
        if (command is not (FollowerCommand.Follow or FollowerCommand.Combat or FollowerCommand.TakeCover))
        {
            return;
        }

        var targetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory?.GoalEnemy);
        if (string.IsNullOrWhiteSpace(targetProfileId)
            && botOwner.Memory?.HaveEnemy != true)
        {
            return;
        }

        TryApplyCombatAssistAction(
            requester,
            command,
            currentMode: null,
            source: "ambient");
    }

    private void TryApplyCombatAssistAction(
        Player requester,
        FollowerCommand command,
        CustomFollowerBrainMode? currentMode,
        string source)
    {
        var targetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory?.GoalEnemy);
        var currentRequestType = botOwner.BotRequestController?.CurRequest?.BotRequestType.ToString() ?? "None";
        var distanceToNearestActionableEnemyMeters = ComputeNearestActionableEnemyDistance(requester, CurrentPosition);
        var activeLayerName = BotDebugBrainInspector.GetActiveLayerName(botOwner);
        var followCooldownBlocked =
            command == FollowerCommand.Follow
            && plugin?.Registry.TryGetCustomBrainSession(Aid, out var customBrainSession) == true
            && customBrainSession.Receiver.IsInFollowCombatSuppressionCooldown(UnityEngine.Time.time);
        var targetVisible = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "IsVisible");
        var canShoot = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "CanShoot");
        var result = CustomFollowerCombatAssistPolicy.Evaluate(
            UnityEngine.Time.time,
            customCombatAssistState,
            command,
            currentMode ?? CustomFollowerBrainMode.CombatPursue,
            followCooldownBlocked,
            activeLayerName,
            currentRequestType,
            botOwner.Memory?.HaveEnemy == true,
            targetVisible,
            canShoot,
            targetProfileId,
            distanceToNearestActionableEnemyMeters);
        customCombatAssistState = result.NextState;
        UpdateCombatAssistSummary(
            source,
            command,
            currentMode,
            activeLayerName,
            currentRequestType,
            followCooldownBlocked,
            botOwner.Memory?.HaveEnemy == true,
            targetVisible,
            canShoot,
            targetProfileId,
            distanceToNearestActionableEnemyMeters,
            result.ShouldActivateSuppression,
            result.ShouldActivateAttackClose,
            result.ShouldActivateTakeCover,
            activated: false);

        if (!result.ShouldActivateSuppression
            && !result.ShouldActivateAttackClose
            && !result.ShouldActivateTakeCover)
        {
            return;
        }

        var activated = result.ShouldActivateTakeCover
            ? FollowerCombatRequestActivator.TryActivateTakeCoverRequest(botOwner, requester)
            : result.ShouldActivateAttackClose
                ? FollowerCombatRequestActivator.TryActivateAttackCloseRequest(botOwner, requester)
                : FollowerCombatRequestActivator.TryActivateSuppressionRequest(botOwner, requester);
        UpdateCombatAssistSummary(
            source,
            command,
            currentMode,
            activeLayerName,
            currentRequestType,
            followCooldownBlocked,
            botOwner.Memory?.HaveEnemy == true,
            targetVisible,
            canShoot,
            targetProfileId,
            distanceToNearestActionableEnemyMeters,
            result.ShouldActivateSuppression,
            result.ShouldActivateAttackClose,
            result.ShouldActivateTakeCover,
            activated);
        if (activated)
        {
            plugin?.LogPluginInfo(
                $"Follower combat assist activated {(result.ShouldActivateTakeCover ? "take-cover" : result.ShouldActivateAttackClose ? "attack-close" : "suppression")}: follower={identitySnapshot.Nickname}, aid={Aid}, source={source}, mode={(currentMode?.ToString() ?? "None")}, target={targetProfileId ?? "None"}");
        }
    }

    private FollowerCommand? GetEffectiveCommand()
    {
        if (activeCommand.HasValue)
        {
            return activeCommand.Value;
        }

        if (plugin?.Registry.TryGetCustomBrainSession(Aid, out var customBrainSession) == true)
        {
            return customBrainSession.Receiver.CurrentState.Command;
        }

        return null;
    }

    private void TryStopStaleCombatRequest(
        CustomFollowerBrainRuntimeSession session,
        Player requester,
        CustomFollowerDebugState debugState)
    {
        var currentRequestType = botOwner.BotRequestController?.CurRequest?.BotRequestType.ToString() ?? "None";
        var hasActionableEnemy = HasActionableEnemy(requester);
        var isInFollowCombatSuppressionCooldown =
            session.Receiver.IsInFollowCombatSuppressionCooldown(UnityEngine.Time.time);
        var cleanupDecision = FollowerCombatRequestCleanupPolicy.Evaluate(
            debugState.Command,
            debugState.Mode,
            currentRequestType,
            hasActionableEnemy,
            isInFollowCombatSuppressionCooldown);
        lastCleanupSummary = BuildCleanupSummary(
            debugState.Command,
            debugState.Mode,
            currentRequestType,
            hasActionableEnemy,
            isInFollowCombatSuppressionCooldown,
            cleanupDecision.ShouldStopCurrentRequest);
        if (!cleanupDecision.ShouldStopCurrentRequest)
        {
            return;
        }

        botOwner.BotRequestController?.TryStopCurrent(requester, false);
        ClearQueuedRequests(requester);
        plugin?.LogPluginInfo(
            $"Follower stale combat request cleared: follower={identitySnapshot.Nickname}, aid={Aid}, command={debugState.Command}, mode={debugState.Mode}, request={currentRequestType}, actionable={hasActionableEnemy}, followCooldown={isInFollowCombatSuppressionCooldown}");
    }

    private void TryUnblockImmediateCombatPressure(
        Player requester,
        CustomFollowerDebugState debugState,
        FollowerCombatPressureContext combatPressure)
    {
        var now = UnityEngine.Time.time;
        var currentRequestType = botOwner.BotRequestController?.CurRequest?.BotRequestType.ToString() ?? "None";
        var activeLayerName = BotDebugBrainInspector.GetActiveLayerName(botOwner);
        var targetVisible = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "IsVisible");
        var canShoot = ReadGoalEnemyBool(botOwner.Memory?.GoalEnemy, "CanShoot");
        var currentRequestAgeSeconds = ObserveCurrentRequestAge(now);
        var isMoving = botOwner.Mover?.IsMoving ?? false;
        var decision = FollowerCombatEngagementUnblockPolicy.Evaluate(
            debugState.Command,
            debugState.Mode,
            activeLayerName,
            currentRequestType,
            combatPressure.HasActionableEnemy,
            targetVisible,
            canShoot,
            combatPressure.IsUnderFire,
            combatPressure.DistanceToNearestActionableEnemyMeters,
            IsHealingActive(),
            currentRequestAgeSeconds,
            isMoving);
        lastUnblockSummary = BuildUnblockSummary(
            debugState.Command,
            debugState.Mode,
            activeLayerName,
            currentRequestType,
            combatPressure,
            targetVisible,
            canShoot,
            IsHealingActive(),
            currentRequestAgeSeconds,
            isMoving,
            decision);
        if (!decision.ShouldInterruptHealing
            && !decision.ShouldStopCombatAssistRequest
            && !decision.ShouldReclaimFromLooting)
        {
            return;
        }

        if (decision.ShouldInterruptHealing)
        {
            TryInterruptCurrentHealing("close-actionable-threat");
        }

        if (decision.ShouldStopCombatAssistRequest)
        {
            botOwner.BotRequestController?.TryStopCurrent(requester, false);
            ClearQueuedRequests(requester);
            plugin?.LogPluginInfo(
                $"Follower close combat request cleared: follower={identitySnapshot.Nickname}, aid={Aid}, command={debugState.Command}, mode={debugState.Mode}, layer={activeLayerName ?? "None"}, request={currentRequestType}, actionable={combatPressure.HasActionableEnemy}, visible={targetVisible}, canShoot={canShoot}, moving={isMoving}, requestAge={FormatDebugMetric(currentRequestAgeSeconds)}, enemyDistance={(combatPressure.DistanceToNearestActionableEnemyMeters < float.MaxValue ? combatPressure.DistanceToNearestActionableEnemyMeters.ToString("0.0") : "None")}");
        }

        if (decision.ShouldReclaimFromLooting)
        {
            FollowerLootingRuntimeDisabler.DisableForFollower(
                botOwner,
                message => plugin?.LogPluginInfo(message));
            plugin?.LogPluginInfo(
                $"Follower looting reclaimed due to combat pressure: follower={identitySnapshot.Nickname}, aid={Aid}, command={debugState.Command}, mode={debugState.Mode}, layer={activeLayerName ?? "None"}, actionable={combatPressure.HasActionableEnemy}, visible={targetVisible}, canShoot={canShoot}");
        }
    }

    private void TryClearStaleEnemyMemory(
        Player requester,
        CustomFollowerDebugState debugState)
    {
        var now = UnityEngine.Time.time;
        var recentThreatAgeSeconds = lastThreatStimulusTimeSeconds > 0f
            ? now - lastThreatStimulusTimeSeconds
            : -1f;
        var hasFreshThreatContext =
            (lastHitTimeSeconds > 0f && now - lastHitTimeSeconds <= 5f)
            || (lastThreatStimulusTimeSeconds > 0f && now - lastThreatStimulusTimeSeconds <= 5f)
            || ReadBoolValue(botOwner.Memory, "IsUnderFire");
        var enemyInfos = botOwner.EnemiesController?.EnemyInfos?.ToArray();
        var goalEnemy = botOwner.Memory?.GoalEnemy;
        var goalTargetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(goalEnemy);
        if ((enemyInfos is null || enemyInfos.Length == 0)
            && string.IsNullOrWhiteSpace(goalTargetProfileId))
        {
            return;
        }

        hasFreshThreatContext = hasFreshThreatContext
            || FollowerThreatMemoryRetentionPolicy.ShouldRetainGoalEnemy(
                goalTargetProfileId,
                lastThreatStimulusAttackerProfileId,
                recentThreatAgeSeconds);

        var registeredFollowerProfileIds = plugin?.Registry.RuntimeFollowers.Select(follower => follower.RuntimeProfileId)
            ?? Array.Empty<string>();
        var enemyStates = (enemyInfos ?? Array.Empty<KeyValuePair<IPlayer, EnemyInfo>>())
            .Where(entry => entry.Value is not null)
            .Select(entry => new FollowerEnemyMemoryState(
                ProfileId: entry.Value.ProfileId,
                IsGoalEnemy: string.Equals(entry.Value.ProfileId, goalTargetProfileId, StringComparison.Ordinal),
                IsVisible: entry.Value.IsVisible,
                CanShoot: entry.Value.CanShoot,
                LastSeenAgeSeconds: ResolveGoalEnemyLastSeenAgeSeconds(entry.Value),
                IsProtected: FollowerProtectionPolicy.ShouldProtectPlayer(
                    botOwner.ProfileId,
                    registeredFollowerProfileIds,
                    requester.ProfileId,
                    entry.Value.ProfileId)))
            .ToList();

        if (goalEnemy is not null
            && !enemyStates.Any(enemy => string.Equals(enemy.ProfileId, goalTargetProfileId, StringComparison.Ordinal)))
        {
            enemyStates.Add(new FollowerEnemyMemoryState(
                ProfileId: goalTargetProfileId,
                IsGoalEnemy: true,
                IsVisible: ReadGoalEnemyBool(goalEnemy, "IsVisible"),
                CanShoot: ReadGoalEnemyBool(goalEnemy, "CanShoot"),
                LastSeenAgeSeconds: ResolveGoalEnemyLastSeenAgeSeconds(goalEnemy),
                IsProtected: FollowerProtectionPolicy.ShouldProtectPlayer(
                    botOwner.ProfileId,
                    registeredFollowerProfileIds,
                    requester.ProfileId,
                    goalTargetProfileId)));
        }

        var cleanupDecision = FollowerEnemyMemoryCleanupPolicy.Evaluate(
            debugState.Command,
            debugState.Mode,
            HasActionableEnemy(requester),
            hasFreshThreatContext,
            enemyStates);
        if (!cleanupDecision.ShouldClearAnyEnemyMemory)
        {
            return;
        }

        var removableProfileIds = new HashSet<string>(cleanupDecision.ProfileIdsToForget, StringComparer.Ordinal);
        var removedCount = 0;
        var goalCleared = false;
        foreach (var enemyEntry in enemyInfos ?? Array.Empty<KeyValuePair<IPlayer, EnemyInfo>>())
        {
            if (enemyEntry.Value is null
                || string.IsNullOrWhiteSpace(enemyEntry.Value.ProfileId)
                || !removableProfileIds.Contains(enemyEntry.Value.ProfileId))
            {
                continue;
            }

            botOwner.Memory?.DeleteInfoAboutEnemy(enemyEntry.Key);
            removedCount++;
        }

        if (cleanupDecision.ShouldClearGoalEnemy
            && goalEnemy is not null
            && botOwner.Memory is not null)
        {
            if (goalEnemy.Person is not null)
            {
                botOwner.Memory.DeleteInfoAboutEnemy(goalEnemy.Person);
            }

            botOwner.Memory.GoalEnemy = null;
            goalCleared = true;

            if (string.Equals(BotEnemyStateResolver.ResolveTargetProfileId(botOwner.Memory.LastEnemy), goalTargetProfileId, StringComparison.Ordinal))
            {
                botOwner.Memory.LastEnemy = null;
            }
        }

        if (removedCount <= 0 && !goalCleared)
        {
            return;
        }

        plugin?.LogPluginInfo(
            $"Follower stale enemy memory cleared: follower={identitySnapshot.Nickname}, aid={Aid}, command={debugState.Command}, mode={debugState.Mode}, removed={removedCount}, clearedGoal={goalCleared}, goalTarget={goalTargetProfileId ?? "None"}");
    }

    private void TryClearDeadEnemyMemory()
    {
        var enemyInfos = botOwner.EnemiesController?.EnemyInfos?.ToArray();
        var goalEnemy = botOwner.Memory?.GoalEnemy;
        var lastEnemy = botOwner.Memory?.LastEnemy;
        var goalTargetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(goalEnemy);
        var lastTargetProfileId = BotEnemyStateResolver.ResolveTargetProfileId(lastEnemy);
        if ((enemyInfos is null || enemyInfos.Length == 0)
            && string.IsNullOrWhiteSpace(goalTargetProfileId)
            && string.IsNullOrWhiteSpace(lastTargetProfileId))
        {
            return;
        }

        var enemyStates = (enemyInfos ?? Array.Empty<KeyValuePair<IPlayer, EnemyInfo>>())
            .Where(entry => entry.Value is not null)
            .Select(entry => new FollowerEnemyLivenessState(
                entry.Value.ProfileId,
                IsPlayerOrEnemyDead(entry.Key) || IsPlayerOrEnemyDead(entry.Value.Person),
                IsGoalEnemy: string.Equals(entry.Value.ProfileId, goalTargetProfileId, StringComparison.Ordinal),
                IsLastEnemy: string.Equals(entry.Value.ProfileId, lastTargetProfileId, StringComparison.Ordinal)))
            .ToList();

        if (goalEnemy is not null
            && !enemyStates.Any(enemy => string.Equals(enemy.ProfileId, goalTargetProfileId, StringComparison.Ordinal)))
        {
            enemyStates.Add(new FollowerEnemyLivenessState(
                goalTargetProfileId,
                IsPlayerOrEnemyDead(goalEnemy.Person),
                IsGoalEnemy: true,
                IsLastEnemy: string.Equals(goalTargetProfileId, lastTargetProfileId, StringComparison.Ordinal)));
        }

        if (lastEnemy is not null
            && !enemyStates.Any(enemy => string.Equals(enemy.ProfileId, lastTargetProfileId, StringComparison.Ordinal)))
        {
            enemyStates.Add(new FollowerEnemyLivenessState(
                lastTargetProfileId,
                IsPlayerOrEnemyDead(lastEnemy.Person),
                IsGoalEnemy: false,
                IsLastEnemy: true));
        }

        var cleanupDecision = FollowerEnemyLivenessCleanupPolicy.Evaluate(enemyStates);
        if (!cleanupDecision.ShouldClearAnyEnemyMemory)
        {
            return;
        }

        var removableProfileIds = new HashSet<string>(cleanupDecision.ProfileIdsToForget, StringComparer.Ordinal);
        var removedCount = 0;
        foreach (var enemyEntry in enemyInfos ?? Array.Empty<KeyValuePair<IPlayer, EnemyInfo>>())
        {
            if (enemyEntry.Value is null
                || string.IsNullOrWhiteSpace(enemyEntry.Value.ProfileId)
                || !removableProfileIds.Contains(enemyEntry.Value.ProfileId))
            {
                continue;
            }

            botOwner.Memory?.DeleteInfoAboutEnemy(enemyEntry.Key);
            removedCount++;
        }

        var goalCleared = false;
        if (cleanupDecision.ShouldClearGoalEnemy
            && goalEnemy is not null
            && botOwner.Memory is not null)
        {
            if (goalEnemy.Person is not null)
            {
                botOwner.Memory.DeleteInfoAboutEnemy(goalEnemy.Person);
            }

            botOwner.Memory.GoalEnemy = null;
            goalCleared = true;
        }

        var lastCleared = false;
        if (cleanupDecision.ShouldClearLastEnemy
            && botOwner.Memory is not null)
        {
            if (lastEnemy?.Person is not null)
            {
                botOwner.Memory.DeleteInfoAboutEnemy(lastEnemy.Person);
            }

            botOwner.Memory.LastEnemy = null;
            lastCleared = true;
        }

        plugin?.LogPluginInfo(
            $"Follower dead enemy memory cleared: follower={identitySnapshot.Nickname}, aid={Aid}, removed={removedCount}, clearedGoal={goalCleared}, clearedLast={lastCleared}, goalTarget={goalTargetProfileId ?? "None"}, lastTarget={lastTargetProfileId ?? "None"}");
    }

    private void ClearQueuedRequests(Player requester)
    {
        var requestController = botOwner.BotRequestController;
        var currentRequest = requestController?.CurRequest;
        if (currentRequest is not null)
        {
            currentRequest.Complete();
            if (ReferenceEquals(requestController?.CurRequest, currentRequest))
            {
                requestController!.CurRequest = null;
            }
        }

        var queuedRequests = botOwner.BotsGroup?.RequestsController?.ListOfRequests
            ?.Where(request =>
                ReferenceEquals(request.Requester, requester)
                && (ReferenceEquals(request.Executor, botOwner)
                    || request.PossibleExecutors?.Contains(botOwner) == true))
            .ToArray();

        if (queuedRequests is null)
        {
            return;
        }

        foreach (var request in queuedRequests)
        {
            request.Complete();
        }
    }

    private bool IsHealingActive()
    {
        return botOwner.Medecine?.FirstAid?.Using == true
            || botOwner.Medecine?.SurgicalKit?.Using == true
            || botOwner.Medecine?.Stimulators?.Using == true;
    }

    private float ObserveCurrentRequestAge(float now)
    {
        var currentRequestType = botOwner.BotRequestController?.CurRequest?.BotRequestType.ToString() ?? "None";
        if (string.Equals(currentRequestType, "None", StringComparison.Ordinal))
        {
            observedRequestType = null;
            observedRequestSinceTimeSeconds = 0f;
            return -1f;
        }

        if (!string.Equals(currentRequestType, observedRequestType, StringComparison.Ordinal))
        {
            observedRequestType = currentRequestType;
            observedRequestSinceTimeSeconds = now;
        }

        return observedRequestSinceTimeSeconds > 0f
            ? now - observedRequestSinceTimeSeconds
            : -1f;
    }

    private static string FormatDebugMetric(float value)
    {
        return value >= 0f && value < float.MaxValue
            ? value.ToString("0.0")
            : "None";
    }

    private void UpdateCombatAssistSummary(
        string source,
        FollowerCommand command,
        CustomFollowerBrainMode? currentMode,
        string? activeLayerName,
        string? currentRequestType,
        bool followCooldownBlocked,
        bool haveEnemy,
        bool targetVisible,
        bool canShoot,
        string? targetProfileId,
        float distanceToNearestActionableEnemyMeters,
        bool shouldActivateSuppression,
        bool shouldActivateAttackClose,
        bool shouldActivateTakeCover,
        bool activated)
    {
        var recommendation = shouldActivateTakeCover
            ? "take-cover"
            : shouldActivateAttackClose
                ? "attack-close"
                : shouldActivateSuppression
                    ? "suppression"
                    : "none";
        var blockReasons = new List<string>();
        if (FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName))
        {
            blockReasons.Add("combatLayer");
        }

        if (FollowerCombatRequestCleanupPolicy.IsCombatAssistRequest(currentRequestType))
        {
            blockReasons.Add($"request={currentRequestType}");
        }

        if (command == FollowerCommand.Follow && followCooldownBlocked)
        {
            blockReasons.Add("followCooldown");
        }

        if (!haveEnemy && string.IsNullOrWhiteSpace(targetProfileId))
        {
            blockReasons.Add("noTarget");
        }
        else if (string.IsNullOrWhiteSpace(targetProfileId)
                 && !targetVisible
                 && !canShoot)
        {
            blockReasons.Add("goalMissing");
        }

        if (!targetVisible && !canShoot)
        {
            blockReasons.Add("noVisNoShoot");
        }

        if (recommendation == "none" && blockReasons.Count == 0)
        {
            blockReasons.Add("policyNone");
        }

        lastCombatAssistSummary =
            $"src={source};cmd={command};mode={(currentMode?.ToString() ?? "None")};pick={recommendation};activated={activated};layer={activeLayerName ?? "None"};req={currentRequestType ?? "None"};dist={FormatDebugMetric(distanceToNearestActionableEnemyMeters)};vis={targetVisible};shoot={canShoot};target={targetProfileId ?? "None"};block={(blockReasons.Count == 0 ? "none" : string.Join("+", blockReasons))}";

        var traceKey =
            $"src={source};cmd={command};mode={(currentMode?.ToString() ?? "None")};pick={recommendation};activated={activated};layer={(FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName) ? "combat" : FollowerCombatLayerPolicy.IsLootingLayer(activeLayerName) ? "loot" : activeLayerName ?? "None")};req={(FollowerCombatRequestCleanupPolicy.IsCombatAssistRequest(currentRequestType) ? currentRequestType : "None")};target={targetProfileId ?? "None"};block={(blockReasons.Count == 0 ? "none" : string.Join("+", blockReasons))}";
        if (string.Equals(lastCombatAssistTraceKey, traceKey, StringComparison.Ordinal))
        {
            return;
        }

        lastCombatAssistTraceKey = traceKey;
        if (plugin?.EnableCombatTraceDiagnostics == true)
        {
            plugin.LogPluginInfo(
                $"Follower combat assist state: follower={identitySnapshot.Nickname}, aid={Aid}, source={source}, command={command}, mode={(currentMode?.ToString() ?? "None")}, pick={recommendation}, activated={activated}, layer={activeLayerName ?? "None"}, request={currentRequestType ?? "None"}, target={targetProfileId ?? "None"}, visible={targetVisible}, canShoot={canShoot}, blocked={(blockReasons.Count == 0 ? "none" : string.Join("+", blockReasons))}");
        }
    }

    private static string BuildCleanupSummary(
        FollowerCommand command,
        CustomFollowerBrainMode mode,
        string? currentRequestType,
        bool hasActionableEnemy,
        bool isInFollowCombatSuppressionCooldown,
        bool shouldStopCurrentRequest)
    {
        var reason = "notCombatAssist";
        if (FollowerCombatRequestCleanupPolicy.IsSuppressionRequest(currentRequestType))
        {
            reason = mode == CustomFollowerBrainMode.CombatPursue
                ? "modeCombatPursue"
                : command == FollowerCommand.Follow && isInFollowCombatSuppressionCooldown
                    ? "followCooldown"
                    : !hasActionableEnemy
                        ? "noActionable"
                        : "keepSuppression";
        }
        else if (FollowerCombatRequestCleanupPolicy.IsTakeCoverRequest(currentRequestType)
                 || FollowerCombatRequestCleanupPolicy.IsAttackCloseRequest(currentRequestType))
        {
            reason = command == FollowerCommand.Follow && isInFollowCombatSuppressionCooldown
                ? "followCooldown"
                : mode != CustomFollowerBrainMode.CombatPursue
                    ? $"mode={mode}"
                    : !hasActionableEnemy
                        ? "noActionable"
                        : "keepCombatRequest";
        }

        return $"cmd={command};mode={mode};req={currentRequestType ?? "None"};actionable={hasActionableEnemy};followCd={isInFollowCombatSuppressionCooldown};clear={shouldStopCurrentRequest};reason={reason}";
    }

    private static string BuildUnblockSummary(
        FollowerCommand command,
        CustomFollowerBrainMode mode,
        string? activeLayerName,
        string? currentRequestType,
        FollowerCombatPressureContext combatPressure,
        bool targetVisible,
        bool canShoot,
        bool isHealing,
        float currentRequestAgeSeconds,
        bool isMoving,
        FollowerCombatEngagementUnblockDecision decision)
    {
        var reason = "none";
        if (command is not (FollowerCommand.Follow or FollowerCommand.Combat))
        {
            reason = "command";
        }
        else if (mode != CustomFollowerBrainMode.CombatPursue)
        {
            reason = $"mode={mode}";
        }
        else if (FollowerCombatLayerPolicy.IsCombatLayer(activeLayerName))
        {
            reason = "combatLayer";
        }
        else if (combatPressure.HasActionableEnemy && targetVisible && !canShoot)
        {
            reason = "visibleUnshootable";
        }
        else if (combatPressure.HasActionableEnemy)
        {
            reason = "immediatePressure";
        }

        return $"cmd={command};mode={mode};layer={activeLayerName ?? "None"};req={currentRequestType ?? "None"};reqAge={FormatDebugMetric(currentRequestAgeSeconds)};moving={isMoving};actionable={combatPressure.HasActionableEnemy};dist={FormatDebugMetric(combatPressure.DistanceToNearestActionableEnemyMeters)};underFire={combatPressure.IsUnderFire};vis={targetVisible};shoot={canShoot};heal={isHealing};stop={decision.ShouldStopCombatAssistRequest};loot={decision.ShouldReclaimFromLooting};interrupt={decision.ShouldInterruptHealing};reason={reason}";
    }

    private bool TryRestoreFollowerPatrol(Player requester)
    {
        var botFollower = botOwner.BotFollower;
        if (botFollower is null)
        {
            return false;
        }

        if (botFollower.PatrolDataFollower is null)
        {
            botFollower.Activate();
        }

        if (!botFollower.HaveBoss)
        {
            return false;
        }

        botFollower.PatrolDataFollower?.InitPlayer(requester);
        botFollower.PatrolDataFollower?.SetIndex(botFollower.Index);

        var pointChooser = PatrollingData.GetPointChooser(botOwner, PatrolMode.simple, botOwner.SpawnProfileData);
        botOwner.PatrollingData.SetMode(PatrolMode.follower, pointChooser);
        botOwner.PatrollingData.Unpause();
        botFollower.BossFindAction();
        return true;
    }

    private bool TryActivatePlayerFollowState(Player requester, bool regroup)
    {
        return FollowerMovementStateApplier.TrySeedMovementOrder(
            botOwner,
            requester,
            regroup ? FollowerCommand.Regroup : FollowerCommand.Follow);
    }

    private FollowerSnapshotDto CaptureLiveSnapshot()
    {
        var profile = botOwner.Profile;
        var skillProgress = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var skill in profile.Skills.DisplayList)
        {
            skillProgress[skill.Id.ToString()] = (int)Math.Round(skill.ProgressValue);
        }

        var healthValues = new Dictionary<string, int>(StringComparer.Ordinal);
        var healthMaximumValues = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var bodyPart in HealCommandBodyParts)
        {
            var health = botOwner.HealthController.GetBodyPartHealth(bodyPart, false);
            healthValues[bodyPart.ToString()] = (int)Math.Round(health.Current);
            healthMaximumValues[bodyPart.ToString()] = (int)Math.Round(health.Maximum);
        }

        var equipmentSnapshot = CaptureEquipmentSnapshot(profile);
        var inventoryItemIds = equipmentSnapshot?.Items
            .Select(item => item.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<string>();
        var appearanceSnapshot = CaptureAppearanceSnapshot(profile);

        return new FollowerSnapshotDto(
            FollowerIdentityResolver.Resolve(profile.ProfileId, profile.AccountId),
            profile.Info.Nickname,
            profile.Info.Side.ToString(),
            profile.Info.Level,
            profile.Experience,
            skillProgress,
            inventoryItemIds,
            healthValues,
            healthMaximumValues,
            equipmentSnapshot,
            appearanceSnapshot);
    }

    private static FollowerEquipmentSnapshotDto? CaptureEquipmentSnapshot(Profile profile)
    {
        try
        {
            var descriptor = new CompleteProfileDescriptorClass(profile, null);
            var inventory = descriptor.Inventory;
            var equipmentDescriptor = inventory?.Equipment;
            var flatItems = inventory?.Gclass1390_0;
            if (inventory is null || equipmentDescriptor is null || flatItems is null || flatItems.Length == 0)
            {
                return null;
            }

            var items = flatItems
                .Select(item => new FollowerEquipmentItemSnapshotDto(
                    item._id.ToString(),
                    item._tpl.ToString(),
                    item.parentId?.ToString(),
                    item.slotId,
                    SerializeInventoryNode(item.location),
                    SerializeInventoryNode(item.upd)))
                .ToArray();

            return new FollowerEquipmentSnapshotDto(equipmentDescriptor.Id.ToString(), items);
        }
        catch
        {
            return null;
        }
    }

    private static FollowerAppearanceSnapshotDto? CaptureAppearanceSnapshot(Profile profile)
    {
        var customization = profile.Customization;
        if (customization is null)
        {
            return null;
        }

        return new FollowerAppearanceSnapshotDto(
            GetCustomizationId(customization, EBodyModelPart.Head),
            GetCustomizationId(customization, EBodyModelPart.Body),
            GetCustomizationId(customization, EBodyModelPart.Feet),
            GetCustomizationId(customization, EBodyModelPart.Hands),
            GetCustomizationId(customization, EBodyModelPart.Voice),
            GetCustomizationId(customization, EBodyModelPart.DogTag));
    }

    private static string? GetCustomizationId(System.Collections.Generic.IReadOnlyDictionary<EBodyModelPart, MongoID> customization, EBodyModelPart part)
    {
        return customization.TryGetValue(part, out var value)
            ? value.ToString()
            : null;
    }

    private static string? SerializeInventoryNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return JsonConvert.SerializeObject(value);
    }

    private readonly record struct FollowerEnemyDebugState(
        bool IsUnderFire,
        bool GoalEnemyHaveSeen,
        float GoalEnemyLastSeenAgeSeconds,
        bool HasActionableEnemy,
        float DistanceToNearestActionableEnemyMeters,
        float DistanceToGoalEnemyMeters,
        string KnownEnemiesSummary,
        string? RecentThreatStimulus,
        float RecentThreatAgeSeconds,
        string? RecentThreatAttackerProfileId);
}
#endif
