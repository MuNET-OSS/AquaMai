using HarmonyLib;
using MAI2.Util;
using Manager;
using Process;
using UnityEngine;

namespace AquaMai.Mods.Utils.EarlyContinue;

public class Process : ContinueProcess
{
    private readonly Traverse<ContinueSequence> state;
    public Process(ProcessDataContainer dataContainer) : base(dataContainer)
    {
        state = Traverse.Create(this).Field<ContinueSequence>("_state");
    }

    private float _timeCounter;
    public override void OnUpdate()
    {
        if (state.Value != ContinueSequence.DispEnd)
        {
            base.OnUpdate();
            return;
        }

        _timeCounter += Time.deltaTime;
        if (_timeCounter < 3f)
        {
            return;
        }
        _timeCounter = 0f;
        if (GameManager.IsSelectContinue[0] || GameManager.IsSelectContinue[1])
        {
            var addCount = EarlyContinue.addTrackCount;
            if (addCount == 0)
            {
                addCount = (uint)(Singleton<UserDataManager>.Instance.IsSingleUser() ? 3 : 4);
            }
            EarlyContinue.currentAddTrackCount += addCount;
            GameManager.SetMaxTrack();
            container.processManager.AddProcess(new NextTrackProcess(container, this), 50);
        }
        else
        {
            bool notShowFade = true;
            for (int j = 0; j < 2; j++)
            {
                UserData userData = Singleton<UserDataManager>.Instance.GetUserData(j);
                if (userData.MapList.Count != 0 && userData.IsEntry && !userData.IsGuest())
                {
                    notShowFade = false;
                    break;
                }
            }

            if (notShowFade)
            {
                container.processManager.AddProcess(new MapResultProcess(container), 50);
                container.processManager.ReleaseProcess(this);
            }
            else
            {
                container.processManager.AddProcess(new FadeProcess(container, this, new MapResultProcess(container)), 50);
            }
        }
        container.processManager.SetVisibleTimers(isVisible: false);
    }
}