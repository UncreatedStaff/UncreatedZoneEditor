#if CLIENT
using Cysharp.Threading.Tasks;
using DevkitServer;
using DevkitServer.Multiplayer.Movement;
using SDG.Framework.Landscapes;
using Uncreated.ZoneEditor.UI;

namespace Uncreated.ZoneEditor.Tools;
internal static class TopViewHelper
{
    private static ERenderMode _oldRenderMode;
    private static bool _oldFog;
    private static float _oldFarClip;
    private static float _oldNearClip;
    private static Vector3 _oldPosition;
    private static Quaternion _oldRotation;
    private static readonly float[] LayerClipDistances = new float[32];
    public static bool IsActive { get; private set; }
    public static void Enter()
    {
        ThreadUtil.assertIsGameThread();

        if (IsActive)
            return;

        _oldRenderMode = GraphicsSettings.renderMode;
        _oldFarClip = MainCamera.instance.farClipPlane;
        _oldNearClip = MainCamera.instance.nearClipPlane;
        _oldFog = RenderSettings.fog;
        RenderSettings.fog = false;

        Transform cameraTransform = MainCamera.instance.transform;
        _oldPosition = cameraTransform.position;
        _oldRotation = cameraTransform.rotation;

        MainCamera.instance.farClipPlane = Landscape.TILE_HEIGHT;
        MainCamera.instance.nearClipPlane = 1f;
        if (_oldRenderMode != ERenderMode.FORWARD)
        {
            GraphicsSettings.renderMode = ERenderMode.FORWARD;
            GraphicsSettings.apply("Entering top view.");

            UniTask.Create(async () =>
            {
                // this is necessary to give it time to switch render modes
                await UniTask.WaitForEndOfFrame(DevkitServerModule.ComponentHost);
                if (IsActive)
                {
                    MainCamera.instance.orthographic = true;
                    EditorUIExtension? editorUIExtension = UIExtensionManager.GetInstance<EditorUIExtension>();
                    if (editorUIExtension != null)
                        editorUIExtension.UseOrthoOffset = true;
                }
            });
        }
        else
        {
            MainCamera.instance.orthographic = true;
            EditorUIExtension? editorUIExtension = UIExtensionManager.GetInstance<EditorUIExtension>();
            if (editorUIExtension != null)
                editorUIExtension.UseOrthoOffset = true;
        }

        IsActive = true;
        QualitySettings.lodBias = float.MaxValue;
        MainCamera.instance.layerCullDistances = LayerClipDistances;
        GraphicsSettings.graphicsSettingsApplied += OnGraphicsSettingsApplied;
    }

    public static void Exit()
    {
        ThreadUtil.assertIsGameThread();

        if (!IsActive)
            return;

        IsActive = false;
        UserMovement.SetEditorTransform(_oldPosition, _oldRotation);

        RenderSettings.fog = _oldFog;
        MainCamera.instance.farClipPlane = _oldFarClip;
        MainCamera.instance.nearClipPlane = _oldNearClip;
        MainCamera.instance.orthographicSize = 20f;
        MainCamera.instance.orthographic = false;
        EditorUIExtension? editorUIExtension = UIExtensionManager.GetInstance<EditorUIExtension>();
        if (editorUIExtension != null)
            editorUIExtension.UseOrthoOffset = false;

        if (_oldRenderMode == ERenderMode.DEFERRED)
        {
            GraphicsSettings.renderMode = ERenderMode.DEFERRED;
        }

        GraphicsSettings.graphicsSettingsApplied -= OnGraphicsSettingsApplied;

        // applying will also reset LOD bias and cull layers.
        GraphicsSettings.apply("Exiting top view.");
    }

    private static void OnGraphicsSettingsApplied()
    {
        if (!IsActive)
            return;

        _oldFarClip = MainCamera.instance.farClipPlane;

        MainCamera.instance.farClipPlane = Landscape.TILE_HEIGHT;

        QualitySettings.lodBias = float.MaxValue;
        MainCamera.instance.layerCullDistances = LayerClipDistances;
    }
}
#endif