#if CLIENT
using System;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Tools;

namespace Uncreated.ZoneEditor.Objects;
public class SphereZoneComponent : BaseZoneComponent
{
    private float _radius;
#nullable disable
    private SphereCollider _collider;
#nullable restore

    public float Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            transform.localScale = new Vector3(value, value, value);
            (Model.CircleInfo ??= new ZoneCircleInfo()).Radius = value;
        }
    }

    public override void Init(ZoneModel model)
    {
        model.CircleInfo ??= new ZoneCircleInfo { Radius = 10f };

        base.Init(model);

        _radius = model.CircleInfo.Radius;
        transform.localScale = new Vector3(_radius, _radius, _radius);
        _collider = gameObject.GetOrAddComponent<SphereCollider>();
        _collider.radius = 1f;
        Collider = _collider;
    }

    public override void RenderGizmos(RuntimeGizmos gizmos)
    {
        base.RenderGizmos(gizmos);

        Vector3 center = transform.position;

        Vector3 scale = transform.localScale;
        float radius = Math.Max(scale.x, Math.Max(scale.y, scale.z));
        Color color = GetRenderColor();
        gizmos.Sphere(center, radius, color);

        if (IsSelected)
        {
            gizmos.SphereProjectionOnTerrain(center, radius, color);
        }
    }

    protected override void ApplyTransform()
    {
        // todo use min or max depending on the previous scale value
        Vector3 scale = transform.localScale;
        Radius = Math.Max(scale.x, Math.Max(scale.y, scale.z));
        transform.rotation = Quaternion.identity;
        base.ApplyTransform();
    }

    public override void RevertToDefault()
    {
        Radius = 10f;
    }
}
#endif