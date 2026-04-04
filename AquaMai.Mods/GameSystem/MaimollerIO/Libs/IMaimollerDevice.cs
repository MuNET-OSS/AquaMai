#nullable enable

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public enum SystemButton
{
    Coin = 0,
    Service = 1,
    Test = 2,
    Select = 3,
}

public interface IMaimollerDevice
{
    void Open();
    void Update();

    bool IsButtonPressed(int buttonIndex1To8);
    bool IsSystemButtonPressed(SystemButton button);
    ulong GetTouchState();

    void LedPreExecute();
    void SetButtonColor(int index, UnityEngine.Color32 color);
    void SetButtonColorFade(int index, UnityEngine.Color32 color, long duration);
    void SetBodyIntensity(int index, byte intensity);
    void SetBillboardColor(UnityEngine.Color32 color);
}
