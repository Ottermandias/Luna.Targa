namespace Luna.Targa.Enums;

/// <summary> The available pixel orders for image data. </summary>
[Flags]
public enum PixelOrder : byte
{
    /// <summary> The flag for Right-to-Left ordering. </summary>
    RightToLeft = 1,

    /// <summary> The flag for Top-to-Bottom ordering. </summary>
    TopToBottom = 2,

    /// <summary> Left-to-Right and Top-to-Bottom. </summary>
    LeftRightTopBottom = TopToBottom,

    /// <summary> Left-to-Right and Bottom-to-Top. </summary>
    LeftRightBottomTop = 0,

    /// <summary> Right-to-Left and Top-to-Bottom. </summary>
    RightLeftTopBottom = RightToLeft | TopToBottom,

    /// <summary> Right-to-Left and Bottom-to-Top. </summary>
    RightLeftBottomTop = RightToLeft,
};
