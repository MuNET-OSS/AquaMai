namespace AquaMai.Mods.GameSystem;

public class InputLatch
{
    private ulong _current;
    private ulong _accumulated;

    public void Update(ulong state)
    {
        _accumulated |= (state & ~_current);
        _current = state;
    }

    public ulong Read()
    {
        var result = _current | _accumulated;
        _accumulated = 0;
        return result;
    }

    public bool ReadBit(int index)
    {
        var mask = 1UL << index;
        var result = (_current & mask) != 0 || (_accumulated & mask) != 0;
        _accumulated &= ~mask;
        return result;
    }

    public ulong ReadBits(ulong mask)
    {
        var result = (_current | _accumulated) & mask;
        _accumulated &= ~mask;
        return result;
    }

    public void Clear()
    {
        _current = 0;
        _accumulated = 0;
    }
}
