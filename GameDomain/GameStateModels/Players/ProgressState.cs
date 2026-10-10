#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ProgressState
{
    public Dictionary<ProgressType, int> Progress { get; set; } = new();

    public int GetLevel(ProgressType progressType)
    {
        if (!Progress.ContainsKey(progressType))
            throw new ArgumentException($"Progress type {progressType} not found.");
        
        return Progress[progressType];
    }
}
