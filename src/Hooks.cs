﻿using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;


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

            On.RegionGateGraphics.DrawSprites += RegionGateGraphics_DrawSprites;
            On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
        }



        private static bool isInit = false;

        private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            try
            {
                if (isInit) return;
                isInit = true;

                MachineConnector.SetRegisteredOI(Plugin.MOD_ID, Options.instance);
        
                IL.RegionGateGraphics.Clamp.Update += Clamp_UpdateIL;

                IL.WaterGate.Update += WaterGate_UpdateIL;
                IL.ElectricGate.Update += ElectricGate_UpdateIL;

                IL.RegionGate.Update += RegionGate_UpdateIL;
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




        private static float GateSpeed => Options.instantGates.Value ? float.MaxValue / 2.0f : (Options.gateSpeed.Value / 100.0f);

        // Thankfully the door itself has easily modifiable speed attributes
        private static void Door_ctor(On.RegionGate.Door.orig_ctor orig, RegionGate.Door self, RegionGate gate, int number)
        {
            orig(self, gate, number);

            self.closeSpeed *= GateSpeed;
            self.openSpeed *= GateSpeed;
        }

        // Closing the clamps uses the fric value
        private static void Clamp_ctor(On.RegionGateGraphics.Clamp.orig_ctor orig, RegionGateGraphics.Clamp self, RegionGateGraphics.DoorGraphic doorG, int side, int number)
        {
            orig(self, doorG, side, number);

            self.fric *= GateSpeed;
        }
        




        // Keep track of the frames
        private static readonly ConditionalWeakTable<RegionGate, RegionGateModule> RegionGateData = new ConditionalWeakTable<RegionGate, RegionGateModule>();

        private class RegionGateModule
        {
            public float startTimer = 0.0f;
            public float washingTimer = 0.0f;
        }

        // Override the normal frame addition - we use a float to count them up and truncate into an int to add to the timer
        private static void RegionGate_Update(On.RegionGate.orig_Update orig, RegionGate self, bool eu)
        {
            if (!RegionGateData.TryGetValue(self, out RegionGateModule regionGateModule))
                regionGateModule = new RegionGateModule();

            if (self.startCounter == 0)
                regionGateModule.startTimer = 0.0f;
            
            if (self.washingCounter == 0)
                regionGateModule.washingTimer = 0.0f;



            orig(self, eu);



            // Delay Before Start
            if (self.mode == RegionGate.Mode.MiddleClosed)
            {
                int playersInZone = self.PlayersInZone();

                if (playersInZone > 0 && playersInZone < 3)
                {
                    GateKarmaGlyph gateKarmaGlyph = self.karmaGlyphs[self.letThroughDir ? 0 : 1];

                    if (!self.dontOpen && self.PlayersStandingStill() && self.EnergyEnoughToOpen && self.MeetRequirement && (gateKarmaGlyph.ShouldAnimate() == 0 || gateKarmaGlyph.animationFinished))
                    {
                        if (self.startCounter > 1)
                            self.startCounter--;

                        regionGateModule.startTimer += 1.0f / (Options.waitTime.Value / 100.0f);

                        if (regionGateModule.startTimer >= 1.0f)
                        {
                            int difference = (int)regionGateModule.startTimer;
                            regionGateModule.startTimer -= difference;

                            self.startCounter += difference;
                        }
                    }
                }
            }

            // Gate Opening Itself
            else if (self.mode == RegionGate.Mode.Waiting)
            {
                if (self.washingCounter > 1) self.washingCounter--;

                regionGateModule.washingTimer += GateSpeed;

                if (regionGateModule.washingTimer >= 1.0f)
                {
                    int difference = (int)regionGateModule.washingTimer;
                    regionGateModule.washingTimer -= difference;

                    self.washingCounter += difference;
                }
            }
        }



       
        // 'Fix' sound persisting after gate is closed or the room is left
        private static void RegionGateGraphics_DrawSprites(On.RegionGateGraphics.orig_DrawSprites orig, RegionGateGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, UnityEngine.Vector2 camPos)
        {
            orig(self,sLeaser, rCam, timeStacker, camPos);

            if (self.gate.mode == RegionGate.Mode.Closed)
                foreach (var sound in rCam.virtualMicrophone.soundObjects.Where(sound => sound.soundData.soundID == SoundID.Gate_Secure_Rail_Down || sound.soundData.soundID == SoundID.Gate_Secure_Rail_Up))
                    sound.Stop();
        }
        
        private static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, UnityEngine.Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            if (self.player?.room?.regionGate == null)
                foreach (var sound in rCam.virtualMicrophone.soundObjects.Where(sound =>
                    sound.soundData.soundID == SoundID.Gate_Bolt || sound.soundData.soundID == SoundID.Gate_Clamps_Moving_LOOP
                    || sound.soundData.soundID == SoundID.Gate_Clamp_Back_Into_Default || sound.soundData.soundID == SoundID.Gate_Clamp_Collision
                    || sound.soundData.soundID == SoundID.Gate_Clamp_In_Position || sound.soundData.soundID == SoundID.Gate_Clamp_Lock
                    || sound.soundData.soundID == SoundID.Gate_Electric_Background_LOOP || sound.soundData.soundID == SoundID.Gate_Electric_Screw_Turning_LOOP
                    || sound.soundData.soundID == SoundID.Gate_Electric_Steam_LOOP || sound.soundData.soundID == SoundID.Gate_Electric_Steam_Puff
                    || sound.soundData.soundID == SoundID.Gate_Panser_Off || sound.soundData.soundID == SoundID.Gate_Panser_On
                    || sound.soundData.soundID == SoundID.Gate_Pillows_In_Place || sound.soundData.soundID == SoundID.Gate_Pillows_Move_In
                    || sound.soundData.soundID == SoundID.Gate_Pillows_Move_Out || sound.soundData.soundID == SoundID.Gate_Poles_And_Rails_In
                    || sound.soundData.soundID == SoundID.Gate_Poles_Out || sound.soundData.soundID == SoundID.Gate_Rails_Collide
                    || sound.soundData.soundID == SoundID.Gate_Secure_Rail_Slam || sound.soundData.soundID == SoundID.Gate_Water_Screw_Turning_LOOP
                    || sound.soundData.soundID == SoundID.Gate_Water_Steam_LOOP || sound.soundData.soundID == SoundID.Gate_Water_Steam_Puff
                    || sound.soundData.soundID == SoundID.Gate_Water_Waterfall_LOOP || sound.soundData.soundID == SoundID.Gate_Water_Working_Background_LOOP
                    || sound.soundData.soundID == SoundID.Gate_Secure_Rail_Down || sound.soundData.soundID == SoundID.Gate_Secure_Rail_Up))
                    sound.Stop();
        }





        // Opening the clamps is hardcoded, default is 3.6f, it uses the fric value but is then immediately overwritten, classic Joar code I guess?
        private static void Clamp_UpdateIL(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            c.GotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(0),
                x => x.MatchLdfld<RegionGateGraphics.Clamp>(nameof(RegionGateGraphics.Clamp.velY)));

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, RegionGateGraphics.Clamp, float>>((velY, self) => velY * (Options.gateSpeed.Value / 100.0f));
        }

        // Fix hypothermia heat to be consistent
        private static void RegionGate_UpdateIL(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            c.GotoNext(MoveType.After,
                x => x.MatchLdfld<AbstractCreature>(nameof(AbstractCreature.Hypothermia)),
                x => x.MatchLdcR4(0.0f));

            c.Index++;

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, RegionGate, float>>((interpolation, self) => interpolation * GateSpeed);
        }

        // Flow rate of water, progress of battery
        private static void WaterGate_UpdateIL(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            while (c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<WaterGate>(nameof(WaterGate.WaterRunning))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, WaterGate, float>>((flow, self) => flow * GateSpeed);
                c.Index++;
            }
        }

        private static void ElectricGate_UpdateIL(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            while (c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<ElectricGate>(nameof(ElectricGate.BatteryRunning))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, ElectricGate, float>>((flow, self) => flow * GateSpeed);
                c.Index++;
            }
        }
    }
}
