using System;
using AquaMai.Mods.GameSystem.ExclusiveTouch;

namespace AquaMai.Mods.GameSystem.PdxTouch;

internal sealed partial class PdxTouchDevice
{
    private const int NewSlotCount = 10;
    private const int NewSlotSize = 6;

    private void OnNewTouchData(byte[] data)
    {
        byte reportId = data[0];
        if (reportId != ReportId) return;

        reportSequence++;
        var diagnosticsEnabled = ExclusiveTouchDiagnostics.Enabled;
        var contacts = diagnosticsEnabled ? new System.Text.StringBuilder() : null;
        var validSlots = 0;

        for (int i = 0; i < NewSlotCount; i++)
        {
            var index = i * NewSlotSize + 1;
            if (data[index] == 0) continue;
            validSlots++;
            bool isPressed = (data[index] & 0x01) == 1;
            var fingerId = data[index + 1];
            ushort x = BitConverter.ToUInt16(data, index + 2);
            ushort y = BitConverter.ToUInt16(data, index + 4);
            if (diagnosticsEnabled)
            {
                if (contacts.Length > 0) contacts.Append(' ');
                contacts.Append($"id={fingerId}:st=0x{data[index]:X2},p={isPressed},x={x},y={y}");
            }
            HandleFinger(x, y, fingerId, isPressed);
        }

        if (diagnosticsEnabled)
        {
            ExclusiveTouchDiagnostics.Log(
                "PDX player={0} report={1} slots={2} {3}",
                PlayerNo + 1, reportSequence, validSlots, contacts);
        }
    }
}
