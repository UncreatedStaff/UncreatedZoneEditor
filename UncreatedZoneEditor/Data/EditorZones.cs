using DevkitServer.Multiplayer.Networking;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Uncreated.ZoneEditor.Multiplayer;
#if CLIENT
using DevkitServer;
#elif SERVER
using DevkitServer.API.UI;
#endif

namespace Uncreated.ZoneEditor.Data;

[EarlyTypeInit]
public sealed class EditorZones : IDisposable
{
    [UsedImplicitly]
    private static readonly NetCall<Vector3, Vector3, string, bool, string, ZoneShape> SendRequestInstantiation
        = new NetCall<Vector3, Vector3, string, bool, string, ZoneShape>(new Guid("f8a68bb92b094b6d8ef583c4069b826a"));
    
    [UsedImplicitly]
    private static readonly NetCall<Vector3, Vector3, string, bool, string, ZoneShape, ulong, NetId> SendInstantiation
        = new NetCall<Vector3, Vector3, string, bool, string, ZoneShape, ulong, NetId>(new Guid("e20a506a94a84024a6a0e2f5eff05aca"));
    
    [UsedImplicitly]
    private static readonly NetCall<Vector3, NetId, NetId> SendRequestAnchorInstantiation = new NetCall<Vector3, NetId, NetId>(new Guid("0aca52c1351045b194086e64edce4cf6"));
    
    [UsedImplicitly]
    private static readonly NetCall<Vector3, NetId, uint[], ulong, NetId> SendAnchorInstantiation = new NetCall<Vector3, NetId, uint[], ulong, NetId>(new Guid("81ffb3936498482eb03cfc2ea7ccfe55"));

#if CLIENT
    internal static readonly CachedMulticastEvent<ZoneAddRequested> EventOnZoneAddRequested = new CachedMulticastEvent<ZoneAddRequested>(typeof(EditorZones), nameof(OnZoneAddRequested));
    internal static readonly CachedMulticastEvent<ZoneAnchorAddRequested> EventOnZoneAnchorAddRequested = new CachedMulticastEvent<ZoneAnchorAddRequested>(typeof(EditorZones), nameof(OnZoneAnchorAddRequested));
    private static readonly CachedMulticastEvent<ZoneSelectionChanged> EventOnZoneSelectionChanged = new CachedMulticastEvent<ZoneSelectionChanged>(typeof(EditorZones), nameof(OnZoneSelectionChanged));
    private static readonly CachedMulticastEvent<ZoneSelectionChangeRequested> EventOnZoneSelectionChangeRequested = new CachedMulticastEvent<ZoneSelectionChangeRequested>(typeof(EditorZones), nameof(OnZoneSelectionChangeRequested));
#elif SERVER
    private static readonly CachedMulticastEvent<ZoneAddRequested> EventOnZoneAddRequested = new CachedMulticastEvent<ZoneAddRequested>(typeof(EditorZones), nameof(OnZoneAddRequested));
    private static readonly CachedMulticastEvent<ZoneAnchorAddRequested> EventOnZoneAnchorAddRequested = new CachedMulticastEvent<ZoneAnchorAddRequested>(typeof(EditorZones), nameof(OnZoneAnchorAddRequested));
#endif
    private static readonly CachedMulticastEvent<ZoneAdded> EventOnZoneAdded = new CachedMulticastEvent<ZoneAdded>(typeof(EditorZones), nameof(OnZoneAdded));
    private static readonly CachedMulticastEvent<ZoneAnchorAdded> EventOnZoneAnchorAdded = new CachedMulticastEvent<ZoneAnchorAdded>(typeof(EditorZones), nameof(OnZoneAnchorAdded));
    private static readonly CachedMulticastEvent<ZoneRemoved> EventOnZoneRemoved = new CachedMulticastEvent<ZoneRemoved>(typeof(EditorZones), nameof(OnZoneRemoved));
    private static readonly CachedMulticastEvent<ZoneAnchorRemoved> EventOnZoneAnchorRemoved = new CachedMulticastEvent<ZoneAnchorRemoved>(typeof(EditorZones), nameof(OnZoneAnchorRemoved));
    private static readonly CachedMulticastEvent<ZoneIndexUpdated> EventOnZoneIndexUpdated = new CachedMulticastEvent<ZoneIndexUpdated>(typeof(EditorZones), nameof(OnZoneIndexUpdated));
    private static readonly CachedMulticastEvent<ZoneAnchorIndexUpdated> EventOnZoneAnchorIndexUpdated = new CachedMulticastEvent<ZoneAnchorIndexUpdated>(typeof(EditorZones), nameof(OnZoneAnchorIndexUpdated));

    internal readonly List<ZoneInfo> ZoneList = [ ];
    private ZoneJsonConfig? _zoneList;

    /// <summary>
    /// Path to the file that stores zones.
    /// </summary>
    public string FilePath => Path.GetFullPath(LevelSavedata.transformName(Level.info.name + "/zones.json"));
    
    /// <summary>
    /// List of all loaded zones.
    /// </summary>
    public IReadOnlyList<ZoneInfo> LoadedZones { get; private set; }
    
    /// <summary>
    /// Singleton instance of <see cref="EditorZones"/>.
    /// </summary>
    public static EditorZones Instance { get; } = new EditorZones();

    /// <summary>
    /// Invoked when a zone is added to the list locally.
    /// </summary>
    public event ZoneAdded OnZoneAdded
    {
        add => EventOnZoneAdded.Add(value);
        remove => EventOnZoneAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when a zone anchor is added to a zone locally.
    /// </summary>
    public event ZoneAnchorAdded OnZoneAnchorAdded
    {
        add => EventOnZoneAnchorAdded.Add(value);
        remove => EventOnZoneAnchorAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when a zone is removed from the list locally.
    /// </summary>
    public event ZoneRemoved OnZoneRemoved
    {
        add => EventOnZoneRemoved.Add(value);
        remove => EventOnZoneRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a zone anchor is removed from a zone locally.
    /// </summary>
    public event ZoneAnchorRemoved OnZoneAnchorRemoved
    {
        add => EventOnZoneAnchorRemoved.Add(value);
        remove => EventOnZoneAnchorRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a zone's index is changed within the list locally.
    /// </summary>
    public event ZoneIndexUpdated OnZoneIndexUpdated
    {
        add => EventOnZoneIndexUpdated.Add(value);
        remove => EventOnZoneIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a zone anchor's index (or it's zone's index) is changed.
    /// </summary>
    public event ZoneAnchorIndexUpdated OnZoneAnchorIndexUpdated
    {
        add => EventOnZoneAnchorIndexUpdated.Add(value);
        remove => EventOnZoneAnchorIndexUpdated.Remove(value);
    }

#if SERVER

    /// <summary>
    /// Invoked when a player requests to add a new zone.
    /// </summary>
    public event ZoneAddRequested OnZoneAddRequested
    {
        add => EventOnZoneAddRequested.Add(value);
        remove => EventOnZoneAddRequested.Remove(value);
    }

    /// <summary>
    /// Invoked when a player requests to add a new zone anchor to a zone.
    /// </summary>
    public event ZoneAnchorAddRequested OnZoneAnchorAddRequested
    {
        add => EventOnZoneAnchorAddRequested.Add(value);
        remove => EventOnZoneAnchorAddRequested.Remove(value);
    }

#endif

#if CLIENT

    /// <summary>
    /// Invoked when the local client is about to request a new zone.
    /// </summary>
    public event ZoneAddRequested OnZoneAddRequested
    {
        add => EventOnZoneAddRequested.Add(value);
        remove => EventOnZoneAddRequested.Remove(value);
    }

    /// <summary>
    /// Invoked when the local client is about to request a new zone anchor.
    /// </summary>
    public event ZoneAnchorAddRequested OnZoneAnchorAddRequested
    {
        add => EventOnZoneAnchorAddRequested.Add(value);
        remove => EventOnZoneAnchorAddRequested.Remove(value);
    }

    /// <summary>
    /// Invoked when a zone is selected or deselected locally.
    /// </summary>
    public event ZoneSelectionChanged OnZoneSelectionChanged
    {
        add => EventOnZoneSelectionChanged.Add(value);
        remove => EventOnZoneSelectionChanged.Remove(value);
    }

    /// <summary>
    /// Invoked before a zone is selected or deselected locally.
    /// </summary>
    public event ZoneSelectionChangeRequested OnZoneSelectionChangeRequested
    {
        add => EventOnZoneSelectionChangeRequested.Add(value);
        remove => EventOnZoneSelectionChangeRequested.Remove(value);
    }

    /// <summary>
    /// The index of the selected zone, or -1.
    /// </summary>
    public int SelectedZoneIndex { get; private set; } = -1;
    
    /// <summary>
    /// The index of the selected zone, or -1.
    /// </summary>
    public int SelectedZoneAnchorIndex { get; private set; } = 0;

    /// <summary>
    /// The zone that's currently selected, or <see langword="null"/>.
    /// </summary>
    public ZoneInfo? SelectedZone => SelectedZoneIndex < 0 ? null : ZoneList[SelectedZoneIndex];
#endif

    private EditorZones()
    {
        LoadedZones = new ReadOnlyCollection<ZoneInfo>(ZoneList);

        SaveManager.onPostSave += SaveZones;
    }

    // called in unload
    void IDisposable.Dispose()
    {
        SaveManager.onPostSave -= SaveZones;
    }

    /// <summary>
    /// Locally add a zone and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="zoneInfo">Information about the new zone to add.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentException">This zone is already added to the list.</exception>
    /// <exception cref="InvalidOperationException">There are already too many zones in the level.</exception>
    /// <returns>The index of the new zone.</returns>
    public int AddZoneLocal(ZoneInfo zoneInfo)
    {
        ThreadUtil.assertIsGameThread();

        int existingIndex = ZoneList.IndexOf(zoneInfo);
        if (existingIndex != -1)
            throw new ArgumentException("This zone is already added to the list.", nameof(zoneInfo));

        if (ZoneList.Count >= ushort.MaxValue)
            throw new InvalidOperationException($"There are already too many zones in the level ({ushort.MaxValue}).");

        int index = ZoneList.Count;
        ZoneList.Add(zoneInfo);

        EventOnZoneAdded.TryInvoke(zoneInfo, index);

        return index;
    }

    /// <summary>
    /// Locally add a zone anchor and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="anchor">Information about the new zone anchor to add.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">There are already too many anchors in the zone.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Anchor index is larger than the current anchor count.</exception>
    /// <exception cref="ArgumentException">There is no zone at <paramref name="zoneIndex"/>. -OR - This zone anchor is already in the zone.</exception>
    /// <returns>The id of the new zone anchor.</returns>
    public ZoneAnchorIdentifier AddZoneAnchorLocal(ZoneAnchor anchor, int zoneIndex, int anchorIndex = -1)
    {
        ThreadUtil.assertIsGameThread();

        if (zoneIndex < 0 || zoneIndex >= Instance.ZoneList.Count)
            throw new ArgumentException($"There is no zone at {zoneIndex}.", nameof(zoneIndex));

        if (ZoneList.Count >= ushort.MaxValue)
            throw new InvalidOperationException($"There are already too many zones in the level ({ushort.MaxValue}).");

        ZoneInfo zone = ZoneList[zoneIndex];

        for (int i = 0; i < zone.Anchors.Count; ++i)
        {
            if (ReferenceEquals(zone.Anchors[i], anchor))
                throw new ArgumentException("This zone anchor is already in the zone.", nameof(anchor));
        }

        if (zone.Anchors.Count >= byte.MaxValue)
            throw new InvalidOperationException($"There are already too many zone anchors in the zone ({byte.MaxValue}).");

        if (!ZoneNetIdDatabase.TryGetZoneNetId(zoneIndex, out NetId zoneNetId))
        {
            throw new ArgumentException($"Unable to find NetId of zone at {zoneIndex}.", nameof(zoneIndex));
        }

        if (anchorIndex < 0)
            anchorIndex = zone.Anchors.Count;
        else if (anchorIndex > zone.Anchors.Count)
            throw new ArgumentOutOfRangeException(nameof(anchorIndex), "Anchor index can not be more than the current anchor count.");

        zone.AddAnchorIntl(anchor, anchorIndex);

        ZoneAnchorIdentifier id = new ZoneAnchorIdentifier(zoneIndex, anchorIndex);
        EventOnZoneAnchorAdded.TryInvoke(anchor, id);

        return id;
    }

    /// <summary>
    /// Locally remove a zone from <see cref="LoadedZones"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool RemoveZoneLocal(ZoneInfo zoneInfo)
    {
        ThreadUtil.assertIsGameThread();

        return RemoveZoneLocal(ZoneList.IndexOf(zoneInfo));
    }

    /// <summary>
    /// Locally remove a zone from <see cref="LoadedZones"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool RemoveZoneLocal(int zoneIndex)
    {
        ThreadUtil.assertIsGameThread();

        if (zoneIndex < 0 || zoneIndex >= ZoneList.Count)
            return false;

        ZoneInfo zone = ZoneList[zoneIndex];

        ZoneList.RemoveAt(zoneIndex);

        EventOnZoneRemoved.TryInvoke(zone, zoneIndex);

        for (int i = zoneIndex; i < ZoneList.Count; ++i)
        {
            ZoneInfo affectedZone = ZoneList[i];

            EventOnZoneIndexUpdated.TryInvoke(affectedZone, i, i + 1);
        }

        return true;
    }

    /// <summary>
    /// Locally remove a zone anchor from a zone.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool RemoveZoneAnchorLocal(ZoneAnchorIdentifier anchor)
    {
        ThreadUtil.assertIsGameThread();

        if (!anchor.CheckSafe())
            return false;

        int zoneIndex = anchor.ZoneIndex;
        ZoneInfo zone = ZoneList[zoneIndex];
        int anchorIndex = anchor.AnchorIndex;

        ZoneAnchor zoneAnchor = zone.Anchors[anchorIndex];

        zone.RemoveAnchorIntl(anchorIndex);

        EventOnZoneAnchorRemoved.TryInvoke(zoneAnchor, anchor);

        for (int i = anchorIndex; i < zone.Anchors.Count; ++i)
        {
            ZoneAnchor affectedAnchor = zone.Anchors[i];
            affectedAnchor.Index = i;

            EventOnZoneAnchorIndexUpdated.TryInvoke(affectedAnchor, new ZoneAnchorIdentifier(zoneIndex, i), new ZoneAnchorIdentifier(zoneIndex, i + 1));
        }

        return true;
    }

#if CLIENT

    /// <summary>
    /// Try to select the given <paramref name="zone"/>, invoking <see cref="OnZoneSelectionChangeRequested"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool RequestSelectZone(ZoneInfo? zone, int anchorIndex = 0)
    {
        if (zone == null)
        {
            return RequestDeselectZone();
        }

        ThreadUtil.assertIsGameThread();

        return RequestSelectZone(ZoneList.IndexOf(zone), anchorIndex);
    }

    /// <summary>
    /// Force select the given <paramref name="zone"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool SelectZone(ZoneInfo? zone, int anchorIndex = 0)
    {
        if (zone == null)
        {
            return DeselectZone();
        }

        ThreadUtil.assertIsGameThread();

        return SelectZone(ZoneList.IndexOf(zone), anchorIndex);
    }

    /// <summary>
    /// Try to select the zone at index <paramref name="zoneIndex"/> in <see cref="LoadedZones"/>, invoking <see cref="OnZoneSelectionChangeRequested"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool RequestSelectZone(int zoneIndex, int anchorIndex = 0)
    {
        if (zoneIndex < 0)
        {
            return RequestDeselectZone();
        }

        ThreadUtil.assertIsGameThread();

        if (zoneIndex >= ZoneList.Count || SelectedZoneIndex == zoneIndex)
            return false;

        ZoneInfo zone = ZoneList[zoneIndex];

        if (anchorIndex < 0 || anchorIndex >= zone.Anchors.Count)
            return false;

        ZoneInfo? old = SelectedZone;
        int oldIndex = SelectedZoneIndex;
        int oldAnchor = SelectedZoneAnchorIndex;
        bool shouldAllow = true;
        EventOnZoneSelectionChangeRequested.TryInvoke(zone, zoneIndex, anchorIndex, old, oldIndex, oldAnchor, ref shouldAllow);

        if (!shouldAllow)
            return false;
        
        SelectedZoneIndex = zoneIndex;
        SelectedZoneAnchorIndex = anchorIndex;
        EventOnZoneSelectionChanged.TryInvoke(zone, zoneIndex, anchorIndex, old, oldIndex, oldAnchor);
        return true;
    }

    /// <summary>
    /// Force select the zone at index <paramref name="zoneIndex"/> in <see cref="LoadedZones"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool SelectZone(int zoneIndex, int anchorIndex = 0)
    {
        if (zoneIndex < 0)
        {
            return DeselectZone();
        }

        ThreadUtil.assertIsGameThread();

        if (zoneIndex < 0 || zoneIndex >= ZoneList.Count || SelectedZoneIndex == zoneIndex)
            return false;

        ZoneInfo zone = ZoneList[zoneIndex];
        ZoneInfo? old = SelectedZone;
        int oldIndex = SelectedZoneIndex;
        int oldAnchor = SelectedZoneAnchorIndex;
        SelectedZoneIndex = zoneIndex;
        SelectedZoneAnchorIndex = anchorIndex;
        EventOnZoneSelectionChanged.TryInvoke(zone, zoneIndex, anchorIndex, old, oldIndex, oldAnchor);
        return true;
    }

    /// <summary>
    /// Force deselect the selected zone, invoking <see cref="OnZoneSelectionChangeRequested"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool RequestDeselectZone()
    {
        ThreadUtil.assertIsGameThread();

        if (SelectedZoneIndex < 0)
            return false;

        ZoneInfo? old = SelectedZone;
        int oldIndex = SelectedZoneIndex;
        int oldAnchor = SelectedZoneAnchorIndex;
        bool shouldAllow = true;
        EventOnZoneSelectionChangeRequested.TryInvoke(null, -1, 0, old, oldIndex, oldAnchor, ref shouldAllow);

        if (!shouldAllow)
            return false;

        SelectedZoneIndex = -1;
        SelectedZoneAnchorIndex = 0;
        EventOnZoneSelectionChanged.TryInvoke(null, -1, 0, old, oldIndex, oldAnchor);
        return true;
    }

    /// <summary>
    /// Force deselect the selected zone.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool DeselectZone()
    {
        ThreadUtil.assertIsGameThread();

        if (SelectedZoneIndex < 0)
            return false;

        ZoneInfo? old = SelectedZone;
        int oldIndex = SelectedZoneIndex;
        int oldAnchor = SelectedZoneAnchorIndex;
        SelectedZoneIndex = -1;
        EventOnZoneSelectionChanged.TryInvoke(null, -1, 0, old, oldIndex, oldAnchor);
        return true;
    }

    /// <summary>
    /// Sends a request to the server to instantiate a new zone.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not on a DevkitServer server.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public void RequestZoneInstantiation(string name, ZoneShape shape, Vector3 center, Vector3 spawn, string? shortName = null)
    {
        ThreadUtil.assertIsGameThread();

        DevkitServerModule.AssertIsDevkitServerClient();
        SendRequestInstantiation.Invoke(center, spawn, name, shortName == null, shortName ?? string.Empty, shape);
    }

    /// <summary>
    /// Sends a request to the server to instantiate a new zone anchor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not on a DevkitServer server.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentException">There is no zone at <paramref name="parentZoneIndex"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="targetAnchorIndex"/> can not be more than the current amount of anchors.</exception>
    public void RequestZoneAnchorInstantiation(Vector3 point, int parentZoneIndex, int targetAnchorIndex = -1)
    {
        ThreadUtil.assertIsGameThread();

        DevkitServerModule.AssertIsDevkitServerClient();

        if (parentZoneIndex < 0 || parentZoneIndex >= ZoneList.Count || !ZoneNetIdDatabase.TryGetZoneNetId(parentZoneIndex, out NetId netId))
        {
            throw new ArgumentException($"There is no zone at {parentZoneIndex}.", nameof(parentZoneIndex));
        }

        ZoneInfo zone = ZoneList[parentZoneIndex];

        if (targetAnchorIndex < 0)
            targetAnchorIndex = zone.Anchors.Count;
        else if (targetAnchorIndex > ZoneList[parentZoneIndex].Anchors.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(targetAnchorIndex), "Anchor index can not be more than the current amount of anchors.");
        }

        NetId afterNetId = NetId.INVALID;

        if (zone.Anchors.Count != 0 &&
            !ZoneNetIdDatabase.TryGetAnchorNetId(new ZoneAnchorIdentifier(parentZoneIndex, targetAnchorIndex == 0 ? zone.Anchors.Count - 1 : targetAnchorIndex - 1), out afterNetId))
        {
            UncreatedZoneEditor.Instance.LogWarning(nameof(EditorZones), $"Unable to find NetId for anchor before target anchor index: {targetAnchorIndex.Format()} on zone {parentZoneIndex}.");
        }

        SendRequestAnchorInstantiation.Invoke(point, netId, afterNetId);
    }

    [NetCall(NetCallSource.FromServer, "e20a506a94a84024a6a0e2f5eff05aca")]
    internal static StandardErrorCode ReceiveInstantiation(MessageContext ctx, Vector3 center, Vector3 spawn, string name, bool isShortNameNull, string shortName, ZoneShape shape, ulong creator, NetId netId)
    {
        ZoneInfo zoneInfo = new ZoneInfo
        {
            Name = name,
            ShortName = isShortNameNull ? null : shortName,
            Center = center,
            Spawn = spawn,
            Creator = new CSteamID(creator),
            Shape = shape,
            NetId = netId
        };

        int zoneIndex = Instance.AddZoneLocal(zoneInfo);

        InitializeZone(zoneInfo, zoneIndex, netId);

        if (creator == Provider.client.m_SteamID)
        {
            Instance.SelectZone(zoneIndex);
        }

        // todo SyncIfAuthority(zoneIndex);

        return StandardErrorCode.Success;
    }

    [NetCall(NetCallSource.FromServer, "81ffb3936498482eb03cfc2ea7ccfe55")]
    internal static StandardErrorCode ReceiveAnchorInstantiation(MessageContext ctx, Vector3 point, NetId zoneNetId, uint[] anchorNetIdsOrder, ulong creator, NetId anchorNetId)
    {
        if (!ZoneNetIdDatabase.TryGetZone(zoneNetId, out int zoneIndex) || zoneIndex < 0 || zoneIndex >= Instance.ZoneList.Count)
        {
            UncreatedZoneEditor.Instance.LogWarning(nameof(EditorZones), $"Unable to find NetId for parent zone {zoneNetId.Format()} for new anchor at {point.Format()} from {creator.Format()}.");
            return StandardErrorCode.NotFound;
        }

        ZoneInfo zone = Instance.ZoneList[zoneIndex];

        int index = Array.IndexOf(anchorNetIdsOrder, anchorNetId.id);

        if (index < 0)
        {
            UncreatedZoneEditor.Instance.LogError(nameof(EditorZones), $"Anchor NetId was not in the anchor order array for zone {zoneIndex.Format()} from {creator.Format()}.");
            return StandardErrorCode.InvalidData;
        }

        if (index > zone.Anchors.Count)
            index = zone.Anchors.Count;

        ZoneAnchor anchor = new ZoneAnchor(zone, -1) { NetId = anchorNetId, Position = point };
        zone.AddAnchorIntl(anchor, index);

        bool anyMismatch = zone.Anchors.Count != anchorNetIdsOrder.Length;
        if (!anyMismatch)
        {
            for (int i = 0; i < zone.Anchors.Count; ++i)
            {
                if (!ZoneNetIdDatabase.TryGetAnchorNetId(new ZoneAnchorIdentifier(zoneIndex, i), out NetId netId))
                {
                    anyMismatch = true;
                    break;
                }

                if (anchorNetIdsOrder[i] == netId.id)
                {
                    continue;
                }

                anyMismatch = true;
                break;
            }
        }

        if (anyMismatch)
        {
            for (int i = 0; i < zone.Anchors.Count; ++i)
            {
                ZoneAnchorIdentifier anchorId = new ZoneAnchorIdentifier(zoneIndex, i);
                ZoneAnchor zoneAnchor = zone.Anchors[i];
                zoneAnchor.Index = i;
                if (!ZoneNetIdDatabase.TryGetAnchorNetId(anchorId, out NetId netId))
                {
                    zoneAnchor.NetId = NetId.INVALID;
                    continue;
                }

                ZoneNetIdDatabase.RemoveAnchor(anchorId);
                zoneAnchor.NetId = Array.IndexOf(anchorNetIdsOrder, netId.id) < 0 ? NetId.INVALID : netId;
            }

            zone.SortAnchorsByOrderIntl(anchorNetIdsOrder);

            ZoneNetIdDatabase.IgnoreIndexChange = true;
            try
            {
                for (int i = 0; i < zone.Anchors.Count; ++i)
                {
                    ZoneAnchor zoneAnchor = zone.Anchors[i];
                    int oldIndex = zoneAnchor.Index;
                    zoneAnchor.Index = i;

                    ZoneAnchorIdentifier anchorId = new ZoneAnchorIdentifier(zoneIndex, i);

                    if (!zoneAnchor.NetId.IsNull())
                    {
                        ZoneNetIdDatabase.RegisterAnchor(anchorId, zoneAnchor.NetId);
                    }

                    if (oldIndex != -1)
                    {
                        EventOnZoneAnchorIndexUpdated.TryInvoke(zoneAnchor, anchorId, new ZoneAnchorIdentifier(zoneIndex, oldIndex));
                    }
                }
            }
            finally
            {
                ZoneNetIdDatabase.IgnoreIndexChange = false;
            }
        }

        ZoneAnchorIdentifier id = new ZoneAnchorIdentifier(zoneIndex, anchor.Index);

        EventOnZoneAnchorAdded.TryInvoke(anchor, id);

        if (creator == Provider.client.m_SteamID)
        {
            // todo Instance.SelectAnchor(id);
        }

        // todo SyncIfAuthority(zoneIndex);

        return StandardErrorCode.Success;
    }
#endif
#if SERVER
    [NetCall(NetCallSource.FromClient, "f8a68bb92b094b6d8ef583c4069b826a")]
    internal static void ReceiveInstantiationRequest(MessageContext ctx, Vector3 center, Vector3 spawn, string name, bool shortNameIsNull, string? shortName, ZoneShape shape)
    {
        if (shortNameIsNull)
            shortName = null;

        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            UncreatedZoneEditor.Instance.LogError(nameof(EditorZones), "Unable to get user from zone instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!UncreatedZoneEditor.Permissions.EditZones.Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, UncreatedZoneEditor.Permissions.EditZones);
            return;
        }

        if (Instance.ZoneList.Count >= ushort.MaxValue)
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendEditorMessage(user, TranslationSource.FromPlugin(UncreatedZoneEditor.Instance), "TooManyZones", [ ushort.MaxValue ]);
            return;
        }

        ZoneInfo zone = new ZoneInfo
        {
            Name = name,
            ShortName = shortName,
            Shape = shape,
            Center = center,
            Spawn = spawn,
            Creator = user.SteamId
        };

        bool shouldAllow = true;
        EventOnZoneAddRequested.TryInvoke(zone, user, ref shouldAllow);
        if (!shouldAllow)
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        Instance.AddZone(zone, ctx);

        UncreatedZoneEditor.Instance.LogDebug(nameof(EditorZones), $"Granted request for instantiation of zone {name.Format(true)} at {center.Format()} from {user.SteamId.Format()}.");

        ctx.Acknowledge(StandardErrorCode.Success);
    }

    [NetCall(NetCallSource.FromClient, "0aca52c1351045b194086e64edce4cf6")]
    internal static void ReceiveAnchorInstantiationRequest(MessageContext ctx, Vector3 point, NetId zoneNetId, NetId afterNetId)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            UncreatedZoneEditor.Instance.LogError(nameof(EditorZones), "Unable to get user from zone anchor instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!UncreatedZoneEditor.Permissions.EditZones.Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, UncreatedZoneEditor.Permissions.EditZones);
            return;
        }

        if (!ZoneNetIdDatabase.TryGetZone(zoneNetId, out int zoneIndex) || zoneIndex < 0 || zoneIndex >= Instance.ZoneList.Count)
        {
            UncreatedZoneEditor.Instance.LogError(nameof(EditorZones), $"Unable to find zone with NetId {zoneNetId.Format()} in anchor instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        ZoneInfo zone = Instance.ZoneList[zoneIndex];

        if (zone.Anchors.Count >= byte.MaxValue)
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendEditorMessage(user, TranslationSource.FromPlugin(UncreatedZoneEditor.Instance), "TooManyZoneAnchors", [ byte.MaxValue ]);
            return;
        }

        int index = zone.Anchors.Count;
        if (zone.Anchors.Count > 0 && !afterNetId.IsNull())
        {
            if (!ZoneNetIdDatabase.TryGetAnchor(afterNetId, out ZoneAnchorIdentifier afterAnchor) || afterAnchor.ZoneIndex != zoneIndex || afterAnchor.AnchorIndex >= zone.Anchors.Count)
            {
                UncreatedZoneEditor.Instance.LogWarning(nameof(EditorZones), $"Unable to find the requested after zone anchor: {afterNetId.Format()}.");
            }
            else
            {
                index = afterAnchor.AnchorIndex + 1;
            }
        }

        ZoneAnchor anchor = new ZoneAnchor(zone, index) { Position = point };

        bool shouldAllow = true;
        EventOnZoneAnchorAddRequested.TryInvoke(anchor, user, ref shouldAllow);
        if (!shouldAllow)
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        Instance.AddZoneAnchor(anchor, zoneIndex, index, ctx);

        UncreatedZoneEditor.Instance.LogDebug(nameof(EditorZones), $"Granted request for instantiation of zone anchor {index.Format()} at {point.Format()} in zone {zoneIndex.Format()} ({zone.Name.Format(false)}) from {user.SteamId.Format()}.");

        ctx.Acknowledge(StandardErrorCode.Success);
    }

    /// <summary>
    /// Add a zone and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <param name="zoneInfo">Information about the new zone to add.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">There are already too many zones in the level.</exception>
    /// <exception cref="ArgumentException">This zone is already added to the list.</exception>
    /// <returns>The index of the new zone.</returns>
    public int AddZone(ZoneInfo zoneInfo, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return AddZone(zoneInfo, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
    }

    internal int AddZone(ZoneInfo zoneInfo, MessageContext ctx)
    {
        int existingIndex = ZoneList.IndexOf(zoneInfo);
        if (existingIndex != -1)
            throw new ArgumentException("This zone is already added to the list.", nameof(zoneInfo));

        if (ZoneList.Count >= ushort.MaxValue)
            throw new InvalidOperationException($"There are already too many zones in the level ({ushort.MaxValue}).");

        zoneInfo.Name ??= string.Empty;

        ulong owner = ctx.GetCaller() is { } user ? user.SteamId.m_SteamID : 0ul;

        if (owner != 0ul)
        {
            zoneInfo.Creator = new CSteamID(owner);
        }

        int index = ZoneList.Count;
        ZoneList.Add(zoneInfo);

        EventOnZoneAdded.TryInvoke(zoneInfo, index);

        InitializeZone(zoneInfo, index, out NetId netId);

        UncreatedZoneEditor.Instance.LogDebug(nameof(EditorZones), $"Zone added: {index.Format()} {zoneInfo.Name}.");

        PooledTransportConnectionList list;
        if (!ctx.IsRequest)
            list = DevkitServerUtility.GetAllConnections();
        else
        {
            ctx.ReplyLayered(SendInstantiation, zoneInfo.Center, zoneInfo.Spawn, zoneInfo.Name, zoneInfo.ShortName == null, zoneInfo.ShortName ?? string.Empty, zoneInfo.Shape, owner, netId);
            list = DevkitServerUtility.GetAllConnections(ctx.Connection);
        }

        SendInstantiation.Invoke(list, zoneInfo.Center, zoneInfo.Spawn, zoneInfo.Name, zoneInfo.ShortName == null, zoneInfo.ShortName ?? string.Empty, zoneInfo.Shape, owner, netId);

        // todo SyncIfAuthority(index);

        return index;
    }

    /// <summary>
    /// Add a zone anchor and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <param name="anchor">Information about the new anchor to add.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">There are already too many anchors in the zone.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Anchor index is larger than the current anchor count.</exception>
    /// <exception cref="ArgumentException">There is no zone at <paramref name="zoneIndex"/>. -OR - This zone anchor is already in the zone.</exception>
    /// <returns>The index of the new zone.</returns>
    public ZoneAnchorIdentifier AddZoneAnchor(ZoneAnchor anchor, int zoneIndex, int anchorIndex = -1, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        if (zoneIndex < 0 || zoneIndex >= Instance.ZoneList.Count)
            throw new ArgumentException($"There is no zone at {zoneIndex}.", nameof(zoneIndex));

        return AddZoneAnchor(anchor, zoneIndex, anchorIndex, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
    }

    internal ZoneAnchorIdentifier AddZoneAnchor(ZoneAnchor anchor, int zoneIndex, int anchorIndex, MessageContext ctx)
    {
        ZoneInfo zone = ZoneList[zoneIndex];

        for (int i = 0; i < zone.Anchors.Count; ++i)
        {
            if (ReferenceEquals(zone.Anchors[i], anchor))
                throw new ArgumentException("This zone anchor is already in the zone.", nameof(anchor));
        }

        if (zone.Anchors.Count >= byte.MaxValue)
            throw new InvalidOperationException($"There are already too many zone anchors in the zone ({byte.MaxValue}).");

        if (!ZoneNetIdDatabase.TryGetZoneNetId(zoneIndex, out NetId zoneNetId))
        {
            throw new ArgumentException($"Unable to find NetId of zone at {zoneIndex}.", nameof(zoneIndex));
        }

        if (anchorIndex < 0)
            anchorIndex = zone.Anchors.Count;
        else if (anchorIndex > zone.Anchors.Count)
            throw new ArgumentOutOfRangeException(nameof(anchorIndex), "Anchor index can not be more than the current anchor count.");

        zone.AddAnchorIntl(anchor, anchorIndex);
        uint[] order = new uint[zone.Anchors.Count];
        for (int i = 0; i < order.Length; ++i)
        {
            if (!ZoneNetIdDatabase.TryGetAnchorNetId(new ZoneAnchorIdentifier(zoneIndex, i), out NetId orderNetId))
                continue;

            order[i] = orderNetId.id;
        }

        anchorIndex = anchor.Index;

        ZoneAnchorIdentifier anchorId = new ZoneAnchorIdentifier(zoneIndex, anchorIndex);
        EventOnZoneAnchorAdded.TryInvoke(anchor, anchorId);

        NetId netId = ZoneNetIdDatabase.AddAnchor(anchorId);

        UncreatedZoneEditor.Instance.LogDebug(nameof(EditorZones), $"Zone anchor added: {anchorId.Format()} at {anchor.Position.Format()}.");

        ulong owner = ctx.GetCaller() is { } user ? user.SteamId.m_SteamID : 0ul;

        PooledTransportConnectionList list;
        if (!ctx.IsRequest)
            list = DevkitServerUtility.GetAllConnections();
        else
        {
            ctx.ReplyLayered(SendAnchorInstantiation, anchor.Position, zoneNetId, order, owner, netId);
            list = DevkitServerUtility.GetAllConnections(ctx.Connection);
        }

        SendAnchorInstantiation.Invoke(list, anchor.Position, zoneNetId, order, owner, netId);

        // todo SyncIfAuthority(zoneIndex);

        return anchorId;
    }
#endif
    internal static void InitializeZone(ZoneInfo zoneInfo, int zoneIndex,
#if SERVER
        out
#endif
            NetId zoneNetId)
    {
#if SERVER
        zoneNetId = ZoneNetIdDatabase.AddZone(zoneIndex);
#else
        ZoneNetIdDatabase.RegisterZone(zoneIndex, zoneNetId);
#endif
        UncreatedZoneEditor.Instance.LogDebug(nameof(EditorZones), $"Assigned zone NetId: {zoneNetId.Format()} to {zoneInfo.Name.Format()}.");
    }

    public void SaveZones()
    {
        ThreadUtil.assertIsGameThread();

        if (!Level.isEditor)
            throw new InvalidOperationException("Only available when editing.");

        string path = FilePath;
        
        if (_zoneList == null || !_zoneList.File.Equals(path))
        {
            _zoneList = new ZoneJsonConfig(path) { ReadOnlyReloading = false };
        }

        ZoneJsonList newConfig = _zoneList.Configuration;

        newConfig.Zones.Clear();
        newConfig.Zones.AddRange(ZoneList.Select(zone => new ZoneJsonModel
        {
            Name = zone.Name,
            ShortName = zone.ShortName,
            Creator = zone.Creator,
            Center = zone.Center,
            Spawn = zone.Spawn,
            Shape = zone.Shape
        }));

        _zoneList.Configuration = newConfig;
        _zoneList.SaveConfig();
    }

    public void ReadZones()
    {
        ThreadUtil.assertIsGameThread();

        string path = FilePath;

        if (_zoneList == null || !_zoneList.File.Equals(path))
        {
            _zoneList = new ZoneJsonConfig(path) { ReadOnlyReloading = false };
        }

        _zoneList.ReloadConfig();

        ZoneList.Clear();

        foreach (ZoneJsonModel model in _zoneList.Configuration.Zones)
        {
            ZoneInfo zoneInfo = new ZoneInfo
            {
                Name = model.Name ?? model.ShortName ?? string.Empty,
                ShortName = model.ShortName,
                Center = model.Center,
                Spawn = model.Spawn,
                Creator = model.Creator,
                Shape = model.Shape
            };

            if (Level.isEditor)
            {
                LoadAnchorInfo(model, zoneInfo);
            }

            ZoneList.Add(zoneInfo);
        }
    }

    private void LoadAnchorInfo(ZoneJsonModel model, ZoneInfo zone)
    {
        
    }
}

#if CLIENT
public delegate void ZoneAddRequested(ZoneInfo zoneToAdd, ref bool shouldAllow);
public delegate void ZoneAnchorAddRequested(ZoneInfo zoneToAdd, ref bool shouldAllow);
public delegate void ZoneSelectionChanged(ZoneInfo? newSelection, int newSelectionIndex, int newAnchorSelectionIndex, ZoneInfo? oldSelection, int oldSelectionIndex, int oldAnchorSelectionIndex);
public delegate void ZoneSelectionChangeRequested(ZoneInfo? newSelection, int newSelectionIndex, int newAnchorSelectionIndex, ZoneInfo? oldSelection, int oldSelectionIndex, int oldAnchorSelectionIndex, ref bool shouldAllow);
#endif

#if SERVER
public delegate void ZoneAddRequested(ZoneInfo zoneToAdd, EditorUser user, ref bool shouldAllow);
public delegate void ZoneAnchorAddRequested(ZoneAnchor anchorToAdd, EditorUser user, ref bool shouldAllow);
#endif

public delegate void ZoneAdded(ZoneInfo newZone, int index);
public delegate void ZoneAnchorAdded(ZoneAnchor newAnchor, ZoneAnchorIdentifier id);
public delegate void ZoneRemoved(ZoneInfo removedZone, int oldIndex);
public delegate void ZoneAnchorRemoved(ZoneAnchor removedAnchor, ZoneAnchorIdentifier oldId);
public delegate void ZoneIndexUpdated(ZoneInfo affectedZone, int newIndex, int oldIndex);
public delegate void ZoneAnchorIndexUpdated(ZoneAnchor affectedAnchor, ZoneAnchorIdentifier newId, ZoneAnchorIdentifier oldId);