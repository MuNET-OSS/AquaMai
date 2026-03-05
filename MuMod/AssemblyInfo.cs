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

// Create and Setup a MelonGame Attribute to mark a Melon as Universal or Compatible with specific Games.
// If no MelonGame Attribute is found or any of the Values for any MelonGame Attribute on the Melon is null or empty it will be assumed the Melon is Universal.
// Values for MelonGame Attribute can be found in the Game's app.info file or printed at the top of every log directly beneath the Unity version.
[assembly: MelonGame(null, null)]
