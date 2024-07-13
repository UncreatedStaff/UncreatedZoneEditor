#if CLIENT
using Cysharp.Threading.Tasks;
using DevkitServer;
using DevkitServer.Core.Tools;
using DevkitServer.Multiplayer.Movement;
using SDG.Framework.Devkit;
using SDG.Framework.Landscapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Objects;
using Uncreated.ZoneEditor.UI;

namespace Uncreated.ZoneEditor.Tools;
public class ZoneEditorTool : DevkitServerSelectionTool
{
    private ZoneModel? _polygonEditTarget;
    private Vector2 _panStart;
    private Vector3 _panStartLocation;
    private bool _isPanning;

    public ZoneModel? PolygonEditTarget
    {
        get => _polygonEditTarget;
        set
        {
            ThreadUtil.assertIsGameThread();

            if (ReferenceEquals(_polygonEditTarget, value))
                return;

            ZoneModel? old = _polygonEditTarget;
            _polygonEditTarget = value;
            if (old is null)
            {
                EnterPolygonEditMode();
            }
            else if (value is null)
            {
                ExitPolygonEditMode(old);
                return;
            }

            TeleportToPolygon();
        }
    }
    public ZoneEditorTool()
    {
        CanRotate = true;
    }

    protected override void OnMiddleClickPicked(ref RaycastHit hit)
    {
        if (!EditorZones.EnumerateSelectedZones().Any() && hit.transform.TryGetComponent(out BaseZoneComponent comp) && ZoneEditorUI.Instance != null)
        {
            ZoneEditorUI.Instance.SelectedShape = comp.Model.Shape;
        }
    }

    protected override void Dequip()
    {
        PolygonEditTarget = null;
    }

    protected override void EarlyInputTick()
    {
        if (_polygonEditTarget != null && EditorInteractEx.IsFlying)
        {
            PolygonEditTarget = null;
        }

        if (_polygonEditTarget != null)
        {
            TickPolygonEdit();
        }
        else
        {
            RuntimeGizmos gizmos = RuntimeGizmos.Get();
            foreach (ZoneModel zone in LevelZones.ZoneList)
            {
                if (zone.Component != null)
                {
                    zone.Component.RenderGizmos(gizmos);
                }
            }
        }
    }

    protected override bool TryRaycastSelectableItems(ref Ray ray, out RaycastHit hit)
    {
        if (_polygonEditTarget == null)
        {
            return Physics.Raycast(ray, out hit, 8192f, 8, QueryTriggerInteraction.Collide);
        }

        hit = default;
        return false;
    }

    public override void RequestInstantiation(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (_polygonEditTarget != null)
        {
            AddVertex(position, select: true);
            return;
        }

        if (ZoneEditorUI.Instance?.CurrentName is not { Length: > 0 } name)
        {
            EditorMessage.SendEditorMessage(UncreatedZoneEditor.Instance.Translations.Translate("CreateZoneNoName"));
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            EditorMessage.SendEditorMessage(UncreatedZoneEditor.Instance.Translations.Translate("CreateZoneNoName"));
            return;
        }

        string? shortName = ZoneEditorUI.Instance.CurrentShortName;
        if (string.IsNullOrWhiteSpace(shortName))
            shortName = null;

        ZoneShape shape = ZoneEditorUI.Instance.SelectedShape;
        EditorZones.AddZoneLocal(position, position, rotation.eulerAngles.y, name, shortName, shape, Provider.client);
    }

    protected override IEnumerable<GameObject> EnumerateAreaSelectableObjects()
    {
        if (_polygonEditTarget != null)
            return Enumerable.Empty<GameObject>();
        
        return LevelZones.ZoneList.Where(x => x.Component != null)
                                  .Select(x => x.Component!.gameObject);
    }

    private ERenderMode _oldRenderMode;
    private float _oldFarClip;

    private void EnterPolygonEditMode()
    {
        DevkitSelectionManager.clear();

        _oldRenderMode = GraphicsSettings.renderMode;
        _oldFarClip = MainCamera.instance.farClipPlane;
        MainCamera.instance.farClipPlane = Landscape.TILE_HEIGHT;
        if (_oldRenderMode != ERenderMode.FORWARD)
        {
            GraphicsSettings.renderMode = ERenderMode.FORWARD;
            GraphicsSettings.apply("Entering polygon edit mode.");

            UniTask.Create(async () =>
            {
                await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost);
                MainCamera.instance.orthographic = true;
            });
        }
        else
        {
            MainCamera.instance.orthographic = true;
        }

        CanMiddleClickPick = false;
        CanRotate = false;
        CanAreaSelect = false;
        CanMoveOnInstantiate = false;
        ZoneEditorUI.Instance?.UpdateFieldsFromSelection();
    }

    private void ExitPolygonEditMode(ZoneModel old)
    {
        CanMiddleClickPick = true;
        CanRotate = true;
        CanAreaSelect = true;
        CanMoveOnInstantiate = true;
        _isPanning = false;

        MainCamera.instance.farClipPlane = _oldFarClip;
        MainCamera.instance.orthographicSize = 20f;
        MainCamera.instance.orthographic = false;

        if (_oldRenderMode == ERenderMode.DEFERRED)
        {
            GraphicsSettings.renderMode = ERenderMode.DEFERRED;
            GraphicsSettings.apply("Exiting polygon edit mode.");
        }

        ZoneEditorUI.Instance?.UpdateFieldsFromSelection();

        if (old.Component == null)
            return;

        DevkitSelectionManager.clear();
        DevkitSelectionManager.add(new DevkitSelection(old.Component.gameObject, old.Component.Collider));
    }

    private void TeleportToPolygon()
    {
        Vector3 center = _polygonEditTarget!.Center;

        UserMovement.SetEditorTransform(
            center with { y = Level.HEIGHT - 5f },
            Quaternion.LookRotation(Vector3.down)
        );

        MainCamera.instance.orthographicSize = CalcPolygonOrthoSize() + 2.5f;
        UncreatedZoneEditor.Instance.LogInfo($"Ortho size: {MainCamera.instance.orthographicSize}.");
    }

    private float CalcPolygonOrthoSize()
    {
        Vector3 center = _polygonEditTarget!.Center;
        Bounds maxDistFromCenter = new Bounds(center - MainCamera.instance.transform.position, new Vector3(20f, 0f, 20f));

        if (_polygonEditTarget!.PolygonInfo != null)
        {
            foreach (Vector2 pt in _polygonEditTarget.PolygonInfo.Points)
            {
                maxDistFromCenter.Encapsulate(new Vector3(center.x + pt.x, 0f, center.z + pt.y));
            }
        }

        Vector3 size = maxDistFromCenter.size;

        float screenRatio = (float)Screen.width / Screen.height;
        float targetRatio = size.x / size.y;
        return screenRatio >= targetRatio
            ? size.y / 2f
            : size.y / 2 * (targetRatio / screenRatio);
    }

    private void TickPolygonEdit()
    {
        const float zoomSpeed = -12f;
        float scrollDelta = Input.GetAxis("mouse_z") * zoomSpeed;
        if (Math.Abs(scrollDelta) >= 0.001f)
        {
            float newOrthoSize = MainCamera.instance.orthographicSize + scrollDelta;
            float desiredOrthoSize = CalcPolygonOrthoSize();
            newOrthoSize = Math.Clamp(newOrthoSize, desiredOrthoSize / 8f, desiredOrthoSize * 4f);
            MainCamera.instance.orthographicSize = newOrthoSize;
            UncreatedZoneEditor.Instance.LogInfo($"Desired ortho size: {desiredOrthoSize}, Actual ortho size: {newOrthoSize}.");
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            TeleportToPolygon();
        }

        Vector2 mousePos = Input.mousePosition;

        // middle click pan
        if (Input.GetKeyDown(KeyCode.Mouse2))
        {
            _panStart = mousePos;
            _isPanning = true;
            _panStartLocation = MainCamera.instance.transform.position;
        }
        else if (_isPanning)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _isPanning = false;
            }
            else if (Input.GetKey(KeyCode.Mouse2))
            {
                Vector2 difference = mousePos - _panStart;
                UserMovement.SetEditorTransform(_panStartLocation - new Vector3(difference.x, 0f, difference.y) / 2f, Quaternion.LookRotation(Vector3.down));
            }
            else
            {
                _isPanning = false;
            }
        }

        RenderPolygon();
    }

    private void RenderPolygon()
    {
        ZoneModel model = _polygonEditTarget!;

        Vector3 center = model.Center with { y = 0f }, spawn = model.Spawn with { y = 0f };

        RuntimeGizmos.Get().Circle(center, Vector3.right, Vector3.forward, 2f, Color.green, layer: EGizmoLayer.Foreground);

        if (!model.Spawn.IsNearlyEqual(model.Center))
        {
            RuntimeGizmos.Get().Circle(spawn, Vector3.right, Vector3.forward, 2f, Color.yellow, layer: EGizmoLayer.Foreground);
        }

        ZonePolygonInfo? polygon = model.PolygonInfo;
        if (polygon == null)
            return;

        Vector2[] points = polygon.Points;
        for (int i = 0; i < points.Length; ++i)
        {
            ref Vector2 pt = ref points[i];
            ref Vector2 next = ref points[(i + 1) % points.Length];

            RuntimeGizmos.Get().Line(new Vector3(pt.x + center.x, 0f, pt.y + center.z), new Vector3(next.x + center.x, 0f, next.y + center.z), Color.white, layer: EGizmoLayer.Foreground);
        }
    }

    private void AddVertex(Vector3 position, bool select = false)
    {

    }
}
#endif