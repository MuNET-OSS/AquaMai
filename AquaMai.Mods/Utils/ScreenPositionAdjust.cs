using System.Collections.Generic;
using System.Reflection;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using AquaMai.Mods.GameSystem;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace AquaMai.Mods.Utils;

[ConfigSection(
    name: "屏幕位置调整",
    zh: """
        屏幕位置调整。适用于手台对不齐的情况，可以分别调整每个屏幕区域的位置
        在游戏中按键开启调整模式
        此功能会对性能产生一定影响
        必须同时开启 ExteraMouseInput 才能使用鼠标模拟触摸
        """,
    en: """
        Screen position adjustment. Suitable for cases where the screen are not aligned, allowing separate adjustments for each screen area.
        Enter adjustment mode by pressing the key in-game.
        This feature may have a slight performance impact.
        ExteraMouseInput must be enabled to use mouse simulation for touch input.
        """
)]
public class ScreenPositionAdjust
{
    [ConfigEntry(name: "上屏紧贴着下屏", en: "Top screen is tightly attached to the bottom screen")]
    public static readonly bool compactMode = false;
    [ConfigEntry(name: "进入调整模式的按键", en: "Key to enter adjustment mode")]
    public static readonly KeyCodeOrName adjustKey = KeyCodeOrName.F10;

    private static GameObject root;
    // main1P sub1P main2P sub2P
    private static Transform[] images = new Transform[4];
    private static Camera camera;
    private static RenderTexture renderTexture;
    private static Transform[] mouseTouchPanels = new Transform[2];

    private static readonly Vector3 mouseTouchPanelsDelta = new Vector3(0, (1920 - 1080) / 2f, 0);

    /// <summary>
    /// 避免一帧延迟
    /// </summary>
    private class CameraUpdater : MonoBehaviour
    {
        void OnPreRender()
        {
            if (camera == null)
            {
                return;
            }
            camera.Render();
        }
    }

    private static float GetSizeFactor()
    {
        return Screen.height / (compactMode ? 1080f + 450f : 1920f);
    }


    /// <summary>
    /// 调整大小时重新调整 texture 大小，让显示质量最好
    /// </summary>
    private class DynamicRenderTextureResizer : MonoBehaviour
    {
        private int lastHeight;

        void Start()
        {
            lastHeight = Screen.height;
        }

        void Update()
        {
            if (Screen.height == lastHeight) return;
#if DEBUG
            MelonLogger.Msg("[ScreenPositionAdjust] Screen height changed, resizing render textures.");
#endif
            lastHeight = Screen.height;
            if (renderTexture != null)
            {
                renderTexture.Release();
                renderTexture.width = (int)(1080 * 2 * GetSizeFactor());
                renderTexture.height = (int)(compactMode ? Screen.height / (1080f + 450f) * 1920f : Screen.height);
                renderTexture.Create();
            }
        }
    }

    private static float[] offsetX = new float[4];
    private static float[] offsetY = new float[4];

    private class AdjustController : MonoBehaviour
    {
        private int index = -1;
        private float speed = 1f;

        private void OnGUI()
        {
            if (index == -1) return;
            var rect = new Rect(0, 0, GuiSizes.FontSize * 50, GuiSizes.FontSize * 15);

            var player = index < 2 ? "1P" : "2P";
            var sub = index % 2 == 0 ? "Main" : "Sub";

            var labelStyle = GUI.skin.GetStyle("label");
            labelStyle.fontSize = GuiSizes.FontSize * 2;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Box(rect, "");
            GUI.Label(rect, string.Format(Locale.ScreenPositionAdjustTip, $"{player} {sub}", speed, adjustKey));
        }

        private void Update()
        {
            if (KeyListener.GetKeyDown(adjustKey))
            {
                index++;
                if (index > 3)
                {
                    index = -1;
                }
                if (index == -1)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        PlayerPrefs.SetFloat($"AquaMaiScreenPositionAdjust-x:{i}", offsetX[i]);
                        PlayerPrefs.SetFloat($"AquaMaiScreenPositionAdjust-y:{i}", offsetY[i]);
                    }
                    PlayerPrefs.Save();
                    mouseTouchPanels[0].position = images[0].position + mouseTouchPanelsDelta;
                    mouseTouchPanels[1].position = images[2].position + mouseTouchPanelsDelta;
                    return;
                }
            }
            if (index == -1) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                offsetX[index] -= speed;
                images[index].localPosition -= new Vector3(speed, 0, 0);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                offsetX[index] += speed;
                images[index].localPosition += new Vector3(speed, 0, 0);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                offsetY[index] += speed;
                images[index].localPosition += new Vector3(0, speed, 0);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                offsetY[index] -= speed;
                images[index].localPosition -= new Vector3(0, speed, 0);
            }
            else if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                speed /= 2f;
            }
            else if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                speed *= 2f;
            }
        }
    }

    [HarmonyPatch]
    public class Init
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return SinglePlayer.WhateverInitialize.TargetMethods();
        }

        public static void Prefix(Transform left, Transform right)
        {
            root = new GameObject("[AquaMai] ScreenPositionAdjust Display", [typeof(Canvas)]);
            root.transform.position = new Vector3(11451, 19198, 0);
            var canvas = root.GetComponent<Canvas>();
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(2160, 1920);
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<DynamicRenderTextureResizer>();
            root.AddComponent<AdjustController>();
            Camera.main.gameObject.AddComponent<CameraUpdater>();

            var compactDelta = 0;
            if (compactMode)
            {
                Camera.main.orthographicSize = 540 + 225;
                // 960 - 540 - 225 = 195
                compactDelta = 195;
            }

            renderTexture = new RenderTexture((int)(1080 * 2 * GetSizeFactor()), (int)(compactMode ? Screen.height / (1080f + 450f) * 1920f : Screen.height), 24, RenderTextureFormat.RGB111110Float)
            {
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
            };

            camera = new GameObject($"[AquaMai] ScreenPositionAdjust Camera").AddComponent<Camera>();
            camera.transform.parent = root.transform;
            camera.enabled = false;
            camera.targetTexture = renderTexture;
            camera.cullingMask = Camera.main.cullingMask;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 960;
            camera.transform.position = Camera.main.transform.position;
            camera.farClipPlane = Camera.main.farClipPlane;
            Camera.main.transform.position = new Vector3(ConfigLoader.Config.GetSectionState(typeof(SinglePlayer)).Enabled ? 11451 - 540 : 11451, 19198, -800);

            for (int i = 0; i < 4; i++)
            {
                var player = i < 2 ? "1P" : "2P";
                var sub = i % 2 == 0 ? "Main" : "Sub";

                var image = new GameObject($"[AquaMai] ScreenPositionAdjust Image {sub} {player}").AddComponent<UnityEngine.UI.RawImage>();
                image.transform.parent = root.transform;
                image.texture = renderTexture;
                image.rectTransform.sizeDelta = new Vector2(1080, sub == "Main" ? 1080 : 450);
                image.transform.localPosition = new Vector3(player == "1P" ? -540 : 540, sub == "Main" ? -420 + compactDelta : 735 - compactDelta, 0);
                image.uvRect = new Rect(
                    player == "1P" ? 0 : 0.5f,
                    sub == "Main" ? 0 : (1920 - 450) / 1920f,
                    0.5f,
                    sub == "Main" ? 1080f / 1920f : 450f / 1920f
                );
                images[i] = image.transform;

                offsetX[i] = PlayerPrefs.GetFloat($"AquaMaiScreenPositionAdjust-x:{i}", 0);
                offsetY[i] = PlayerPrefs.GetFloat($"AquaMaiScreenPositionAdjust-y:{i}", 0);
                images[i].localPosition += new Vector3(offsetX[i], offsetY[i], 0);
            }

            mouseTouchPanels[0] = left.Find("MouseTouchPanel");
            mouseTouchPanels[1] = right.Find("MouseTouchPanel");
            mouseTouchPanels[0].position = images[0].position + mouseTouchPanelsDelta;
            mouseTouchPanels[1].position = images[2].position + mouseTouchPanelsDelta;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MouseTouchPanel), "Start")]
    public static void FixTouchPanelPlayer(ref int ___PlayerID, MouseTouchPanel __instance)
    {
        ___PlayerID = __instance.transform.parent.parent.name.Equals("LeftMonitor") ? 0 : 1;
    }
}