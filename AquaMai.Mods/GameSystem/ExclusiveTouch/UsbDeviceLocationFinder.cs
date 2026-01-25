using System;
using System.Collections.Generic;
using System.Linq;
using LibUsbDotNet.Main;

namespace AquaMai.Mods.GameSystem.ExclusiveTouch;

/// <summary>
/// 基于 USB 物理位置路径的设备查找器
/// 支持格式: "2.2" (端口链)
/// </summary>
public class UsbDeviceLocationFinder : UsbDeviceFinder
{
    private readonly string locationPath;
    
    public UsbDeviceLocationFinder(int vid, int pid, string locationPath) 
        : base(vid, pid)
    {
        this.locationPath = locationPath?.Trim();
    }

    public override bool Check(UsbRegistry usbRegistry)
    {
        // 先检查 VID/PID
        if (!base.Check(usbRegistry)) return false;
        
        // 如果没有指定位置,就只匹配 VID/PID
        if (string.IsNullOrWhiteSpace(locationPath)) return true;
        
        // 获取 LocationPaths (SPDRP 0x23)
        var locationPaths = usbRegistry[SPDRP.LocationPaths] as string[];
        if (locationPaths != null && locationPaths.Length > 0)
        {
            // 检查是否有任何一个路径匹配
            foreach (var path in locationPaths)
            {
                if (MatchLocation(path, locationPath))
                {
                    return true;
                }
            }
        }
        
        // 尝试 LocationInformation (SPDRP 0x0D)
        var locationInfo = usbRegistry[SPDRP.LocationInformation] as string;
        if (!string.IsNullOrEmpty(locationInfo))
        {
            if (MatchLocation(locationInfo, locationPath))
            {
                return true;
            }
        }
        
        // 尝试 DeviceID
        var deviceId = usbRegistry["DeviceID"] as string;
        if (!string.IsNullOrEmpty(deviceId))
        {
            if (deviceId.IndexOf(locationPath, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private bool MatchLocation(string deviceLocation, string targetLocation)
    {
        if (string.IsNullOrEmpty(deviceLocation)) return false;
        
        // 完全匹配
        if (deviceLocation.Equals(targetLocation, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        // 部分匹配 (包含)
        if (deviceLocation.IndexOf(targetLocation, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }
        
        // 提取端口号进行匹配
        var devicePorts = ExtractPortNumbers(deviceLocation);
        var targetPorts = ExtractPortNumbers(targetLocation);
        
        if (devicePorts.Count > 0 && targetPorts.Count > 0)
        {
            return PortsMatch(devicePorts, targetPorts);
        }
        
        return false;
    }
    
    private List<int> ExtractPortNumbers(string path)
    {
        var numbers = new List<int>();
        
        // Windows 格式: PCIROOT(0)#PCI(0801)#PCI(0003)#USBROOT(0)#USB(2)#USB(2)#USBMI(1)
        // 只提取 #USB(n) 部分,忽略 USBROOT 和 USBMI
        var usbMatches = System.Text.RegularExpressions.Regex.Matches(
            path, 
            @"#USB\((\d+)\)"
        );
        
        foreach (System.Text.RegularExpressions.Match match in usbMatches)
        {
            if (match.Groups.Count > 1 && int.TryParse(match.Groups[1].Value, out int port))
            {
                numbers.Add(port);
            }
        }
        
        // 如果没有找到 Windows 格式,尝试简单的点分格式: 2.2 或 2.3.1
        if (numbers.Count == 0)
        {
            var parts = path.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                // 跳过 "bus" 和 "addr" 这样的前缀
                if (part.StartsWith("bus", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("addr", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                if (int.TryParse(part.Trim(), out int port))
                {
                    numbers.Add(port);
                }
            }
        }
        
        return numbers;
    }
    
    private bool PortsMatch(List<int> devicePorts, List<int> targetPorts)
    {
        if (targetPorts.Count == 0) return false;
        if (devicePorts.Count < targetPorts.Count) return false;

        // 检查 targetPorts 是否是 devicePorts 的子序列
        int j = 0;
        for (int i = 0; i < devicePorts.Count && j < targetPorts.Count; i++)
        {
            if (devicePorts[i] == targetPorts[j])
            {
                j++;
            }
        }

        return j == targetPorts.Count;
    }
}
