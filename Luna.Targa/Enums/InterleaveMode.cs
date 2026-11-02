namespace Luna.Targa.Enums;

/// <summary> The interleave modes the Targa format supports. </summary>
/// <remarks> As of Targa 2.0 Specifications, interleaved modes are no longer supported, so we do not either. </remarks>
public enum InterleaveMode : byte
{
    /// <summary> No interleaving. </summary>
    None,

    /// <summary> Two-way, i.e. even-odd, interleaving. </summary>
    TwoWay,

    /// <summary> Four-way interleaving. </summary>
    FourWay,

    /// <summary> Eight-way interleaving. </summary>
    /// <remarks> According to the specification, this is just reserved and not currently supported. </remarks>
    EightWay,
}
