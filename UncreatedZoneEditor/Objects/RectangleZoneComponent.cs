#if CLIENT
using System;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Tools;

namespace Uncreated.ZoneEditor.Objects;
public class RectangleZoneComponent : BaseZoneComponent
{
    private Vector3 _size;
#nullable disable
    private BoxCollider _collider;
#nullable restore

    public Vector3 Size
    {
        get => _size;
        set
        {
            _size = value;
            transform.localScale = value;
            (Model.AABBInfo ??= new ZoneAABBInfo()).Size = value;
        }
    }

    public override void Init(ZoneModel model)
    {
        model.AABBInfo ??= new ZoneAABBInfo { Size = new Vector3(10f, 10f, 10f) };

        base.Init(model);

        _size = model.AABBInfo.Size;
        transform.localScale = _size;

        _collider = gameObject.GetOrAddComponent<BoxCollider>();
        _collider.size = Vector3.one;
        _collider.isTrigger = true;
        Collider = _collider;
    }

    public override void RenderGizmos(RuntimeGizmos gizmos)
    {
        base.RenderGizmos(gizmos);

        Vector3 center = transform.position;
        Vector3 scale = transform.localScale;
        gizmos.Box(center, scale, Color.white);
        if (IsSelected)
        {
            Bounds bounds = new Bounds(center, scale);
            gizmos.AABBProjectedOnTerrain(in bounds, Color.white);
        }
    }

    protected override void ApplyTransform()
    {
        Vector3 scale = transform.localScale;
        Size = scale;
        base.ApplyTransform();
    }

    public override void RevertToDefault()
    {
        Size = new Vector3(10f, 10f, 10f);
    }
}
#endif