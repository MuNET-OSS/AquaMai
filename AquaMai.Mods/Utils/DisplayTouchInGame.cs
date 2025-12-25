using System;
using System.Collections.Generic;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using AquaMai.Mods.UX.PracticeMode;
using HarmonyLib;
using Manager;
using MelonLoader;
using Monitor;
using Monitor.Game;
using Process;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AquaMai.Mods.Utils;

[ConfigSection(
    name: "实时触摸显示",
    zh: "在游戏过程中显示触摸输入，可用于调试吃和蹭的问题")]
[EnableGameVersion(23000)]
public static class DisplayTouchInGame
{
    private static GameObject prefab;
    private static readonly List<GameObject>[] canvasGameObjects = new List<GameObject>[] { new(), new() };
    private static TextMeshProUGUI tmp;
    private static TextMeshProUGUI[] tmps = new TextMeshProUGUI[2];
    // 0: 不显示，1: 上框透明底，2: 上框白底，3: 下框，4: 上框透明底+下框，5: 上框白底+下框
    public static int[] displayType = [0, 0];
    public const int displayTypeVer = 2;

    [ConfigEntry(
        name: "默认显示",
        zh: "关了的话，可以用按键切换显示")]
    public static bool defaultOn = true;

    public static void OnBeforePatch()
    {
        if (defaultOn)
        {
            displayType = [1, 1];
        }
        else
        {
            displayType = [0, 0];
        }
    }

    [ConfigEntry(name: "切换显示按键")]
    public static readonly KeyCodeOrName key = KeyCodeOrName.None;
    [ConfigEntry] private static readonly bool longPress = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommonMonitor), "HideDebugInfoText")]
    public static void CommonMonitorInitialize(TextMeshProUGUI ____romVersionText)
    {
        tmp = ____romVersionText;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectProcess), nameof(MusicSelectProcess.OnUpdate))]
    public static void OnMusicSelectProcessUpdate()
    {
        if (!KeyListener.GetKeyDownOrLongPress(key, longPress)) return;
        displayType = displayType[0] == 0 ? [1, 1] : [0, 0];
        MessageHelper.ShowMessage(displayType[0] != 0 ? Locale.NextPlayShowTouchDisplay : Locale.NextPlayHideTouchDisplay);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), nameof(GameProcess.OnUpdate))]
    public static void OnGameProcessUpdate()
    {
        for (int i = 0; i < 2; i++)
        {
            // 只有上框白底（2）才会创建 tmps；组合模式（5=2+3）同样需要更新计时
            if (displayType[i] is not (2 or 5)) continue;
            if (tmps[i] == null) continue;
            tmps[i].text = $"{TimeSpan.FromMilliseconds(PracticeMode.CurrentPlayMsec):mm\\:ss\\.fff}";
        }
        if (!KeyListener.GetKeyDownOrLongPress(key, longPress)) return;
        displayType = displayType[0] == 0 ? [1, 1] : [0, 0];
        for (int i = 0; i < 2; i++)
        {
            if (canvasGameObjects[i] == null) continue;
            foreach (var go in canvasGameObjects[i])
            {
                if (go == null) continue;
                go.SetActive(displayType[i] != 0);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.RegisterMouseTouchPanel))]
    public static void RegisterMouseTouchPanel(int target, MouseTouchPanel targetMouseTouchPanel)
    {
        if (target != 0)
            return;
        prefab = targetMouseTouchPanel.gameObject;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), nameof(GameProcess.OnStart))]
    public static void OnGameStart(GameMonitor[] ____monitors)
    {
        if (prefab == null)
        {
            MelonLogger.Error("[DisplayTouchInGame] prefab is null");
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            canvasGameObjects[i].Clear();
            var type = displayType[i];
            if (type is < 1 or > 5) continue;
            if (type is 4 or 5)
            {
                CreateDisplay(3, ____monitors[i]);
                type -= 3;
            }
            if (type > 3) continue;
            CreateDisplay(type, ____monitors[i]);
        }
    }

    // type 只能传入 1 2 3，如果是 4 或 5 的话，拆分成两个 call
    private static void CreateDisplay(int type, GameMonitor monitor)
    {
        if (type is < 1 or > 3) throw new ArgumentException("这不对吧");
        var sub = monitor.gameObject.transform.Find("Canvas/Sub");
        if (type == 3)
        {
            sub = Traverse.Create(monitor).Field<GameCtrl>("GameController").Value?.transform;
        }
        if (sub == null)
        {
            MelonLogger.Error($"[DisplayTouchInGame] sub is null");
            return;
        }
        var index = monitor.MonitorIndex;
        var canvas = new GameObject("[AquaMai] DisplayTouchInGame");
        canvas.transform.SetParent(sub, false);
        canvas.SetActive(type > 0);
        canvasGameObjects[index].Add(canvas);
        GameObject buttons = null;

        if (type == 3)
        {
            var rect = canvas.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1080, 1080);
            rect.localPosition = Vector3.zero;
            var canvasComp = canvas.AddComponent<Canvas>();
            canvasComp.renderMode = RenderMode.WorldSpace;
            canvasComp.sortingOrder = -32768;
            // canvasComp.sortingOrder = 1;
            // canvasComp.sortingLayerID = -385436797; // GameMovie
        }
        if (type == 2)
        {
            var rect = canvas.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1080, 450);
            rect.localPosition = Vector3.zero;
            var img = canvas.AddComponent<Image>();
            img.color = Color.white;

            var t = Object.Instantiate(tmp, canvas.transform, false);
            t.text = "";
            t.transform.localPosition = new Vector3(-500f, 0f, 0f);
            t.transform.localScale = Vector3.one * 2;
            tmps[index] = t;
        }

        if (type != 3)
        {
            // init button display
            buttons = new GameObject("Buttons");
            buttons.transform.SetParent(canvas.transform, false);
            buttons.transform.localPosition = Vector3.zero;
            buttons.transform.localScale = Vector3.one * 450 / 1080f;
        }

        var touchPanel = Object.Instantiate(prefab, canvas.transform, false);
        Object.Destroy(touchPanel.GetComponent<MouseTouchPanel>());
        foreach (Transform item in touchPanel.transform)
        {
            if (item.name.StartsWith("CircleGraphic"))
            {
                Object.Destroy(item.gameObject);
                continue;
            }
            Object.Destroy(item.GetComponent<MeshButton>());
            Object.Destroy(item.GetComponent<Collider>());
        }
        touchPanel.transform.localPosition = Vector3.zero;
        var touchDisplay = touchPanel.AddComponent<Display>();
        touchDisplay.player = index;
        touchDisplay.type = type;

        if (type != 3)
        {
            foreach (Transform item in touchPanel.transform)
            {
                var customGraphic = item.GetComponent<CustomGraphic>();
                customGraphic.color = Color.blue;
                if (item.name.StartsWith("A"))
                {
                    var btn = Object.Instantiate(item, buttons.transform, false);
                    btn.name = item.name;
                }
                var tmp = item.GetComponentInChildren<TextMeshProUGUI>();
                tmp.color = Color.black;
            }

            touchPanel.transform.localScale = Vector3.one * 0.95f * 450 / 1080f;
            var buttonDisplay = buttons.AddComponent<Display>();
            buttonDisplay.player = index;
            buttonDisplay.isButton = true;
            buttonDisplay.type = type;
        }

    }

    private class Display : MonoBehaviour
    {
        public int player;
        public bool isButton;
        public int type;

        private List<CustomGraphic> _buttonList;
        private Color _offTouchCol = new Color(0f, 0f, 1f);
        private Color _onTouchCol = new Color(1f, 0f, 0f);

        private void Start()
        {
            if (isButton)
            {
                _offTouchCol = Color.clear;
            }
            _buttonList = new List<CustomGraphic>();
            foreach (Transform item in transform)
            {
                CustomGraphic component = item.GetComponent<CustomGraphic>();
                _buttonList.Add(component);
            }
            if (type == 3)
            {
                _offTouchCol = Color.clear;
                _onTouchCol = new Color(1f, 0f, 0f, 0.3f);
            }
        }

        private void OnGUI()
        {
            foreach (CustomGraphic graphic in _buttonList)
            {
                if (!Enum.TryParse(graphic.name, out InputManager.TouchPanelArea button)) return;
                if (isButton)
                {
                    graphic.color = InputManager.GetButtonPush(player, (InputManager.ButtonSetting)button) ? _onTouchCol : _offTouchCol;
                }
                else
                {
                    graphic.color = InputManager.GetTouchPanelAreaPush(player, button) ? _onTouchCol : _offTouchCol;
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MouseTouchPanel), "Start")]
    public static void Workaround() { }
    /*
     * 和 https://github.com/MewoLab/AquaMai/blob/983f887be48d6c5c85a81460fa3c45d2d35c3852/AquaMai.Mods/GameSystem/TestProof.cs#L78-L87 的情况是一样的...
     * 在Maimoller的Mod Patch之后，MouseTouchPanel.Start就不会被调用了，但是放一个钩子在它身上，就一切正常了...
     * 我没有能力研究的特别透彻，就先和上面采取一样的做法了...留给后来者研究吧
     */
}