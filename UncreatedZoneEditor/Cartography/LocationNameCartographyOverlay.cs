#if CLIENT
using DevkitServer.API.Cartography;
using DevkitServer.API.Cartography.Compositors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.UI;

namespace Uncreated.ZoneEditor.Cartography;
public class LocationNameCartographyOverlay : ICartographyCompositor
{
    public bool SupportsChart => true;
    public bool SupportsSatellite => true;

    public bool Composite(in CartographyCaptureData data, Lazy<RenderTexture> texture, bool isExplicitlyDefined)
    {
        List<LocationDevkitNode> nodes = LocationDevkitNodeSystem.Get().GetAllNodes().ToList();
        if (nodes.Count == 0)
            return false;

        RenderTexture rt = texture.Value;

        GameObject uiObject = new GameObject("Cartography Canvas");

        GameObject cameraObject = new GameObject("Cartography Camera");

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.aspect = (float)data.ImageSize.x / data.ImageSize.y;
        camera.orthographicSize = data.ImageSize.y / 2f;
        //camera.transform.SetPositionAndRotation(CartographyTool.CaptureBounds.center with
        //{
        //    y = CartographyTool.LegacyMapping ? 1028f : CartographyTool.CaptureBounds.max.y
        //}, CartographyTool.TransformMatrix.rotation);

        // hide other layers
        camera.cullingMask = RayMasks.UI;
        camera.clearFlags = CameraClearFlags.Depth;

        Canvas canvas = uiObject.AddComponent<Canvas>();
        CanvasScaler scaler = uiObject.AddComponent<CanvasScaler>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1.0f;

        Local? localization = Level.info?.getLocalization();
        foreach (LocationDevkitNode node in nodes)
        {
            string? locName = node.locationName;
            if (string.IsNullOrWhiteSpace(locName))
                continue;

            string key = locName.Replace(' ', '_');
            if (localization != null && localization.has(key))
                locName = localization.format(key);

            Vector2 mapLocation = CartographyTool.WorldCoordsToMapCoords(node.transform.position);

            // create location uGUI label
            Type labelType = Type.GetType("SDG.Unturned.GlazierLabel_uGUI, Assembly-CSharp", throwOnError: true)!;

            ISleekLabel label = (ISleekLabel)Activator.CreateInstance(labelType, [ Glazier.Get() ]);
            labelType.GetMethod("ConstructNew", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!.Invoke(label, Array.Empty<object>());
            labelType.GetMethod("SynchronizeColors", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!.Invoke(label, Array.Empty<object>());

            GameObject textObj = (GameObject)labelType.GetProperty("gameObject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!.GetValue(label);
            textObj.SetLayerRecursively(LayerMasks.UI);

            RectTransform rectTransform = textObj.GetRectTransform();
            rectTransform.SetParent(canvas.transform);

            label.PositionOffset_X = -200f;
            label.PositionOffset_Y = -30f;
            label.PositionScale_X = mapLocation.x / data.ImageSize.x;
            label.PositionScale_Y = mapLocation.y / data.ImageSize.y;
            label.SizeOffset_X = 400f;
            label.SizeOffset_Y = 60f;
            label.Text = locName;
            label.TextColor = ESleekTint.FONT;
            label.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        }

        camera.targetTexture = rt;
        camera.Render();

        Object.Destroy(cameraObject);
        Object.Destroy(uiObject);

        return true;
    }
}
#endif