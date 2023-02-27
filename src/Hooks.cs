using IL;
using IL.MoreSlugcats;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MoreSlugcats;
using On;
using Smoke;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FasterGates
{
    internal static class Hooks
    {
        public static void ApplyHooks()
        {
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;

            On.RegionGate.Update += RegionGate_Update;

            On.RegionGate.Door.ctor += Door_ctor;

            On.RegionGateGraphics.Clamp.ctor += Clamp_ctor;
        }

        // Closing the clamps uses the fric value
        private static void Clamp_ctor(On.RegionGateGraphics.Clamp.orig_ctor orig, RegionGateGraphics.Clamp self, RegionGateGraphics.DoorGraphic doorG, int side, int number)
        {
            orig(self, doorG, side, number);

            self.fric *= (Options.gateSpeed.Value / 100.0f);
        }

        // Thankfully the door itself has easily modifiable speed attributes
        private static void Door_ctor(On.RegionGate.Door.orig_ctor orig, RegionGate.Door self, RegionGate gate, int number)
        {
            orig(self, gate, number);
            
            self.closeSpeed *= (Options.gateSpeed.Value / 100.0f);
            self.openSpeed *= (Options.gateSpeed.Value / 100.0f);
        }

        // Keep track of the frames
        private static Dictionary<RegionGate, float> floatStartCounters = new Dictionary<RegionGate, float>();
        private static Dictionary<RegionGate, float> floatWashingCounters = new Dictionary<RegionGate, float>();

        // Override the normal frame addition - we use a float to count them up and truncate into an int to add to the buffer
        private static void RegionGate_Update(On.RegionGate.orig_Update orig, RegionGate self, bool eu)
        {
            if (self.startCounter == 0) floatStartCounters[self] = 0;
            if (self.washingCounter == 0) floatWashingCounters[self] = 0;

            orig(self, eu);

            // Delay Before Start
            if (self.mode == RegionGate.Mode.MiddleClosed)
            {
                int num = self.PlayersInZone();
                if (num > 0 && num < 3)
                {
                    GateKarmaGlyph gateKarmaGlyph = self.karmaGlyphs[self.letThroughDir ? 0 : 1];

                    if (!self.dontOpen && self.PlayersStandingStill() && self.EnergyEnoughToOpen && self.MeetRequirement && (gateKarmaGlyph.ShouldAnimate() == 0 || gateKarmaGlyph.animationFinished))
                    {
                        if (self.startCounter > 1) self.startCounter--;

                        floatStartCounters[self] += 1.0f / (Options.waitTime.Value / 100.0f);

                        if (floatStartCounters[self] >= 1.0f)
                        {
                            int difference = (int)floatStartCounters[self];
                            floatStartCounters[self] -= difference;

                            self.startCounter += difference;
                        }
                    }
                }
            }

            // Gate Opening Itself
            if (self.mode == RegionGate.Mode.Waiting)
            {
                if (self.washingCounter > 1) self.washingCounter--;

                floatWashingCounters[self] += Options.gateSpeed.Value / 100.0f;

                if (floatWashingCounters[self] >= 1.0f)
                {
                    int difference = (int)floatWashingCounters[self];
                    floatWashingCounters[self] -= difference;

                    self.washingCounter += difference;
                }
            }
        }

        private static bool isInit = false;

        private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            if (isInit) return;
            isInit = true;

            MachineConnector.SetRegisteredOI(Plugin.MOD_ID, Options.instance);
        
            try
            {
                IL.RegionGateGraphics.Clamp.Update += Clamp_Update;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError(e);
            }
        }

        // Opening the clamps is hardcoded, default is 3.6f
        // It uses the fric value but is then immediately overwritten, classic Joar code I guess?
        private static void Clamp_Update(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            c.GotoNext(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(0),
                x => x.MatchLdfld<RegionGateGraphics.Clamp>("velY"),
                x => x.MatchLdcR4(3.6f));

            c.Index += 3;

            c.Remove();
            c.Emit(OpCodes.Ldarg_0);

            c.EmitDelegate<Func<RegionGateGraphics.Clamp, float>>((self) =>
            {
                return 3.6f * (Options.gateSpeed.Value / 100.0f);
            });
        }
    }
}
