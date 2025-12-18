using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DB;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Manager.UserDatas;
using MelonLoader;
using Net;
using Net.Packet;
using Net.Packet.Mai2;
using Net.VO;

namespace AquaMai.Core.Helpers;

public static class Shim
{
    private static T Iife<T>(Func<T> func) => func();

    public static readonly string apiSuffix = Iife(() =>
    {
        try
        {
            var baseNetQueryConstructor = typeof(NetQuery<VOSerializer, VOSerializer>)
                .GetConstructors()
                .First();
            return ((INetQuery)baseNetQueryConstructor.Invoke(
            [
                .. baseNetQueryConstructor
                    .GetParameters()
                    .Select((parameter, i) => i == 0 ? "" : parameter.DefaultValue)
            ])).Api;
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Failed to resolve the API suffix: {e}");
            return null;
        }
    });

    public static bool NetHttpClientDecryptsResponse { get; private set; }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(NetHttpClient), "ReadCallback")]
    public static IEnumerable<CodeInstruction> NetHttpClientReadCallbackTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var instList = instructions.ToList();
        NetHttpClientDecryptsResponse = instList.Any(inst =>
            inst.opcode == OpCodes.Callvirt &&
            inst.operand is MethodInfo method &&
            method.Name == "Decrypt");
        return instList; // No changes
    }

    public static string RemoveApiSuffix(string api)
    {
        return !string.IsNullOrEmpty(apiSuffix) && api.EndsWith(apiSuffix)
            ? api.Substring(0, api.Length - apiSuffix.Length)
            : api;
    }

    public static byte[] DecryptNetPacketBody(byte[] encrypted)
    {
        var type = AccessTools.TypeByName("Net.CipherAES");
        if (type == null)
        {
            MelonLogger.Warning("No matching Net.CipherAES class found");
            return encrypted;
        }
        var methods = type.GetMethods();
        var method = methods.FirstOrDefault(it => it.Name == "Decrypt" && it.GetParameters().Length <= 2);
        if (method == null)
        {
            MelonLogger.Warning("No matching Net.CipherAES.Decrypt() method found");
            // Assume encryption code is removed.
            return encrypted;
        }
        if (method.GetParameters().Length == 1)
        {
            return (byte[])method.Invoke(null, [encrypted]);
        }
        else
        {
            object[] args = [encrypted, null];
            var result = (bool)method.Invoke(null, args);
            if (result)
            {
                return (byte[])args[1];
            }
            else
            {
                throw new Exception("Net.CipherAES.Decrypt() failed");
            }
        }
    }

    public static byte[] EncryptNetPacketBody(byte[] data)
    {
        var methods = AccessTools.TypeByName("Net.CipherAES").GetMethods();
        var method = methods.FirstOrDefault(it => it.Name == "Encrypt" && it.GetParameters().Length <= 2);
        if (method == null)
        {
            MelonLogger.Warning("No matching Net.CipherAES.Encrypt() method found");
            // Assume encryption code is removed.
            return data;
        }
        if (method.GetParameters().Length == 1)
        {
            return (byte[])method.Invoke(null, [data]);
        }
        else
        {
            object[] args = [data, null];
            var result = (bool)method.Invoke(null, args);
            if (result)
            {
                return (byte[])args[1];
            }
            else
            {
                throw new Exception("Net.CipherAES.Encrypt() failed");
            }
        }
    }

    public delegate string GetAccessTokenMethod(int index);

    public static readonly GetAccessTokenMethod GetAccessToken = Iife<GetAccessTokenMethod>(() =>
    {
        var tOperationManager = Traverse.Create(Singleton<OperationManager>.Instance);
        var tGetAccessToken = tOperationManager.Method("GetAccessToken", [typeof(int)]);
        if (!tGetAccessToken.MethodExists())
        {
            return (index) => throw new MissingMethodException("No matching OperationManager.GetAccessToken() method found");
        }
        return (index) => tGetAccessToken.GetValue<string>(index);
    });

    public delegate PacketUploadUserPlaylog PacketUploadUserPlaylogCreator(int index, UserData src, int trackNo, Action<int> onDone, Action<PacketStatus> onError = null);

    public static readonly PacketUploadUserPlaylogCreator CreatePacketUploadUserPlaylog = Iife<PacketUploadUserPlaylogCreator>(() =>
    {
        var type = typeof(PacketUploadUserPlaylog);
        if (type.GetConstructor([typeof(int), typeof(UserData), typeof(int), typeof(Action<int>), typeof(Action<PacketStatus>)]) is ConstructorInfo ctor1)
        {
            return (index, src, trackNo, onDone, onError) =>
            {
                var args = new object[] { index, src, trackNo, onDone, onError };
                return (PacketUploadUserPlaylog)ctor1.Invoke(args);
            };
        }
        else if (type.GetConstructor([typeof(int), typeof(UserData), typeof(int), typeof(string), typeof(Action<int>), typeof(Action<PacketStatus>)]) is ConstructorInfo ctor2)
        {
            return (index, src, trackNo, onDone, onError) =>
            {
                var accessToken = GetAccessToken(index);
                var args = new object[] { index, src, trackNo, accessToken, onDone, onError };
                return (PacketUploadUserPlaylog)ctor2.Invoke(args);
            };
        }
        else
        {
            throw new MissingMethodException("No matching PacketUploadUserPlaylog constructor found");
        }
    });

    public delegate PacketUpsertUserAll PacketUpsertUserAllCreator(int index, UserData src, Action<int> onDone, Action<PacketStatus> onError = null);

    public static readonly PacketUpsertUserAllCreator CreatePacketUpsertUserAll = Iife<PacketUpsertUserAllCreator>(() =>
    {
        var type = typeof(PacketUpsertUserAll);
        if (type.GetConstructor([typeof(int), typeof(UserData), typeof(Action<int>), typeof(Action<PacketStatus>)]) is ConstructorInfo ctor1)
        {
            return (index, src, onDone, onError) =>
            {
                var args = new object[] { index, src, onDone, onError };
                return (PacketUpsertUserAll)ctor1.Invoke(args);
            };
        }
        else if (type.GetConstructor([typeof(int), typeof(UserData), typeof(string), typeof(Action<int>), typeof(Action<PacketStatus>)]) is ConstructorInfo ctor2)
        {
            return (index, src, onDone, onError) =>
            {
                var accessToken = GetAccessToken(index);
                var args = new object[] { index, src, accessToken, onDone, onError };
                return (PacketUpsertUserAll)ctor2.Invoke(args);
            };
        }
        else
        {
            throw new MissingMethodException("No matching PacketUpsertUserAll constructor found");
        }
    });

    public static IEnumerable<UserScore>[] GetUserScoreList(UserData userData)
    {
        var tUserData = Traverse.Create(userData);

        var tScoreList = tUserData.Property("ScoreList");
        if (tScoreList.PropertyExists())
        {
            return tScoreList.GetValue<List<UserScore>[]>();
        }

        var tScoreDic = tUserData.Property("ScoreDic");
        if (tScoreDic.PropertyExists())
        {
            var scoreDic = tScoreDic.GetValue<Dictionary<int, UserScore>[]>();
            return scoreDic.Select(dic => dic.Values).ToArray();
        }

        throw new MissingFieldException("No matching UserData.ScoreList/ScoreDic found");
    }

    private static ConstructorInfo UserRateCtor = typeof(UserRate).GetConstructors().First(it => it.GetParameters().Length is 4 or 5);

    public static UserRate CreateUserRate(int musicId, int level, uint achievement, uint romVersion, PlayComboflagID comboflagID)
    {
        if (UserRateCtor.GetParameters().Length == 5)
        {
            return (UserRate)UserRateCtor.Invoke([musicId, level, achievement, romVersion, comboflagID]);
        }
        else
        {
            return (UserRate)UserRateCtor.Invoke([musicId, level, achievement, romVersion]);
        }
    }

    public static readonly Action<bool> Set_GameManager_IsNormalMode = GameInfo.GameVersion < 25500 ? (_) => { } : (value) => { GameManager.IsNormalMode = value; };
    private static readonly Func<bool> IsKaleidxScopeModeGetter = GameInfo.GameVersion < 25000 ? () => false : () => GameManager.IsKaleidxScopeMode;
    public static bool IsKaleidxScopeMode => IsKaleidxScopeModeGetter();
}