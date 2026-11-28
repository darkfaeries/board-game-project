namespace GameServer;

public sealed class BalancedRandom<T>
{
    private readonly T[] _items;
    private int _position;

    public BalancedRandom(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();

        if (_items.Length == 0)
            throw new ArgumentException("At least one item is required.", nameof(items));

        _position = _items.Length;
    }

    public T Next()
    {
        if (_position == _items.Length)
        {
            Random.Shared.Shuffle(_items);
            _position = 0;
        }

        return _items[_position++];
    }
}
