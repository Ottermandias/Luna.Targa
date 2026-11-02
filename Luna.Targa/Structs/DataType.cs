using System.Runtime.CompilerServices;

namespace Luna.Targa.Structs;

internal struct TrueColorDataType : IDataType
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool ValidatePacketData(in Header header, in Package package)
        => true;
}

internal struct ColorMapDataType : IDataType
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool ValidatePacketData(in Header header, in Package package)
        => TargaReader.ValidateColorMap(package.Data, header);
}

internal interface IDataType
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public abstract static bool ValidatePacketData(in Header header, in Package package);
}
