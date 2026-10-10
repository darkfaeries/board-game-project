namespace GameServer;

public class MapService() {
    private const string DEFAULT_MAP_DIR = "/GameDomain/GameFiles/Maps/Default_Map";
    public static BoardState LoadMap(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = AppContext.BaseDirectory + DEFAULT_MAP_DIR;
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("Not found dirrectory for default map " + directory);
        }

        using var hexes = new StreamReader(System.IO.Path.Combine(directory, "hexes.csv"));
        using var nodes = new StreamReader(System.IO.Path.Combine(directory, "nodes.csv"));
        using var paths = new StreamReader(System.IO.Path.Combine(directory, "paths.csv"));
        
        return new BoardState(hexes, nodes, paths);
    }
}