#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ProgressState
{
    public Dictionary<ProgressType, int> Progress { get; set; } = new()
    {
        { ProgressType.Food, 0 },
        { ProgressType.Clothing, 0 },
        { ProgressType.Shelter, 0 },
        { ProgressType.Hunting, 0 }
    };

    public int GetLevel(ProgressType progressType)
    {
        if (!Progress.ContainsKey(progressType))
            throw new ArgumentException($"Progress type {progressType} not found.");
        
        return Progress[progressType];
    }
}
