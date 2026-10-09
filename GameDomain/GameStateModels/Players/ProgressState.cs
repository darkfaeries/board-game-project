#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ProgressState
{
    public Dictionary<ProgressType, int> Progress { get; set; }

    public int GetLevel(ProgressType progressType)
    {
        return Progress[progressType];
    }
}
