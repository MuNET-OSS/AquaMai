using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager;
using System;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "音频输出设置",
    zh: "音频独占与八声道设置",
    en: "Audio Exclusive and 8-Channel Settings")]
public static class Sound
{
    [ConfigEntry(
        name: "音频独占",
        en: "Enable Audio Exclusive")]
    private readonly static bool enableExclusive = false;

    [ConfigEntry(
        name: "位深度",
        zh: "如果不知道是什么，请不要修改",
        en: "If you don't know what it is, please don't modify it")]
    private readonly static ushort bitDepth = 24;

    [ConfigEntry(
        name: "八声道",
        en: "Enable 8-Channel")]
    private readonly static bool enable8Channel = false;

    [ConfigEntry(
        name: "乐曲音量",
        en: "Music Volume.")]
    private readonly static float musicVolume = 1.0f;

    private static CriAtomUserExtension.AudioClientShareMode AudioShareMode => enableExclusive ? CriAtomUserExtension.AudioClientShareMode.Exclusive : CriAtomUserExtension.AudioClientShareMode.Shared;

    private const ushort wBitsPerSample = 32;
    private const uint nSamplesPerSec = 48000u;
    private static ushort nChannels => enable8Channel ? (ushort)8 : (ushort)2;
    private static ushort nBlockAlign => (ushort)(wBitsPerSample / 8 * nChannels);
    private static uint nAvgBytesPerSec => nSamplesPerSec * nBlockAlign;

    private static CriAtomUserExtension.WaveFormatExtensible CreateFormat() =>
        new()
        {
            Format = new CriAtomUserExtension.WaveFormatEx
            {
                wFormatTag = 65534,
                nSamplesPerSec = nSamplesPerSec,
                wBitsPerSample = wBitsPerSample,
                cbSize = 22,
                nChannels = nChannels,
                nBlockAlign = nBlockAlign,
                nAvgBytesPerSec = nAvgBytesPerSec
            },
            Samples = new CriAtomUserExtension.Samples
            {
                wValidBitsPerSample = bitDepth,
            },
            dwChannelMask = enable8Channel ? 1599u : 3u,
            SubFormat = new Guid("00000001-0000-0010-8000-00aa00389b71")
        };

    [HarmonyPrefix]
    // Original typo
    [HarmonyPatch(typeof(WasapiExclusive), "Intialize")]
    public static bool InitializePrefix()
    {
        CriAtomUserExtension.SetAudioClientShareMode(AudioShareMode);
        CriAtomUserExtension.SetAudioBufferTime(160000uL);
        var format = CreateFormat();
        CriAtomUserExtension.SetAudioClientFormat(ref format);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SoundManager), "Play")]
    public static void PlayPrefix(SoundManager.AcbID acbID,
        SoundManager.PlayerID playerID,
        int cueID,
        bool prepare,
        int target,
        int startTime,
        ref float volume)
    {
        if (acbID == SoundManager.AcbID.Music && playerID == SoundManager.PlayerID.Music)
        {
            volume = musicVolume;
        }
    }
}
