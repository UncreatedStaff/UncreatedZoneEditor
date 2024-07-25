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
        Collider = _collider;
    }

    public override void RenderGizmos(RuntimeGizmos gizmos)
    {
        base.RenderGizmos(gizmos);

        Transform transform = this.transform;
        Vector3 center = transform.position;

        Vector3 size = RoundSize();

        Color color = GetRenderColor();
        if (!transform.rotation.IsNearlyIdentity())
        {
            gizmos.Box(transform.localToWorldMatrix, Vector3.one, Color.gray);
            gizmos.Box(center, size, color);
        }
        else
        {
            gizmos.Box(transform.localToWorldMatrix, Vector3.one, color);
        }

        if (!IsSelected)
            return;

        Bounds bounds = new Bounds(center, size);
        gizmos.AABBProjectedOnTerrain(in bounds, color);
    }

    private Vector3 RoundSize()
    {
        Transform transform = this.transform;
        Vector3 euler = transform.rotation.eulerAngles;
        euler.x = Mathf.Round(euler.x / 90f) * 90f;
        euler.y = Mathf.Round(euler.y / 90f) * 90f;
        euler.z = Mathf.Round(euler.z / 90f) * 90f;
        Vector3 scale = Quaternion.Euler(euler) * transform.localScale;
        return new Vector3(Math.Abs(scale.x), Math.Abs(scale.y), Math.Abs(scale.z));
    }

    protected override void ApplyTransform()
    {
        Vector3 scale = RoundSize();

        transform.rotation = Quaternion.identity;
        Size = scale;

        base.ApplyTransform();
    }

    public override void RevertToDefault()
    {
        Size = new Vector3(10f, 10f, 10f);
    }
}
#endif