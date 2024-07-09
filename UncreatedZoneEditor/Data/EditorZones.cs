using DevkitServer;
using DevkitServer.Multiplayer.Networking;
using SDG.Framework.Devkit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Uncreated.ZoneEditor.Multiplayer;
#if CLIENT
using Uncreated.ZoneEditor.Objects;
#elif SERVER
using DevkitServer.API.UI;
#endif

namespace Uncreated.ZoneEditor.Data;

[EarlyTypeInit]
public sealed class EditorZones : IDisposable, IDirtyable
{
    [UsedImplicitly]
    private static readonly NetCall<Vector3, Vector3, float, string, bool, string, ZoneShape> SendRequestInstantiation
        = new NetCall<Vector3, Vector3, float, string, bool, string, ZoneShape>(new Guid("f8a68bb92b094b6d8ef583c4069b826a"));
    
    [UsedImplicitly]
    private static readonly NetCall<Vector3, Vector3, float, string, bool, string, ZoneShape, ulong, NetId> SendInstantiation
        = new NetCall<Vector3, Vector3, float, string, bool, string, ZoneShape, ulong, NetId>(new Guid("e20a506a94a84024a6a0e2f5eff05aca"));
    
    [UsedImplicitly]
    private static readonly NetCall<Vector3, NetId, NetId> SendRequestAnchorInstantiation = new NetCall<Vector3, NetId, NetId>(new Guid("0aca52c1351045b194086e64edce4cf6"));
    
    [UsedImplicitly]
    private static readonly NetCall<Vector3, NetId, uint[], ulong, NetId> SendAnchorInstantiation = new NetCall<Vector3, NetId, uint[], ulong, NetId>(new Guid("81ffb3936498482eb03cfc2ea7ccfe55"));

    [UsedImplicitly]
    private static readonly NetCall<NetId, Vector3> SendMoveAnchor = new NetCall<NetId, Vector3>(new Guid("ea43360070774da6b81dc1d7d01e06c5"));

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
    private static readonly CachedMulticastEvent<ZoneAnchorMoved> EventOnZoneAnchorMoved = new CachedMulticastEvent<ZoneAnchorMoved>(typeof(EditorZones), nameof(OnZoneAnchorMoved));
    private static readonly CachedMulticastEvent<ZoneShapeChanged> EventOnZoneShapeChanged = new CachedMulticastEvent<ZoneShapeChanged>(typeof(EditorZones), nameof(OnZoneShapeChanged));
    private static readonly CachedMulticastEvent<ZoneRemoved> EventOnZoneRemoved = new CachedMulticastEvent<ZoneRemoved>(typeof(EditorZones), nameof(OnZoneRemoved));
    private static readonly CachedMulticastEvent<ZoneAnchorRemoved> EventOnZoneAnchorRemoved = new CachedMulticastEvent<ZoneAnchorRemoved>(typeof(EditorZones), nameof(OnZoneAnchorRemoved));
    private static readonly CachedMulticastEvent<ZoneIndexUpdated> EventOnZoneIndexUpdated = new CachedMulticastEvent<ZoneIndexUpdated>(typeof(EditorZones), nameof(OnZoneIndexUpdated));
    private static readonly CachedMulticastEvent<ZoneAnchorIndexUpdated> EventOnZoneAnchorIndexUpdated = new CachedMulticastEvent<ZoneAnchorIndexUpdated>(typeof(EditorZones), nameof(OnZoneAnchorIndexUpdated));

    internal readonly List<ZoneInfo> ZoneList = [ ];
    private ZoneJsonConfig? _zoneList;

    private bool _isDirty;

    /// <summary>
    /// If saving needs to happen before quitting.
    /// </summary>
    /// <remarks>Part of the <see cref="IDirtyable"/> interface for <see cref="DirtyManager"/>.</remarks>
    public bool isDirty
    {
        get => _isDirty;
        set
        {
            if (isDirty == value) return;
            _isDirty = value;
            if (value) DirtyManager.markDirty(this);
            else DirtyManager.markClean(this);
        }
    }

    /// <summary>
    /// Path to the file that stores zones.
    /// </summary>
    public string FilePath => Path.GetFullPath(Level.info.path + "/Uncreated/zones.json");
    
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
    /// Invoked when a zone anchor is moved locally.
    /// </summary>
    public event ZoneAnchorMoved OnZoneAnchorMoved
    {
        add => EventOnZoneAnchorMoved.Add(value);
        remove => EventOnZoneAnchorMoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a zone's shape is changed locally.
    /// </summary>
    public event ZoneShapeChanged OnZoneShapeChanged
    {
        add => EventOnZoneShapeChanged.Add(value);
        remove => EventOnZoneShapeChanged.Remove(value);
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
    public int SelectedZoneAnchorIndex { get; private set; } = -1;

    /// <summary>
    /// The zone that's currently selected, or <see langword="null"/>.
    /// </summary>
    public ZoneInfo? SelectedZone => SelectedZoneIndex < 0 ? null : ZoneList[SelectedZoneIndex];

    /// <summary>
    /// If the spawn arrow is the one selected.
    /// </summary>
    public bool IsSpawnPositionSelected { get; internal set; }
#endif

    private EditorZones()
    {
        LoadedZones = new ReadOnlyCollection<ZoneInfo>(ZoneList);

        Level.onLevelLoaded += OnLevelLoaded;
    }

    // called in Unload().
    void IDisposable.Dispose()
    {
        Level.onLevelLoaded -= OnLevelLoaded;
    }

    private void OnLevelLoaded(int level)
    {
        if (level == Level.BUILD_INDEX_GAME)
        {
            ReadZones();
        }
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

#if CLIENT
        if (Level.isEditor)
        {
            AddAnchorComponents(zoneInfo);
        }
#endif

        UncreatedZoneEditor.Instance.LogDebug($"Added new zone at index {index.Format()}: {zoneInfo.Name.Format()} at {zoneInfo.Center.Format()}.");

        EventOnZoneAdded.TryInvoke(zoneInfo, index);
        
        for (int i = 0; i < zoneInfo.Anchors.Count; ++i)
        {
            EventOnZoneAnchorAdded.TryInvoke(zoneInfo.Anchors[i], new ZoneAnchorIdentifier(index, i));
        }

        isDirty = true;
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

        if (anchorIndex < 0)
            anchorIndex = zone.Anchors.Count;
        else if (anchorIndex > zone.Anchors.Count)
            throw new ArgumentOutOfRangeException(nameof(anchorIndex), "Anchor index can not be more than the current anchor count.");

        zone.AddAnchorIntl(anchor, anchorIndex);

        return ApplyAddAnchorIntl(anchor, zoneIndex, zone, anchorIndex);
    }

    private ZoneAnchorIdentifier ApplyAddAnchorIntl(ZoneAnchor anchor, int zoneIndex, ZoneInfo zone, int anchorIndex)
    {
        ZoneAnchorIdentifier id = new ZoneAnchorIdentifier(zoneIndex, anchorIndex);

        UncreatedZoneEditor.Instance.LogDebug($"Added new zone anchor at index {id.Format()}: {zone.Name.Format()} at {anchor.Position.Format()}.");

        for (int i = zone.Anchors.Count - 1; i > anchorIndex; --i)
        {
            ZoneAnchor affectedAnchor = zone.Anchors[i];
            affectedAnchor.Index = i;
#if CLIENT
            if (affectedAnchor.Component != null)
            {
                affectedAnchor.Component.gameObject.name = $"{zone.Name}[{i}]";
                affectedAnchor.Component.transform.SetSiblingIndex(i);
            }
#endif

            EventOnZoneAnchorIndexUpdated.TryInvoke(affectedAnchor, new ZoneAnchorIdentifier(zoneIndex, i), new ZoneAnchorIdentifier(zoneIndex, i - 1));
        }

        EventOnZoneAnchorAdded.TryInvoke(anchor, id);

        isDirty = true;
        return id;
    }

    /// <summary>
    /// Locally set the position of an anchor.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="newLocalPosition">Local position of the anchor relative to the zone center.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentException">There is no zone or anchor at the given indices.</exception>
    public void MoveAnchorLocal(ZoneAnchorIdentifier anchor, Vector3 newLocalPosition)
    {
        ThreadUtil.assertIsGameThread();

        if (!anchor.CheckSafe())
        {
            throw new ArgumentException($"Anchor {anchor} does not exist.");
        }

        ZoneInfo zone = ZoneList[anchor.ZoneIndex];
        ZoneAnchor zoneAnchor = zone.Anchors[anchor.AnchorIndex];

        Vector3 oldPosition = zoneAnchor.Position - zone.Center;

#if CLIENT
        if (zoneAnchor.Component != null)
        {
            zoneAnchor.Component.transform.localPosition = newLocalPosition;
        }
#endif

        zoneAnchor.Position = zone.Center + newLocalPosition;
        zoneAnchor.TemporaryPosition = zoneAnchor.Position;

        UncreatedZoneEditor.Instance.LogDebug($"Moved zone anchor at index {anchor.Format()}: {zone.Name.Format()} at ({oldPosition.Format()} -> {newLocalPosition.Format()}).");

        //switch (zone.Shape)
        //{
        //    case ZoneShape.Cylinder:
        // todo
        //}

        EventOnZoneAnchorMoved.TryInvoke(zoneAnchor, anchor, newLocalPosition, oldPosition);

        isDirty = true;
    }

    public void SetZoneShapeLocal(int zoneIndex, ZoneShape shape)
    {
        ThreadUtil.assertIsGameThread();

        if (zoneIndex < 0 || zoneIndex >= ZoneList.Count)
        {
            throw new ArgumentException($"Zone at {zoneIndex} does not exist.");
        }

        ZoneInfo zone = ZoneList[zoneIndex];
        ZoneShape oldShape = zone.Shape;

        if (oldShape == shape)
        {
            return;
        }

        zone.Shape = shape;

        EventOnZoneShapeChanged.TryInvoke(zone, zoneIndex, shape, oldShape);

        isDirty = true;
    }

    /// <summary>
    /// Set the position of an anchor and replciate to remotes.
    /// </summary>
    /// <remarks>Replicates to remotes.</remarks>
    /// <param name="newLocalPosition">Local position of the anchor relative to the zone center.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentException">There is no zone or anchor at the given indices.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="UncreatedZoneEditor.Permissions.EditZones"/>.</exception>
    public void MoveAnchor(ZoneAnchorIdentifier anchor, Vector3 newLocalPosition)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        CheckEditPermission();
#endif
        MoveAnchorLocal(anchor, newLocalPosition);

        if (!DevkitServerModule.IsEditing)
        {
            return;
        }

        if (!ZoneNetIdDatabase.TryGetAnchorNetId(anchor, out NetId netId))
        {
            UncreatedZoneEditor.Instance.LogWarning(nameof(EditorZones), $"Failed to find NetId for zone anchor {anchor.Format()}. Did not replicate in MoveAnchor({anchor.Format()}, {newLocalPosition.Format()}).");
            return;
        }

#if CLIENT
        SendMoveAnchor.Invoke(netId, newLocalPosition);
#else
        SendMoveAnchor.Invoke(DevkitServerUtility.GetAllConnections(), netId, newLocalPosition);
#endif
    }

    [NetCall(NetCallSource.FromEither, "ea43360070774da6b81dc1d7d01e06c5")]
    private static void ReceiveMoveAnchor(MessageContext ctx, NetId anchorNetId, Vector3 newLocalPosition)
    {
#if SERVER
        EditorUser? user = ctx.GetCaller();
        if (user == null)
        {
            UncreatedZoneEditor.Instance.LogWarning(nameof(EditorZones), "Unknown user invoking ReceiveMoveAnchor.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!UncreatedZoneEditor.Permissions.EditZones.Has(user.SteamId.m_SteamID))
        {
            UncreatedZoneEditor.Instance.LogWarning(nameof(EditorZones), $"No permissions for user {user.SteamId.Format()} invoking ReceiveMoveAnchor.");
            EditorMessage.SendNoPermissionMessage(user, UncreatedZoneEditor.Permissions.EditZones);
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }
#endif

        if (!ZoneNetIdDatabase.TryGetAnchor(anchorNetId, out ZoneAnchorIdentifier anchor) || !anchor.CheckSafe())
        {
            UncreatedZoneEditor.Instance.LogWarning(nameof(EditorZones), $"Anchor not found by NetId {anchorNetId.Format()} in ReceiveMoveAnchor.");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        Instance.MoveAnchorLocal(anchor, newLocalPosition);
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

#if CLIENT
        if (zone.Component is not null)
        {
            if (zone.Component != null)
                Object.Destroy(zone.Component.gameObject);

            zone.Component = null;
        }

        foreach (ZoneAnchor anchor in zone.Anchors)
        {
            anchor.Component = null;
        }
#endif

        EventOnZoneRemoved.TryInvoke(zone, zoneIndex);

        for (int i = zoneIndex; i < ZoneList.Count; ++i)
        {
            ZoneInfo affectedZone = ZoneList[i];

            EventOnZoneIndexUpdated.TryInvoke(affectedZone, i, i + 1);
        }

        isDirty = true;
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

#if CLIENT
        if (zoneAnchor.Component is not null)
        {
            if (zoneAnchor.Component != null)
                Object.Destroy(zoneAnchor.Component.gameObject);

            zoneAnchor.Component = null;
        }
#endif

        EventOnZoneAnchorRemoved.TryInvoke(zoneAnchor, anchor);

        for (int i = anchorIndex; i < zone.Anchors.Count; ++i)
        {
            ZoneAnchor affectedAnchor = zone.Anchors[i];
            affectedAnchor.Index = i;
#if CLIENT
            if (affectedAnchor.Component != null)
            {
                affectedAnchor.Component.gameObject.name = $"{zone.Name}[{i}]";
                affectedAnchor.Component.transform.SetSiblingIndex(i);
            }
#endif

            EventOnZoneAnchorIndexUpdated.TryInvoke(affectedAnchor, new ZoneAnchorIdentifier(zoneIndex, i), new ZoneAnchorIdentifier(zoneIndex, i + 1));
        }

        isDirty = true;
        return true;
    }

#if CLIENT

    /// <summary>
    /// Try to select the given <paramref name="zone"/>, invoking <see cref="OnZoneSelectionChangeRequested"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool RequestSelectZone(ZoneInfo? zone, int anchorIndex = -1)
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
    public bool SelectZone(ZoneInfo? zone, int anchorIndex = -1)
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
    public bool RequestSelectZone(int zoneIndex, int anchorIndex = -1)
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

        IsSpawnPositionSelected = false;
        SelectedZoneIndex = zoneIndex;
        SelectedZoneAnchorIndex = anchorIndex;
        EventOnZoneSelectionChanged.TryInvoke(zone, zoneIndex, anchorIndex, old, oldIndex, oldAnchor);
        return true;
    }

    /// <summary>
    /// Force select the zone at index <paramref name="zoneIndex"/> in <see cref="LoadedZones"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public bool SelectZone(int zoneIndex, int anchorIndex = -1)
    {
        if (zoneIndex < 0)
        {
            return DeselectZone();
        }

        ThreadUtil.assertIsGameThread();

        if (zoneIndex < 0 || zoneIndex >= ZoneList.Count || SelectedZoneIndex == zoneIndex)
            return false;

        IsSpawnPositionSelected = false;
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
        EventOnZoneSelectionChangeRequested.TryInvoke(null, -1, -1, old, oldIndex, oldAnchor, ref shouldAllow);

        if (!shouldAllow)
            return false;

        IsSpawnPositionSelected = false;
        SelectedZoneIndex = -1;
        SelectedZoneAnchorIndex = -1;
        EventOnZoneSelectionChanged.TryInvoke(null, -1, -1, old, oldIndex, oldAnchor);
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
        EventOnZoneSelectionChanged.TryInvoke(null, -1, -1, old, oldIndex, oldAnchor);
        return true;
    }

    /// <summary>
    /// Sends a request to the server to instantiate a new zone.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not on a DevkitServer server.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public void RequestZoneInstantiation(string name, ZoneShape shape, Vector3 center, Vector3 spawn, float height, string? shortName = null)
    {
        ThreadUtil.assertIsGameThread();

        DevkitServerModule.AssertIsDevkitServerClient();
        SendRequestInstantiation.Invoke(center, spawn, height, name, shortName == null, shortName ?? string.Empty, shape);
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
            TemporaryCenter = center,
            Spawn = spawn,
            TemporarySpawn = spawn,
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

        ZoneAnchor anchor = new ZoneAnchor(zone, -1) { NetId = anchorNetId, Position = point, TemporaryPosition = point };
        zone.AddAnchorIntl(anchor, index);

        for (int i = zone.Anchors.Count - 1; i > index; --i)
        {
            ZoneAnchor affectedAnchor = zone.Anchors[i];
            affectedAnchor.Index = i;
            EventOnZoneAnchorIndexUpdated.TryInvoke(affectedAnchor, new ZoneAnchorIdentifier(zoneIndex, i), new ZoneAnchorIdentifier(zoneIndex, i - 1));
        }
        
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

        for (int i = 0; i < zone.Anchors.Count; ++i)
        {
            ZoneAnchor affectedAnchor = zone.Anchors[i];
            if (affectedAnchor.Component == null)
                continue;

            affectedAnchor.Component.gameObject.name = $"{zone.Name}[{i}]";
            affectedAnchor.Component.transform.SetSiblingIndex(i);
        }

        ZoneAnchorIdentifier id = new ZoneAnchorIdentifier(zoneIndex, anchor.Index);

        EventOnZoneAnchorAdded.TryInvoke(anchor, id);

        if (creator == Provider.client.m_SteamID)
        {
            // todo Instance.SelectAnchor(id);
        }

        // todo SyncIfAuthority(zoneIndex);

        Instance.isDirty = true;
        return StandardErrorCode.Success;
    }
#endif
#if SERVER
    [NetCall(NetCallSource.FromClient, "f8a68bb92b094b6d8ef583c4069b826a")]
    internal static void ReceiveInstantiationRequest(MessageContext ctx, Vector3 center, Vector3 spawn, float height, string name, bool shortNameIsNull, string? shortName, ZoneShape shape)
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
            Creator = user.SteamId,
            Height = float.IsFinite(height) ? height : 128f
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
    /// <exception cref="InvalidOperationException">There are already too many zones in the level. -OR- There are too many anchors in this zone.</exception>
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

        if (zoneInfo.Anchors.Count > byte.MaxValue)
            throw new InvalidOperationException($"There are too many anchors on this zone (max: {byte.MaxValue}).");

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
            ctx.ReplyLayered(SendInstantiation, zoneInfo.Center, zoneInfo.Spawn, zoneInfo.Height, zoneInfo.Name, zoneInfo.ShortName == null, zoneInfo.ShortName ?? string.Empty, zoneInfo.Shape, owner, netId);
            list = DevkitServerUtility.GetAllConnections(ctx.Connection);
        }

        SendInstantiation.Invoke(list, zoneInfo.Center, zoneInfo.Spawn, zoneInfo.Height, zoneInfo.Name, zoneInfo.ShortName == null, zoneInfo.ShortName ?? string.Empty, zoneInfo.Shape, owner, netId);

        for (int i = 0; i < zoneInfo.Anchors.Count; ++i)
        {
            SendAnchor(zoneInfo, index, zoneInfo.Anchors[i], ctx, netId);
        }

        // todo SyncIfAuthority(index);

        isDirty = true;
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

        return SendAnchor(zone, zoneIndex, anchor, ctx, zoneNetId);
    }
    private ZoneAnchorIdentifier SendAnchor(ZoneInfo zone, int zoneIndex, ZoneAnchor anchor, MessageContext ctx, NetId zoneNetId)
    {
        uint[] order = new uint[zone.Anchors.Count];
        for (int i = 0; i < order.Length; ++i)
        {
            if (!ZoneNetIdDatabase.TryGetAnchorNetId(new ZoneAnchorIdentifier(zoneIndex, i), out NetId orderNetId))
                continue;

            order[i] = orderNetId.id;
        }

        int anchorIndex = anchor.Index;

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

        isDirty = true;
        return anchorId;
    }
#endif
#if CLIENT
    internal static void CheckEditPermission()
    {
        if (DevkitServerModule.IsEditing && !UncreatedZoneEditor.Permissions.EditZones.Has())
            throw new NoPermissionsException(UncreatedZoneEditor.Permissions.EditZones);
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

    private void ClearZoneList()
    {
#if CLIENT
        foreach (ZoneInfo zone in ZoneList)
        {
            if (zone.Component is not null)
            {
                if (zone.Component != null)
                {
                    Object.Destroy(zone.Component.gameObject);
                }

                zone.Component = null;
            }

            foreach (ZoneAnchor anchor in zone.Anchors)
            {
                // already destroyed by parent
                anchor.Component = null;
            }
        }
#endif

        ZoneList.Clear();
    }

    void IDirtyable.save() => SaveZones();

    public void SaveZones()
    {
        ThreadUtil.assertIsGameThread();

        if (!Level.isEditor)
            throw new InvalidOperationException("Only available when editing.");

        string path = FilePath;
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);
        
        if (_zoneList == null || !_zoneList.File.Equals(path))
        {
            _zoneList = new ZoneJsonConfig(path) { ReadOnlyReloading = false };
        }

        ZoneJsonList newConfig = _zoneList.Configuration ?? new ZoneJsonList();

        newConfig.Zones ??= [ ];
        newConfig.Zones.Clear();
        newConfig.Zones.AddRange(ZoneList.Select(CreateJsonModel));

        _zoneList.Configuration = newConfig;
        _zoneList.SaveConfig();
    }
    
    private ZoneJsonModel CreateJsonModel(ZoneInfo zone)
    {
        ZoneJsonModel model = new ZoneJsonModel
        {
            Name = zone.Name,
            ShortName = zone.ShortName,
            Creator = zone.Creator,
            Center = zone.Center,
            Spawn = zone.Spawn,
            Shape = zone.Shape,
            Height = zone.Height
        };

        switch (zone.Shape)
        {
            case ZoneShape.Cylinder or ZoneShape.Sphere:
                model.CircleInfo = new ZoneJsonCircleInfo
                {
                    Radius = 10f
                }; // todo
                break;

            case ZoneShape.AABB:
                model.AABBInfo = new ZoneJsonAABBInfo
                {
                    Size = Vector3.one
                }; // todo
                break;

            case ZoneShape.Polygon:
                model.PolygonInfo = new ZoneJsonPolygonInfo
                {
                    Points = new Vector3[zone.Anchors.Count]
                };

                for (int i = 0; i < zone.Anchors.Count; i++)
                {
                    ZoneAnchor anchor = zone.Anchors[i];
                    model.PolygonInfo.Points[i] = anchor.Position - zone.Center;
                }

                break;
        }

        return model;
    }

    public void ReadZones()
    {
        ThreadUtil.assertIsGameThread();

        string path = FilePath;
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);

        if (_zoneList == null || !_zoneList.File.Equals(path))
        {
            _zoneList = new ZoneJsonConfig(path) { ReadOnlyReloading = false };
        }

        _zoneList.ReloadConfig();

        ClearZoneList();

        foreach (ZoneJsonModel model in _zoneList.Configuration.Zones)
        {
            ZoneInfo zoneInfo = new ZoneInfo
            {
                Name = model.Name ?? model.ShortName ?? string.Empty,
                ShortName = model.ShortName,
                Center = model.Center,
                TemporaryCenter = model.Center,
                Spawn = model.Spawn,
                TemporarySpawn = model.Spawn,
                Creator = model.Creator,
                Shape = model.Shape,
                Height = model.Height
            };

            if (!LoadAnchorInfo(model, zoneInfo))
            {
                continue;
            }

#if CLIENT
            if (Level.isEditor)
            {
                AddAnchorComponents(zoneInfo);
            }
#endif

            ZoneList.Add(zoneInfo);
        }
    }

#if CLIENT
    private static void AddAnchorComponents(ZoneInfo zone)
    {
        GameObject mainGameObject;
        if (zone.Component == null)
        {
            mainGameObject = new GameObject(zone.Name);
            zone.Component = mainGameObject.AddComponent<ZoneComponent>();
        }
        else
        {
            mainGameObject = zone.Component.gameObject;
        }

        zone.Component.Init(zone);

        for (int i = 0; i < zone.Anchors.Count; i++)
        {
            ZoneAnchor anchor = zone.Anchors[i];
            anchor.Index = i;
            GameObject gameObject;
            if (anchor.Component != null)
            {
                gameObject = anchor.Component.gameObject;
                gameObject.name = $"{zone.Name}[{i}]";
                gameObject.transform.SetParent(mainGameObject.transform, true);
            }
            else
            {
                gameObject = new GameObject($"{zone.Name}[{i}]")
                {
                    transform = { parent = mainGameObject.transform }
                };

                anchor.Component = gameObject.AddComponent<ZoneAnchorComponent>();
            }

            gameObject.transform.SetSiblingIndex(i);
            anchor.Component.Init(anchor);
        }

        zone.Component.RebuildVisuals();
    }
#endif
    private bool LoadAnchorInfo(ZoneJsonModel model, ZoneInfo zone)
    {
        Vector3 center = model.Center;
     
        switch (model.Shape)
        {
            case ZoneShape.Cylinder when model.CircleInfo != null:
                float radius = model.CircleInfo.Radius;
                zone.AddAnchorIntl(new ZoneAnchor(zone, 0) { Position = new Vector3(center.x + radius, center.y, center.z + radius) }, 0, false);
                zone.AddAnchorIntl(new ZoneAnchor(zone, 1) { Position = new Vector3(center.x - radius, center.y, center.z - radius) }, 1, false);
                return true;

            case ZoneShape.Sphere when model.CircleInfo != null:
                radius = model.CircleInfo.Radius;
                zone.AddAnchorIntl(new ZoneAnchor(zone, 0) { Position = new Vector3(center.x + radius, center.y + radius, center.z + radius) }, 0, false);
                zone.AddAnchorIntl(new ZoneAnchor(zone, 1) { Position = new Vector3(center.x - radius, center.y - radius, center.z - radius) }, 1, false);
                return true;

            case ZoneShape.AABB when model.AABBInfo != null:
                Vector3 extents = model.AABBInfo.Size / 2f;
                zone.AddAnchorIntl(new ZoneAnchor(zone, 0) { Position = new Vector3(center.x + extents.x, center.y + extents.y, center.z + extents.z) }, 0, false);
                zone.AddAnchorIntl(new ZoneAnchor(zone, 1) { Position = new Vector3(center.x + extents.x, center.y + extents.y, center.z - extents.z) }, 1, false);
                zone.AddAnchorIntl(new ZoneAnchor(zone, 2) { Position = new Vector3(center.x + extents.x, center.y - extents.y, center.z + extents.z) }, 2, false);
                zone.AddAnchorIntl(new ZoneAnchor(zone, 3) { Position = new Vector3(center.x + extents.x, center.y - extents.y, center.z - extents.z) }, 3, false);
                zone.AddAnchorIntl(new ZoneAnchor(zone, 4) { Position = new Vector3(center.x - extents.x, center.y + extents.y, center.z + extents.z) }, 4, false);
                zone.AddAnchorIntl(new ZoneAnchor(zone, 5) { Position = new Vector3(center.x - extents.x, center.y + extents.y, center.z - extents.z) }, 5, false);
                zone.AddAnchorIntl(new ZoneAnchor(zone, 6) { Position = new Vector3(center.x - extents.x, center.y - extents.y, center.z + extents.z) }, 6, false);
                zone.AddAnchorIntl(new ZoneAnchor(zone, 7) { Position = new Vector3(center.x - extents.x, center.y - extents.y, center.z - extents.z) }, 7, false);
                return true;

            case ZoneShape.Polygon when model.PolygonInfo != null:
                for (int i = 0; i < model.PolygonInfo.Points.Length; i++)
                {
                    Vector3 point = model.PolygonInfo.Points[i];
                    zone.AddAnchorIntl(new ZoneAnchor(zone, i) { Position = center + point }, i, false);
                }

                return true;

            default:
                UncreatedZoneEditor.Instance.LogWarning($"Zone {zone.Name.Format()} either has an invalid shape ({zone.Shape.Format()}) or is missing the associated data property for the shape.");
                if (Level.isEditor)
                {
                    isDirty = true;
                }
                return false;
        }
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
public delegate void ZoneAnchorMoved(ZoneAnchor anchor, ZoneAnchorIdentifier id, Vector3 newLocalPosition, Vector3 oldLocalPosition);
public delegate void ZoneShapeChanged(ZoneInfo zone, int zoneIndex, ZoneShape newShape, ZoneShape oldShape);
public delegate void ZoneRemoved(ZoneInfo removedZone, int oldIndex);
public delegate void ZoneAnchorRemoved(ZoneAnchor removedAnchor, ZoneAnchorIdentifier oldId);
public delegate void ZoneIndexUpdated(ZoneInfo affectedZone, int newIndex, int oldIndex);
public delegate void ZoneAnchorIndexUpdated(ZoneAnchor affectedAnchor, ZoneAnchorIdentifier newId, ZoneAnchorIdentifier oldId);