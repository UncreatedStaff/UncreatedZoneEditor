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

    [UsedImplicitly]
    internal static NetCall<ushort, NetId> SendBindZone = new NetCall<ushort, NetId>(new Guid("d8243590cbe14ad1b67137635eec3a61"));

    public ushort CurrentDataVersion => 0;

    internal static void Init()
    {
        EditorZones.OnZoneRemoved += OnZoneRemoved;
        EditorZones.OnZoneIndexUpdated += OnZoneIndexUpdated;
    }

    internal static void Shutdown()
    {
        EditorZones.OnZoneRemoved -= OnZoneRemoved;
        EditorZones.OnZoneIndexUpdated -= OnZoneIndexUpdated;
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
    
    private static void EnsureCapacity(int index)
    {
        ++index;

        if (NetIds.Capacity < index)
            NetIds.Capacity = index;

        while (NetIds.Count < index)
            NetIds.Add(NetId.INVALID);
    }

    private static void OnZoneIndexUpdated(ZoneModel zone, int oldIndex)
    {
        if (!DevkitServerModule.IsEditing || IgnoreIndexChange)
            return;

        EnsureCapacity(zone.Index);

        NetId blockingNetId = NetIds[zone.Index];
        NetId netId = NetIds.Count > oldIndex ? NetIds[oldIndex] : NetId.INVALID;

        if (!blockingNetId.IsNull())
        {
            UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Released blocking net id to save zone: # {oldIndex.Format()} ({netId.Format()}, # {zone.Index.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, zone.Index);
        NetIds[oldIndex] = NetId.INVALID;
        NetIds[zone.Index] = netId;
        UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Moved zone NetId: # {oldIndex.Format()} ({netId.Format()}, # {zone.Index.Format()}).");
    }

    private static void OnZoneRemoved(ZoneModel zone)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId netId = NetIds[zone.Index];
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        NetIds[zone.Index] = NetId.INVALID;
        UncreatedZoneEditor.Instance.LogDebug(nameof(ZoneNetIdDatabase), $"Removed zone NetId: ({netId.Format()}, # {zone.Index.Format()}).");
    }

#if CLIENT
    [NetCall(NetCallSource.FromServer, "d8243590cbe14ad1b67137635eec3a61")]
    private static StandardErrorCode ReceiveBindZone(MessageContext ctx, ushort zoneIndex, NetId netId)
    {
        RegisterZone(zoneIndex, netId);
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

    public static NetId AddZone(int zoneIndex)
    {
        ThreadUtil.assertIsGameThread();

        NetId netId = NetIdRegistry.Claim();

        RegisterZone(zoneIndex, netId);

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

#if SERVER
    internal static void AssignExisting()
    {
        NetIds.Clear();

        List<ZoneModel> zones = LevelZones.ZoneList;

        int index = 0;

        int ct = Math.Min(ushort.MaxValue, zones.Count);

        for (; index < ct; ++index)
        {
            AddZone(index);
        }

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
            NetId netId = NetIds[i];
            if (netId.IsNull())
                continue;

            NetIdRegistry.Assign(netId, (byte)i);
        }
    }

#elif SERVER

    public ZoneNetIdReplicatedLevelData SaveData(CSteamID user)
    {
        uint[] netIds = new uint[Math.Min(ushort.MaxValue, NetIds.Count)];

        for (int i = 0; i < netIds.Length; ++i)
        {
            netIds[i] = NetIds[i].id;
        }

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