#if SPT_CLIENT
using EFT;

namespace FriendlyPMC.CoreFollowers.Modules;

internal sealed class PlayerFollowerBotsGroup : BotsGroup
{
    public PlayerFollowerBotsGroup(
        BotZone zone,
        IBotGame botGame,
        BotOwner initialBot,
        List<BotOwner> enemies,
        DeadBodiesController deadBodiesController,
        List<Player> allPlayers,
        Player localPlayer)
        : base(zone, botGame, initialBot, enemies, deadBodiesController, allPlayers, false)
    {
        RemoveEnemy(localPlayer);
        AddAlly(localPlayer);
        Side = localPlayer.Side;
    }
}
#endif
