#if CLIENT
using DevkitServer.API.Cartography;
using DevkitServer.API.Cartography.Compositors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using UnityEngine.UI;

namespace Uncreated.ZoneEditor.Cartography;
public class LocationNameCartographyOverlay : ICartographyCompositor
{
    public bool SupportsChart => true;
    public bool SupportsSatellite => true;

    public bool Composite(in CartographyCaptureData data, Lazy<RenderTexture> texture, bool isExplicitlyDefined, JsonElement config)
    {
        List<LocationDevkitNode> nodes = LocationDevkitNodeSystem.Get().GetAllNodes().ToList();
        if (nodes.Count == 0)
            return false;

        RenderTexture rt = texture.Value;

        GameObject cameraObject = new GameObject("Cartography Camera");

        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.SetPositionAndRotation(CartographyTool.CaptureBounds.center with
        {
            y = CartographyTool.LegacyMapping ? 1028f : CartographyTool.CaptureBounds.max.y
        }, CartographyTool.TransformMatrix.rotation);
        cameraObject.transform.localScale = Vector3.one;

        camera.orthographic = true;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 10000f;
        camera.aspect = CartographyTool.CaptureSize.x / CartographyTool.CaptureSize.y;
        camera.orthographicSize = CartographyTool.CaptureSize.y * 0.5f;

        // hide other layers
        camera.cullingMask = RayMasks.UI;
        camera.clearFlags = CameraClearFlags.Depth;

        GameObject uiObject = new GameObject("Cartography Canvas")
        {
            layer = LayerMasks.UI,
            tag = "UI"
        };

        Canvas canvas = uiObject.AddComponent<Canvas>();
        CanvasScaler scaler = uiObject.AddComponent<CanvasScaler>();
        canvas.worldCamera = camera;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.planeDistance = 0.31f;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.Normal |
                                          AdditionalCanvasShaderChannels.Tangent |
                                          AdditionalCanvasShaderChannels.TexCoord1;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        if (config.ValueKind != JsonValueKind.Undefined && config.TryGetProperty("scale", out JsonElement scaleElement) && scaleElement.ValueKind == JsonValueKind.Number && scaleElement.TryGetSingle(out float scale) && float.IsFinite(scale))
        {
            scaler.scaleFactor = scale;
        }
        else
        {
            scaler.scaleFactor = 1.5f;
        }

        Local? localization = Level.info?.getLocalization();
        foreach (LocationDevkitNode node in nodes)
        {
            string? locName = node.locationName;
            if (string.IsNullOrWhiteSpace(locName))
                continue;

            string key = locName.Replace(' ', '_');
            if (localization != null && localization.has(key))
                locName = localization.format(key);

            Vector3 worldPos = node.transform.position;
            Vector2 mapLocation = CartographyTool.WorldCoordsToMapCoords(worldPos);

            // create location uGUI label

            ISleekLabel label = CreateLabel(canvas);

            label.PositionOffset_X = -200f;
            label.PositionOffset_Y = -30f;
            label.PositionScale_X = mapLocation.x / data.ImageSize.x;
            label.PositionScale_Y = 1f - mapLocation.y / data.ImageSize.y;
            label.SizeOffset_X = 400f;
            label.SizeOffset_Y = 60f;
            label.Text = locName;
            label.TextColor = ESleekTint.FONT;
            label.TextContrastContext = ETextContrastContext.ColorfulBackdrop;

            label.Update();
        }

        camera.targetTexture = rt;
        camera.Render();

        Object.Destroy(cameraObject);
        Object.Destroy(uiObject);

        return true;
    }

    private static readonly Type _labelType = Type.GetType("SDG.Unturned.GlazierLabel_uGUI, Assembly-CSharp", throwOnError: true)!;
    private static readonly MethodInfo _mtdConstructNew = _labelType.GetMethod("ConstructNew", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!;
    private static readonly MethodInfo _mtdSynchronizeColors = _labelType.GetMethod("SynchronizeColors", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!;
    private static readonly MethodInfo _propGameObject = _labelType.GetProperty("gameObject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!.GetMethod;
    private ISleekLabel CreateLabel(Canvas parentCanvas)
    {
        ISleekLabel label = (ISleekLabel)Activator.CreateInstance(_labelType, Glazier.Get());

        _mtdConstructNew.Invoke(label, Array.Empty<object>());
        _mtdSynchronizeColors.Invoke(label, Array.Empty<object>());

        GameObject textObj = (GameObject)_propGameObject.Invoke(label, Array.Empty<object>());

        RectTransform rectTransform = textObj.GetRectTransform();
        rectTransform.SetParent(parentCanvas.transform);
        rectTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        rectTransform.localScale = Vector3.one;

        return label;
    }
}
#endif