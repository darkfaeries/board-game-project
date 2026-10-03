#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class OccupantRef
{
    public Guid PlayerId { get; set; }
    public Guid PieceId { get; set; }
    public PieceType Type { get; set; }
}
