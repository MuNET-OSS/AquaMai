using System;
using System.Collections.Generic;

namespace AquaMai.Core;

// Unity 2017 兼容
public static class Extensions
{
    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key, TValue defaultValue = default)
    {
        return dic.TryGetValue(key, out var value) ? value : defaultValue;
    }
    public static TValue GetExValueOrDefault<TKey1, TKey2, TValue>(this Dictionary<TKey1, Dictionary<TKey2, TValue>> dic, TKey1 key1, TKey2 key2, TValue defaultValue = default)
    {
        Dictionary<TKey2, TValue> dictionary;
        if (dic.TryGetValue(key1, out dictionary))
        {
            return dictionary.TryGetValue(key2, out var value) ? value : defaultValue;
        }
        return defaultValue;
    }

    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
    {
        key = kvp.Key;
        value = kvp.Value;
    }

    public static bool TryGetValue<T>(this T[] arr, int index, out T value)
    {
        if (index >= 0 && index < arr.Length)
        {
            value = arr[index];
            return true;
        }
        value = default;
        return false;
    }
}