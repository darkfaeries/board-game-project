using GameServer;
using Microsoft.Extensions.Logging.Abstractions;

// Each check creates its own session. No server, client, or network connection is needed.
var tests = new List<(string Name, Action Run)>
{
    ("SetupBoard: three players, 20 tribe counters", () => Checks.TribeCounters(3, 5)),
    ("SetupBoard: four players, 32 tribe counters", () => Checks.TribeCounters(4, 8)),
    ("SetupBoard: enemies belong to the correct regions", Checks.EnemyRegions),
    ("SetupBoard: single eligible hex and non-forest tiger", Checks.SingleEligibleHex),
    ("SetupBoard: missing Eurasia throws", () => Checks.MissingRegion(Region.Eurasia)),
    ("SetupBoard: missing America throws", () => Checks.MissingRegion(Region.America)),
    ("SetupBoard: sessions have independent boards", Checks.IndependentSessions),
    ("PlaceInitialCamp: valid placement changes the session board", Checks.ValidPlacement),
    ("PlaceInitialCamp: null coordinates rejected", () => Checks.Rejected(f => null!)),
    ("PlaceInitialCamp: unknown node rejected", () => Checks.Rejected(f => new AxialCoords(1000, 0))),
    ("PlaceInitialCamp: non-start node rejected", () => Checks.Rejected(f =>
        f.State.Board.Nodes.Values.First(n => !n.IsStartSpace).Coords)),
    ("PlaceInitialCamp: wrong player rejected", Checks.WrongPlayer),
    ("PlaceInitialCamp: unknown player rejected", Checks.UnknownPlayer),
    ("PlaceInitialCamp: no current player rejected", () => Checks.Rejected(f =>
    {
        f.State.CurrentPlayerId = null;
        return f.StartNodes[0];
    })),
    ("PlaceInitialCamp: own camp blocks node", () => Checks.Occupied(false, false)),
    ("PlaceInitialCamp: another player's camp blocks node", () => Checks.Occupied(false, true)),
    ("PlaceInitialCamp: own explorer blocks node", () => Checks.Occupied(true, false)),
    ("PlaceInitialCamp: another player's explorer blocks node", () => Checks.Occupied(true, true)),
    ("PlaceInitialCamp: duplicate request rejected", Checks.Duplicate),
    ("PlaceInitialCamp: third accepted, fourth rejected", Checks.CampLimit),
    ("PlaceInitialCamp: other players' camps do not count toward limit", Checks.OtherPlayersCamps),
    ("PlaceInitialCamp: parallel duplicate requests add one camp", Checks.ConcurrentDuplicate),
    ("PlaceInitialCamp: parallel distinct requests respect limit", Checks.ConcurrentLimit)
};

foreach (var status in new[] { GameStatus.WaitingForPlayers, GameStatus.Playing, GameStatus.Finished })
{
    var captured = status;
    tests.Add(($"PlaceInitialCamp: status {captured} rejected", () => Checks.Rejected(f =>
    {
        f.State.Status = captured;
        return f.StartNodes[0];
    })));
}

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception error)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}\n{error}");
    }
}
Console.WriteLine($"Total: {tests.Count}; passed: {tests.Count - failed}; failed: {failed}");
return failed == 0 ? 0 : 1;

static class Checks
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void TribeCounters(int players, int perTribe)
    {
        var f = new Fixture(players);
        var board = f.State.Board;

        foreach (var node in board.Nodes.Values)
            node.HasTribeCounter = !(players == 3 ? node.CanSettle3p : node.CanSettle);

        f.Service.SetupBoard(f.Session, players);

        Require(ReferenceEquals(board, f.Session.GetGameState().Board), "Setup replaced the board.");
        foreach (var node in board.Nodes.Values)
        {
            var expected = players == 3 ? node.CanSettle3p : node.CanSettle;
            Require(node.HasTribeCounter == expected,
                $"Wrong counter flag at ({node.Coords.q}, {node.Coords.r}).");
        }
        var counters = board.Nodes.Values.Where(n => n.HasTribeCounter).ToArray();
        Require(counters.Length == 4 * perTribe, "Wrong total for Default_Map.");
        Require(counters.All(n => n.Tribe.HasValue), "A counter has no tribe.");
        foreach (var tribe in Enum.GetValues<Tribe>())
            Require(counters.Count(n => n.Tribe == tribe) == perTribe, $"Wrong count for {tribe}.");
    }

    public static void EnemyRegions()
    {
        var f = new Fixture();
        // Repetition checks invariants, not randomness quality or coordinate variation.
        for (var i = 0; i < 50; i++)
        {
            f.State.Board.NeanderthalPosition = null;
            f.State.Board.SabertoothTigerPosition = null;
            f.Service.SetupBoard(f.Session, 3);
            AssertEnemy(f.State.Board, f.State.Board.NeanderthalPosition, Region.Eurasia);
            AssertEnemy(f.State.Board, f.State.Board.SabertoothTigerPosition, Region.America);
        }
    }

    private static void AssertEnemy(BoardState board, AxialCoords? position, Region region)
    {
        Require(position != null, $"Enemy in {region} was not placed.");
        Require(board.Hexes.TryGetValue(position!, out var hex), "Enemy is outside the board.");
        Require(hex!.Region == region, $"Expected {region}, found {hex.Region}.");
    }

    public static void SingleEligibleHex()
    {
        var f = new Fixture();
        var board = f.State.Board;
        board.Hexes.Clear();
        var eurasia = new AxialCoords(0, 0);
        var america = new AxialCoords(3, 0);
        board.Hexes.Add(eurasia, new Hex(eurasia, TerrainType.Mountains, 4, Region.Eurasia));
        board.Hexes.Add(america, new Hex(america, TerrainType.Plains, 5, Region.America));
        var africa = new AxialCoords(6, 0);
        var australia = new AxialCoords(9, 0);
        board.Hexes.Add(africa, new Hex(africa, TerrainType.Forest, 6, Region.Africa));
        board.Hexes.Add(australia, new Hex(australia, TerrainType.Forest, 8, Region.Australia));
        f.Service.SetupBoard(f.Session, 3);
        Require(eurasia.Equals(board.NeanderthalPosition), "Incorrect Neanderthal position.");
        Require(america.Equals(board.SabertoothTigerPosition), "Tiger must also support non-forest hexes.");
    }

    public static void MissingRegion(Region missing)
    {
        var f = new Fixture();
        var board = f.State.Board;
        foreach (var coords in board.Hexes.Where(p => p.Value.Region == missing).Select(p => p.Key).ToArray())
            board.Hexes.Remove(coords);
        try { f.Service.SetupBoard(f.Session, 3); }
        catch (InvalidOperationException)
        {
            Require(board.NeanderthalPosition == null && board.SabertoothTigerPosition == null,
                "Enemy positions were partially assigned.");
            return;
        }
        throw new InvalidOperationException($"Expected failure without {missing}.");
    }

    public static void IndependentSessions()
    {
        var first = new Fixture();
        var second = new Fixture();
        var before = Snapshot(second.State);
        first.Service.SetupBoard(first.Session, 3);
        Require(first.Service.PlaceInitialCamp(first.Session, first.Player.Id, first.StartNodes[0]), "Placement failed.");
        Require(Snapshot(second.State) == before, "Another session was changed.");
    }

    public static void ValidPlacement()
    {
        var f = new Fixture();
        var board = f.State.Board;
        var before = Snapshot(f.State);
        var target = f.StartNodes[0];
        Require(f.Service.PlaceInitialCamp(f.Session, f.Player.Id, target), "Valid camp rejected.");
        Require(ReferenceEquals(board, f.Session.GetGameState().Board), "Board reference changed.");
        Require(board.CampPositions.Count == 1 && board.CampPositions[target] == f.Player.Id, "Wrong camp owner/position.");
        board.CampPositions.Remove(target);
        Require(Snapshot(f.State) == before, "Placement changed resources, turn, counters, or other positions.");
    }

    // The callback prepares a rejected request. Snapshot is taken AFTER arranging it.
    public static void Rejected(Func<Fixture, AxialCoords> arrange)
    {
        var f = new Fixture();
        var target = arrange(f);
        AssertRejected(f, f.Player.Id, target);
    }

    private static void AssertRejected(Fixture f, Guid playerId, AxialCoords target)
    {
        var before = Snapshot(f.State);
        Require(!f.Service.PlaceInitialCamp(f.Session, playerId, target), "Invalid request accepted.");
        Require(Snapshot(f.State) == before, "Rejected request changed state.");
    }

    public static void WrongPlayer()
    {
        var f = new Fixture();
        AssertRejected(f, f.State.Players[1].Id, f.StartNodes[0]);
    }

    public static void UnknownPlayer()
    {
        var f = new Fixture();
        var unknown = Guid.NewGuid();
        f.State.CurrentPlayerId = unknown; // Isolate existence check from turn check.
        AssertRejected(f, unknown, f.StartNodes[0]);
    }

    public static void Occupied(bool explorer, bool otherPlayer)
    {
        var f = new Fixture();
        var positions = explorer ? f.State.Board.ExplorerPositions : f.State.Board.CampPositions;
        positions.Add(f.StartNodes[0], otherPlayer ? f.State.Players[1].Id : f.Player.Id);
        AssertRejected(f, f.Player.Id, f.StartNodes[0]);
    }

    public static void Duplicate()
    {
        var f = new Fixture();
        var target = f.StartNodes[0];
        Require(f.Service.PlaceInitialCamp(f.Session, f.Player.Id, target), "First placement failed.");
        // A new coordinate instance must still identify the same occupied node.
        AssertRejected(f, f.Player.Id, new AxialCoords(target.q, target.r));
    }

    public static void CampLimit()
    {
        var f = new Fixture();
        for (var i = 0; i < 3; i++)
            Require(f.Service.PlaceInitialCamp(f.Session, f.Player.Id, f.StartNodes[i]), $"Camp {i + 1} rejected.");
        AssertRejected(f, f.Player.Id, f.StartNodes[3]);
        Require(f.State.Board.CampPositions.Count == 3, "Expected exactly three camps.");
    }

    public static void OtherPlayersCamps()
    {
        var f = new Fixture();
        for (var i = 0; i < 3; i++) f.State.Board.CampPositions.Add(f.StartNodes[i], f.State.Players[1].Id);
        Require(f.Service.PlaceInitialCamp(f.Session, f.Player.Id, f.StartNodes[3]), "Other player's camps used our quota.");
        Require(f.State.Board.CampPositions.Values.Count(id => id == f.Player.Id) == 1, "Incorrect owner count.");
    }

    public static void ConcurrentDuplicate()
    {
        var f = new Fixture();
        var successes = 0;
        Parallel.For(0, 32, _ =>
        {
            if (f.Service.PlaceInitialCamp(f.Session, f.Player.Id, f.StartNodes[0]))
                Interlocked.Increment(ref successes);
        });
        Require(successes == 1 && f.State.Board.CampPositions.Count == 1, "Duplicate concurrent placement.");
    }

    public static void ConcurrentLimit()
    {
        var f = new Fixture();
        var successes = 0;
        Parallel.ForEach(f.StartNodes, target =>
        {
            if (f.Service.PlaceInitialCamp(f.Session, f.Player.Id, target))
                Interlocked.Increment(ref successes);
        });
        Require(successes == 3 && f.State.Board.CampPositions.Count == 3, "Concurrent calls violated the limit.");
        Require(f.State.Board.CampPositions.Values.All(id => id == f.Player.Id), "Incorrect owner.");
    }

    private static string Snapshot(GameState state)
    {
        // Project coordinate-keyed dictionaries to arrays: default JSON cannot serialize those keys.
        var board = state.Board;
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            state.Status, state.Phase, state.CurrentPlayerId, state.TurnNumber,
            state.SetupPlacementsCompleted, state.SetupPlayerOrder,
            board.NeanderthalPosition, board.SabertoothTigerPosition,
            Camps = board.CampPositions.OrderBy(p => p.Key.q).ThenBy(p => p.Key.r)
                .Select(p => new { p.Key.q, p.Key.r, Owner = p.Value }).ToArray(),
            Explorers = board.ExplorerPositions.OrderBy(p => p.Key.q).ThenBy(p => p.Key.r)
                .Select(p => new { p.Key.q, p.Key.r, Owner = p.Value }).ToArray(),
            Nodes = board.Nodes.Values.OrderBy(n => n.Coords.q).ThenBy(n => n.Coords.r)
                .Select(n => new { n.Coords.q, n.Coords.r, n.HasTribeCounter }).ToArray(),
            Players = state.Players.Select(p => new
            {
                p.Id, p.Resources.Resources, p.TribeCounterNumber, p.TribesCollected
            }).ToArray()
        });
    }
}

sealed class Fixture
{
    public BoardService Service { get; }
    public GameSession Session { get; }
    public GameState State { get; }
    public Player Player => State.Players[0];
    public AxialCoords[] StartNodes { get; }

    public Fixture(int playerCount = 3)
    {
        var maps = System.IO.Path.Combine(AppContext.BaseDirectory, "TestMaps");
        var map = System.IO.Path.Combine(maps, "Default_Map");
        using var hexes = new StreamReader(System.IO.Path.Combine(map, "hexes.csv"));
        using var nodes = new StreamReader(System.IO.Path.Combine(map, "nodes.csv"));
        using var paths = new StreamReader(System.IO.Path.Combine(map, "paths.csv"));
        State = new GameState(new BoardState(hexes, nodes, paths));
        for (var i = 0; i < playerCount; i++)
            State.Players.Add(new Player($"Player-{i}", Enum.GetValues<PlayerColor>()[i]));
        State.Status = GameStatus.Setup;
        State.Phase = TurnPhase.Actions;
        State.CurrentPlayerId = Player.Id;
        State.SetupPlayerOrder = State.Players.Select(p => p.Id).ToArray();
        Session = new GameSession(NullLogger<GameSession>.Instance, State);
        Service = new BoardService(maps, NullLogger<BoardService>.Instance);
        StartNodes = State.Board.Nodes.Values.Where(n => n.IsStartSpace)
            .Select(n => n.Coords).ToArray();
        Checks.Require(StartNodes.Length >= 4, "Test map needs at least four start nodes.");
    }
}
