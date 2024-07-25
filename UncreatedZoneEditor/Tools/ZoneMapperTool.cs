#if CLIENT
using DevkitServer.API.Cartography;
using DevkitServer.Multiplayer.Movement;
using SDG.Framework.Devkit;
using System;
using System.Collections.Generic;
using Uncreated.ZoneEditor.Data;
using Uncreated.ZoneEditor.Objects;
using Uncreated.ZoneEditor.UI;
using Uncreated.ZoneEditor.Utility;

namespace Uncreated.ZoneEditor.Tools;
public class ZoneMapperTool : IDevkitTool
{
    private Vector2 _panStart;
    private Vector3 _panStartLocation;
    private bool _isPanning;
    private int _selectedZone = -1;
    private int _selectedLineIndex = -1;
    private readonly Dictionary<string, Vector3> _centerCache = new Dictionary<string, Vector3>(32, StringComparer.Ordinal);

    void IDevkitTool.equip()
    {
        EditorUIExtension? editorUIExtension = UIExtensionManager.GetInstance<EditorUIExtension>();
        if (editorUIExtension != null)
            editorUIExtension.IsEnabled = true;

        TopViewHelper.Enter();
        ResetCamera();
        _centerCache.Clear();
    }

    void IDevkitTool.dequip()
    {
        EditorUIExtension? editorUIExtension = UIExtensionManager.GetInstance<EditorUIExtension>();
        if (editorUIExtension != null)
            editorUIExtension.IsEnabled = false;

        TopViewHelper.Exit();
        _centerCache.Clear();
        _selectedZone = -1;
        _selectedLineIndex = -1;
        UpdateSelectedZone();
    }

    void IDevkitTool.update()
    {
        if (EditorInteractEx.IsFlying || EditorMovement.isMoving)
        {
            if (ZoneMapperUI.Instance != null)
                ZoneMapperUI.Instance.Close();

            return;
        }

        bool input = Glazier.Get().ShouldGameProcessKeyDown && Glazier.Get().ShouldGameProcessInput;

        bool needsNametagUpdate = false;
        if (input)
        {
            const float zoomSpeed = -20f;
            float scrollDelta = Input.GetAxis("mouse_z") * zoomSpeed;
            if (Math.Abs(scrollDelta) >= 0.001f)
            {
                float newOrthoSize = MainCamera.instance.orthographicSize + scrollDelta;
                newOrthoSize = Math.Clamp(newOrthoSize, 10f, GetMaxOrthoSize());
                MainCamera.instance.orthographicSize = newOrthoSize;
                needsNametagUpdate = true;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                ResetCamera();
                needsNametagUpdate = true;
            }
        }

        float cameraSizeY = MainCamera.instance.orthographicSize;
        float cameraSizeX = MainCamera.instance.aspect * cameraSizeY;

        Vector2 mousePos = Input.mousePosition;

        if (input)
        {
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
                    Vector2 difference = (mousePos - _panStart) * (cameraSizeY / Screen.height * 4);
                    UserMovement.SetEditorTransform(_panStartLocation - new Vector3(difference.x, 0f, difference.y) / 2f, Quaternion.LookRotation(Vector3.down));
                    needsNametagUpdate = false;
                }
                else
                {
                    _isPanning = false;
                }
            }
        }

        Vector3 mouseWorldPos3 = MainCamera.instance.ScreenToWorldPoint(mousePos);
        Vector2 mouseWorldPos2 = new Vector2(mouseWorldPos3.x, mouseWorldPos3.z);

        RuntimeGizmos gizmos = RuntimeGizmos.Get();

        Rect cameraSize = MainCamera.instance.pixelRect;
        Vector2 grabTargetCameraSize = new Vector2(16f / cameraSize.width * cameraSizeX, 16f / cameraSize.height * cameraSizeY);

        bool isMouseConsumed = false;
        for (int i = 0; i < LevelZones.ZoneList.Count; i++)
        {
            ZoneModel zone = LevelZones.ZoneList[i];
            Vector3 center = zone.Center with { y = 0 };

            if (zone.IsPrimary)
            {
                Color grabColor = Color.green;

                if (input && !isMouseConsumed && center.x + grabTargetCameraSize.x > mouseWorldPos2.x
                                     && center.x - grabTargetCameraSize.x < mouseWorldPos2.x
                                     && center.z + grabTargetCameraSize.y > mouseWorldPos2.y
                                     && center.z - grabTargetCameraSize.y < mouseWorldPos2.y)
                {
                    grabColor = Color.yellow;
                    isMouseConsumed = TickOverZone(i);
                }

                gizmos.Circle(center, Vector3.right, Vector3.forward, Math.Max(grabTargetCameraSize.x, grabTargetCameraSize.y), grabColor, layer: EGizmoLayer.Foreground);
            }

            Color color = zone.Component?.Model == null
                ? zone.IsPrimary ? BaseZoneComponent.GizmoPrimaryColor : BaseZoneComponent.GizmoNonPrimaryColor
                : zone.Component.GetRenderColor();

            switch (zone.Shape)
            {
                case ZoneShape.AABB when zone.AABBInfo != null:
                    Vector3 size = zone.AABBInfo.Size;
                    gizmos.UpRectangle(center, new Vector2(size.x, size.z), color, layer: EGizmoLayer.Foreground);
                    break;

                case ZoneShape.Cylinder or ZoneShape.Sphere when zone.CircleInfo != null:
                    gizmos.Circle(center, Vector3.right, Vector3.forward, zone.CircleInfo.Radius, color, layer: EGizmoLayer.Foreground);
                    break;

                case ZoneShape.Polygon when zone.PolygonInfo != null:
                    Vector2[] points = zone.PolygonInfo.Points;
                    Vector3 begin = default, end = default;
                    for (int j = 0; j < points.Length; ++j)
                    {
                        ref readonly Vector2 pt1 = ref points[j];
                        ref readonly Vector2 pt2 = ref points[(j + 1) % points.Length];

                        begin.x = pt1.x + center.x;
                        begin.z = pt1.y + center.z;
                        end.x = pt2.x + center.x;
                        end.z = pt2.y + center.z;
                        gizmos.Line(begin, end, color, layer: EGizmoLayer.Foreground);
                    }
                    break;
            }
        }

        if (needsNametagUpdate)
        {
            EditorUIExtension? editorUIExtension = UIExtensionManager.GetInstance<EditorUIExtension>();
            if (editorUIExtension != null)
                editorUIExtension.UpdateAllLocationTags();
        }

        ZoneModel? closestLineZone = null;
        int closestLineIndex = -1;
        float closestLineSqrDist = 0f;

        // hovering line
        if (input && !isMouseConsumed)
        {
            for (int i = 0; i < LevelZones.ZoneList.Count; ++i)
            {
                ZoneModel zone = LevelZones.ZoneList[i];
                if (!zone.IsPrimary)
                    continue;
                Vector2 center = new Vector2(zone.Center.x, zone.Center.z);
                for (int j = 0; j < zone.UpstreamZones.Count; ++j)
                {
                    string? target = zone.UpstreamZones[j].ZoneName;
                    if (target == null)
                        continue;

                    Vector3 targetCenter3 = CenterFromName(target);
                    Vector2 targetCenter2 = new Vector2(targetCenter3.x, targetCenter3.z);
                    Vector2 ray = center - targetCenter2;
                    float dist = GraphicsHelper.SqrDistanceToLine(in center, in ray, in mouseWorldPos2);
                    if (closestLineZone != null && !(dist < closestLineSqrDist) || !GraphicsHelper.IsInRect(in mouseWorldPos2, in center, in targetCenter2))
                        continue;

                    closestLineSqrDist = dist;
                    closestLineZone = zone;
                    closestLineIndex = j;
                }
            }

            if (closestLineZone != null)
            {
                string target = closestLineZone.UpstreamZones[closestLineIndex].ZoneName;
                Vector2 center = new Vector2(closestLineZone.Center.x, closestLineZone.Center.z);
                Vector3 targetCenter = CenterFromName(target);
                Vector2 ray = center - new Vector2(targetCenter.x, targetCenter.z);
                
                Vector2 hitPoint = GraphicsHelper.ClosestPointOnLine(in center, in ray, in mouseWorldPos2);
                if ((hitPoint - mouseWorldPos2).sqrMagnitude < grabTargetCameraSize.sqrMagnitude)
                {
                    isMouseConsumed = true;
                    if (Input.GetKeyDown(KeyCode.Mouse0))
                    {
                        _selectedZone = closestLineZone.Index;
                        _selectedLineIndex = closestLineIndex;
                        UpdateSelectedZone();
                    }
                }
                else
                {
                    closestLineZone = null;
                }
            }
        }

        if (input && !isMouseConsumed && _selectedZone >= 0 && Input.GetKeyUp(KeyCode.Mouse0))
        {
            _selectedZone = -1;
            _selectedLineIndex = -1;
            UpdateSelectedZone();
        }

        for (int i = 0; i < LevelZones.ZoneList.Count; ++i)
        {
            ZoneModel zone = LevelZones.ZoneList[i];
            if (!zone.IsPrimary)
                continue;
            Vector3 center = zone.Center with { y = 0f };
            for (int j = 0; j < zone.UpstreamZones.Count; ++j)
            {
                string? target = zone.UpstreamZones[j].ZoneName;
                if (target == null)
                    continue;

                Vector3 targetCenter = CenterFromName(target);

                Color color = Color.red;
                if (i == _selectedZone && j == _selectedLineIndex)
                {
                    color = Color.yellow;
                }
                else if (closestLineZone == zone && closestLineIndex == j && isMouseConsumed)
                {
                    color = new Color32(255, 153, 51, 255);
                    EditorUI.hint(EEditorMessage.FOCUS, UncreatedZoneEditor.Instance.Translations.Translate("SelectedUpstreamWeightHint", zone.UpstreamZones[j].Weight));
                }

                Vector3 midPoint = Vector3.LerpUnclamped(center, targetCenter, 0.15f);
                gizmos.Line(center, midPoint, Color.green, layer: EGizmoLayer.Foreground);
                gizmos.Line(midPoint, targetCenter, color, layer: EGizmoLayer.Foreground);
            }
        }

        if (_selectedZone < 0)
            return;

        if (_selectedZone >= LevelZones.ZoneList.Count)
        {
            _selectedZone = -1;
        }
        else if (_selectedLineIndex < 0)
        {
            gizmos.Line(LevelZones.ZoneList[_selectedZone].Center with { y = 0f }, mouseWorldPos3, Color.green, layer: EGizmoLayer.Foreground);
        }
        else if (input && Input.GetKeyDown(KeyCode.Delete))
        {
            DeleteSelectedUpstream();
            _selectedLineIndex = -1;
            _selectedZone = -1;
            UpdateSelectedZone();
        }
    }

    private void UpdateSelectedZone()
    {
        UncreatedZoneEditor.Instance.LogInfo($"Selected zone updated: {_selectedZone.Format()} (line: {_selectedLineIndex.Format()}).");
        if (ZoneMapperUI.Instance is { IsActive: true })
        {
            ZoneMapperUI.Instance.UpdateSelectedZone(_selectedZone, _selectedLineIndex);
        }
    }

    private Vector3 CenterFromName(string target)
    {
        if (_centerCache.TryGetValue(target, out Vector3 targetCenter))
            return targetCenter;

        for (int k = 0; k < LevelZones.ZoneList.Count; ++k)
        {
            ZoneModel otherZone = LevelZones.ZoneList[k];
            if (otherZone.IsPrimary && otherZone.Name.Equals(target, StringComparison.Ordinal))
            {
                _centerCache.Add(target, targetCenter = otherZone.Center with { y = 0f });
            }
        }

        return targetCenter;
    }

    internal void UpdateSelectedWeight(float newWeight)
    {
        if (_selectedLineIndex < 0 || _selectedZone < 0 || _selectedZone >= LevelZones.ZoneList.Count)
            return;

        ZoneModel zone = LevelZones.ZoneList[_selectedZone];
        if (_selectedLineIndex >= zone.UpstreamZones.Count)
            return;

        zone.UpstreamZones[_selectedLineIndex].Weight = newWeight;
        UncreatedZoneEditor.Instance.isDirty = true;
    }
    
    private void DeleteSelectedUpstream()
    {
        if (_selectedZone < 0 || _selectedZone >= LevelZones.ZoneList.Count || _selectedLineIndex < 0)
            return;

        ZoneModel zone = LevelZones.ZoneList[_selectedZone];
        if (_selectedLineIndex >= zone.UpstreamZones.Count)
            return;

        zone.UpstreamZones.RemoveAt(_selectedLineIndex);
        --_selectedLineIndex;
        UncreatedZoneEditor.Instance.isDirty = true;
    }

    private static void AddUpstream(int fromIndex, int toIndex, float weight = 1)
    {
        if (fromIndex < 0 || fromIndex >= LevelZones.ZoneList.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex), "Zone doesn't exist.");
        if (toIndex < 0 || toIndex >= LevelZones.ZoneList.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex), "Zone doesn't exist.");

        ZoneModel zone = LevelZones.ZoneList[fromIndex];
        string name = LevelZones.ZoneList[toIndex].Name;
        if (name.Equals(zone.Name, StringComparison.Ordinal) || zone.UpstreamZones.Exists(x => name.Equals(x.ZoneName, StringComparison.Ordinal)))
            return;

        if (weight <= 0f)
            weight = 1f;
        
        zone.UpstreamZones.Add(new UpstreamZone { ZoneName = name, Weight = weight });
        UncreatedZoneEditor.Instance.isDirty = true;
    }

    private bool TickOverZone(int zone)
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            _selectedZone = zone;
            _selectedLineIndex = -1;
            UpdateSelectedZone();
        }
        else if (Input.GetKeyUp(KeyCode.Mouse0))
        {
            if (zone != _selectedZone && _selectedZone >= 0 && _selectedZone < LevelZones.ZoneList.Count)
            {
                AddUpstream(_selectedZone, zone);
            }
            _selectedZone = -1;
            _selectedLineIndex = -1;
            UpdateSelectedZone();
        }

        return true;
    }

    private void ResetCamera()
    {
        UserMovement.SetEditorTransform(CartographyTool.CaptureBounds.center with
        {
            y = CartographyTool.LegacyMapping ? 1028f : CartographyTool.CaptureBounds.max.y
        }, CartographyTool.TransformMatrix.rotation);

        MainCamera.instance.orthographicSize = GetMaxOrthoSize();

        if (!_isPanning)
            return;

        _panStart = Input.mousePosition;
        _panStartLocation = MainCamera.instance.transform.position;
        _selectedZone = -1;
        _selectedLineIndex = -1;
        UpdateSelectedZone();
    }

    private static float GetMaxOrthoSize()
    {
        float screenRatio = (float)Screen.width / Screen.height;
        float targetRatio = CartographyTool.CaptureSize.x / CartographyTool.CaptureSize.y;
        return screenRatio >= targetRatio
            ? CartographyTool.CaptureSize.y / 2f
            : CartographyTool.CaptureSize.y / 2f * (targetRatio / screenRatio);
    }
}
#endif