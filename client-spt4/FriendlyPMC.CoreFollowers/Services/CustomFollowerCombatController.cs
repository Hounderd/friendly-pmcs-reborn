namespace FriendlyPMC.CoreFollowers.Services;

public sealed class CustomFollowerCombatController
{
    public bool ShouldPreferPlayerTarget(CustomFollowerBrainDecision decision)
    {
        return decision.PreferPlayerTarget;
    }
}
