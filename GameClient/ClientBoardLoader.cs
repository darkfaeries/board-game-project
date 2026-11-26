#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace GameClient;

public static class ClientBoardLoader
{
    private static readonly Dictionary<string, TerrainType> TerrainNames = new()
    {
        ["hills"] = TerrainType.Hills,
        ["plains"] = TerrainType.Plains,
        ["mountains"] = TerrainType.Mountains,
        ["forest"] = TerrainType.Forest
    };

    private static readonly Dictionary<string, Region> RegionNames = new()
    {
        ["africa"] = Region.Africa,
        ["eurasia"] = Region.Eurasia,
        ["australia"] = Region.Australia,
        ["america"] = Region.America
    };

    private static readonly Dictionary<string, Tribe> TribeNames = new()
    {
        ["Indo-Europeans"] = Tribe.Indo_Europeans,
        ["Asians"] = Tribe.Asians,
        ["Austronesians"] = Tribe.Austronesians,
        ["Americans"] = Tribe.Americans
    };

    public static BoardState LoadBoard(StreamReader hexesReader, StreamReader nodesReader, StreamReader pathsReader)
    {
        var hexes = ReadHexesCsv(hexesReader);
        var nodes = ReadNodesCsv(nodesReader);
        var (explorationPaths, blockedPaths) = ReadPathsCsv(pathsReader);

        return new BoardState(hexes, nodes, explorationPaths, blockedPaths);
    }

    private static Dictionary<AxialCoords, Hex> ReadHexesCsv(StreamReader hexesRd)
    {
        var colNames = hexesRd.ReadLine()?.Split(',');
        if (colNames == null)
        {
            throw new InvalidOperationException("Hexes CSV file is empty or invalid.");
        }

        var dict = new Dictionary<string, int>();
        for (int i = 0; i < colNames.Length; i++)
        {
            dict[colNames[i].Trim()] = i;
        }

        var hexes = new Dictionary<AxialCoords, Hex>();
        while (!hexesRd.EndOfStream)
        {
            var line = hexesRd.ReadLine();
            var fields = line?.Split(',');
            if (fields == null) continue;

            int q = int.Parse(fields[dict["q"]].Trim());
            int r = int.Parse(fields[dict["r"]].Trim());
            var coords = new AxialCoords(q, r);
            var region = RegionNames[fields[dict["region"]].Trim()];
            int number = int.Parse(fields[dict["number"]].Trim());
            var terrain = TerrainNames[fields[dict["terrain"]].Trim()];

            hexes.Add(coords, new Hex(coords, terrain, number, region));
        }

        return hexes;
    }

    private static Dictionary<AxialCoords, Node> ReadNodesCsv(StreamReader nodeRd)
    {
        var colNames = nodeRd.ReadLine()?.Split(',');
        if (colNames == null)
        {
            throw new InvalidOperationException("Nodes CSV file is empty or invalid.");
        }

        var dict = new Dictionary<string, int>();
        for (int i = 0; i < colNames.Length; i++)
        {
            dict[colNames[i].Trim()] = i;
        }

        var nodes = new Dictionary<AxialCoords, Node>();
        while (!nodeRd.EndOfStream)
        {
            var line = nodeRd.ReadLine();
            var fields = line?.Split(',');
            if (fields == null) continue;

            int q = int.Parse(fields[dict["q"]].Trim());
            int r = int.Parse(fields[dict["r"]].Trim());
            var coords = new AxialCoords(q, r);

            bool start = bool.Parse(fields[dict["start"]].Trim());
            bool settle = bool.Parse(fields[dict["settle"]].Trim());
            bool settle3Player = bool.Parse(fields[dict["settle_3player"]].Trim());

            Tribe? tribe = null;
            if (settle)
            {
                tribe = TribeNames[fields[dict["tribe"]].Trim()];
            }

            nodes.Add(coords, new Node(coords, tribe, start, settle, settle3Player));
        }

        return nodes;
    }

    private static (Dictionary<Path, ExplorationPath>, HashSet<Path>) ReadPathsCsv(StreamReader pathRd)
    {
        var colNames = pathRd.ReadLine()?.Split(',');
        if (colNames == null)
        {
            throw new InvalidOperationException("Paths CSV file is empty or invalid.");
        }

        var dict = new Dictionary<string, int>();
        for (int i = 0; i < colNames.Length; i++)
        {
            dict[colNames[i].Trim()] = i;
        }

        var explorationPaths = new Dictionary<Path, ExplorationPath>();
        var blockedPaths = new HashSet<Path>();

        while (!pathRd.EndOfStream)
        {
            var line = pathRd.ReadLine();
            var fields = line?.Split(',');
            if (fields == null) continue;

            int fromQ = int.Parse(fields[dict["from_q"]].Trim());
            int fromR = int.Parse(fields[dict["from_r"]].Trim());
            int toQ = int.Parse(fields[dict["to_q"]].Trim());
            int toR = int.Parse(fields[dict["to_r"]].Trim());
            var fromCoords = new AxialCoords(fromQ, fromR);
            var toCoords = new AxialCoords(toQ, toR);
            var path = new Path(fromCoords, toCoords);

            bool isBlocked = bool.Parse(fields[dict["no_path"]].Trim());
            if (isBlocked)
            {
                blockedPaths.Add(path);
                continue;
            }

            var tribe = TribeNames[fields[dict["race"]].Trim()];
            int clothing = int.Parse(fields[dict["clothing"]].Trim());
            int shelter = int.Parse(fields[dict["shelter"]].Trim());

            explorationPaths.Add(path, new ExplorationPath(clothing, shelter, tribe));
        }

        return (explorationPaths, blockedPaths);
    }
}
