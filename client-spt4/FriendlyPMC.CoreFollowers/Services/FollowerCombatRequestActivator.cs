#if SPT_CLIENT
using System.Reflection;
using EFT;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerCombatRequestActivator
{
    private static Type? cachedRequestControllerType;
    private static MethodInfo? tryActivateSuppressionByEnemyMethod;
    private static MethodInfo? tryActivateSuppressionByExecutorMethod;

    public static bool TryActivateSuppressionRequest(
        BotOwner botOwner,
        Player requester)
    {
        if (botOwner?.BotsGroup?.RequestsController is not { } requestController || requester is null)
        {
            return false;
        }

        EnsureSuppressionMethodsCached(requestController);

        if (botOwner.Memory?.GoalEnemy is EnemyInfo goalEnemy
            && TryInvokeSuppression(requestController, tryActivateSuppressionByEnemyMethod, requester, goalEnemy))
        {
            return true;
        }

        return TryInvokeSuppression(requestController, tryActivateSuppressionByExecutorMethod, requester, botOwner);
    }

    public static bool TryActivateAttackCloseRequest(
        BotOwner botOwner,
        Player requester)
    {
        if (botOwner?.BotsGroup?.RequestsController is not { } requestController
            || requester is null)
        {
            return false;
        }

        return requestController.TryAddRequest(new FollowerAttackCloseRequest(botOwner, requester));
    }

    public static bool TryActivateTakeCoverRequest(
        BotOwner botOwner,
        Player requester,
        float periodSeconds = 8f)
    {
        if (botOwner?.BotsGroup?.RequestsController is not { } requestController
            || requester is null)
        {
            return false;
        }

        return requestController.TryActivateGetInCover(requester, botOwner, null, periodSeconds);
    }

    private static void EnsureSuppressionMethodsCached(object requestController)
    {
        var requestControllerType = requestController.GetType();
        if (cachedRequestControllerType == requestControllerType)
        {
            return;
        }

        var methods = requestControllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        tryActivateSuppressionByEnemyMethod = FollowerCombatRequestReflectionPolicy.ResolveTwoParameterMethod(
            methods,
            "TryActivateSuppressionRequest",
            typeof(Player),
            typeof(EnemyInfo));
        tryActivateSuppressionByExecutorMethod = FollowerCombatRequestReflectionPolicy.ResolveTwoParameterMethod(
            methods,
            "TryActivateSuppressionRequest",
            typeof(Player),
            typeof(BotOwner));
        cachedRequestControllerType = requestControllerType;
    }

    private static bool TryInvokeSuppression(object requestController, MethodInfo? method, Player requester, object target)
    {
        if (method is null)
        {
            return false;
        }

        var result = method.Invoke(requestController, new[] { requester, target });
        return FollowerCombatRequestReflectionPolicy.WasInvocationSuccessful(method, result);
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class FollowerCombatRequestActivator
{
}
#endif
