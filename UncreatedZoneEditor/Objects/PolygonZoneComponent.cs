#if CLIENT
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SDG.Framework.Landscapes;
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
            (Model.PolygonInfo ??= new ZonePolygonInfo()).MinimumHeight = float.IsFinite(_minHeight) ? _minHeight : null;
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
            (Model.PolygonInfo ??= new ZonePolygonInfo()).MinimumHeight = float.IsFinite(_maxHeight) ? _maxHeight : null;
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
        model.PolygonInfo ??= new ZonePolygonInfo { Points = DefaultPoints, MinimumHeight = float.NegativeInfinity, MaximumHeight = float.PositiveInfinity };

        base.Init(model);

        _points = [ ..model.PolygonInfo.Points ];

        _minHeight = model.PolygonInfo.MinimumHeight ?? float.NegativeInfinity;
        _maxHeight = model.PolygonInfo.MaximumHeight ?? float.PositiveInfinity;
        _collider = gameObject.GetOrAddComponent<MeshCollider>();
        _collider.convex = false;
        _collider.isTrigger = true;
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

        Vector3 center = transform.position;

        float maxPosY = center.y + (!float.IsFinite(_maxHeight) ? Landscape.TILE_HEIGHT / 2f : _maxHeight);
        float minPosY = center.y + (!float.IsFinite(_minHeight) ? Landscape.TILE_HEIGHT / -2f : _minHeight);
        for (int i = 0; i < _points.Count; ++i)
        {
            Vector3 point = _points[i];
            Vector3 nextPoint = _points[(i + 1) % _points.Count];

            gizmos.Line(point with { y = minPosY }, nextPoint with { y = minPosY }, Color.white);
            gizmos.Line(point with { y = maxPosY }, nextPoint with { y = maxPosY }, Color.white);
            gizmos.Line(point with { y = minPosY }, point with { y = maxPosY }, Color.white);
            if (IsSelected)
            {
                gizmos.LineAlongTerrain(point, nextPoint, Color.white);
            }
        }
    }


    [UsedImplicitly]
    private void LateUpdate()
    {
        if (!_meshDirty)
            return;

        Mesh mesh = PolygonMeshGenerator.CreateMesh(_points, -1, Vector3.zero, out _);
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