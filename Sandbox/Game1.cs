using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using System;
using System.IO;

namespace Sandbox;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    // private SpriteBatch _spriteBatch;
    private BoardState _board;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here

        string maps_path = "../GameDomain/GameFiles/Maps/";
        string current_map_dir = "Default_Map/";

        string hexes_file = "hexes.csv";
        string nodes_file = "nodes.csv";
        string paths_file = "paths.csv";

        string hexes_path = maps_path + current_map_dir + hexes_file;
        string nodes_path = maps_path + current_map_dir + nodes_file;
        string paths_path = maps_path + current_map_dir + paths_file;

        using (var hexes_rd = new StreamReader(hexes_path))
        {
            using (var nodes_rd = new StreamReader(nodes_path))
            {
                using (var paths_rd = new StreamReader(paths_path))
                {
                    _board = new BoardState(hexes_rd, nodes_rd, paths_rd);
                }
            }
        }

        Console.WriteLine($"hexes loaded: {_board.Hexes.Count}");
        Console.WriteLine($"nodes loaded: {_board.Nodes.Count}");
        Console.WriteLine($"explo paths loaded: {_board.ExplorationPaths.Count}");

        base.Initialize();
    }

    protected override void LoadContent()
    {
        // _spriteBatch = new SpriteBatch(GraphicsDevice);

        // TODO: use this.Content to load your game content here
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // TODO: Add your update logic here

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // TODO: Add your drawing code here

        base.Draw(gameTime);
    }
}
