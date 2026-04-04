using System.Linq;
using System.Reflection;
using Tomlet;

namespace MuMod.Utils;

public static class TomletShim
{
    private static MethodInfo methodOld;
    private static MethodInfo methodNew;

    static TomletShim()
    {
        var method = typeof(TomletMain).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(it => it.Name == "To");
        methodOld = method.FirstOrDefault(it => it.GetParameters().Length == 1 && it.GetParameters()[0].ParameterType == typeof(string));
        methodNew = method.FirstOrDefault(it => it.GetParameters().Length == 2 && it.GetParameters()[0].ParameterType == typeof(string));
    }

    public static T To<T>(string toml)
    {
        if (methodOld != null)
        {
            return (T)methodOld.MakeGenericMethod(typeof(T)).Invoke(null, [toml]);
        }
        if (methodNew != null)
        {
            return (T)methodNew.MakeGenericMethod(typeof(T)).Invoke(null, [toml, null]);
        }
        throw new System.InvalidOperationException("Tomlet methods not found. Please ensure you are using the correct version of Tomlet.");
    }
}
