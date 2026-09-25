using System;
using AquaMai.Mods.GameSystem.ExclusiveTouch;

namespace AquaMai.Mods.GameSystem.PdxTouch;

internal sealed partial class PdxTouchDevice
{
    private const int OldSlotStart = 1;
    private const int OldSlotSize = 10;
    private const int OldCountIndex = 61;
    private const int OldScanTimeIndex = 62;

    private void OnOldTouchData(byte[] data)
    {
        if (data[0] != ReportId) return;

        reportSequence++;
        var diagnosticsEnabled = ExclusiveTouchDiagnostics.Enabled;
        int count = data[OldCountIndex];
        ushort scanTime = BitConverter.ToUInt16(data, OldScanTimeIndex);
        int remainingBefore = remaining;
        if (count > 0)
        {
            if (remaining > 0 && diagnosticsEnabled)
            {
                ExclusiveTouchDiagnostics.Log(
                    "PDX-AT32 player={0} report={1} resync drop-pending={2}",
                    PlayerNo + 1, reportSequence, pendingUpdates.Count);
            }

            pendingUpdates.Clear();
            remaining = count;
        }
        else if (remaining <= 0)
        {
            if (diagnosticsEnabled)
            {
                ExclusiveTouchDiagnostics.Log(
                    "PDX-AT32 player={0} report={1} zero-count-no-pending",
                    PlayerNo + 1, reportSequence);
            }
            return;
        }

        int take = Math.Min(remaining, SlotsPerReport);
        var contacts = diagnosticsEnabled ? new System.Text.StringBuilder() : null;
        releaseUpdates.Clear();
        for (int i = 0; i < take; i++)
        {
            var index = OldSlotStart + i * OldSlotSize;
            var fingerId = data[index + 1];
            ushort x = BitConverter.ToUInt16(data, index + 2);
            ushort y = BitConverter.ToUInt16(data, index + 4);
            ushort w = BitConverter.ToUInt16(data, index + 6);
            ushort h = BitConverter.ToUInt16(data, index + 8);

            // 旧 PDX 首帧 status=04 也带面积，用面积判定比 Tip Switch 早一帧
            bool isPressed = w > 0 || h > 0;
            if (diagnosticsEnabled)
            {
                if (contacts.Length > 0) contacts.Append(' ');
                contacts.Append($"id={fingerId},p={isPressed},x={x},y={y},w={w},h={h}");
            }

            var update = new TouchUpdate(x, y, fingerId, isPressed);
            pendingUpdates.Add(update);
            if (!isPressed)
            {
                releaseUpdates.Add(update);
            }
        }

        remaining -= take;
        if (diagnosticsEnabled)
        {
            ExclusiveTouchDiagnostics.Log(
                "PDX-AT32 player={0} report={1} count={2} scan={3:X4} remaining={4}->{5} take={6} {7}",
                PlayerNo + 1, reportSequence, count, scanTime, remainingBefore, remaining, take, contacts);
        }

        if (releaseUpdates.Count > 0)
        {
            HandleReleases(releaseUpdates);
        }

        if (remaining == 0)
        {
            HandleFrame(pendingUpdates);
            pendingUpdates.Clear();
        }
    }
}
