namespace GameServer;

public record MoveResult(List<AxialCoords> Route, bool DecisionRequired = false);