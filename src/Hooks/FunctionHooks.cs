using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FasterGates;

public static partial class Hooks
{
    public static void ApplyFunctionHooks()
    {
        On.RegionGate.Update += RegionGate_Update;
        On.RegionGate.Door.ctor += Door_ctor;

        On.RegionGateGraphics.Clamp.ctor += Clamp_ctor;

        On.RegionGateGraphics.DrawSprites += RegionGateGraphics_DrawSprites;
        On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
        
        try
        {
            IL.RegionGateGraphics.Clamp.Update += Clamp_UpdateIL;

            IL.WaterGate.Update += WaterGate_UpdateIL;
            IL.ElectricGate.Update += ElectricGate_UpdateIL;

            IL.RegionGate.Update += RegionGate_UpdateIL;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError("IL Error:\n" + e);
        }
    }



    private static float GateSpeed => ModOptions.instantGates.Value ? 1000.0f : (ModOptions.gateSpeed.Value / 100.0f);

    // Keep track of the frames
    private static readonly ConditionalWeakTable<RegionGate, RegionGateModule> RegionGateData = new();

    private class RegionGateModule
    {
        public float StartTimer { get; set; } = 0.0f;
        public float WashingTimer { get; set; } = 0.0f;
    }



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
    


    // Override the normal frame addition - we use a float to count them up and truncate into an int to add to the timer
    private static void RegionGate_Update(On.RegionGate.orig_Update orig, RegionGate self, bool eu)
    {
        orig(self, eu);
     
        if (!RegionGateData.TryGetValue(self, out RegionGateModule regionGateModule))
        {
            regionGateModule = new RegionGateModule();
            RegionGateData.Add(self, regionGateModule);
        }

        // Delay Before Start
        if (self.mode == RegionGate.Mode.MiddleClosed)
        {
            int playersInZone = self.PlayersInZone();

            if (playersInZone > 0 && playersInZone < 3)
            {
                GateKarmaGlyph gateKarmaGlyph = self.karmaGlyphs[self.letThroughDir ? 0 : 1];

                if (!self.dontOpen && self.PlayersStandingStill() && self.EnergyEnoughToOpen && self.MeetRequirement && (gateKarmaGlyph.ShouldAnimate() == 0 || gateKarmaGlyph.animationFinished))
                {
                    self.startCounter--;
                    regionGateModule.StartTimer += 1.0f / (ModOptions.waitTime.Value / 100.0f);

                    if (regionGateModule.StartTimer >= 1.0f)
                    {
                        int difference = (int)regionGateModule.StartTimer;

                        regionGateModule.StartTimer -= difference;
                        self.startCounter += difference;
                    }
                }
                else
                {
                    regionGateModule.StartTimer = 0.0f;
                    regionGateModule.WashingTimer = 0.0f;
                }
            }
            else
            {
                regionGateModule.StartTimer = 0.0f;
                regionGateModule.WashingTimer = 0.0f;
            }
        }

        // Gate Opening Itself
        else if (self.mode == RegionGate.Mode.Waiting)
        {
            self.washingCounter--;
            regionGateModule.WashingTimer += GateSpeed;

            if (regionGateModule.WashingTimer >= 1.0f)
            {
                int difference = (int)regionGateModule.WashingTimer;

                regionGateModule.WashingTimer -= difference;
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

        if (self.player?.room != null && self.player?.room.regionGate == null)
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
        c.EmitDelegate<Func<float, RegionGateGraphics.Clamp, float>>((velY, self) => velY * (ModOptions.gateSpeed.Value / 100.0f));
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
