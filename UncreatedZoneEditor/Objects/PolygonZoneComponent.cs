#if CLIENT
using SDG.Framework.Landscapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Tools;
using Uncreated.ZoneEditor.Utility;

namespace Uncreated.ZoneEditor.Objects;
public class PolygonZoneComponent : BaseZoneComponent
{
#nullable disable
    private MeshCollider _collider;
    private List<Vector2> _points;
    private ReadOnlyCollection<Vector2> _readOnlyPoints;
    private int _tempAddedPointIndex = -1;
#nullable restore

    private bool _meshDirty;
    private float _maxHeight;
    private float _minHeight;
    public IReadOnlyList<Vector2> Points
    {
        get => _readOnlyPoints;
        set
        {
            if (UserControl.ActiveTool is ZoneEditorTool tool)
            {
                tool.CancelDrag();
            }

            _points = [ ..value ];
            _readOnlyPoints = new ReadOnlyCollection<Vector2>(_points);
            (Model.PolygonInfo ??= new ZonePolygonInfo()).Points = _points.ToArray();
            _meshDirty = true;
            InvokeDimensionUpdate();
        }
    }

    public float MinimumHeight
    {
        get => _minHeight;
        set
        {
            if (_minHeight == value)
                return;

            _minHeight = value;
            (Model.PolygonInfo ??= new ZonePolygonInfo()).MinimumHeight = float.IsFinite(value) ? value : null;
            _meshDirty = true;
            InvokeDimensionUpdate();
        }
    }

    public float MaximumHeight
    {
        get => _maxHeight;
        set
        {
            if (_maxHeight == value)
                return;

            _maxHeight = value;
            (Model.PolygonInfo ??= new ZonePolygonInfo()).MaximumHeight = float.IsFinite(value) ? value : null;
            _meshDirty = true;
            InvokeDimensionUpdate();
        }
    }

    private static Vector2[] DefaultPoints =>
    [
        // equilateral triangle
        new Vector2(10f, 0f),
        new Vector2(10f * MathF.Cos(120 * Mathf.Deg2Rad), 10f * MathF.Sin(120 * Mathf.Deg2Rad)),
        new Vector2(10f * MathF.Cos(240 * Mathf.Deg2Rad), 10f * MathF.Sin(240 * Mathf.Deg2Rad))
    ];

    public override void Init(ZoneModel model)
    {
        model.PolygonInfo ??= new ZonePolygonInfo { Points = DefaultPoints, MinimumHeight = null, MaximumHeight = null };

        base.Init(model);

        _points = [ ..model.PolygonInfo.Points ];

        _minHeight = model.PolygonInfo.MinimumHeight ?? float.NegativeInfinity;
        _maxHeight = model.PolygonInfo.MaximumHeight ?? float.PositiveInfinity;
        _collider = gameObject.GetOrAddComponent<MeshCollider>();
        _collider.convex = false;
        _meshDirty = true;
        Collider = _collider;
    }

    public override void RevertToDefault()
    {
        Points = DefaultPoints;
        MinimumHeight = float.NegativeInfinity;
        MaximumHeight = float.PositiveInfinity;
    }

    public override void RenderGizmos(RuntimeGizmos gizmos)
    {
        base.RenderGizmos(gizmos);

        float maxPosY = !float.IsFinite(_maxHeight) ? Landscape.TILE_HEIGHT / 2f : _maxHeight;
        float minPosY = !float.IsFinite(_minHeight) ? Landscape.TILE_HEIGHT / -2f : _minHeight;
        Color color = Model.IsPrimary ? GizmoPrimaryColor : GizmoNonPrimaryColor;
        for (int i = 0; i < _points.Count; ++i)
        {
            Vector2 point = _points[i];
            Vector2 nextPoint = _points[(i + 1) % _points.Count];

            Vector3 worldPt = transform.TransformPoint(new Vector3(point.x, 0f, point.y));
            Vector3 nextWorldPt = transform.TransformPoint(new Vector3(nextPoint.x, 0f, nextPoint.y));

            gizmos.Line(worldPt with { y = minPosY }, nextWorldPt with { y = minPosY }, color);
            gizmos.Line(worldPt with { y = maxPosY }, nextWorldPt with { y = maxPosY }, color);
            gizmos.Line(worldPt with { y = minPosY }, worldPt with { y = maxPosY }, color);
            if (IsSelected)
            {
                gizmos.LineAlongTerrain(worldPt, nextWorldPt, color);
            }
        }
    }

    protected override void ApplyTransform()
    {
        base.ApplyTransform();

        Vector3 center = Center;
        for (int i = 0; i < _points.Count; ++i)
        {
            Vector2 point = _points[i];
            Vector3 worldPt = transform.TransformPoint(new Vector3(point.x, 0f, point.y));
            worldPt -= center;
            _points[i] = new Vector2(worldPt.x, worldPt.z);
        }

        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;

        (Model.PolygonInfo ??= new ZonePolygonInfo()).Points = _points.ToArray();
        _meshDirty = true;

        InvokeDimensionUpdate();
    }

    public bool DeletePoint(int index, out bool wasTempAdd)
    {
        ThreadUtil.assertIsGameThread();

        if (Model.PolygonInfo == null || Model.PolygonInfo.Points.Length <= index || index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Index does not correspond to a point.");

        Vector3 oldPoint = _points[index];
        _points.RemoveAt(index);

        wasTempAdd = _tempAddedPointIndex == index;
        if (wasTempAdd)
        {
            _tempAddedPointIndex = -1;
        }

        if (!CheckPointsValid(_points))
        {
            _points.Insert(index, oldPoint);
            return false;
        }

        if (_tempAddedPointIndex > index)
            --_tempAddedPointIndex;

        Model.PolygonInfo!.Points = _points.ToArray();
        if (!wasTempAdd)
        {
            _meshDirty = true;
            InvokeDimensionUpdate();
        }
        return true;
    }

    public bool AddPoint(Vector2 location, int beforeIndex)
    {
        if (beforeIndex < 0)
            beforeIndex = _points.Count;

        for (int i = 0; i < _points.Count; ++i)
        {
            Vector2 pt = _points[i];
            if (Math.Abs(location.x - pt.x) <= 0.001f && Math.Abs(location.y - pt.y) <= 0.001f)
            {
                UncreatedZoneEditor.Instance.LogWarning($"Tried to add duplicate vertex: {location.Format("F1")} ({beforeIndex.Format()}).");
                return false;
            }
        }

        ThreadUtil.assertIsGameThread();

        if (Model.PolygonInfo == null || Model.PolygonInfo.Points.Length < beforeIndex || beforeIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(beforeIndex), "Index does not correspond to a point.");

        _points.Insert(beforeIndex, location);
        
        if (!CheckPointsValid(_points))
        {
            _points.RemoveAt(beforeIndex);
            return false;
        }

        if (_tempAddedPointIndex >= beforeIndex)
            ++_tempAddedPointIndex;

        Model.PolygonInfo!.Points = _points.ToArray();
        _meshDirty = true;
        InvokeDimensionUpdate();
        return true;
    }

    public bool MovePoint(Vector2 location, int index, out bool wasTempAdd)
    {
        ThreadUtil.assertIsGameThread();

        if (Model.PolygonInfo == null || Model.PolygonInfo.Points.Length <= index || index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Index does not correspond to a point.");

        wasTempAdd = _tempAddedPointIndex == index;

        for (int i = 0; i < _points.Count; ++i)
        {
            Vector2 pt = _points[i];
            if (i != index && Math.Abs(location.x - pt.x) <= 0.001f && Math.Abs(location.y - pt.y) <= 0.001f)
            {
                UncreatedZoneEditor.Instance.LogWarning($"Tried to move duplicate vertex: {location.Format("F1")} ({index.Format()}).");
                return false;
            }
        }

        Vector3 old = _points[index];
        _points[index] = location;

        if (wasTempAdd)
        {
            _tempAddedPointIndex = -1;
        }

        if (!CheckPointsValid(_points))
        {
            _points[index] = old;
            return false;
        }

        Model.PolygonInfo!.Points[index] = location;
        if (!wasTempAdd)
        {
            _meshDirty = true;
            InvokeDimensionUpdate();
        }
        return true;
    }

    public void TempAddPoint(Vector2 location, int beforeIndex)
    {
        ThreadUtil.assertIsGameThread();

        if (Model.PolygonInfo == null || Model.PolygonInfo.Points.Length <= beforeIndex || beforeIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(beforeIndex), "Index does not correspond to a point.");

        _tempAddedPointIndex = beforeIndex;
        _points.Insert(beforeIndex, location);
        Model.PolygonInfo!.Points = _points.ToArray();
    }

    public bool TryAbandonTempPoint()
    {
        if (_tempAddedPointIndex < 0)
            return false;

        _points.RemoveAt(_tempAddedPointIndex);
        Model.PolygonInfo!.Points = _points.ToArray();
        return true;
    }

    private static int[]? _triBuffer;
    internal bool CheckPointsValid() => CheckPointsValid(_points);
    internal static bool CheckPointsValid(List<Vector2> pts)
    {
        try
        {
            int triVertsNeeded = (pts.Count - 2) * 3;
            if (_triBuffer == null || _triBuffer.Length < triVertsNeeded)
            {
                _triBuffer = new int[triVertsNeeded];
            }

            new PolygonTriangulationProcessor(pts, 0).WriteTriangles(_triBuffer, -1);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Merge sequential points that are close enough to each other into one point.
    /// </summary>
    /// <param name="distance"></param>
    public void MergeByDistance(float distance = 0.001f)
    {
        bool wasChanged = false;
        List<Vector2> pts = _points;

        for (int i = 0; i < pts.Count; ++i)
        {
            Vector2 pt = pts[i];

            int max = pts.Count;
            int j = i + 1;
            if (i == pts.Count - 1)
            {
                max = i;
                j = 0;
            }

            for (; j < max; ++j)
            {
                Vector2 pt2 = pts[j];
                if (Math.Abs(pt2.x - pt.x) > distance || Math.Abs(pt2.y - pt.y) > distance)
                    break; // only check sequential points

                pts.RemoveAt(j);
                --j;
                wasChanged = true;
                UncreatedZoneEditor.Instance.LogWarning($"Removed duplicate vertex: {pt2.Format("F1")} ({j.Format()}).");
            }
        }

        if (!wasChanged)
            return;

        if (UserControl.ActiveTool is ZoneEditorTool tool)
        {
            tool.CancelDrag();
        }

        (Model.PolygonInfo ??= new ZonePolygonInfo()).Points = _points.ToArray();
        _meshDirty = true;
        InvokeDimensionUpdate();
    }

    [UsedImplicitly]
    private void LateUpdate()
    {
        if (!_meshDirty || UserControl.ActiveTool is ZoneEditorTool { PolygonEditTarget: not null })
            return;

        MergeByDistance();

        Mesh mesh = PolygonMeshGenerator.CreateMesh(_points, -1,
            !float.IsFinite(MinimumHeight) ? Landscape.TILE_HEIGHT / -2f : MinimumHeight,
            !float.IsFinite(MaximumHeight) ? Landscape.TILE_HEIGHT / 2f : MaximumHeight,
            Vector3.zero, out _
        );

        Mesh? oldMesh = _collider.sharedMesh;
        _collider.sharedMesh = mesh;
        _meshDirty = false;
        if (oldMesh != null)
        {
            Destroy(oldMesh);
        }
    }

    [UsedImplicitly]
    protected override void OnDestroy()
    {
        Mesh? oldMesh = _collider.sharedMesh;
        _collider.sharedMesh = null;
        if (oldMesh != null)
        {
            Destroy(oldMesh);
        }
    }
}
#endif