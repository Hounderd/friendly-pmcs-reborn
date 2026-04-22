#if SPT_CLIENT
using System.Reflection;
using BepInEx.Bootstrap;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;

namespace FriendlyPMC.CoreFollowers.Services;

internal static class LootingBotsInteropBridge
{
    private const string LootingBotsPluginKey = "me.skwizzy.lootingbots";
    private const string ExternalTypeName = "LootingBots.External, skwizzy.LootingBots";

    private static bool loadedChecked;
    private static bool interopInitialized;
    private static bool isLootingBotsLoaded;
    private static Type? externalType;
    private static MethodInfo? forceBotToScanLootMethod;
    private static MethodInfo? preventBotFromLootingMethod;
    private static MethodInfo? checkIfInventoryFullMethod;
    private static MethodInfo? getNetLootValueMethod;
    private static MethodInfo? getItemPriceMethod;

    public static bool IsLootingBotsLoaded()
    {
        if (!loadedChecked)
        {
            loadedChecked = true;
            isLootingBotsLoaded = Chainloader.PluginInfos.ContainsKey(LootingBotsPluginKey);
        }

        return isLootingBotsLoaded;
    }

    public static bool Init()
    {
        if (!IsLootingBotsLoaded())
        {
            return false;
        }

        if (!interopInitialized)
        {
            interopInitialized = true;
            externalType = Type.GetType(ExternalTypeName);
            if (externalType is not null)
            {
                forceBotToScanLootMethod = AccessTools.Method(externalType, "ForceBotToScanLoot");
                preventBotFromLootingMethod = AccessTools.Method(externalType, "PreventBotFromLooting");
                checkIfInventoryFullMethod = AccessTools.Method(externalType, "CheckIfInventoryFull");
                getNetLootValueMethod = AccessTools.Method(externalType, "GetNetLootValue");
                getItemPriceMethod = AccessTools.Method(externalType, "GetItemPrice");
            }
        }

        return externalType is not null;
    }

    public static bool TryForceBotToScanLoot(BotOwner botOwner)
    {
        return Init()
            && forceBotToScanLootMethod is not null
            && (bool)forceBotToScanLootMethod.Invoke(null, [botOwner])!;
    }

    public static bool TryPreventBotFromLooting(BotOwner botOwner, float durationSeconds)
    {
        return Init()
            && preventBotFromLootingMethod is not null
            && (bool)preventBotFromLootingMethod.Invoke(null, [botOwner, durationSeconds])!;
    }

    public static bool CheckIfInventoryFull(BotOwner botOwner)
    {
        return Init()
            && checkIfInventoryFullMethod is not null
            && (bool)checkIfInventoryFullMethod.Invoke(null, [botOwner])!;
    }

    public static float GetNetLootValue(BotOwner botOwner)
    {
        return Init()
            && getNetLootValueMethod is not null
            ? (float)getNetLootValueMethod.Invoke(null, [botOwner])!
            : 0f;
    }

    public static float GetItemPrice(Item item)
    {
        return Init()
            && getItemPriceMethod is not null
            ? (float)getItemPriceMethod.Invoke(null, [item])!
            : 0f;
    }
}
#else
namespace FriendlyPMC.CoreFollowers.Services;

internal static class LootingBotsInteropBridge
{
    public static bool IsLootingBotsLoaded() => false;

    public static bool Init() => false;

    public static bool TryForceBotToScanLoot(object botOwner) => false;

    public static bool TryPreventBotFromLooting(object botOwner, float durationSeconds) => false;
}
#endif
