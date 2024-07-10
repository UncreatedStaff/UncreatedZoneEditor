#if CLIENT
using System;
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
        Vector3 center;
        float height;
        if (!float.IsFinite(_maxHeight) && !float.IsFinite(_minHeight))
        {
            height = 2048f;
            center = new Vector3(0f, 0f, 0f);
        }
        else if (!float.IsFinite(_maxHeight))
        {
            height = 1024f + _minHeight;
            center = new Vector3(0f, _minHeight + 512f + _minHeight / 2f, 0f);
        }
        else if (!float.IsFinite(_minHeight))
        {
            height = _maxHeight - 1024f;
            center = new Vector3(0f, _maxHeight - 512f - _maxHeight / 2f, 0f);
        }
        else
        {
            height = _maxHeight - _minHeight;
            center = new Vector3(0f, _minHeight + (_maxHeight - _minHeight) / 2f, 0f);
        }

        transform.localScale = new Vector3(_radius, height, _radius);
        Center = center;
    }

    protected override void ApplyTransform()
    {
        Vector3 scale = transform.localScale;
        Radius = Math.Min(scale.x, scale.z);
        base.ApplyTransform();
    }

    public override void Init(ZoneModel model)
    {
        model.CircleInfo ??= new ZoneCircleInfo { Radius = 10f, MinimumHeight = float.NegativeInfinity, MaximumHeight = float.PositiveInfinity };

        base.Init(model);

        _radius = model.CircleInfo.Radius;
        _minHeight = model.CircleInfo.MinimumHeight ?? float.NegativeInfinity;
        _maxHeight = model.CircleInfo.MaximumHeight ?? float.PositiveInfinity;

        _collider = gameObject.GetOrAddComponent<CapsuleCollider>();
        _collider.radius = 1f;
        _collider.height = 1f;
        UpdateHeightOrRadius();
        _collider.isTrigger = true;
        Collider = _collider;
    }

    public override void RenderGizmos(RuntimeGizmos gizmos)
    {
        base.RenderGizmos(gizmos);

        Vector3 scale = transform.localScale;
        float radius = Math.Min(scale.x, scale.z);
        gizmos.Cylinder(transform.position, Vector3.up, scale.y, radius, Color.white, alongTerrain: IsSelected);
    }

    public override void RevertToDefault()
    {
        Radius = 10f;
        MinimumHeight = float.NegativeInfinity;
        MaximumHeight = float.PositiveInfinity;
    }
}
#endif