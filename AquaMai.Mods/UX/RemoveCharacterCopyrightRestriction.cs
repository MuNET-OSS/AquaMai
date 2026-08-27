using AquaMai.Config.Attributes;
using HarmonyLib;
using Manager.MaiStudio;

namespace AquaMai.Mods.UX;

[ConfigSection(
    name: "移除成绩照片的角色限制",
    en: "When the character team leader is a copyright character (usually an non-official character), the game prevents them from being shown as the character on the game result photo. Enabling this option removes that restriction so any character can appear on game result photo.",
    zh: "如果旅行伙伴队长是版权角色（通常是非官方的角色）的话，游戏默认是不允许将其作为成绩照片上的立绘角色的。开启此项后，将移除此限制，任何角色都可以出现在成绩照片中。")]
public class RemoveCharacterCopyrightRestriction
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CharaData), nameof(CharaData.isCopyright), MethodType.Getter)]
    public static bool get_isCopyright(ref bool __result)
    {
        __result = false;
        return false;
    }
}
