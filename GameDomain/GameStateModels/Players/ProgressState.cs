#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ProgressState
{
    public int Food { get; set; }
    public int Hunting { get; set; }
    public int Shelter { get; set; }
    public int Clothing { get; set; }

    public int GetLevel(ProgressType progressType)
    {
        switch (progressType)
        {
            case ProgressType.Food: return Food;
            case ProgressType.Hunting: return Hunting;
            case ProgressType.Shelter: return Shelter;
            case ProgressType.Clothing: return Clothing;
            default: throw new ArgumentOutOfRangeException(nameof(progressType));
        }
    }
}
