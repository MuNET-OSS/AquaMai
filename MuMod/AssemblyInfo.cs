using System.Reflection;
using MelonLoader;

[assembly: AssemblyTitle(MuMod.Main.Description)]
[assembly: AssemblyDescription(MuMod.Main.Description)]
[assembly: AssemblyCompany(MuMod.Main.Author)]
[assembly: AssemblyProduct(nameof(MuMod))]
[assembly: AssemblyCopyright("Created by " + MuMod.Main.Author)]
[assembly: AssemblyTrademark(nameof(MuMod))]
[assembly: AssemblyVersion(MuMod.Main.LoaderVersion)]
[assembly: AssemblyFileVersion(MuMod.Main.LoaderVersion)]
[assembly: MelonInfo(typeof(MuMod.Main), MuMod.Main.Description, MuMod.Main.LoaderVersion, MuMod.Main.Author)]
[assembly: MelonColor(255, 212, 196, 246)]
[assembly: HarmonyDontPatchAll]
[assembly: MelonGame(null, null)]
