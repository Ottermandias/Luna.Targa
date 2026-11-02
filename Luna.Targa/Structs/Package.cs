using System.Collections;

namespace Luna.Targa.Structs;

internal readonly ref struct Package(int count, int repetitions, ReadOnlySpan<byte> data)
{
    public readonly ReadOnlySpan<byte> Data        = data;
    public readonly int                Repetitions = repetitions;
    public readonly int                Count       = count;
}

internal ref struct PackageEnumerator(ReadOnlySpan<byte> imageData, byte bytesPerPixel, int fixedCount) : IEnumerator<Package>
{
    private const    int                MissingDataIndex = int.MaxValue - 1;
    private readonly ReadOnlySpan<byte> _data            = imageData;
    private readonly int                _bytesPerPixel   = bytesPerPixel;
    public           int                CurrentIndex { get; private set; } = -1;
    public           Package            Current      { get; private set; }

    public bool MoveNext()
    {
        if (fixedCount >= 0)
        {
            if (CurrentIndex is -1)
            {
                CurrentIndex = 0;
                Current      = new Package(fixedCount, 1, _data);
                return true;
            }

            return false;
        }

        if (CurrentIndex + 1 >= _data.Length)
            return false;

        var packet     = _data[++CurrentIndex];
        var packetSize = 1 + (packet & 0x7F);
        var (repetitions, count, dataSize) =
            packet >> 7 is 1 ? (packetSize, 1, _bytesPerPixel) : (1, packetSize, packetSize * _bytesPerPixel);
        var start = CurrentIndex + 1;
        // Do not throw here but use int.MaxValue as signal value for invalid data.
        if (_data.Length < start + dataSize)
        {
            CurrentIndex = MissingDataIndex;
            return false;
        }

        CurrentIndex += dataSize;
        Current      =  new Package(count, repetitions, _data[start..(CurrentIndex + 1)]);
        return true;
    }

    public void Reset()
    {
        Current      = default;
        CurrentIndex = -1;
    }

    public bool HasMissingData
        => CurrentIndex is MissingDataIndex;

    object IEnumerator.Current
        => throw new NotSupportedException();

    public void Dispose()
    { }
}
