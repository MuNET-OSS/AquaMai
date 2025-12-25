using System.Collections.Generic;
using AquaMai.Config.Attributes;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Monitor;
using Process;
using UnityEngine;

namespace AquaMai.Mods.Utils;

[ConfigSection(
    name: "稳定度指示器",
    zh: "在屏幕中心显示每次判定的精确区间",
    en: "Show information about the exact timing for each hit during gameplay in the center of the screen.")]
public class UnstableRate
{
    // The playfield goes from bottom left (-1080, -960) to top right (0, 120)
    // 使用了 local space
    private const float BaselineHeight = -70;
    private const float BaselineCenter = 0;
    private const float BaselineHScale = 25;
    private const float CenterMarkerHeight = 20;

    private const float JudgeHeight = 20;
    private const float JudgeFadeDelay = 1;
    private const float JudgeFadeTime = 1;
    private const float JudgeAlpha = 0.8f;

    private const float LineThickness = 4;

    private const float TimingBin = 16.666666f;

    // 0: 不显示，1: 显示，剩下来留给以后
    public static int[] displayType = [1, 1];

    private struct Timing
    {
        // Timings are in multiple of TimingBin (16.666666ms)
        public int windowStart;
        public int windowEnd;
        public Color color;
    }

    private static readonly Timing[] Timings =
    [
        new() { windowStart = 0, windowEnd = 1, color = new Color(1.000f, 0.843f, 0.000f) }, // Critical (#ffd700)
        new() { windowStart = 1, windowEnd = 3, color = new Color(1.000f, 0.647f, 0.000f) }, // Perfect (#ffa500)
        new() { windowStart = 3, windowEnd = 6, color = new Color(1.000f, 0.078f, 0.576f) },  // Great (#ff1493)
        new() { windowStart = 6, windowEnd = 9, color = new Color(0.000f, 0.502f, 0.000f) },  // Good (#008000)
    ];
    private static readonly Timing Miss = new() { windowStart = 999, windowEnd = 999, color = Color.grey };
    private static readonly Material LineMaterial = new(Shader.Find("Sprites/Default"));

    private static GameObject[] baseObjects = new GameObject[2];
    private static LinePool[] linePools = new LinePool[2];

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    public static void OnGameProcessStart(GameProcess __instance, GameMonitor[] ____monitors)
    {
        // Set up the baseline (the static part of the display)
        for (int i = 0; i < 2; i++)
        {
            if (displayType[i] == 0) continue;
            var userData = UserDataManager.Instance.GetUserData(i);
            if (!userData.IsEntry) continue;
            var main = ____monitors[i].gameObject.transform.Find("Canvas/Main");
            var go = new GameObject("[AquaMai] UnstableRate");
            go.transform.SetParent(main, false);
            baseObjects[i] = go;
            linePools[i] = new LinePool(go);
            SetupBaseline(go);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NoteBase), "Judge")]
    public static void OnJudge(NoteBase __instance, float ___JudgeTimingDiffMsec)
    {
        if (displayType[__instance.MonitorId] == 0) return;

        // How many milliseconds early or late the player hit
        var msec = ___JudgeTimingDiffMsec;

        // Account for the offset
        var optionJudgeTiming = Singleton<GamePlayManager>.Instance.GetGameScore(__instance.MonitorId).UserOption.GetJudgeTimingFrame();
        msec -= optionJudgeTiming * TimingBin;

        // Don't process misses
        var timing = GetTiming(msec);
        if (timing.windowStart == Miss.windowStart)
        {
            return;
        }

        var pool = linePools[__instance.MonitorId];
        if (pool == null)
        {
            return;
        }

        var line = pool.Get();

        line.SetPosition(0, new Vector3(BaselineCenter + BaselineHScale * (msec / TimingBin), BaselineHeight + JudgeHeight, 0));
        line.SetPosition(1, new Vector3(BaselineCenter + BaselineHScale * (msec / TimingBin), BaselineHeight - JudgeHeight, 0));

        line.startColor = timing.color;
        line.endColor = timing.color;

        // Setup fade-out
        var judgeTick = line.gameObject.GetComponent<JudgeTick>();
        if (judgeTick == null)
        {
            judgeTick = line.gameObject.AddComponent<JudgeTick>();
        }
        judgeTick.SetLine(line, pool);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HoldNote), "JudgeHoldHead")]
    public static void OnJudgeHold(HoldNote __instance, float ___JudgeTimingDiffMsec)
    {
        // The calculations are the same for the hold note heads
        OnJudge(__instance, ___JudgeTimingDiffMsec);
    }

    private static void SetupBaseline(GameObject go)
    {
        LineRenderer line;

        // Draw lines from the center outwards in both directions
        for (float sign = -1; sign <= 1; sign += 2)
        {
            // Draw each timing window in a different color
            foreach (var timing in Timings)
            {
                line = CreateLine(go, flatCaps: true);

                line.SetPosition(0, new Vector3(BaselineCenter + sign * BaselineHScale * timing.windowStart, BaselineHeight, 0));
                line.SetPosition(1, new Vector3(BaselineCenter + sign * BaselineHScale * timing.windowEnd, BaselineHeight, 0));

                line.startColor = timing.color;
                line.endColor = timing.color;
            }
        }

        // Center marker
        line = CreateLine(go);

        // Setting z-coordinate to -1 to make sure it stays in the foreground
        line.SetPosition(0, new Vector3(BaselineCenter, BaselineHeight + CenterMarkerHeight, -1));
        line.SetPosition(1, new Vector3(BaselineCenter, BaselineHeight - CenterMarkerHeight, -1));

        line.startColor = Color.white;
        line.endColor = Color.white;
    }

    private static LineRenderer CreateLine(GameObject go, bool flatCaps = false)
    {
        var obj = new GameObject();
        obj.transform.SetParent(go.transform, false);

        // We can't add the line directly as a component of the monitor, because it can only
        // have one LineRenderer component at a time.
        var line = obj.AddComponent<LineRenderer>();
        line.material = LineMaterial;
        line.useWorldSpace = false;
        line.startWidth = LineThickness;
        line.endWidth = LineThickness;
        line.positionCount = 2;
        line.numCapVertices = flatCaps ? 0 : 6;

        return line;
    }

    private static Timing GetTiming(float msec)
    {
        // Convert from milliseconds to multiples of TimingBin, the same unit used in
        // the lookup table.
        var hitTime = Mathf.Abs(msec) / TimingBin;

        // Search the timing interval that the hit lands in
        foreach (var timing in Timings)
        {
            // Using >= and < just like NoteJudge
            if (hitTime >= timing.windowStart && hitTime < timing.windowEnd)
            {
                return timing;
            }
        }

        return Miss;
    }

    private class LinePool
    {
        private readonly Queue<LineRenderer> _pool = new();
        private readonly GameObject _parent;
        private const int InitialPoolSize = 128;

        public LinePool(GameObject parent)
        {
            _parent = parent;

            // 预创建对象
            for (int i = 0; i < InitialPoolSize; i++)
            {
                var line = CreateLine(_parent);
                line.gameObject.SetActive(false);
                _pool.Enqueue(line);
            }
        }

        public LineRenderer Get()
        {
            LineRenderer line;
            if (_pool.Count > 0)
            {
                line = _pool.Dequeue();
                line.gameObject.SetActive(true);
            }
            else
            {
                line = CreateLine(_parent);
            }

            return line;
        }

        public void Return(LineRenderer line)
        {
            line.gameObject.SetActive(false);
            _pool.Enqueue(line);
        }
    }

    // 动画
    private class JudgeTick : MonoBehaviour
    {
        private float _elapsedTime;
        private LineRenderer _line;
        private LinePool _pool;
        private Color _initialColor;

        public void SetLine(LineRenderer line, LinePool pool)
        {
            _line = line;
            _pool = pool;
            _initialColor = line.startColor;
            _elapsedTime = 0;

            var color = _initialColor;
            color.a *= JudgeAlpha;

            _line.startColor = color;
            _line.endColor = color;
        }

        public void Update()
        {
            _elapsedTime += Time.deltaTime;

            if (_elapsedTime < JudgeFadeDelay)
                return;

            var fadeProgress = (_elapsedTime - JudgeFadeDelay) / JudgeFadeTime;

            if (fadeProgress >= 1.0f)
            {
                _pool.Return(_line);
                return;
            }

            Color color = _initialColor;
            color.a = JudgeAlpha * (1.0f - fadeProgress);

            _line.startColor = color;
            _line.endColor = color;
        }
    }
}
