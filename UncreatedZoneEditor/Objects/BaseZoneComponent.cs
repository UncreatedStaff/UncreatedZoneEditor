#if CLIENT
using System.Globalization;
using Cysharp.Threading.Tasks;
using DevkitServer;
using SDG.Framework.Devkit.Interactable;
using SDG.Framework.Utilities;
using Uncreated.ZoneEditor.Data;

namespace Uncreated.ZoneEditor.Objects;
public abstract class BaseZoneComponent : MonoBehaviour,
    IDevkitInteractableBeginSelectionHandler,
    IDevkitInteractableEndSelectionHandler,
    IDevkitSelectionTransformableHandler,
    IDevkitSelectionCopyableHandler
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
            Model.Center = value;
            transform.position = value;
        }
    }

    public Vector3 Spawn
    {
        get => _spawn;
        set
        {
            _spawn = value;
            Model.Spawn = value;
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
        Model = model;
        _center = model.Center;
        _spawn = model.Spawn;
        _spawnYaw = model.SpawnYaw;

        transform.position = _center;
        gameObject.layer = 3;
        gameObject.tag = "Logic";
    }

    public abstract void RevertToDefault();

    public virtual void RenderGizmos(RuntimeGizmos gizmos)
    {
        gizmos.Arrow(transform.position + Vector3.up * 10f, Vector3.down, 10f, Color.green);
        gizmos.Arrow(transform.position + Vector3.up * 10f, Vector3.down, 10f, Color.green);
    }

    void IDevkitInteractableBeginSelectionHandler.beginSelection(InteractionData data)
    {
        IsSelected = true;
        UniTask.Create(async () =>
        {
            await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost);
            EditorZones.BroadcastZoneSelected(Model, true);
        });
    }

    void IDevkitInteractableEndSelectionHandler.endSelection(InteractionData data)
    {
        IsSelected = false;
        UniTask.Create(async () =>
        {
            await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost);
            EditorZones.BroadcastZoneSelected(Model, false);
        });
    }

    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        ApplyTransform();
    }

    protected virtual void ApplyTransform()
    {
        _center = transform.position;
        Model.Center = _center;
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
        if (IsRemoved || Model == null || Level.isExiting || Provider.isApplicationQuitting)
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

    public GameObject copySelection()
    {
        ZoneModel newModel = (ZoneModel)Model.Clone();

        string newName = newModel.Name + " Copy";
        if (LevelZones.ZoneList.Exists(x => x.Name.Equals(newName)))
        {
            newName += " ";
            string name;
            int num = 1;
            do
            {
                ++num;
                name = newName + num.ToString(CultureInfo.InvariantCulture);
            }
            while (LevelZones.ZoneList.Exists(x => x.Name.Equals(name)));
            newName = name;
        }

        newModel.Name = newName;
        newModel.ShortName = null;

        return EditorZones.AddFromModelLocal(newModel);
    }
}
#endif