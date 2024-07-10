#if CLIENT
using SDG.Framework.Devkit.Interactable;
using Uncreated.ZoneEditor.Data;

namespace Uncreated.ZoneEditor.Objects;
public abstract class BaseZoneComponent : MonoBehaviour,
    IDevkitInteractableBeginSelectionHandler,
    IDevkitInteractableEndSelectionHandler,
    IDevkitSelectionTransformableHandler
{
    private Vector3 _center;
    private Vector3 _spawn;
    private float _spawnYaw;
    internal int AddBackAtIndex = -1;
    internal bool IsRemoved;
    internal bool IsRemovedForShapeChange;
#nullable disable
    public Collider Collider { get; protected set; }
    public ZoneModel Model { get; private set; }
#nullable restore
    public bool IsSelected { get; internal set; }
    public Vector3 Center
    {
        get => _center;
        set
        {
            _center = value;
            transform.position = value;
        }
    }

    public Vector3 Spawn
    {
        get => _spawn;
        set
        {
            _spawn = value;
            InvokeDimensionUpdate();
        }
    }

    public float SpawnYaw
    {
        get => _spawnYaw;
        set
        {
            _spawnYaw = value;
            InvokeDimensionUpdate();
        }
    }

    protected void InvokeDimensionUpdate()
    {
        UncreatedZoneEditor.Instance.isDirty = true;
        EditorZones.EventOnZoneDimensionsUpdated.TryInvoke(Model);
    }

    public virtual void Init(ZoneModel model)
    {
        Center = model.Center;
        Spawn = model.Spawn;
        SpawnYaw = model.SpawnYaw;

        gameObject.layer = 3;
        gameObject.tag = "Logic";
        Model = model;
    }

    public abstract void RevertToDefault();

    public virtual void RenderGizmos(RuntimeGizmos gizmos)
    {
        gizmos.Arrow(transform.position + Vector3.up * 10f, Vector3.down, 10f, Color.green);
        gizmos.Arrow(transform.position + Vector3.up * 10f, Vector3.down, 10f, Color.green);
    }

    void IDevkitInteractableBeginSelectionHandler.beginSelection(InteractionData data)
    {
        EditorZones.SelectedZone = Model;
    }

    void IDevkitInteractableEndSelectionHandler.endSelection(InteractionData data)
    {
        if (EditorZones.SelectedZone == Model)
        {
            EditorZones.SelectedZone = null;
        }
    }

    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        ApplyTransform();
    }

    protected virtual void ApplyTransform()
    {
        Model.Center = transform.position;
    }

    [UsedImplicitly]
    protected virtual void OnEnable()
    {
        if (Model == null)
            return;

        EditorZones.AddFromModelLocal(Model);
        UncreatedZoneEditor.Instance.LogDebug($"Added {Model.Name.Format()} from disabled object (@ {AddBackAtIndex.Format()}).");
    }

    [UsedImplicitly]
    protected virtual void OnDisable()
    {
        if (IsRemoved || Model == null)
            return;

        EditorZones.TemporarilyRemoveZoneLocal(Model, EditorZones.GetIndexQuick(Model));
        UncreatedZoneEditor.Instance.LogDebug($"Removed {Model.Name.Format()} from enabled object (@ {Model.Index.Format()}).");
    }

    [UsedImplicitly]
    protected virtual void OnDestroy()
    {
        if (EditorZones.ComponentsPendingUndo.Remove(this))
        {
            UncreatedZoneEditor.Instance.LogDebug($"Permanently removed {Model.Name.Format()} as disabled object (@ {Model.Index.Format()}).");
        }
    }
}
#endif