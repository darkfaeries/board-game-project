#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class RequirementBox
{
    // these are non-modifiable
    public int ClothingRequired { get; }
    public int ShelterRequired { get; }

    // не уверен, что это должно быть здесь; пока оставим
    public bool CanPass(Player player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        return player.Progress.Clothing >= ClothingRequired
            && player.Progress.Shelter >= ShelterRequired;
    }
}
