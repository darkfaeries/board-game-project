#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class RequirementBox
{
    public int ClothingRequired { get; set; }
    public int ShelterRequired { get; set; }

    public bool CanPass(Player player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        return player.Progress.Clothing >= ClothingRequired
            && player.Progress.Shelter >= ShelterRequired;
    }
}
