#if CLIENT
using System;
using SDG.Framework.Landscapes;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Tools;

namespace Uncreated.ZoneEditor.Objects;
public class CircleZoneComponent : BaseZoneComponent
{
    private float _radius;
    private float _maxHeight;
    private float _minHeight;
#nullable disable
    private CapsuleCollider _collider;
#nullable restore

    public float Radius
    {
        get => _radius;
        set
        {
            if (_radius == value)
                return;

            _radius = value;
            (Model.CircleInfo ??= new ZoneCircleInfo()).Radius = value;
            UpdateHeightOrRadius();
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
            UpdateHeights();
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
            UpdateHeights();
            InvokeDimensionUpdate();
        }
    }

    private void UpdateHeights()
    {
        Model.CircleInfo ??= new ZoneCircleInfo();

        Model.CircleInfo.MinimumHeight = float.IsFinite(_minHeight) ? _minHeight : null;
        Model.CircleInfo.MaximumHeight = float.IsFinite(_maxHeight) ? _maxHeight : null;
        UpdateHeightOrRadius();
    }

    private void UpdateHeightOrRadius()
    {
        float maxHeight = _maxHeight, minHeight = _minHeight;
        if (!float.IsFinite(maxHeight))
            maxHeight = Landscape.TILE_HEIGHT / 2f;
        if (!float.IsFinite(minHeight))
            minHeight = -Landscape.TILE_HEIGHT / 2f;

        float height = maxHeight - minHeight;
        float center = minHeight + (maxHeight - minHeight) / 2f;

        transform.localScale = new Vector3(_radius, height, _radius);
        Center = Center with { y = center };
    }

    protected override void ApplyTransform()
    {
        base.ApplyTransform();

        Vector3 scale = transform.localScale;
        transform.rotation = Quaternion.identity;

        float rad = Math.Min(scale.x, scale.z);
        if (_radius != rad)
        {
            Radius = rad;
        }
        else
        {
            UpdateHeightOrRadius();
        }
    }

    public override void Init(ZoneModel model)
    {
        model.CircleInfo ??= new ZoneCircleInfo { Radius = 10f, MinimumHeight = null, MaximumHeight = null };

        base.Init(model);

        _radius = model.CircleInfo.Radius;
        _minHeight = model.CircleInfo.MinimumHeight ?? float.NegativeInfinity;
        _maxHeight = model.CircleInfo.MaximumHeight ?? float.PositiveInfinity;

        _collider = gameObject.GetOrAddComponent<CapsuleCollider>();
        _collider.radius = 1f;
        _collider.height = 1f;
        UpdateHeightOrRadius();
        Collider = _collider;
    }

    public override void RenderGizmos(RuntimeGizmos gizmos)
    {
        base.RenderGizmos(gizmos);

        Vector3 scale = transform.localScale;
        float radius = Math.Min(scale.x, scale.z);
        gizmos.Cylinder(transform.position, Vector3.up, scale.y, radius, GetRenderColor(), alongTerrain: IsSelected);
    }

    public override void RevertToDefault()
    {
        Radius = 10f;
        MinimumHeight = float.NegativeInfinity;
        MaximumHeight = float.PositiveInfinity;
    }
}
#endif