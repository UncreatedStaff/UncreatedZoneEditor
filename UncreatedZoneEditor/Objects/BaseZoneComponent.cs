#if CLIENT
using System;
using Cysharp.Threading.Tasks;
using DevkitServer;
using SDG.Framework.Devkit.Interactable;
using System.Globalization;
using DanielWillett.ReflectionTools;
using Uncreated.ZoneEditor.Data;

namespace Uncreated.ZoneEditor.Objects;
public abstract class BaseZoneComponent : MonoBehaviour,
    IDevkitInteractableBeginSelectionHandler,
    IDevkitInteractableEndSelectionHandler,
    IDevkitSelectionTransformableHandler,
    IDevkitSelectionCopyableHandler
{
    private static readonly StaticGetter<Shader>? GetLogicShader =
        Accessor.GenerateStaticGetter<Shader>(
            AccessorExtensions.DevkitServer.GetType("DevkitServer.Core.SharedResources"), "LogicShader",
            throwOnError: false);

    private Vector3 _center;
    private Vector3 _spawn;
    private float _spawnYaw;
    internal int AddBackAtIndex = -1;
    internal bool IsRemoved;
    internal bool IsRemovedForShapeChange;
    protected internal static readonly Color GizmoNonPrimaryColor = new Color32(140, 140, 140, 255);
    protected internal static readonly Color GizmoPrimaryColor = Color.white;
#nullable disable   
    public Collider Collider { get; protected set; }
    public ZoneModel Model { get; private set; }
    public GameObject PlayerSpawnObject { get; private set; }
#nullable restore
    public bool IsSelected { get; internal set; }
    public bool IsPlayerSelected { get; internal set; }
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

        Transform child = transform.Find("UZE_Player");
        if (child != null)
        {
            PlayerSpawnObject = child.gameObject;
        }
        else
        {
            GameObject childObj = Instantiate(Resources.Load<GameObject>("Edit/Player"));

            childObj.name = "UZE_Player";
            childObj.transform.SetPositionAndRotation(_spawn, Quaternion.Euler(0f, _spawnYaw, 0f));
            childObj.transform.SetParent(transform, true);
            childObj.layer = 3;

            Shader? logicShader = GetLogicShader?.Invoke();
            if (logicShader != null && childObj.TryGetComponent(out Renderer renderer))
            {
                renderer.material.shader = logicShader;
            }

            childObj.AddComponent<PlayerSpawnWidgetComponent>().Init(this);

            childObj.SetActive(false);

            PlayerSpawnObject = childObj;
        }

        Vector3 scale = transform.localScale;
        PlayerSpawnObject.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
    }

    public Color GetRenderColor()
    {
        return Model.Type switch
        {
            ZoneType.MainBase => Model.IsPrimary ? new Color32(255, 153, 153, 255) : new Color32(255, 77, 77, 255),
            ZoneType.AntiMainCampArea => Model.IsPrimary ? new Color32(255, 153, 255, 255) : new Color32(255, 26, 255, 255),
            ZoneType.Other => Model.IsPrimary ? new Color32(179, 230, 255, 255) : new Color32(102, 204, 255, 255),
            _ => Model.IsPrimary ? GizmoPrimaryColor : GizmoNonPrimaryColor
        };
    }

    public abstract void RevertToDefault();

    public virtual void RenderGizmos(RuntimeGizmos gizmos)
    {
        if (Model.IsPrimary)
        {
            gizmos.Arrow(transform.position + Vector3.up * 10f, Vector3.down, 10f, Color.green);
            gizmos.Arrow(_spawn + Vector3.up * 5f, Vector3.down, 5f, Color.magenta);
        }
    }

    void IDevkitInteractableBeginSelectionHandler.beginSelection(InteractionData data)
    {
        IsSelected = true;
        PlayerSpawnObject?.SetActive(true);
        UniTask.Create(async () =>
        {
            await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost);
            EditorZones.BroadcastZoneSelected(Model, true);
        });
    }

    void IDevkitInteractableEndSelectionHandler.endSelection(InteractionData data)
    {
        IsSelected = false;
        PlayerSpawnObject?.SetActive(false);
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
        if (PlayerSpawnObject != null)
        {
            _spawn = PlayerSpawnObject.transform.position;
            _spawnYaw = PlayerSpawnObject.transform.eulerAngles.y;

            PlayerSpawnObject.transform.SetPositionAndRotation(_spawn, Quaternion.Euler(0f, _spawnYaw, 0f));
            Vector3 scale = transform.localScale;
            PlayerSpawnObject.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        }

        Model.Spawn = _spawn;
        Model.SpawnYaw = _spawnYaw;

        UncreatedZoneEditor.Instance.isDirty = true;
    }

    [UsedImplicitly]
    protected virtual void OnEnable()
    {
        if (Model == null)
            return;

        EditorZones.AddFromModelLocal(Model);
        PlayerSpawnObject?.SetActive(true);
        UncreatedZoneEditor.Instance.LogDebug($"Added {Model.Name.Format()} from disabled object (@ {AddBackAtIndex.Format()}).");
    }

    [UsedImplicitly]
    protected virtual void OnDisable()
    {
        if (IsRemoved || Model == null || Level.isExiting || Provider.isApplicationQuitting)
            return;

        EditorZones.TemporarilyRemoveZoneLocal(Model, LevelZones.GetIndexQuick(Model));
        PlayerSpawnObject?.SetActive(false);
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

    GameObject IDevkitSelectionCopyableHandler.copySelection()
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