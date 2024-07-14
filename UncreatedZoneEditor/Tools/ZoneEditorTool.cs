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
    private bool _isSnappingToLine;
    private bool _isSnappingToGrid;
    private ERenderMode _oldRenderMode;
    private float _oldFarClip;
    private Vector3 _oldPosition;
    private Quaternion _oldRotation;

    private const float GridSquareSize = 1f;
    private const int GridSize = 16;

    private const int SnapLinesCount = 20;

    // not using ray because it normalizes the direction
    private static readonly Vector2[] SnapBufferOrigins = new Vector2[SnapLinesCount];
    private static readonly Vector2[] SnapBufferDirections = new Vector2[SnapLinesCount];

    private const float OneOverSqrt2 = 0.7071067812f;

    private int _vertexDragIndex = -1;

    /// <summary>
    /// The polygon being edited by top-down editor.
    /// </summary>
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
                if (!ReferenceEquals(UserControl.ActiveTool, this))
                    return;

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
            return;

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
        return LevelZones.ZoneList.Where(x => x.Component != null)
                                  .Select(x => x.Component!.gameObject);
    }

    private void EnterPolygonEditMode()
    {
        DevkitSelectionManager.clear();

        _oldRenderMode = GraphicsSettings.renderMode;
        _oldFarClip = MainCamera.instance.farClipPlane;

        Transform cameraTransform = MainCamera.instance.transform;
        _oldPosition = cameraTransform.position;
        _oldRotation = cameraTransform.rotation;

        MainCamera.instance.farClipPlane = Landscape.TILE_HEIGHT;
        if (_oldRenderMode != ERenderMode.FORWARD)
        {
            GraphicsSettings.renderMode = ERenderMode.FORWARD;
            GraphicsSettings.apply("Entering polygon edit mode.");

            UniTask.Create(async () =>
            {
                await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost);
                if (ReferenceEquals(UserControl.ActiveTool, this))
                {
                    MainCamera.instance.orthographic = true;
                }
            });
        }
        else
        {
            MainCamera.instance.orthographic = true;
        }

        CancelDrag();
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
        CancelDrag();

        UserMovement.SetEditorTransform(_oldPosition, _oldRotation);

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
    }

    private float CalcPolygonOrthoSize()
    {
        Vector3 center = _polygonEditTarget!.Center;
        Bounds maxDistFromCenter = new Bounds(center, new Vector3(8f, 0f, 8f));

        if (_polygonEditTarget!.PolygonInfo != null)
        {
            foreach (Vector2 pt in _polygonEditTarget.PolygonInfo.Points)
            {
                maxDistFromCenter.Encapsulate(new Vector3(center.x + pt.x, 0f, center.z + pt.y));
            }
        }

        maxDistFromCenter.Encapsulate(MainCamera.instance.transform.position);

        Vector3 size = maxDistFromCenter.size;

        float screenRatio = (float)Screen.width / Screen.height;
        float targetRatio = size.x / size.y;
        return screenRatio >= targetRatio
            ? size.y / 2f
            : size.y / 2f * (targetRatio / screenRatio);
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

        Vector3 mousePositionInWorld = MainCamera.instance.ScreenToWorldPoint(mousePos);

        Vector2 selectPosition = new Vector2(mousePositionInWorld.x, mousePositionInWorld.z);

        if (_vertexDragIndex >= 0
            && _polygonEditTarget is { PolygonInfo: not null, Component: PolygonZoneComponent poly }
            && _polygonEditTarget.PolygonInfo.Points.Length > _vertexDragIndex
            && poly != null)
        {
            _isSnappingToGrid = InputUtil.IsHoldingControl();
            _isSnappingToLine = !_isSnappingToGrid
                                    && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                                    && _polygonEditTarget is { PolygonInfo.Points.Length: > 3 };

            if (_isSnappingToLine)
            {
                CalculateLineSnaps(in selectPosition);
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                Vector3 center = _polygonEditTarget!.Center;

                if (!poly.MovePoint(new Vector2(selectPosition.x - center.x, selectPosition.y - center.z), _vertexDragIndex, out _))
                {
                    EditorMessage.SendEditorMessage(TranslationSource.FromPlugin(UncreatedZoneEditor.Instance), "PolygonIntersectsItself");
                }

                CancelDrag();
            }
            else if (Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Mouse1))
            {
                CancelDrag();
            }
            else if (Input.GetKeyUp(KeyCode.Mouse0))
            {
                // confirm drag
                Vector3 center = _polygonEditTarget.Center;
                Vector2 worldPos = SnapWorldPosition(in selectPosition);

                if (!poly.MovePoint(new Vector2(worldPos.x - center.x, worldPos.y - center.z), _vertexDragIndex, out _))
                {
                    EditorMessage.SendEditorMessage(TranslationSource.FromPlugin(UncreatedZoneEditor.Instance), "PolygonIntersectsItself");
                }

                CancelDrag();
            }

            if (!_isPanning
                && Input.GetKeyDown(KeyCode.Delete)
                && _polygonEditTarget is { PolygonInfo.Points.Length: > 3 })
            {
                int index = _vertexDragIndex;
                CancelDrag();
                if (!poly.DeletePoint(index, out _))
                {
                    EditorMessage.SendEditorMessage(TranslationSource.FromPlugin(UncreatedZoneEditor.Instance), "PolygonIntersectsItself");
                }
            }
        }
        else if (_vertexDragIndex >= 0)
        {
            CancelDrag();
        }

        Vector2 addPoint = FindPointOnNearestLine(in selectPosition, in selectPosition, true, out bool isCloseEnoughToAddOnLine, out int addIndex);

        if (!isCloseEnoughToAddOnLine)
            addPoint = selectPosition;

        if (_vertexDragIndex < 0
            && Input.GetKeyDown(KeyCode.E)
            && _polygonEditTarget is { PolygonInfo: not null, Component: PolygonZoneComponent poly2 }
            && _polygonEditTarget.PolygonInfo.Points.Length > _vertexDragIndex
            && poly2 != null)
        {
            Vector3 center = _polygonEditTarget!.Center;

            UncreatedZoneEditor.Instance.LogDebug($"Adding new ponit: {addPoint}, {isCloseEnoughToAddOnLine}, {addIndex}.");

            // move cursor to the selected spot
            if (isCloseEnoughToAddOnLine && Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsServer)
            {
                Vector3 screenPos = MainCamera.instance.WorldToScreenPoint(new Vector3(addPoint.x, 0f, addPoint.y));
                WindowsCursorPositionHelper.SetCursorPos(Mathf.RoundToInt(screenPos.x), Mathf.RoundToInt(screenPos.y));
            }

            if (!isCloseEnoughToAddOnLine)
            {
                FindPointOnNearestLine(in selectPosition, in selectPosition, true, out _, out addIndex);
                UncreatedZoneEditor.Instance.LogDebug($"New addIndex: {addIndex}.");
            }

            poly2.TempAddPoint(new Vector2(addPoint.x - center.x, addPoint.y - center.z), addIndex);
            _vertexDragIndex = addIndex;
        }

        RenderPolygon(selectPosition);

        RuntimeGizmos gizmos = RuntimeGizmos.Get();

        Vector2 calcSize = default;
        if (_polygonEditTarget is { PolygonInfo.Points.Length: > 0 })
        {
            Vector3 center = _polygonEditTarget.Center;
            Vector2 halfHandleSize = new Vector2(5f, 5f);
            Vector2[] points = _polygonEditTarget.PolygonInfo.Points;
            for (int i = 0; i < points.Length; ++i)
            {
                GetPoint3d(i, out Vector3 worldPos, in selectPosition, in center, points);
                
                bool isDragging = i == _vertexDragIndex;
                Vector2 screenPoint = MainCamera.instance.WorldToScreenPoint(worldPos);
                bool isHover = isDragging || Math.Abs(mousePos.x - screenPoint.x) <= halfHandleSize.x && Math.Abs(mousePos.y - screenPoint.y) <= halfHandleSize.y;

                if (isHover && _vertexDragIndex < 0 && Input.GetKeyDown(KeyCode.Mouse0))
                {
                    // start dragging
                    _vertexDragIndex = i;
                    worldPos = mousePositionInWorld with { y = 0 };
                }

                if (i == 0)
                {
                    // calculate the size of the box in world coords
                    Vector3 br = MainCamera.instance.ScreenToWorldPoint(screenPoint + halfHandleSize / 2f);
                    Vector3 tl = MainCamera.instance.ScreenToWorldPoint(screenPoint - halfHandleSize / 2f);
                    calcSize.x = Math.Abs(br.x - tl.x);
                    calcSize.y = Math.Abs(br.y - tl.y);
                    calcSize.x = calcSize.y = Math.Max(calcSize.x, calcSize.y);
                }

                gizmos.UpRectangle(worldPos, calcSize, isHover ? isDragging ? Color.green : Color.white : Color.gray, layer: EGizmoLayer.Foreground);
            }

            gizmos.UpPlus(new Vector3(addPoint.x, 0f, addPoint.y), calcSize, Color.red, layer: EGizmoLayer.Foreground);
        }

        if (_vertexDragIndex >= 0)
        {
            if (_isSnappingToLine)
            {
                RenderSnappingLines();
            }
            else if (_isSnappingToGrid)
            {
                RenderSnappingGrid(in selectPosition);
            }
        }
    }

    private static void RenderSnappingLines()
    {
        RuntimeGizmos gizmos = RuntimeGizmos.Get();
        Color color = new Color(0.0625f, 0.0625f, 0.0625f, 0.85f);

        for (int i = 0; i < SnapLinesCount; ++i)
        {
            ref Vector2 origin = ref SnapBufferOrigins[i];
            ref Vector2 direction = ref SnapBufferDirections[i];

            gizmos.Line(
                new Vector3(origin.x - direction.x * 20f, 0f, origin.y - direction.y * 20f),
                new Vector3(origin.x + direction.x * 20f, 0f, origin.y + direction.y * 20f),
                color,
                layer: EGizmoLayer.Foreground
            );
        }
    }

    private void RenderSnappingGrid(in Vector2 centerPos)
    {
        const int gridSquaresPerQuad = GridSize / 2;

        Vector3 snappedCenter = SnapWorldPosition(in centerPos);
        RuntimeGizmos gizmos = RuntimeGizmos.Get();
        Color color = new Color(0.0625f, 0.0625f, 0.0625f, 0.85f);

        for (int i = -gridSquaresPerQuad; i <= gridSquaresPerQuad; ++i)
        {
            gizmos.Line(
                new Vector3(snappedCenter.x + i * GridSquareSize, 0f, snappedCenter.y - gridSquaresPerQuad * GridSquareSize),
                new Vector3(snappedCenter.x + i * GridSquareSize, 0f, snappedCenter.y + gridSquaresPerQuad * GridSquareSize),
                color,
                layer: EGizmoLayer.Foreground
            );
        }

        for (int i = -gridSquaresPerQuad; i <= gridSquaresPerQuad; ++i)
        {
            gizmos.Line(
                new Vector3(snappedCenter.x - gridSquaresPerQuad * GridSquareSize, 0f, snappedCenter.y + i * GridSquareSize),
                new Vector3(snappedCenter.x + gridSquaresPerQuad * GridSquareSize, 0f, snappedCenter.y + i * GridSquareSize),
                color,
                layer: EGizmoLayer.Foreground
            );
        }
    }

    private Vector2 FindPointOnNearestLine(
        in Vector2 worldPos,
        in Vector2 mouseWorldPos,
        bool requireInSegment,
        out bool isCloseEnoughToAddOnLine,
        out int addIndex
    )
    {
        Vector3 center3d = _polygonEditTarget!.Center;
        Vector2 center = new Vector2(center3d.x, center3d.z);
        Vector2[] points = _polygonEditTarget!.PolygonInfo!.Points;

        float minSqrDist = 0f;
        Vector2 pointOnLine = default;
        int index = 0;
        for (int i = 0; i < points.Length; ++i)
        {
            GetPoint2d(i, out Vector2 pt, in mouseWorldPos, in center, points);
            GetPoint2d((i + 1) % points.Length, out Vector2 next, in mouseWorldPos, in center, points);

            Vector2 dir2 = (next - pt).normalized;
            float sqrDist = SqrDistanceToLine(in pt, in dir2, in worldPos);
            if (i != 0 && sqrDist >= minSqrDist)
                continue;

            Vector2 testPoint = ClosestPointOnLine(in pt, in dir2, in worldPos);

            if (requireInSegment && (testPoint.x < Math.Min(pt.x, next.x)
                || testPoint.x > Math.Max(pt.x, next.x)
                || testPoint.y < Math.Min(pt.y, next.y)
                || testPoint.y > Math.Max(pt.y, next.y)))
            {
                // not part of segment
                continue;
            }

            // only let continuing snap from outside the polygon
            if (!requireInSegment && InsidePolygon(testPoint - center, points))
            {
                continue;
            }

            index = i;
            minSqrDist = sqrDist;
            pointOnLine = testPoint;
        }

        isCloseEnoughToAddOnLine = MathF.Pow(worldPos.x - pointOnLine.x, 2) + MathF.Pow(worldPos.y - pointOnLine.y, 2) <= 12f;
        addIndex = (index + 1) % points.Length;

        return pointOnLine;
    }

    private static bool InsidePolygon(Vector2 relPoint, Vector2[] points)
    {
        int intersects = 0;
        for (int i = 0; i < points.Length; ++i)
        {
            ref Vector2 point = ref points[i];
            ref Vector2 next = ref points[(i + 1) % points.Length];

            if (relPoint.y < Math.Min(point.y, next.y) || relPoint.y >= Math.Max(point.y, next.y))
                continue;

            if (Math.Abs(point.x - next.x) < 0.001f && next.x < relPoint.x)
                continue;

            float dx = point.x - next.x,
                  dy = point.y - next.y;

            float m = dy / dx;
            float intx = -(m * point.x - next.y);

            float xPos = (relPoint.y - intx) / m;
            if (xPos >= relPoint.x)
                ++intersects;
        }

        return intersects % 2 == 1;
    }

    internal bool CancelDrag()
    {
        if (_vertexDragIndex < 0)
            return false;

        _vertexDragIndex = -1;
        _isSnappingToLine = false;
        _isSnappingToGrid = false;
        if (_polygonEditTarget is { PolygonInfo: not null, Component: PolygonZoneComponent poly })
        {
            poly.TryAbandonTempPoint();
        }
        return true;
    }

    private void CalculateLineSnaps(in Vector2 mouseWorldPos)
    {
        ZoneModel target = _polygonEditTarget!;
        Vector2[] points = target.PolygonInfo!.Points;
        int lastIndex = _vertexDragIndex == 0 ? points.Length - 1 : (_vertexDragIndex - 1);
        int lastLastIndex = lastIndex == 0 ? points.Length - 1 : (lastIndex - 1);
        int nextIndex = (_vertexDragIndex + 1) % points.Length;
        int nextNextIndex = (nextIndex + 1) % points.Length;

        Vector3 center3d = target.Center;
        Vector2 center = new Vector2(center3d.x, center3d.z);

        Vector2 point = points[_vertexDragIndex];
        point.x += center.x;
        point.y += center.y;

        GetPoint2d(lastIndex, out Vector2 lastPoint, in mouseWorldPos, in center, points);
        GetPoint2d(lastLastIndex, out Vector2 lastLastPoint, in mouseWorldPos, in center, points);
        GetPoint2d(nextIndex, out Vector2 nextPoint, in mouseWorldPos, in center, points);
        GetPoint2d(nextNextIndex, out Vector2 nextNextPoint, in mouseWorldPos, in center, points);

        for (int i = 0; i < 8; ++i)
            SnapBufferOrigins[i] = lastPoint;
        for (int i = 8; i < 16; ++i)
            SnapBufferOrigins[i] = nextPoint;

        for (int i = 0; i < 1; ++i)
        {
            int index = i * 8;
            SnapBufferDirections[index]     = Vector2.right;
            SnapBufferDirections[index + 1] = Vector2.left;
            SnapBufferDirections[index + 2] = Vector2.up;
            SnapBufferDirections[index + 3] = Vector2.down;
            SnapBufferDirections[index + 4] = new Vector2( OneOverSqrt2,  OneOverSqrt2);
            SnapBufferDirections[index + 5] = new Vector2(-OneOverSqrt2,  OneOverSqrt2);
            SnapBufferDirections[index + 6] = new Vector2(-OneOverSqrt2, -OneOverSqrt2);
            SnapBufferDirections[index + 7] = new Vector2( OneOverSqrt2, -OneOverSqrt2);
        }

        SnapBufferOrigins[16] = lastLastPoint;
        SnapBufferDirections[16] = (lastLastPoint - lastPoint).normalized;

        SnapBufferOrigins[17] = lastPoint;
        SnapBufferDirections[17] = (lastPoint - point).normalized;

        SnapBufferOrigins[18] = nextNextPoint;
        SnapBufferDirections[18] = (nextPoint - nextNextPoint).normalized;

        SnapBufferOrigins[19] = nextPoint;
        SnapBufferDirections[19] = (point - nextPoint).normalized;
    }

    private Vector2 SnapWorldPosition(in Vector2 currentPosition)
    {
        if (!_isSnappingToGrid && !_isSnappingToLine)
        {
            return currentPosition;
        }

        if (_isSnappingToGrid)
        {
            return new Vector2(MathF.Round(currentPosition.x / GridSquareSize) * GridSquareSize, MathF.Round(currentPosition.y / GridSquareSize) * GridSquareSize);
        }

        // get closest snap line
        float minSqrDist = 0f;
        int index = 0;
        for (int i = 0; i < SnapLinesCount; ++i)
        {
            ref Vector2 origin = ref SnapBufferOrigins[i];
            ref Vector2 direction = ref SnapBufferDirections[i];
            float sqrDist = SqrDistanceToLine(in origin, in direction, in currentPosition);

            if (i != 0 && sqrDist >= minSqrDist)
                continue;

            index = i;
            minSqrDist = sqrDist;
        }

        if (minSqrDist > 12.25f /* 3.5m */)
        {
            return currentPosition;
        }

        return ClosestPointOnLine(in SnapBufferOrigins[index], in SnapBufferDirections[index], in currentPosition);
    }

    private static float SqrDistanceToLine(in Vector2 rayOrigin, in Vector2 rayDirection, in Vector2 point)
    {
        if (Math.Abs(rayDirection.x) <= float.Epsilon)
        {
            return Math.Abs(point.x - rayOrigin.x);
        }

        float a = rayDirection.y / rayDirection.x;
        float c = rayOrigin.y - a * rayOrigin.x;

        float div = a * point.x - point.y + c;
        float sqrDist = div * div / (a * a + 1);
        return sqrDist;
    }

    private static Vector2 ClosestPointOnLine(in Vector2 rayOrigin, in Vector2 rayDirection, in Vector2 point)
    {
        if (Math.Abs(rayDirection.x) <= float.Epsilon)
        {
            return new Vector2(rayOrigin.x, point.y);
        }

        float a = rayDirection.y / rayDirection.x;
        float c = rayOrigin.y - a * rayOrigin.x;

        return new Vector2(
            (-(-point.x - a * point.y) - a * c) / (a * a + 1),
            (a * (point.x + a * point.y) + c) / (a * a + 1)
        );
    }

    private void RenderPolygon(Vector2 mouseWorldPos)
    {
        ZoneModel model = _polygonEditTarget!;

        Vector3 center = model.Center with { y = 0f }, spawn = model.Spawn with { y = 0f };

        RuntimeGizmos gizmos = RuntimeGizmos.Get();

        gizmos.Circle(center, Vector3.right, Vector3.forward, 2f, Color.green, layer: EGizmoLayer.Foreground);

        if (!model.Spawn.IsNearlyEqual(model.Center))
        {
            gizmos.Circle(spawn, Vector3.right, Vector3.forward, 2f, Color.yellow, layer: EGizmoLayer.Foreground);
        }

        ZonePolygonInfo? polygon = model.PolygonInfo;
        if (polygon == null)
            return;

        Vector2[] points = polygon.Points;
        for (int i = 0; i < points.Length; ++i)
        {
            GetPoint3d(i, out Vector3 pt, in mouseWorldPos, in center, points);
            GetPoint3d((i + 1) % points.Length, out Vector3 next, in mouseWorldPos, in center, points);
            gizmos.Line(pt, next, Color.white, layer: EGizmoLayer.Foreground);
        }
    }

    private void GetPoint3d(int index, out Vector3 pt, in Vector2 mouseWorldPos, in Vector3 center, Vector2[] points)
    {
        Vector2 pos;
        if (index == _vertexDragIndex)
        {
            pos = SnapWorldPosition(in mouseWorldPos);
            pt.x = pos.x;
            pt.y = 0;
            pt.z = pos.y;
        }
        else
        {
            pos = points[index];
            pt.x = pos.x + center.x;
            pt.y = 0f;
            pt.z = pos.y + center.z;
        }
    }

    private void GetPoint2d(int index, out Vector2 pt, in Vector2 mouseWorldPos, in Vector2 center, Vector2[] points)
    {
        if (index == _vertexDragIndex)
        {
            pt = SnapWorldPosition(in mouseWorldPos);
        }
        else
        {
            Vector2 pos = points[index];
            pt.x = pos.x + center.x;
            pt.y = pos.y + center.y;
        }
    }
}
#endif