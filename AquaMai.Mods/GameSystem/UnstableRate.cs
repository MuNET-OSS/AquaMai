using AquaMai.Config.Attributes;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Monitor;
using Process;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "UnstableRate",
    en: "Show information about the exact timing for each hit during gameplay in the center of the screen.")]
public class UnstableRate
{
    // The playfield goes from bottom left (-1080, -960) to top right (0, 120)
    private const float BaselineHeight = -480;
    private const float BaselineCenter = -540;
    private const float BaselineHScale = 25;
    private const float CenterMarkerHeight = 20;

    private const float JudgeHeight = 20;
    private const float JudgeFadeDelay = 1;
    private const float JudgeFadeTime = 1;
    private const float JudgeAlpha = 0.8f;

    private const float LineThickness = 4;

    private const float TimingBin = 16.666666f;

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
    private static readonly Timing Miss = new() {windowStart = 999, windowEnd = 999, color = Color.grey };

    private static GameMonitor _monitor;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameProcess), "OnStart")]
    public static void OnGameProcessStart(GameProcess __instance)
    {
        _monitor = Traverse.Create(__instance).Field("_monitors").GetValue<GameMonitor[]>()[0];

        // Set up the baseline (the static part of the display)
        SetupBaseline();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NoteBase), "Judge")]
    public static void OnJudge(NoteBase __instance)
    {
        // How many milliseconds early or late the player hit
        var msec = Traverse.Create(__instance).Field("JudgeTimingDiffMsec").GetValue<float>();

        // Account for the offset
        var optionJudgeTiming = Singleton<GamePlayManager>.Instance.GetGameScore(0).UserOption.GetJudgeTimingFrame();
        msec -= optionJudgeTiming * TimingBin;

        // Don't process misses
        var timing = GetTiming(msec);
        if (timing.windowStart == Miss.windowStart)
        {
            return;
        }

        // Create judgement tick
        var line = CreateLine();

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
    public static void OnJudgeHold(HoldNote __instance)
    {
        // The calculations are the same for the hold note heads
        OnJudge(__instance);
    }

    private static void SetupBaseline()
    {
        LineRenderer line;

        // Draw lines from the center outwards in both directions
        for (float sign = -1; sign <= 1; sign += 2)
        {
            // Draw each timing window in a different color
            foreach (var timing in Timings)
            {
                line = CreateLine();

                line.SetPosition(0, new Vector3(BaselineCenter + sign * BaselineHScale * timing.windowStart, BaselineHeight, 0));
                line.SetPosition(1, new Vector3(BaselineCenter + sign * BaselineHScale * timing.windowEnd, BaselineHeight, 0));

                line.startColor = timing.color;
                line.endColor = timing.color;
            }
        }

        // Center marker
        line = CreateLine();

        // Setting z-coordinate to -1 to make sure it stays in the foreground
        line.SetPosition(0, new Vector3(BaselineCenter, BaselineHeight + CenterMarkerHeight, -1));
        line.SetPosition(1, new Vector3(BaselineCenter, BaselineHeight - CenterMarkerHeight, -1));

        line.startColor = Color.white;
        line.endColor = Color.white;
    }

    private static LineRenderer CreateLine()
    {
        var obj = new GameObject();
        obj.transform.SetParent(_monitor.transform);

        // We can't add the line directly as a component of the monitor, because it can only
        // have one LineRenderer component at a time.
        var line = obj.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startWidth = LineThickness;
        line.endWidth = LineThickness;
        line.positionCount = 2;
        line.numCapVertices = 6; // Make the ends round

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
