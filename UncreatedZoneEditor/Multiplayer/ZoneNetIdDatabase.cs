using DanielWillett.SpeedBytes;
using DevkitServer;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using System;
using System.Collections.Generic;
using Uncreated.ZoneEditor.Data;

namespace Uncreated.ZoneEditor.Multiplayer;
public sealed class ZoneNetIdDatabase : IReplicatedLevelDataSource<ZoneNetIdReplicatedLevelData>
{
    internal static bool IgnoreIndexChange;
    private static readonly List<NetId> NetIds = new List<NetId>(32);
    private static readonly Dictionary<ZoneAnchorIdentifier, NetId> AnchorNetIds = new Dictionary<ZoneAnchorIdentifier, NetId>(512);

    [UsedImplicitly]
    internal static NetCall<ushort, NetId> SendBindZone = new NetCall<ushort, NetId>(new Guid("d8243590cbe14ad1b67137635eec3a61"));

    [UsedImplicitly]
    internal static NetCall<int, NetId> SendBindAnchor = new NetCall<int, NetId>(new Guid("ede93407879d42df819d4b7e87015318"));
    public ushort CurrentDataVersion => 0;

    internal static void Init()
    {
        EditorZones.Instance.OnZoneRemoved += OnZoneRemoved;
        EditorZones.Instance.OnZoneIndexUpdated += OnZoneIndexUpdated;
        EditorZones.Instance.OnZoneAnchorRemoved += OnAnchorRemoved;
        EditorZones.Instance.OnZoneAnchorIndexUpdated += OnAnchorIndexUpdated;
    }

    internal static void Shutdown()
    {
        EditorZones.Instance.OnZoneRemoved -= OnZoneRemoved;
        EditorZones.Instance.OnZoneIndexUpdated -= OnZoneIndexUpdated;
        EditorZones.Instance.OnZoneAnchorRemoved -= OnAnchorRemoved;
        EditorZones.Instance.OnZoneAnchorIndexUpdated -= OnAnchorIndexUpdated;
    }

    public static bool TryGetZone(NetId netId, out int zoneIndex)
    {
        object? value = NetIdRegistry.Get(netId);

        if (value is int zoneIndexUnboxed)
        {
            zoneIndex = zoneIndexUnboxed;
            return true;
        }

        zoneIndex = -1;
        return false;
    }

    public static bool TryGetZoneNetId(int zoneIndex, out NetId netId)
    {
        netId = NetIds.Count > zoneIndex ? NetIds[zoneIndex] : NetId.INVALID;
        return !netId.IsNull();
    }
    
    public static bool TryGetAnchor(NetId netId, out ZoneAnchorIdentifier anchor)
    {
        object? value = NetIdRegistry.Get(netId);

        if (value is ZoneAnchorIdentifier anchorUnboxed)
        {
            anchor = anchorUnboxed;
            return true;
        }

        anchor = default;
        return false;
    }

    public static bool TryGetAnchorNetId(ZoneAnchorIdentifier anchor, out NetId netId)
    {
        if (AnchorNetIds.TryGetValue(anchor, out netId))
            return !netId.IsNull();

        netId = NetId.INVALID;
        return false;
    }

    private static void EnsureCapacity(int index)
    {
        ++index;

        if (NetIds.Capacity < index)
            NetIds.Capacity = index;

        while (NetIds.Count < index)
            NetIds.Add(NetId.INVALID);
    }

    private static void OnZoneIndexUpdated(ZoneInfo zone, int newIndex, int oldIndex)
    {
        if (!DevkitServerModule.IsEditing || IgnoreIndexChange)
            return;

        EnsureCapacity(newIndex);

        NetId blockingNetId = NetIds[newIndex];
        NetId netId = NetIds.Count > oldIndex ? NetIds[oldIndex] : NetId.INVALID;

        if (!blockingNetId.IsNull())
        {
            UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Released blocking net id to save zone: # {oldIndex.Format()} ({netId.Format()}, # {newIndex.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, newIndex);
        NetIds[oldIndex] = NetId.INVALID;
        NetIds[newIndex] = netId;
        UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Moved zone NetId: # {oldIndex.Format()} ({netId.Format()}, # {newIndex.Format()}).");
    }

    private static void OnAnchorIndexUpdated(ZoneAnchor anchor, ZoneAnchorIdentifier newId, ZoneAnchorIdentifier oldId)
    {
        if (!DevkitServerModule.IsEditing || IgnoreIndexChange)
            return;

        if (!AnchorNetIds.TryGetValue(newId, out NetId blockingNetId))
        {
            blockingNetId = NetId.INVALID;
        }
        if (!AnchorNetIds.TryGetValue(oldId, out NetId netId))
        {
            netId = NetId.INVALID;
        }

        if (!blockingNetId.IsNull())
        {
            UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Released blocking net id to save zone: # {oldId.Format()} ({netId.Format()}, # {newId.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, newId);
        AnchorNetIds.Remove(oldId);
        AnchorNetIds[newId] = netId;
        UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Moved zone NetId: # {oldId.Format()} ({netId.Format()}, # {newId.Format()}).");
    }

    private static void OnZoneRemoved(ZoneInfo zone, int oldIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId netId = NetIds[oldIndex];
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        NetIds[oldIndex] = NetId.INVALID;
        UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Removed zone NetId: ({netId.Format()}, # {oldIndex.Format()}).");
    }

    private static void OnAnchorRemoved(ZoneAnchor anchor, ZoneAnchorIdentifier anchorId)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        if (!AnchorNetIds.TryGetValue(anchorId, out NetId netId) || netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        AnchorNetIds.Remove(anchorId);
        UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Removed zone NetId: ({netId.Format()}, # {anchorId.Format()}).");
    }

#if CLIENT
    [NetCall(NetCallSource.FromServer, "d8243590cbe14ad1b67137635eec3a61")]
    private static StandardErrorCode ReceiveBindZone(MessageContext ctx, ushort zoneIndex, NetId netId)
    {
        RegisterZone(zoneIndex, netId);
        return StandardErrorCode.Success;
    }

    [NetCall(NetCallSource.FromServer, "ede93407879d42df819d4b7e87015318")]
    private static StandardErrorCode ReceiveBindAnchor(MessageContext ctx, int anchorPacked, NetId netId)
    {
        RegisterAnchor(new ZoneAnchorIdentifier(anchorPacked), netId);
        return StandardErrorCode.Success;
    }
#endif

    public static void RemoveZone(int oldIndex)
    {
        NetId id = NetIds.Count > oldIndex ? NetIds[oldIndex] : NetId.INVALID;
        if (id.IsNull())
        {
            UncreatedZoneEditor.Instance.LogWarning(nameof(ZoneNetIdDatabase), $"Unable to release NetId to zone {oldIndex.Format()}, NetId not registered.");
            return;
        }

        if (!NetIdRegistry.Release(id))
            UncreatedZoneEditor.Instance.LogWarning(nameof(ZoneNetIdDatabase), $"Unable to release NetId to zone {oldIndex.Format()}, NetId not registered in NetIdRegistry.");

        if (NetIds.Count > oldIndex)
        {
            NetIds[oldIndex] = NetId.INVALID;
        }

        if (Level.isLoaded)
            UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Released zone NetId: {id.Format()} ({oldIndex.Format()}).");
    }

    public static void RemoveAnchor(ZoneAnchorIdentifier anchor)
    {
        if (!AnchorNetIds.TryGetValue(anchor, out NetId id) || id.IsNull())
        {
            UncreatedZoneEditor.Instance.LogWarning(nameof(ZoneNetIdDatabase), $"Unable to release NetId to zone anchor {anchor.Format()}, NetId not registered.");
            return;
        }

        if (!NetIdRegistry.Release(id))
            UncreatedZoneEditor.Instance.LogWarning(nameof(ZoneNetIdDatabase), $"Unable to release NetId to zone anchor {anchor.Format()}, NetId not registered in NetIdRegistry.");

        AnchorNetIds.Remove(anchor);

        if (Level.isLoaded)
            UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Released zone anchor NetId: {id.Format()} ({anchor.Format()}).");
    }

    public static NetId AddZone(int zoneIndex)
    {
        ThreadUtil.assertIsGameThread();

        NetId netId = NetIdRegistry.Claim();

        RegisterZone(zoneIndex, netId);

        return netId;
    }
    
    public static NetId AddAnchor(ZoneAnchorIdentifier anchor)
    {
        ThreadUtil.assertIsGameThread();

        NetId netId = NetIdRegistry.Claim();

        RegisterAnchor(anchor, netId);

        return netId;
    }

    internal static void RegisterZone(int zoneIndex, NetId netId)
    {
        NetId old = zoneIndex < NetIds.Count ? NetIds[zoneIndex] : NetId.INVALID;
        if (!old.IsNull() && old != netId && NetIdRegistry.Release(old))
        {
            if (Level.isLoaded)
                UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Released old NetId pairing: {old.Format()}.");
        }

        EnsureCapacity(zoneIndex);
        NetIds[zoneIndex] = netId;
        NetIdRegistry.Assign(netId, zoneIndex);

        if (Level.isLoaded)
            UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Claimed new NetId: {netId.Format()} to zone {zoneIndex.Format()}.");
    }

    internal static void RegisterAnchor(ZoneAnchorIdentifier anchor, NetId netId)
    {
        if (AnchorNetIds.TryGetValue(anchor, out NetId old) && !old.IsNull() && old != netId && NetIdRegistry.Release(old))
        {
            if (Level.isLoaded)
                UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Released old NetId pairing: {old.Format()}.");
        }

        AnchorNetIds[anchor] = netId;
        NetIdRegistry.Assign(netId, anchor);

        if (Level.isLoaded)
            UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Claimed new NetId: {netId.Format()} to zone anchor {anchor.Format()}.");
    }

#if SERVER
    internal static void AssignExisting()
    {
        NetIds.Clear();

        List<ZoneInfo> zones = EditorZones.Instance.ZoneList;

        int index = 0;

        int ct = Math.Min(ushort.MaxValue, zones.Count);

        for (; index < ct; ++index)
            AddZone(index);

        UncreatedZoneEditor.Instance.LogInfo(nameof(ZoneNetIdDatabase), $"Assigned NetIds for {index.Format()} zone{index.S()}.");
    }
#endif

#if CLIENT

    public void LoadData(ZoneNetIdReplicatedLevelData data)
    {
        NetIds.Clear();
        for (int i = 0; i < data.NetIds.Length; ++i)
        {
            NetIds.Add(new NetId(data.NetIds[i]));
        }

        for (int i = 0; i < NetIds.Count; ++i)
        {
            if (NetIds[i].IsNull())
                continue;

            NetIdRegistry.Assign(NetIds[i], (byte)i);
        }
    }

#elif SERVER

    public ZoneNetIdReplicatedLevelData SaveData(CSteamID user)
    {
        uint[] netIds = new uint[Math.Min(ushort.MaxValue, NetIds.Count)];

        for (int i = 0; i < netIds.Length; ++i)
            netIds[i] = NetIds[i].id;

        return new ZoneNetIdReplicatedLevelData
        {
            NetIds = netIds
        };
    }

#endif

    public void WriteData(ByteWriter writer, ZoneNetIdReplicatedLevelData data)
    {
        writer.Write(data.NetIds);
    }

    public ZoneNetIdReplicatedLevelData ReadData(ByteReader reader, ushort dataVersion)
    {
        return new ZoneNetIdReplicatedLevelData
        {
            NetIds = reader.ReadUInt32Array()
        };
    }
}

#nullable disable
public class ZoneNetIdReplicatedLevelData
{
    public uint[] NetIds { get; set; }
}