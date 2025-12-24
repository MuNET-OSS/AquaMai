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
    zh: "在屏幕中心显示每个击打的确切时间信息",
    en: "Show information about the exact timing for each hit during gameplay in the center of the screen.")]
public class UnstableRate
{
    // The playfield goes from bottom left (-1080, -960) to top right (0, 120)
    // 由于使用了 local space，所以高度和中心都是 0
    private const float BaselineHeight = 0;
    private const float BaselineCenter = 0;
    private const float BaselineHScale = 25;
    private const float CenterMarkerHeight = 20;

    private const float JudgeHeight = 20;
    private const float JudgeFadeDelay = 1;
    private const float JudgeFadeTime = 1;
    private const float JudgeAlpha = 0.8f;

    private const float LineThickness = 4;

    private const float TimingBin = 16.666666f;

    [ConfigEntry("默认显示")]
    public static bool defaultOn = false;

    // 0: 不显示，1: 显示，剩下来留给以后
    public static int[] displayType = [1, 1];

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

    private struct Timing
    {
        // Timings are in multiple of TimingBin (16.666666ms)
        public int windowStart;
        public int windowEnd;
        public Color color;
    }

    private static readonly Timing[] Timings =
    [
        new() { windowStart = 0, windowEnd = 1, color = new Color(0.133f, 0.712f, 0.851f) }, // Critical
        new() { windowStart = 1, windowEnd = 3, color = new Color(0.122f, 0.484f, 0.861f) }, // Perfect
        new() { windowStart = 3, windowEnd = 6, color = new Color(0.102f, 0.731f, 0.078f) },  // Great
        new() { windowStart = 6, windowEnd = 9, color = new Color(0.925f, 0.730f, 0.110f) },  // Good
    ];
    private static readonly Timing Miss = new() { windowStart = 999, windowEnd = 999, color = Color.grey };
    private static readonly Material LineMaterial = new(Shader.Find("Sprites/Default"));

    private static GameObject[] baseObjects = new GameObject[2];

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    public static void OnGameProcessStart(GameProcess __instance, GameMonitor[] ____monitors)
    {
        // Set up the baseline (the static part of the display)
        for (int i = 0; i < 2; i++)
        {
            if (displayType[i] == 0) continue;
            var main = ____monitors[i].gameObject.transform.Find("Canvas/Main");
            var go = new GameObject("[AquaMai] UnstableRate");
            go.transform.SetParent(main, false);
            baseObjects[i] = go;
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

        var go = baseObjects[__instance.MonitorId];
        if (go == null)
        {
            return;
        }
        // Create judgement tick
        var line = CreateLine(go);

        line.SetPosition(0, new Vector3(BaselineCenter + BaselineHScale * (msec / TimingBin), BaselineHeight + JudgeHeight, 0));
        line.SetPosition(1, new Vector3(BaselineCenter + BaselineHScale * (msec / TimingBin), BaselineHeight - JudgeHeight, 0));

        line.startColor = timing.color;
        line.endColor = timing.color;

        // Setup fade-out
        var judgeTick = line.gameObject.AddComponent<JudgeTick>();
        judgeTick.SetLine(line);

        // Destroy it once the fade-out is over
        Object.Destroy(line.gameObject, JudgeFadeDelay + JudgeFadeTime);
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

    // Handles the fade-out of judgment ticks
    private class JudgeTick : MonoBehaviour
    {
        private float _elapsedTime;
        private LineRenderer _line;
        private Color _initialColor;

        public void SetLine(LineRenderer line)
        {
            _line = line;
            _initialColor = line.startColor;

            // We needed to store the full color above, so we didn't apply the alpha before
            var color = _initialColor;
            color.a *= JudgeAlpha;

            _line.startColor = color;
            _line.endColor = color;
        }

        public void Update()
        {
            _elapsedTime += Time.deltaTime;

            // Only start the fade-out after a short delay
            if (_elapsedTime < JudgeFadeDelay)
                return;

            Color color = _initialColor;

            color.a = JudgeAlpha * (1.0f - Mathf.Clamp01((_elapsedTime - JudgeFadeDelay) / JudgeFadeTime));

            _line.startColor = color;
            _line.endColor = color;
        }
    }
}
