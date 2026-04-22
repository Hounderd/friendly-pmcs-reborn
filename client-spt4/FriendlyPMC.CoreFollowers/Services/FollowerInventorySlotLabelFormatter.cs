namespace FriendlyPMC.CoreFollowers.Services;

public static class FollowerInventorySlotLabelFormatter
{
    public static string Format(string? slotId)
    {
        return slotId switch
        {
            null or "" => "Item",
            "hideout" => "Stash",
            "main" => "Container",
            "Headwear" => "Headwear",
            "FaceCover" => "Face Cover",
            "ArmorVest" => "Armor",
            "TacticalVest" => "Rig",
            "Backpack" => "Backpack",
            "Pockets" => "Pockets",
            "SecuredContainer" => "Secure Container",
            "Earpiece" => "Headset",
            "Eyewear" => "Eyewear",
            "FirstPrimaryWeapon" => "Primary Weapon",
            "SecondPrimaryWeapon" => "Secondary Weapon",
            "Holster" => "Holster",
            "Scabbard" => "Scabbard",
            "ArmBand" => "Armband",
            _ => SplitPascalCase(slotId),
        };
    }

    private static string SplitPascalCase(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(value[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
