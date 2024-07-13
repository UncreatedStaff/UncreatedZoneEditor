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
#nullable restore

    private bool _meshDirty;
    private float _maxHeight;
    private float _minHeight;
    public IReadOnlyList<Vector2> Points
    {
        get => _readOnlyPoints;
        set
        {
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
        for (int i = 0; i < _points.Count; ++i)
        {
            Vector2 point = _points[i];
            Vector2 nextPoint = _points[(i + 1) % _points.Count];

            Vector3 worldPt = transform.TransformPoint(new Vector3(point.x, 0f, point.y));
            Vector3 nextWorldPt = transform.TransformPoint(new Vector3(nextPoint.x, 0f, nextPoint.y));

            gizmos.Line(worldPt with { y = minPosY }, nextWorldPt with { y = minPosY }, Color.white);
            gizmos.Line(worldPt with { y = maxPosY }, nextWorldPt with { y = maxPosY }, Color.white);
            gizmos.Line(worldPt with { y = minPosY }, worldPt with { y = maxPosY }, Color.white);
            if (IsSelected)
            {
                gizmos.LineAlongTerrain(worldPt, nextWorldPt, Color.white);
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


    [UsedImplicitly]
    private void LateUpdate()
    {
        if (!_meshDirty)
            return;

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