using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Uncreated.ZoneEditor.Data;

/// <summary>
/// Represents a <see cref="ZoneAnchor"/> by it's indices.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 4)]
public readonly struct ZoneAnchorIdentifier :
    IEquatable<ZoneAnchorIdentifier>,
    IComparable<ZoneAnchorIdentifier>,
    ITerminalFormattable
{
    [FieldOffset(0)]
    private readonly int _data;

    /// <summary>
    /// The index of the zone in <see cref="EditorZones.LoadedZones"/>.
    /// </summary>
    public int ZoneIndex => unchecked ( (ushort)(_data >> 16) );

    /// <summary>
    /// The index of the anchor in <see cref="ZoneInfo.Anchors"/>.
    /// </summary>
    public int AnchorIndex => unchecked ( (ushort)_data );

    public ZoneAnchorIdentifier(int zoneIndex, int anchorIndex)
    {
        if (zoneIndex is > ushort.MaxValue or < ushort.MinValue)
            throw new ArgumentOutOfRangeException(nameof(zoneIndex), $"Must be <= {ushort.MaxValue} and >= {ushort.MinValue}.");

        if (anchorIndex is > ushort.MaxValue or < ushort.MinValue)
            throw new ArgumentOutOfRangeException(nameof(anchorIndex), $"Must be <= {ushort.MaxValue} and >= {ushort.MinValue}.");

        _data = unchecked ( (ushort)zoneIndex << 16 | (ushort)anchorIndex );
    }

    public ZoneAnchorIdentifier(ushort zoneIndex, ushort anchorIndex)
    {
        _data = zoneIndex << 16 | anchorIndex;
    }

    internal ZoneAnchorIdentifier(int data) => _data = data;

    /// <summary>
    /// Checks all indexes to see if accessing by index is safe.
    /// </summary>
    public bool CheckSafe()
    {
        List<ZoneInfo> list = EditorZones.Instance.ZoneList;
        int zoneIndex = ZoneIndex;
        return zoneIndex < list.Count && AnchorIndex < list[zoneIndex].Anchors.Count;
    }

    public override bool Equals(object? obj) => obj is ZoneAnchorIdentifier anchor && Equals(anchor);
    public bool Equals(ZoneAnchorIdentifier other) => other._data == _data;
    public int CompareTo(ZoneAnchorIdentifier other) => _data.CompareTo(other._data);
    public override int GetHashCode() => _data;
    public static bool operator ==(ZoneAnchorIdentifier left, ZoneAnchorIdentifier right) => left.Equals(right);
    public static bool operator !=(ZoneAnchorIdentifier left, ZoneAnchorIdentifier right) => !left.Equals(right);
    public override string ToString() => $"Zone #{ZoneIndex} / Anchor #{AnchorIndex}";
    public string Format(ITerminalFormatProvider provider) => $"Zone #{ZoneIndex.Format()} / Anchor #{AnchorIndex.Format()}".Colorize(FormattingColorType.Struct);
}
