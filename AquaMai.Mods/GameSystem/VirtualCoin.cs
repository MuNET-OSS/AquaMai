using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using AMDaemon;
using AquaMai.Config.Attributes;
using AquaMai.Config.Types;
using AquaMai.Core.Attributes;
using HarmonyLib;
using Mai2.Mai2Cue;
using Main;
using Manager;
using MelonLoader;
using Process;
using UnityEngine;
using Credit = AMDaemon.Credit;
using Input = UnityEngine.Input;
using Type = System.Type;


namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    "虚拟投币",
    zh: """
        在不依赖 SegaTools 的前提下实现的投币功能
        支持通过键盘按键或 HTTP 接口远程增加点数
        注意:该功能增加的点数游戏重启后不会保存
        """,
    en: """
        Implements a custom Credit system without relying on SegaTools
        Supports adding Credits via keyboard input or remotely through an HTTP service
        Note: Credits added by this feature are not persisted and will be reset after restarting the game
        """)]
public static class VirtualCoin
{
    [ConfigEntry(
        "启用键盘按键",
        """
        Adding Credits via keyboard input.
        """,
        """
        使用键盘按键增加点数
        """)]
    private static readonly bool IsUseKeyboard = true;

    [ConfigEntry(
        "绑定按键",
        """
        Bind keyboard.
        """,
        """
        绑定使用的键盘按键
        """)]
    public static readonly KeyCodeID CoinKey = (KeyCodeID)35;

    [ConfigEntry(
        "启用远程投币",
        """
        Remotely add Credits via an HTTP interface.
        """,
        """
        通过 HTTP 接口远程增加点数
        """)]
    private static readonly bool IsUseRemote = false;

    [ConfigEntry(
        "监听端口",
        """
        Listening port for the HTTP service.
        """,
        """
        HTTP服务监听的端口
        """)]
    private static readonly int Port = 7654;

    private static readonly int Prot = Port;
    [ConfigEntry(
        "密码验证",
        """
        Enable password verification for the HTTP service. leave it blank to skip.When requesting, the url contains '?password='
        """,
        """
        设置访问密码,留空不使用,请求的时候url带'?password='
        """)]
    private static readonly string Password = "";

    [ConfigEntry(
        "启用音效",
        """
        Play Sound Effects.
        """,
        """
        是否播放音效
        """)]
    private static readonly bool IsPlaySound = true;

    private static int _bufferCredit = 0;

    //缓存以提升性能
    private static IntPtr Pointer;

    private static MethodInfo call_Method;

    private static MethodInfo creditUnit_isGameCostEnough_Method;

    private static MethodInfo creditUnit_payGameCost_Method;

    public static void OnAfterPatch()
    {
        if (IsUseRemote)
        {
            CreditHttpServerHost.StartOnce();
            MelonModLogger.Msg("[VirtualCoin] HTTP Server started: " +
                               (CreditHttpServerHost.IsRunning ? $"Success on {Port}" : "Failed"));
        }
    }

    //该功能实现实际上很简单，就是整一个缓存点数，扣Credit的时候优先扣缓存中的Credit，然后不够再交给原逻辑

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CreditUnit), "Credit", MethodType.Getter)]
    public static void CreditPatch(ref uint __result)
    {
        __result += (uint)_bufferCredit;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CreditUnit), "IsZero", MethodType.Getter)]
    public static void IsZorePatch(ref bool __result)
    {
        __result = __result && _bufferCredit == 0;
    }

    public static int GetNeedCost(CreditUnit __instance, int gameCostIndex, int count)
    {
        var gameCosts = __instance.GameCosts;
        if (gameCosts == null)
        {
            throw new Exception("Failed to get GameCosts value");
        }

        var needCost = count * (int)gameCosts[gameCostIndex];

# if DEBUG
        MelonLogger.Msg($"NeedCost:{needCost} Credits.");
# endif

        return needCost;
    }

    public static IntPtr GetPointer(CreditUnit __instance)
    {
        var pointerProp = AccessTools.Property(typeof(CreditUnit), "Pointer");
        if (pointerProp == null)
        {
            throw new Exception("Failed to access Pointer property");
        }

        var pointerValue = pointerProp.GetValue(__instance);
        if (pointerValue == null)
        {
            throw new Exception("Failed to get Pointer value");
        }

        return (IntPtr)pointerValue;
    }

    public static MethodInfo GetAPIMethod(string apiName, params Type[] parameterTypes)
    {
        var apiType = AccessTools.TypeByName("AMDaemon.Api");
        if (apiType == null)
            throw new Exception("Failed to access AMDaemon.Api type");

        if (apiName == "Call")
        {
            var methods = apiType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "Call" && m.GetParameters().Length == 1)
                .ToArray();

            // 1.优先匹配非泛型: bool Call(Func<bool>)
            var nonGenericBool = methods.FirstOrDefault(m =>
                !m.IsGenericMethodDefinition &&
                m.ReturnType == typeof(bool) &&
                m.GetParameters()[0].ParameterType == typeof(Func<bool>));

            if (nonGenericBool != null)
                return nonGenericBool;

            // 2.兼容泛型: T Call<T>(Func<T>) 闭包成 bool
            var genericDef = methods.FirstOrDefault(m =>
                m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters()[0].ParameterType.IsGenericType &&
                m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Func<>));

            if (genericDef != null)
                return genericDef.MakeGenericMethod(typeof(bool));

            var overloads = string.Join(" | ", methods.Select(m => m.ToString()));
            throw new Exception("Failed to access Api.Call(Func<bool>) method. Overloads: " + overloads);
        }

        MethodInfo calledMethod;
        if (parameterTypes != null && parameterTypes.Length > 0)
            calledMethod = AccessTools.Method(apiType, apiName, parameterTypes);
        else
            calledMethod = AccessTools.Method(apiType, apiName);

        if (calledMethod == null)
            throw new Exception($"Failed to access Api.{apiName} method");

        return calledMethod;
    }




    [HarmonyPrefix]
    [HarmonyPatch(typeof(CreditUnit), "IsGameCostEnough", new Type[] { typeof(int), typeof(int) })]
    public static bool IsGameCostEnough(CreditUnit __instance, ref bool __result, int gameCostIndex, int count)
    {
        try
        {
            var needCost = GetNeedCost(__instance, gameCostIndex, count);
            if (_bufferCredit >= needCost)
            {
                __result = true;
                return false;
            }
            else if (_bufferCredit != 0 && _bufferCredit < needCost)
            {
                needCost -= (int)_bufferCredit;
                if (call_Method == null)
                {
                    call_Method = GetAPIMethod("Call");
                }

                if (creditUnit_isGameCostEnough_Method == null)
                {
                    creditUnit_isGameCostEnough_Method = GetAPIMethod("CreditUnit_isGameCostEnough");
                }

                Func<bool> lambda = () =>
                    (bool)creditUnit_isGameCostEnough_Method.Invoke(null, new object[] { GetPointer(__instance), 0, (int)needCost });
                __result = (bool)call_Method.Invoke(null, new object[] { lambda });
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            MelonLogger.Warning("[VirtualCoin]Patch IsGameCostEnough Failed:" + e.Message);
            return true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CreditUnit), "PayGameCost", new Type[] { typeof(int), typeof(int) })]
    public static bool PayGameCostPatch(CreditUnit __instance, ref bool __result, int gameCostIndex, int count)
    {
        try
        {
            var needCost = (uint)GetNeedCost(__instance, gameCostIndex, count);
            if (_bufferCredit - needCost >= 0)
            {
                _bufferCredit -= (int)needCost;

# if DEBUG
                MelonLogger.Msg($"#1:_buffer:{_bufferCredit}");
# endif

                return false;
            }

            if (_bufferCredit > 0 && _bufferCredit - needCost < 0)
            {
                needCost -= (uint)_bufferCredit;
                _bufferCredit = 0;

# if DEBUG
                MelonLogger.Msg($"#2:_buffer:{_bufferCredit};needCost:{needCost}");
# endif

                if (call_Method == null)
                {
                    call_Method = GetAPIMethod("Call");
                }
                if (creditUnit_payGameCost_Method == null)
                {
                    creditUnit_payGameCost_Method = GetAPIMethod("CreditUnit_payGameCost");
                }

                Func<bool> lambda = () =>
                    (bool)creditUnit_payGameCost_Method.Invoke(null, new object[] { GetPointer(__instance), 0, (int)needCost });
                __result = (bool)call_Method.Invoke(null, new object[] { lambda });
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            MelonLogger.Warning("[VirtualCoin]Patch PayGameCost Failed" + e.Message);
            return true;
        }
    }

    [EnableIf(nameof(IsUseKeyboard))]
    [HarmonyPatch(typeof(GameMainObject), "Update")]
    [HarmonyPostfix]
    public static void OnUpdatePatch()
    {
        if (Input.GetKeyDown(getKeyCode(CoinKey)))
        {
            _bufferCredit += 1;
            if (IsPlaySound)
            {
                SoundManager.PlaySystemSE(Cue.SE_SYS_CREDIT);
            }
        }
    }

    private static KeyCode getKeyCode(KeyCodeID keyCodeID)
    {
        try
        {
            return (KeyCode)Enum.Parse(typeof(KeyCode), keyCodeID.ToString());
        }
        catch (Exception)
        {
            return KeyCode.Equals;
        }
    }

    private static class CreditHttpServerHost
    {
        private static readonly object StartSync = new object();

        private static HttpListener _listener;

        private static Thread _listenThread;

        private static volatile bool _running;

        public static bool IsRunning => _running;

        public static void StartOnce()
        {
            if (_running)
            {
                return;
            }

            lock (StartSync)
            {
                if (_running)
                {
                    return;
                }

                try
                {
                    var port = GetPort();
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    _listener.Start();

                    _running = true;
                    _listenThread = new Thread(ListenLoop)
                    {
                        IsBackground = true,
                        Name = "AMDaemon.CreditHttpServer"
                    };
                    _listenThread.Start();
                }
                catch
                {
                    _running = false;
                    try
                    {
                        _listener?.Close();
                    }
                    catch
                    {
                    }

                    _listener = null;
                }
            }
        }

        private static int GetPort()
        {
            if (Port > 0 && Port <= 65535)
            {
                return Port;
            }

            return 6543;
        }

        private static void ListenLoop()
        {
            while (_running && _listener != null)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch
                {
                    break;
                }

                try
                {
                    HandleRequest(context);
                }
                catch
                {
                    WriteJson(context.Response, 500, "{\"ok\":false,\"error\":\"internal_error\"}");
                }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";

            if (string.Equals(path, "/", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, 200, "{\"ok\":true,\"service\":[\"credit\",\"add\"]}");
                return;
            }

            if (string.Equals(path, "/credit", StringComparison.OrdinalIgnoreCase))
            {
                if (!CheckPassword(context.Request))
                {
                    WriteJson(context.Response, 401, "{\"ok\":false,\"error\":\"unauthorized\"}");
                    return;
                }

                uint credit = 0;
                uint amDcredit = 0;
                uint remain = 0;
                uint bufferCredit = 0;
                var freePlay = false;

                var player = Credit.Players[0];
                bufferCredit = (uint)_bufferCredit;
                credit = player.Credit;
                amDcredit = credit - (uint)bufferCredit;
                remain = player.Remain;
                freePlay = player.IsFreePlay;
                WriteJson(context.Response, 200,
                    "{\"ok\":true,\"bufferCredit\":" + bufferCredit + ",\"Credit\":" + credit + ",\"AMDCredit\":" +
                    amDcredit + ",\"remain\":" + remain + ",\"isFreePlay\":" + (freePlay ? "true" : "false") + "}");
                return;
            }

            if (string.Equals(path, "/add", StringComparison.OrdinalIgnoreCase))
            {
                if (!CheckPassword(context.Request))
                {
                    WriteJson(context.Response, 401, "{\"ok\":false,\"error\":\"unauthorized\"}");
                    return;
                }

                _bufferCredit += 1;
                if (IsPlaySound)
                {
                    SoundManager.PlaySystemSE(Cue.SE_SYS_CREDIT);
                }

                WriteJson(context.Response, 200, "{\"ok\":true,\"bufferCredit\":" + _bufferCredit + "}");
                return;
            }

            WriteJson(context.Response, 404, "{\"ok\":false,\"error\":\"not_found\"}");
        }

        private static void WriteJson(HttpListenerResponse response, int statusCode, string payload)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(payload);
            response.ContentLength64 = bytes.LongLength;
            using var stream = response.OutputStream;
            stream.Write(bytes, 0, bytes.Length);
        }

        private static bool CheckPassword(HttpListenerRequest request)
        {
            if (string.IsNullOrEmpty(Password))
            {
                return true; // skip
            }

            var input = request.QueryString["password"];
            return string.Equals(input, Password, StringComparison.Ordinal);
        }
    }
}
