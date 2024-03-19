using System;
using System.Linq;

namespace FasterGates;

public static partial class Hooks
{
    public static void ApplyInit() => On.RainWorld.OnModsInit += RainWorld_OnModsInit;

    public static bool IsInit { get; private set; } = false;

    private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        try
        {
            ModOptions.RegisterOI();

            if (IsInit) return;
            IsInit = true;

            var mod = ModManager.ActiveMods.FirstOrDefault(mod => mod.id == Plugin.MOD_ID);

            Plugin.MOD_NAME = mod.name;
            Plugin.VERSION = mod.version;
            Plugin.AUTHORS = mod.authors;

            ApplyFunctionHooks();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError(e);
        }
        finally
        {
            orig(self);
        }
    }
}
