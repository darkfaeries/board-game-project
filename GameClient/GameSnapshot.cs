using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GameClient;

/// <summary>
/// Состояние партии, которое клиент может собрать без изменений на сервере:
/// игроки с фигурами и ресурсами приходят из REST /api/sessions/{code}/players,
/// текущий игрок — из событий хаба (GameStarted / TurnChanged).
/// </summary>
public sealed class GameSnapshot
{
    public List<PlayerSnapshot> Players { get; } = new();
    public string? GameCode { get; set; }
    public Guid? YouPlayerId { get; set; }
    public Guid? CurrentPlayerId { get; set; }
    public GameStatus Status { get; set; } = GameStatus.WaitingForPlayers;

    // Фазу и признак «кубик уже брошен» восстанавливает клиент по событиям хаба.
    public TurnPhase Phase { get; set; } = TurnPhase.Roll;
    public bool RollUsed { get; set; }
    public int? LastRoll { get; set; }

    public PlayerSnapshot? CurrentPlayer =>
        CurrentPlayerId is Guid id ? Players.FirstOrDefault(player => player.Id == id) : null;

    public PlayerSnapshot? You =>
        YouPlayerId is Guid id ? Players.FirstOrDefault(player => player.Id == id) : null;

    public bool IsMyTurn => YouPlayerId is not null && YouPlayerId == CurrentPlayerId;
}

public sealed class PlayerSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlayerColor Color { get; set; }
    public Dictionary<ResourceType, int> Resources { get; } = new();
    public Dictionary<ProgressType, int> Progress { get; } = new();
    public List<CoordDto> Camps { get; } = new();
    public List<CoordDto> Explorers { get; } = new();
    public int TribesCollected { get; set; }

    public int Resource(ResourceType type) =>
        Resources.TryGetValue(type, out var amount) ? amount : 0;

    public int ProgressLevel(ProgressType type) =>
        Progress.TryGetValue(type, out var level) ? level : 0;
}

public sealed class CoordDto
{
    public int Q { get; set; }
    public int R { get; set; }

    public AxialCoords ToCoords() => new(Q, R);
}

public static class GameSnapshotParser
{
    public static GameSnapshot ParsePlayers(string json, Guid? youId, Guid? currentPlayerId)
    {
        var snapshot = new GameSnapshot
        {
            YouPlayerId = youId,
            CurrentPlayerId = currentPlayerId,
        };

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return snapshot;

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
                snapshot.Players.Add(ParsePlayer(element));
        }

        return snapshot;
    }

    private static PlayerSnapshot ParsePlayer(JsonElement element)
    {
        var player = new PlayerSnapshot
        {
            Id = ReadGuid(element, "id"),
            Name = ReadString(element, "name") ?? string.Empty,
            Color = ReadEnum(element, "color", PlayerColor.Red),
        };

        if (TryGetProperty(element, "resources", out var resources))
        {
            // Player.Resources — это ResourceInventory, поэтому словарь лежит на уровень глубже.
            resources = UnwrapMap(resources, "resources");
            if (resources.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in resources.EnumerateObject())
                {
                    if (TryParseEnum<ResourceType>(property.Name, out var type))
                        player.Resources[type] = property.Value.ValueKind == JsonValueKind.Number
                            ? property.Value.GetInt32()
                            : 0;
                }
            }
        }

        if (TryGetProperty(element, "progress", out var progress))
        {
            progress = UnwrapMap(progress, "progress");
            if (progress.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in progress.EnumerateObject())
                {
                    if (TryParseEnum<ProgressType>(property.Name, out var type))
                        player.Progress[type] = property.Value.ValueKind == JsonValueKind.Number
                            ? property.Value.GetInt32()
                            : 0;
                }
            }
        }

        ReadCoords(element, "campPositions", player.Camps);
        ReadCoords(element, "explorerPositions", player.Explorers);

        if (TryGetProperty(element, "tribesCollected", out var tribes) &&
            tribes.ValueKind == JsonValueKind.Array)
        {
            player.TribesCollected = tribes.GetArrayLength();
        }

        return player;
    }

    private static void ReadCoords(JsonElement element, string propertyName, List<CoordDto> target)
    {
        if (!TryGetProperty(element, propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            target.Add(new CoordDto
            {
                Q = ReadInt(item, "q"),
                R = ReadInt(item, "r"),
            });
        }
    }

    // Разворачивает вложенность вида {"resources": {"Meat": 1, ...}} в сам словарь.
    private static JsonElement UnwrapMap(JsonElement element, string innerName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            TryGetProperty(element, innerName, out var inner) &&
            inner.ValueKind == JsonValueKind.Object)
            return inner;

        return element;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int ReadInt(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static Guid ReadGuid(JsonElement element, string name) =>
        Guid.TryParse(ReadString(element, name), out var value) ? value : Guid.Empty;

    private static TEnum ReadEnum<TEnum>(JsonElement element, string name, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (!TryGetProperty(element, name, out var value))
            return fallback;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) &&
            Enum.IsDefined(typeof(TEnum), number))
            return (TEnum)Enum.ToObject(typeof(TEnum), number);

        if (value.ValueKind == JsonValueKind.String &&
            TryParseEnum(value.GetString(), out TEnum parsed))
            return parsed;

        return fallback;
    }

    private static bool TryParseEnum<TEnum>(string? text, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (Enum.TryParse(text, ignoreCase: true, out value))
            return true;

        // Словарь с enum-ключами может прийти числами: "0", "1", ...
        if (int.TryParse(text, out var number) && Enum.IsDefined(typeof(TEnum), number))
        {
            value = (TEnum)Enum.ToObject(typeof(TEnum), number);
            return true;
        }

        return false;
    }
}
