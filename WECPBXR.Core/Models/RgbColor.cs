namespace WECPBXR.Core.Models;

public sealed record RgbColor(byte Red, byte Green, byte Blue)
{
    public string ToHexString()
    {
        return $"#{Red:X2}{Green:X2}{Blue:X2}";
    }
}
