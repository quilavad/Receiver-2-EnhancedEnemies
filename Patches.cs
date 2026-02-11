using HarmonyLib;
using Receiver2;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Configuration;
using System.Reflection;
using System.Reflection.Emit;
using VLB;

namespace EnhancedEnemies.Patches
{
    /*[HarmonyPatch]
    public static class MyopicTurrets
    {
        static float spread = 0.05f;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BulletTrajectoryCluster), nameof(BulletTrajectoryCluster.Evaluate))]
        static bool InaccurateFire(ref bool __result, Ray ray, CartridgeSpec spec, float player_hit_multiplier, int num_trajectories, out BulletTrajectory best_trajectory)
        {
            BulletTrajectory[] array = new BulletTrajectory[num_trajectories];
			for (int i = 0; i < num_trajectories; i++)
			{
				Quaternion quaternion = Quaternion.Lerp(Quaternion.identity, global::UnityEngine.Random.rotationUniform, spread + 0.001f * (float)i);
				array[i] = BulletTrajectoryManager.PlanTrajectory(ray.origin, spec, quaternion * ray.direction, true);
			}
			float[] array2 = new float[num_trajectories];
			float num = float.MinValue;
			best_trajectory = null;
			for (int j = 0; j < num_trajectories; j++)
			{
				array2[j] = BulletTrajectoryCluster.EvaluateTrajectory(array[j], player_hit_multiplier);
				if (array2[j] > num)
				{
					num = array2[j];
					best_trajectory = array[j];
				}
			}
			__result = num > 0f;
            return false;
        }
    }*/

    [HarmonyPatch]
    public static class TurretMain
    {
        internal static ConfigEntry<int> weight;
        internal static string[] coloredComponents = ["point_pivot/gun_pivot/gun_assembly/ammo_box", "point_pivot/gun_pivot/gun_assembly/ammo_destroy/ammo_box_ammo_box_shard", "point_pivot/gun_pivot/gun_assembly/ammo_destroy/ammo_box_ammo_box_shard_001", "base_physics/cutout/base_cutout_viz", "base_physics/standard/base_viz", "armor_base/armor_base_viz"];
        //"point_pivot/gun_pivot/gun_assembly/gun", 

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), nameof(TurretScript.Start))]
        static void TurretSetup(TurretScript __instance)
        {
            int shotgunWeight = ShotgunTurrets.enabled.Value ? ShotgunTurrets.weight.Value : 0;
            int lancerWeight = LancerTurrets.enabled.Value ? LancerTurrets.weight.Value : 0;
            int random = (int) (Random(TurretSeed(__instance), "Turret Main") * (weight.Value + shotgunWeight + lancerWeight));
            //Plugin.Logger.LogInfo(random);

            if (random < shotgunWeight)
            {
                ShotgunTurrets.TurretSetup(__instance);
            }
            else if ((random -= shotgunWeight) < lancerWeight)
            {
                LancerTurrets.TurretSetup(__instance);
            }
        }

        public static int TurretSeed(TurretScript turret)
        {
            int seed;
            if (turret.kds.identifier == "")
            {
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                turret.kds.identifier = seed.ToString();
                return seed;
            }
            if (!int.TryParse(turret.kds.identifier, out seed))
            {
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }
            return seed;
        }

        public static double Random(int seed, string name)
        {
            return ((double)(seed + name.GetHashCode())) / 4294967296L + .5;
        }

        internal static void VanillaExecuteTrajectory(BulletTrajectory trajectory)
		{
			ReceiverEvents.TriggerEvent(ReceiverEventTypeBulletTrajectory.Created, trajectory);
			BulletTrajectoryManager.active_trajectories.Add(trajectory);
		}

        [HarmonyPatch(typeof(TurretScript), "UpdateLight")]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            Label setColor = il.DefineLabel();
            Label notShotgun = il.DefineLabel();
            Label isRegularTurret = il.DefineLabel();
            Label end = il.DefineLabel();

            return new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Light), "set_color")))
                //for some reason, the color wont override unless i replace this line of code even though its the exact same line of code. ciarence's mod does the same thing
                .SetAndAdvance(OpCodes.Callvirt, AccessTools.Method(typeof(Light), "set_color", [typeof(Color)]))
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TurretScript), nameof(TurretScript.gun_pivot_camera_light))),

                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ShotgunTurrets), nameof(ShotgunTurrets.shotgunSet))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(HashSet<TurretScript>), nameof(HashSet<TurretScript>.Contains))),
                    new CodeInstruction(OpCodes.Brfalse_S, notShotgun),
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ShotgunTurrets), nameof(ShotgunTurrets.lightColor))),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ConfigEntry<Color>), "get_Value")),
                    new CodeInstruction(OpCodes.Br_S, setColor),

                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets.lancerSet))).WithLabels([notShotgun]),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(HashSet<TurretScript>), nameof(HashSet<TurretScript>.Contains))),
                    new CodeInstruction(OpCodes.Brfalse_S, isRegularTurret),
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets.lightColor))),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ConfigEntry<Color>), "get_Value")),
                    
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Light), "set_color", [typeof(Color)])).WithLabels([setColor]),
                    new CodeInstruction(OpCodes.Br_S, end),

                    new CodeInstruction(OpCodes.Pop).WithLabels([isRegularTurret])
                )
                .AddLabels([end])
                .Instructions();
        }
    }

    [HarmonyPatch]
    public static class ShotgunTurrets
    {
        internal static HashSet<TurretScript> shotgunSet = new HashSet<TurretScript>();
        internal static FieldInfo mpb_info = AccessTools.Field(typeof(TurretScript), "mpb");
        internal static FieldInfo color_id_info = AccessTools.Field(typeof(TurretScript), "color_id");
        internal static AccessTools.FieldRef<TurretScript, MeshRenderer> flare_renderer_access = AccessTools.FieldRefAccess<TurretScript, MeshRenderer>("flare_renderer");
        internal static AccessTools.FieldRef<TurretScript, VolumetricLightBeam> beam_access = AccessTools.FieldRefAccess<TurretScript, VolumetricLightBeam>("beam");
        internal static AccessTools.FieldRef<TurretScript, Light> gun_pivot_camera_light_point_access = AccessTools.FieldRefAccess<TurretScript, Light>("gun_pivot_camera_light_point");
        internal static ConfigEntry<bool> enabled;
        internal static ConfigEntry<float> spread;
        internal static ConfigEntry<int> pellets;
        internal static ConfigEntry<float> fireInterval;
        internal static ConfigEntry<float> alertDelay;
        //internal static ConfigEntry<float> chance;
        internal static ConfigEntry<int> weight;
        internal static ConfigEntry<Color> lightColor;
        internal static ConfigEntry<Color> componentColor;
        internal static CartridgeSpec _00Pellet = Init00Pellet();

        static CartridgeSpec Init00Pellet()
        {
            CartridgeSpec cs = new()
            {
                gravity = true,
                extra_mass = 1.63f,
                mass = 3.54f,
                speed = 396f,
                diameter = 0.00838f,
                density = 11340f
            };
            float num = cs.diameter * 0.5f;
			float num2 = cs.mass / 1000f / cs.density;
			cs.cylinder_length = num2 / (3.1415927f * num * num);
            return cs;
        }

        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(TurretScript), nameof(TurretScript.Start))]
        internal static void TurretSetup(TurretScript __instance)
        {
            //if (new System.Random(TurretMain.TurretSeed(__instance) + "Shotgun Turrets".GetHashCode()).NextDouble() < chance.Value && enabled.Value)
            {
                shotgunSet.Add(__instance);
                if (shotgunSet.Count >= 50)
                {
                    Plugin.Logger.LogWarning($"shotgun turret register abnormally large ({shotgunSet.Count}); you have an unusual number of turrets or they are not being deregistered");
                }

                __instance.fire_interval = fireInterval.Value;
                __instance.blind_fire_interval = fireInterval.Value;
                foreach (string s in TurretMain.coloredComponents)
                {
                    __instance.transform.Find(s).GetComponent<MeshRenderer>().material.color = componentColor.Value;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), nameof(TurretScript.OnDestroy))]
        static void Deregister(TurretScript __instance)
        {
            shotgunSet.Remove(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TurretScript), "UpdateCameraAlive")]
        static void AlertDelayPre(ref AIState ___ai_state, out AIState __state)
        {
            __state = ___ai_state;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), "UpdateCameraAlive")]
        static void AlertDelayPost(TurretScript __instance, ref float ___alert_delay, AIState __state)
        {
			if (shotgunSet.Contains(__instance) && __instance.CanSeePlayer())
			{
				if (__state == AIState.Idle)
				{
					___alert_delay *= alertDelay.Value / 0.6f;
				}
			}
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BulletTrajectoryManager), nameof(BulletTrajectoryManager.ExecuteTrajectory))]
        static bool TurretShotgunShot(BulletTrajectory trajectory)
        {
            if (shotgunSet.Contains(trajectory.bullet_source.GetComponent<TurretScript>()))// &&
                //(trajectory.bullet_source_entity_type == ReceiverEntityType.Turret ||
                //trajectory.bullet_source_entity_type == ReceiverEntityType.CeilingTurret))
            {
                Vector3 start = trajectory.movement_events[0].start_pos;
                Vector3 direction = trajectory.movement_events[0].end_pos - start;

                for (int i = 0; i < pellets.Value; i++)
                {
                    Quaternion quaternion = Quaternion.Lerp(Quaternion.identity, global::UnityEngine.Random.rotationUniform, spread.Value / 2f / 180f);
                    BulletTrajectory bt = BulletTrajectoryManager.PlanTrajectory(start, _00Pellet, quaternion * direction, true);
                    bt.draw_path = trajectory.draw_path;
                    bt.bullet_source = trajectory.bullet_source;
                    bt.bullet_source_entity_type = trajectory.bullet_source_entity_type;
                    TurretMain.VanillaExecuteTrajectory(bt);
                }
                return false;
            }
            else
            {
                return true;
            }
        }

    }

    [HarmonyPatch]
    public static class LancerTurrets
    {
        internal static HashSet<TurretScript> lancerSet = [];
        internal static ConfigEntry<bool> enabled;
        internal static ConfigEntry<int> weight;
        internal static ConfigEntry<float> fireInterval;
        internal static ConfigEntry<float> alertDelay;
        internal static ConfigEntry<float> sweepDuration;
        internal static ConfigEntry<float> sweepSpeed;
        internal static ConfigEntry<bool> fireViaSecurityCameraEnabled;
        internal static ConfigEntry<float> groupSizeViaSecurityCamera;
        internal static CartridgeSpec _50AP = Init50AP();
        internal static ConfigEntry<Color> lightColor;
        internal static ConfigEntry<Color> componentColor;
        internal static AccessTools.FieldRef<TurretScript, StochasticVision> vision_access = AccessTools.FieldRefAccess<TurretScript, StochasticVision>("vision");
        internal static AccessTools.FieldRef<StochasticVision, float> kConsecutiveBlockedPerSecond_access = AccessTools.FieldRefAccess<StochasticVision, float>("kConsecutiveBlockedPerSecond");

        static CartridgeSpec Init50AP()
        {
            CartridgeSpec cs = new()
            {
                gravity = true,
                extra_mass = 60f,
                mass = 42f,
                speed = 999,
                diameter = 0.013f,
                density = 11340f * 1.5f
            };
            float num = cs.diameter * 0.5f;
			float num2 = cs.mass / 1000f / cs.density;
			cs.cylinder_length = num2 / (3.1415927f * num * num);
            return cs;
        }

        internal static void TurretSetup(TurretScript __instance)
        {
            lancerSet.Add(__instance);
            if (lancerSet.Count >= 50)
            {
                Plugin.Logger.LogWarning($"lancer turret register abnormally large ({lancerSet.Count}); you have an unusual number of turrets or they are not being deregistered");
            }
            __instance.fire_interval = fireInterval.Value;
            __instance.blind_fire_interval = fireInterval.Value;
            kConsecutiveBlockedPerSecond_access(vision_access(__instance)) = 40 / sweepDuration.Value;
            foreach (string s in TurretMain.coloredComponents)
            {
                __instance.transform.Find(s).GetComponent<MeshRenderer>().material.color = componentColor.Value;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TurretScript), "UpdateCameraAlive")]
        static void AlertDelayPre(ref AIState ___ai_state, out AIState __state)
        {
            __state = ___ai_state;
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), "UpdateCameraAlive")]
        static void AlertDelayPost(TurretScript __instance, ref float ___alert_delay, AIState __state)
        {
			if (lancerSet.Contains(__instance) && __instance.CanSeePlayer())
			{
				if (__state == AIState.Idle)
				{
					___alert_delay *= alertDelay.Value / 0.6f;
				}
			}
        }
        
        /*public static int GetInstructionSize(OpCode opcode)
        {
            // 1. Calculate OpCode size
            // Most are 1 byte, but some (like 'ldarg.0' or 'volatile.') are 2 bytes (prefixed with 0xFE)
            int size = opcode.Size;

            // 2. Add the size of the operand
            size += opcode.OperandType switch
            {
                // No operand
                OperandType.InlineNone => 0,

                // 1-byte operands (Short branches, byte arguments)
                OperandType.ShortInlineBrTarget => 1,
                OperandType.ShortInlineI => 1,
                OperandType.ShortInlineVar => 1,

                // 2-byte operands (Char/Short)
                OperandType.InlineVar => 2,

                // 4-byte operands (Ints, Floats, Tokens, Branch Targets)
                OperandType.InlineBrTarget => 4,
                OperandType.InlineField => 4,
                OperandType.InlineI => 4,
                OperandType.InlineMethod => 4,
                OperandType.InlineSig => 4,
                OperandType.InlineString => 4,
                OperandType.InlineType => 4,
                OperandType.ShortInlineR => 4, // "Short" float is 32-bit (4 bytes)

                // 8-byte operands (Longs, Doubles)
                OperandType.InlineI8 => 8,
                OperandType.InlineR => 8,

                // Special case: Switch statement
                // Format: uint32 count, followed by (count * int32) targets
                // Note: This method cannot accurately size a 'switch' without knowing the target count.
                OperandType.InlineSwitch => throw new ArgumentException("Switch opcode size depends on the number of targets."),

                _ => 0
            };

            return size;
        }*/
    }

    [HarmonyPatch]
    public static class LancerTurrets_BypassPenCheck
    {
        [HarmonyPatch(typeof(BulletTrajectoryManager), nameof(BulletTrajectoryManager.PlanTrajectory))]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            Label bypassStuckInside = il.DefineLabel();
            Label bypassStop = il.DefineLabel();
            Label bypassRicochet = il.DefineLabel();
            Label bypassRedirect = il.DefineLabel();

            var codes = new List<CodeInstruction>(instructions);
            int i = 0;

            for (; i < codes.Count; i++)
            {
                Label endIf = il.DefineLabel();
                //IL_017C
                if (codes[i+2].opcode == OpCodes.Ldfld && ((FieldInfo)codes[i+2].operand) == AccessTools.Field(typeof(BallisticProperties), nameof(BallisticProperties.hollow)))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets._50AP)));
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(object), nameof(object.Equals), [typeof(object), typeof(object)]));
                    yield return new CodeInstruction(OpCodes.Brtrue_S, endIf);
                    for (int j = 0; j < 4; j++)
                        yield return codes[i++];
                    yield return codes[i++].WithLabels([endIf]);
                    break;
                }
                else yield return codes[i];
            }
            for (; i < codes.Count; i++)
            {
                //IL_021A
                if (codes[i].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 18)
                {
                    yield return codes[i++];
                    yield return codes[i++];
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets._50AP)));
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(object), nameof(object.Equals), [typeof(object), typeof(object)]));
                    yield return new CodeInstruction(OpCodes.Brtrue_S, bypassStuckInside);
                    /*yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Plugin), nameof(Plugin.Logger)));
                    yield return new CodeInstruction(OpCodes.Ldstr, "sibypassbypass?");
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ManualLogSource), nameof(ManualLogSource.LogInfo)));*/
                    break;
                }
                else yield return codes[i];
            }
            for (; i < codes.Count; i++)
            {
                //IL_02E3
                if (codes[i].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 15)
                {
                    yield return codes[i++].WithLabels([bypassStuckInside]);
                    break;
                }
                else yield return codes[i];
            }

            for (; i < codes.Count; i++)
            {
                Label endIf = il.DefineLabel();
                //IL_038E
                if (codes[i+1].opcode == OpCodes.Ldfld && ((FieldInfo)codes[i+1].operand) == AccessTools.Field(typeof(BallisticProperties), nameof(BallisticProperties.hollow)))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets._50AP)));
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(object), nameof(object.Equals), [typeof(object), typeof(object)]));
                    yield return new CodeInstruction(OpCodes.Brtrue_S, endIf);
                    for (int j = 0; j < 3; j++)
                        yield return codes[i++];
                    yield return codes[i++].WithLabels([endIf]);
                    break;
                }
                else yield return codes[i];
            }

            for (; i < codes.Count; i++)
            {
                Label endIf = il.DefineLabel();
                //IL_06ED
                if (codes[i].opcode == OpCodes.Ldloc_1
                && codes[i+1].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i+1].operand).LocalIndex == 35)
                {
                    yield return codes[i++];
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets._50AP)));
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(object), nameof(object.Equals), [typeof(object), typeof(object)]));
                    yield return new CodeInstruction(OpCodes.Brfalse_S, endIf);
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Br_S, bypassRicochet);
                    yield return codes[i++].WithLabels([endIf]);
                    break;
                }
                else yield return codes[i];
            }

            for (; i < codes.Count; i++)
            {
                //Label endIf = il.DefineLabel();
                //IL_082C
                if (codes[i+1].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i+1].operand).LocalIndex == 39)
                {
                    yield return codes[i++].WithLabels([bypassRicochet]);
                    yield return codes[i++];
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets._50AP)));
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(object), nameof(object.Equals), [typeof(object), typeof(object)]));
                    yield return new CodeInstruction(OpCodes.Brtrue_S, bypassStop);
                    //yield return new CodeInstruction(OpCodes.Br_S, bypassStop);
                    yield return codes[i++];
                    break;
                }
                else yield return codes[i];
            }
            for (; i < codes.Count; i++)
            {
                //IL_0B7F
                if (codes[i].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 39)
                {
                    yield return codes[i++].WithLabels([bypassStop]);
                    break;
                }
                else yield return codes[i];
            }

            for (; i < codes.Count; i++)
            {
                //IL_0E38
                if (codes[i+4].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i+4].operand).LocalIndex == 56)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets._50AP)));
                    yield return new CodeInstruction(OpCodes.Box, typeof(CartridgeSpec));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(object), nameof(object.Equals), [typeof(object), typeof(object)]));
                    yield return new CodeInstruction(OpCodes.Brtrue_S, bypassRedirect);
                    for (int j = 0; j < 8; j++)
                        yield return codes[i++];
                    yield return codes[i++].WithLabels([bypassRedirect]);
                    break;
                }
                else yield return codes[i];
            }

            for (; i < codes.Count; i++)
                yield return codes[i];
        }
    }

    [HarmonyPatch]
    public static class LancerTurrets_SetAmmo
    {
        [HarmonyPatch(typeof(TurretScript), "UpdateBarrel")]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            Label isRegularTurret = il.DefineLabel();
            Label endif = il.DefineLabel();
            Label setCartridge = il.DefineLabel();

            return new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(TurretScript), "cartridge_spec")))
                //.SetOperandAndAdvance(AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets._50AP)))
                //.RemoveInstruction()
                .Advance(1)
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets.lancerSet))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(HashSet<TurretScript>), nameof(HashSet<TurretScript>.Contains), [typeof(TurretScript)])),
                    new CodeInstruction(OpCodes.Brfalse_S, isRegularTurret),
                    new CodeInstruction(OpCodes.Pop),
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets._50AP))).WithLabels([setCartridge])
                    /*new CodeInstruction(OpCodes.Stsfld, AccessTools.Field(typeof(TurretScript), "cartridge_spec"))*/
                    /*new CodeInstruction(OpCodes.Br_S, endif),
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(TurretScript), "cartridge_spec")).WithLabels([isRegularTurret])*/
                )
                .AddLabels([isRegularTurret])
                //.Advance(1)
                //.AddLabels([endif])
                /*.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(BulletTrajectoryCluster), nameof(BulletTrajectoryCluster.Evaluate))))
                .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BulletTrajectoryCluster), nameof(BulletTrajectoryCluster.Evaluate))))
                .MatchBack(false, new CodeMatch(i => i.opcode == OpCodes.Brfalse_S))
                .SetInstruction(new CodeInstruction(OpCodes.Brfalse_S, setCartridge))
                .MatchBack(false, new CodeMatch(i => i.opcode == OpCodes.Ble_Un_S))
                .SetInstruction(new CodeInstruction(OpCodes.Ble_Un_S, setCartridge))*/
                .Instructions();
        }
    }

    [HarmonyPatch]
    public static class LancerTurrets_LeadAfterVisionLoss
    {
        [HarmonyPatch(typeof(TurretScript), "UpdateCameraAlive")]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            Label cannotSeeThisFrame = il.DefineLabel();
            Label vanillaTargeting = il.DefineLabel();
            Label endif = il.DefineLabel();
            Label notExternalVision = il.DefineLabel();
            Label noFire = il.DefineLabel();
            //Label notJustLostSight = il.DefineLabel();

            //var cm =
            return new CodeMatcher(instructions)
                //IL_0103
                .MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(RobotScript), nameof(RobotScript.CanSeePlayer))))
                .Advance(2)
                .InsertAndAdvance(
                    // if (lancerSet.contains(__instance) && !BestVision.can_see_player_this_frame)

                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets.lancerSet))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(HashSet<TurretScript>), nameof(HashSet<TurretScript>.Contains), [typeof(TurretScript)])),
                    new CodeInstruction(OpCodes.Brfalse_S, vanillaTargeting),

                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TurretScript), "get_BestVision")),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(StochasticVision), nameof(StochasticVision.can_see_player_this_frame))),
                    new CodeInstruction(OpCodes.Brtrue_S, vanillaTargeting),
                    /*new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TurretScript), "trigger_down")),
                    new CodeInstruction(OpCodes.Brfalse_S, canSeeThisFrame),*/

                    /*new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Plugin), nameof(Plugin.Logger))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TurretScript), "get_BestVision")),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(StochasticVision), "_consecutive_blocked")),
                    new CodeInstruction(OpCodes.Box, typeof(float)),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ManualLogSource), nameof(ManualLogSource.LogInfo))),*/

                    //      if (BestVision._consecutive_blocked < 0.01f)

                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TurretScript), "get_BestVision")),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(StochasticVision), "_consecutive_blocked")),
                    new CodeInstruction(OpCodes.Ldc_R4, 0.2f),
                    new CodeInstruction(OpCodes.Bge_S, cannotSeeThisFrame),

                    //          target_pos = BestVision.last_spotted_point

                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TurretScript), "get_BestVision")),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(StochasticVision), nameof(StochasticVision.last_spotted_point))),
                    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(RobotScript), "target_pos")),

                    //      skip target_pos assignment
                    new CodeInstruction(OpCodes.Br_S, cannotSeeThisFrame)
                    
                )
                .AddLabels([vanillaTargeting])
                .MatchForward(false, new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(RobotScript), "target_pos")))
                .Advance(1)
                .MatchForward(false, new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(RobotScript), "target_pos")))
                .Advance(1)
                .AddLabels([cannotSeeThisFrame])
                //IL_025D
                .MatchForward(false, new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(StochasticVision), nameof(StochasticVision.can_see_player))))
                .Advance(1)
                .RemoveInstruction()
                //IL_0264
                //.MatchForward(true, new CodeMatch(i => i.opcode == OpCodes.Stfld && ((FieldInfo)i.operand) == AccessTools.Field(typeof(TurretScript), "trigger_down")))
                .InsertAndAdvance(
                    // if (!vision.can_see_player && fireViaSecurityCameraEnable.Value && lancerSet.contauns(__instance)) //that is, relying on external vision

                    new CodeInstruction(OpCodes.Brtrue_S, notExternalVision),
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets.fireViaSecurityCameraEnabled))),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ConfigEntry<bool>), "get_Value")),
                    new CodeInstruction(OpCodes.Brfalse_S, noFire),
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets.lancerSet))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(HashSet<TurretScript>), nameof(HashSet<TurretScript>.Contains))),
                    new CodeInstruction(OpCodes.Brfalse_S, noFire),

                    //      target_pos += Random.insideUnitSphere * groupSizeViaSecurityCamera.Value

                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RobotScript), "target_pos")),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Random), "get_insideUnitSphere")),
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets.groupSizeViaSecurityCamera))),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ConfigEntry<float>), "get_Value")),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Vector3), "op_Multiply", [typeof(Vector3), typeof(float)])),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Vector3), "op_Addition")),
                    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(RobotScript), "target_pos")),

                    new CodeInstruction(OpCodes.Br_S, endif),

                    // else if (lancerSet.contains(__instance) && !BestVision.can_see_player_this_frame)

                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets.lancerSet))).WithLabels([notExternalVision]),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(HashSet<TurretScript>), nameof(HashSet<TurretScript>.Contains))),
                    new CodeInstruction(OpCodes.Brfalse_S, endif),

                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TurretScript), "get_BestVision")),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(StochasticVision), nameof(StochasticVision.can_see_player_this_frame))),
                    new CodeInstruction(OpCodes.Brtrue_S, endif),

                    //      target_pos += target_vel.normalized * 2f * Time.deltaTime

                    new CodeInstruction(OpCodes.Ldarg_0),//.WithLabels([notJustLostSight]),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RobotScript), "target_pos")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(TurretScript), "target_vel")),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Vector3), "get_normalized")),
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LancerTurrets), nameof(LancerTurrets.sweepSpeed))),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ConfigEntry<float>), "get_Value")),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Time), "get_deltaTime")),
                    new CodeInstruction(OpCodes.Mul),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Vector3), "op_Multiply", [typeof(Vector3), typeof(float)])),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Vector3), "op_Addition")),
                    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(RobotScript), "target_pos"))
                )
                .AddLabels([endif])
                .MatchForward(false, new CodeMatch(OpCodes.Ret))
                .AddLabels([noFire])
                
                .Instructions();

            /*int offset = 0;
            foreach(CodeInstruction c in cm)
            {
                Plugin.Logger.LogInfo($"{offset:X}: {c.ToString()}");
                offset += LancerTurrets.GetInstructionSize((c.opcode));
            }
            return cm;*/
        }
    }

    [HarmonyPatch]
    public static class SleepyTurrets
    {
        private static Dictionary<TurretScript, float> sleepTimers = new Dictionary<TurretScript, float>();
        internal static AccessTools.FieldRef<TurretScript, AIState> state_access = AccessTools.FieldRefAccess<TurretScript, AIState>("ai_state");
        internal static ConfigEntry<bool> enabled;
        internal static ConfigEntry<bool> overrideLevelStartAsleepChance;
        internal static ConfigEntry<float> startAsleepChance;
        internal static ConfigEntry<float> sleepyChance;
        internal static ConfigEntry<float> sleepTimeout;
        internal static ConfigEntry<float> wakeupDelay;
        internal static ConfigEntry<float> firstWakeupExtraDelay;
        internal static string[] coloredComponents = ["point_pivot/gun_pivot/armor_gun/armor_gun_viz", "point_pivot/gun_pivot/gun_assembly/camera_armor", "point_pivot/gun_pivot/gun_assembly/motion_sensor"];
        internal static ConfigEntry<Color> componentColor;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), nameof(TurretScript.Update))]
        static void SleepTimer(TurretScript __instance, ref AIState ___ai_state)
        {
            if (enabled.Value && sleepTimers.ContainsKey(__instance))
            {
                if (___ai_state == AIState.Aiming)
                {
                    sleepTimers[__instance] = sleepTimeout.Value;
                }
                else if (___ai_state == AIState.Idle)
                {
                    if ((sleepTimers[__instance] -= Time.deltaTime) <= 0f)
                    {
                        ___ai_state = AIState.Standby;
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), nameof(TurretScript.OnDestroy))]
        static void Deregister(TurretScript __instance)
        {
            sleepTimers.Remove(__instance);
            //Plugin.Logger.LogInfo($"turret timer deregistered; new size {sleepTimers.Count}");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TurretScript), nameof(TurretScript.Start))]
        static void TurretSetup(TurretScript __instance, ref float ___standby_wakeup_delay, ref AIState ___ai_state)
        {
            System.Random random = new System.Random(TurretMain.TurretSeed(__instance));
            double r = random.NextDouble();
            if (overrideLevelStartAsleepChance.Value)
            {
                __instance.start_in_standby_mode = r < startAsleepChance.Value;
            }
            if (__instance.start_in_standby_mode)
            {
                ___standby_wakeup_delay = firstWakeupExtraDelay.Value;
                if (__instance.is_ceiling_mounted)
                {
                    ___ai_state = AIState.Idle;
                    __instance.StartCoroutine(DelayStartAsleep(__instance));
                }
            
                r = random.NextDouble();
                if (r < sleepyChance.Value)
                {
                    sleepTimers[__instance] = sleepTimeout.Value;
                    if (sleepTimers.Count >= 50)
                    {
                        Plugin.Logger.LogWarning($"sleepy turret register abnormally large ({sleepTimers.Count}); you have an unusual number of turrets or they are not being deregistered");
                    }
                    foreach (string s in coloredComponents)
                    {
                        __instance.transform.Find(s).GetComponent<MeshRenderer>().material.color = componentColor.Value;
                    }
                }
            }
        }

        static IEnumerator<WaitForSeconds> DelayStartAsleep(TurretScript turret)
        {
            yield return new WaitForSeconds(1f);
            //if (state_access(turret) == AIState.Idle)
            {
                state_access(turret) = AIState.Standby;
            }
        }

        struct OverrideWakeupState
        {
            public AIState ai_state;
            public float standby_wakeup_delay;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TurretScript), "UpdateSensor")]
        static void OverrideWakeupDelayPre(AIState ___ai_state, ref float ___standby_wakeup_delay, out OverrideWakeupState __state)
        {
            __state = new OverrideWakeupState
            {
                ai_state = ___ai_state,
                standby_wakeup_delay = ___standby_wakeup_delay
            };
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), "UpdateSensor")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Method Declaration", "Harmony003:Harmony non-ref patch parameters modified", Justification = "im not modifying it dough")]
        static void OverrideWakeupDelayPost(TurretScript __instance, ref float ___standby_wakeup_delay, OverrideWakeupState __state)
        {
            if (__state.ai_state == AIState.Standby)
			{
				if (!__instance.motion_sensor_alive || __instance.powered_off)
				{
					return;
				}
				if (__instance.CanSeePlayer())
				{
                    ___standby_wakeup_delay = __state.standby_wakeup_delay + wakeupDelay.Value;
                    if (sleepTimers.ContainsKey(__instance))
                    {
                        sleepTimers[__instance] = sleepTimeout.Value + ___standby_wakeup_delay;
                    }
				}
			}
        }
    }

    [HarmonyPatch]
    internal class SleepyDrones
    {
        private static Dictionary<ShockDrone, float> wakeUpTimers = new Dictionary<ShockDrone, float>();
        private static Dictionary<ShockDrone, float> sleepTimers = new Dictionary<ShockDrone, float>();
        private static HashSet<ShockDrone> grounded = new HashSet<ShockDrone>();
        private static Dictionary<CameraPart, ShockDrone> cameraToDrone = new Dictionary<CameraPart, ShockDrone>();
        private static Dictionary<OscillatorPart, ShockDrone> oscillatorToDrone = new Dictionary<OscillatorPart, ShockDrone>();
        private static Dictionary<MotorPart, ShockDrone> motorToDrone = new Dictionary<MotorPart, ShockDrone>();
        internal static AccessTools.FieldRef<SensorPart, RobotPart> part_access = AccessTools.FieldRefAccess<SensorPart, RobotPart>("part");
        //internal static Type cr = AccessTools.TypeByName("CachedRaycasts");
        //internal static object cached_raycasts_access = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.FieldRefAccess), [typeof(string)], [typeof(SensorPart),cr])/*.MakeGenericMethod(typeof(SensorPart),cr)*/.Invoke(null, ["cached_raycasts"]);
        internal static AccessTools.FieldRef<ShockDrone, ShockDroneState> state_access = AccessTools.FieldRefAccess<ShockDrone, ShockDroneState>("state");
        internal static AccessTools.FieldRef<MotorPart, FMOD.Studio.EventInstance> event_instance_motor_access = AccessTools.FieldRefAccess<MotorPart, FMOD.Studio.EventInstance>("event_instance_motor");
        internal static MethodInfo throttle_set_access = AccessTools.PropertySetter(typeof(MotorPart),nameof(MotorPart.Throttle));
        internal static ConfigEntry<bool> startAsleepEnabled;
        internal static ConfigEntry<float> startAsleepChance;
        internal static ConfigEntry<float> wakeupDelay;
        internal static ConfigEntry<float> firstWakeupExtraDelay;
        internal static ConfigEntry<bool> sleepyEnabled;
        internal static ConfigEntry<float> sleepTimeout;
        internal static ConfigEntry<float> sleepyChance;
        internal static ConfigEntry<float> sleepVolumeMod;
        internal static float firstSleepDelay = 3f;
        internal static ConfigEntry<float> groundedChance;
        //internal static ConfigEntry<float> wakeupAngle;
        //internal static ConfigEntry<float> wakeUpRange;
        internal static string[] coloredComponents = ["Back/Camera/Intact/camera_armor_001", "Back/Camera/Broken/camera_armor_shard", "Back/Camera/Broken/camera_armor_shard_001", "Back/Camera/Broken/camera_armor_shard_002", "Back/Camera/Broken/camera_armor_shard_003", "Back/Camera/Broken/camera_armor_shard_004", "Back/Camera/Broken/camera_armor_shard_005", "Front/Sensor/Intact/motion_sensor_001", "Front/Sensor/Broken/motion_sensor_shard", "Front/Sensor/Broken/motion_sensor_shard_001", "Front/Sensor/Broken/motion_sensor_shard_002", "Front/Sensor/Broken/motion_sensor_shard_003", "Front/Sensor/Broken/motion_sensor_shard_004", "Front/Sensor/Broken/motion_sensor_shard_005"];
        internal static ConfigEntry<Color> componentColor;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShockDrone), "Start")]
        static void DroneSetup(ShockDrone __instance)
        {
            cameraToDrone[__instance.camera_part] = __instance;
            oscillatorToDrone[__instance.oscillator_part] = __instance;
            motorToDrone[__instance.motor_part] = __instance;
            if (startAsleepEnabled.Value && UnityEngine.Random.value < startAsleepChance.Value)
            {
                wakeUpTimers[__instance] = firstWakeupExtraDelay.Value;
                if (sleepyEnabled.Value && UnityEngine.Random.value < sleepyChance.Value)
                {
                    sleepTimers[__instance] = sleepTimeout.Value + firstSleepDelay;
                    foreach (string s in coloredComponents)
                    {
                        __instance.transform.Find(s).GetComponent<MeshRenderer>().material.color = componentColor.Value;
                    }
                }
                if (UnityEngine.Random.value < groundedChance.Value)
                {
                    grounded.Add(__instance);
                }
                __instance.StartCoroutine(DelayStartAsleep(__instance));
            }
            if (cameraToDrone.Count >= 50)
            {
                Plugin.Logger.LogWarning($"sleepy drone register abnormally large ({cameraToDrone.Count}); you have an unusual number of drones or they are not being deregistered");
            }
        }

        static IEnumerator<WaitForSeconds> DelayStartAsleep(ShockDrone drone)
        {
            yield return new WaitForSeconds(firstSleepDelay);
            if (state_access(drone) == ShockDroneState.Idle)
            {
                ReversePatch_TransitionToState(drone, ShockDroneState.Standby);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CameraPart), "OnDestroy")]
        static void Deregister(CameraPart __instance)
        {
            if (cameraToDrone.ContainsKey(__instance))
            {
                ShockDrone drone = cameraToDrone[__instance];
                sleepTimers.Remove(drone);
                wakeUpTimers.Remove(drone);
                grounded.Remove(drone);
                cameraToDrone.Remove(__instance);
                oscillatorToDrone.Remove(drone.oscillator_part);
                motorToDrone.Remove(drone.motor_part);
                //Plugin.Logger.LogInfo($"drone timer deregistered; new size {sleepTimers.Count}");
            }
        }

        /*static bool SensorCanWakeUp(SensorPart sensor)
        {
            if (sensor.battery.Alive && part_access(sensor).Alive)
			{
				LocalAimHandler player_instance = LocalAimHandler.player_instance;
				if (player_instance != null)
				{
					return Vector3.Angle(sensor.transform.forward, player_instance.transform.position - sensor.transform.position) < wakeupAngle.Value && Vector3.Distance(sensor.transform.position, player_instance.visual_capsule_collider.ClosestPoint(sensor.transform.position)) <= wakeUpRange.Value && !Traverse.Create(cached_raycasts_access).Method("CachedLineCast",0, sensor.transform.position, player_instance.CenterPos(), ReceiverCoreScript.Instance().layer_mask_static_world).GetValue<bool>();
				}
			}
			return false;
        }*/

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ShockDrone), "StandbyUpdate")]
        static bool StandbyUpdate(ShockDrone __instance, ref ShockDroneState __result)
        {
            // wakeup from being kicked
            if (__instance.camera_part.Alive &&
                ((__instance.motor_part.Velocity.magnitude > 3.5f && 
                    Vector3.Angle(__instance.motor_part.Velocity, __instance.motor_part.TargetPosition - __instance.motor_part.transform.position) > 45f) ||
                (__instance.motor_part.Throttle < .1f) &&
                    __instance.motor_part.Velocity.magnitude > .8f &&
                    Vector3.Distance(__instance.motor_part.transform.position, __instance.motor_part.TargetPosition) < .1f))
            {
                //Plugin.Logger.LogInfo($"kick awake {__instance.motor_part.Velocity.magnitude} {Vector3.Angle(__instance.motor_part.Velocity, __instance.motor_part.transform.position - __instance.motor_part.TargetPosition)}");
                __instance.motor_part.FacePosition = __instance.transform.position;
                __result = ShockDroneState.Idle;
                return false;
            }
            if (part_access(__instance.sensor_part).Alive && __instance.camera_part.CanSeePlayer)
            //__instance.sensor_part.CanSensePlayer())
            //SensorCanWakeUp(__instance.sensor_part))
            {
                /*AudioManager.PlayOneShot3D(__instance.sound_events.alert, __instance.transform.position, 1f, 0.66f);
                if (wakeUpTimers.ContainsKey(__instance))
                {
                    wakeUpTimers[__instance] += wakeupDelay.Value;
                }
                else
                {
                    wakeUpTimers[__instance] = wakeupDelay.Value + firstWakeupExtraDelay.Value;
                }
                if (sleepTimers.ContainsKey(__instance))
                {
                    sleepTimers[__instance] = sleepTimeout.Value + wakeUpTimers[__instance];
                }*/
                __result = ShockDroneState.Idle;
                return false;
            }
            else
            {
                return true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShockDrone), "OnExitState")]
        static void OnExitSleep(ShockDrone __instance, ShockDroneState ___state)
        {
            if (___state == ShockDroneState.Standby)
            {
                AudioManager.PlayOneShot3D(__instance.sound_events.alert, __instance.transform.position, 1f, 0.66f);
                if (wakeUpTimers.ContainsKey(__instance))
                {
                    wakeUpTimers[__instance] += wakeupDelay.Value;
                }
                else
                {
                    wakeUpTimers[__instance] = wakeupDelay.Value + firstWakeupExtraDelay.Value;
                }
                if (sleepTimers.ContainsKey(__instance))
                {
                    sleepTimers[__instance] = sleepTimeout.Value + wakeUpTimers[__instance];
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ShockDrone), "IdleUpdate")]
        static bool IdleUpdate(ShockDrone __instance, ref float ___idle_look_angle)
        {
            if (wakeUpTimers.ContainsKey(__instance))
            {
                if ((wakeUpTimers[__instance] -= Time.deltaTime) < 0)
                {
                    wakeUpTimers.Remove(__instance);
                    ___idle_look_angle = __instance.transform.rotation.eulerAngles.y;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(OscillatorPart), "UpdateCameraMovement")]
        static bool StopCameraWhileAsleep(OscillatorPart __instance, ref float ___angle, float ___speed)
        {
            if (!oscillatorToDrone.ContainsKey(__instance))
            {
                return true;
            }
            ShockDrone drone = oscillatorToDrone[__instance];
            ShockDroneState state = state_access(drone);
            if (state == ShockDroneState.Standby ||
            (state == ShockDroneState.Idle && wakeUpTimers.ContainsKey(drone)))
            {
                //level camera if landed
                if (drone.motor_part.Throttle < .1f)
                {
                    if (___angle > 0f)
                    {
                        ___angle -= ___speed * Time.deltaTime;
                        if (___angle < 0f)
                        {
                            ___angle = 0f;
                        }
                    }
                    else if (___angle < 0f)
                    {
                        ___angle += ___speed * Time.deltaTime;
                        if (___angle > 0f)
                        {
                            ___angle = 0f;
                        }
                    }
                    __instance.transform.localRotation = Quaternion.Euler(___angle, 0f, 0f);
                }
                //Plugin.Logger.LogInfo(___angle);
                return false;
            }
            else
            {
                return true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShockDrone), "IdleUpdate")]
        static void SleepTimer(ShockDrone __instance, ref ShockDroneState __result)
        {
            if (sleepyEnabled.Value && __result == ShockDroneState.Idle)
            {
                if (sleepTimers.ContainsKey(__instance) && (sleepTimers[__instance] -= Time.deltaTime) < 0)
                {
                    __result = ShockDroneState.Standby;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShockDrone), "AlertUpdate")]
        static void ResetTimer(ShockDrone __instance)
        {
            if (sleepTimers.ContainsKey(__instance))
            {
                sleepTimers[__instance] = sleepTimeout.Value;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MotorPart), "UpdateSound")]
        static void ModSleepNoise(MotorPart __instance)
        {
            ShockDrone drone = motorToDrone[__instance];
            if (state_access(drone) == ShockDroneState.Standby)
            {
                FMOD.Studio.EventInstance eim = event_instance_motor_access(__instance);
                eim.getVolume(out float v);
                eim.setVolume(v * sleepVolumeMod.Value);
            }
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ShockDrone), "TransitionToState")]
        static void ReversePatch_TransitionToState(object instance, ShockDroneState to_state)
        {
            throw new System.NotImplementedException("method ShockDrone.TransitionToState was not reverse patched");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MotorPart), "FixedUpdate")]
        static bool UpdateRotorWhileAsleep(MotorPart __instance)
        {
            ShockDrone drone = motorToDrone[__instance];
            //Plugin.Logger.LogInfo($"{__instance.Throttle} {__instance.Velocity.magnitude}");
            if (state_access(drone) == ShockDroneState.Standby && grounded.Contains(drone))
            {
                if (Physics.Raycast(drone.transform.position, new Vector3(0, -1, 0), out RaycastHit hitInfo, 5, 1 << 13 | 1 << 27, QueryTriggerInteraction.Ignore))
                {
                    __instance.TargetPosition = hitInfo.point;
                    //Plugin.Logger.LogInfo($"{Vector3.Distance(drone.transform.position, __instance.TargetPosition)} {__instance.Velocity.magnitude}");
                    if (Vector3.Distance(drone.transform.position, __instance.TargetPosition) < .1f &&
                        __instance.Velocity.magnitude < .5f)
                    {
                        throttle_set_access.Invoke(__instance,[0]);
                        return false;
                    }
                }

                /*Collider[] colliders = Physics.OverlapCapsule(drone.transform.position, drone.transform.position + new Vector3(0, -30, 0), 0.5f, 1 << 13, QueryTriggerInteraction.Ignore);
                if (colliders.Count() > 0)
                {
                    Vector3 landingSpot = colliders[0].ClosestPoint(drone.transform.position);
                    foreach (Collider collider in colliders)
                    {
                        Vector3 spot = collider.ClosestPoint(drone.transform.position);
                        if (spot.y > landingSpot.y)
                        {
                            landingSpot = spot;
                        }
                    }
                    __instance.TargetPosition = landingSpot;
                    //__instance.TargetPosition += new Vector3(0,-0.1f,0);
                    return true;
                }*/
            }
            return true;
        }
    }

    internal class SleepySecurityCameras
    {
        private static Dictionary<SecurityCamera, float> wakeUpTimers = new Dictionary<SecurityCamera, float>();
        private static Dictionary<SecurityCamera, float> sleepTimers = new Dictionary<SecurityCamera, float>();
        internal static MethodInfo CanSeePlayerAccess = AccessTools.PropertyGetter(typeof(SecurityCamera),"CanSeePlayer");
        internal static ConfigEntry<bool> startAsleepEnabled;
        internal static ConfigEntry<float> startAsleepChance;
        internal static ConfigEntry<float> wakeupDelay;
        internal static ConfigEntry<float> firstWakeupExtraDelay;
        internal static ConfigEntry<bool> sleepyEnabled;
        internal static ConfigEntry<float> sleepTimeout;
        internal static ConfigEntry<float> sleepyChance;
        internal static string[] coloredComponents = ["Assembly/Cover/Intact/viz", "Assembly/Cover/Broken/BrokenPart1/viz", "Assembly/Cover/Broken/BrokenPart2/viz", "Assembly/Cover/Broken/BrokenPart3/viz", "Assembly/Cover/Broken/BrokenPart4/viz", "Assembly/Cover/Broken/BrokenPart5/viz", "Assembly/Cover/Broken/BrokenPart6/viz", "Assembly/Cover/Broken/BrokenPart7/viz", "Assembly/Cover/Broken/BrokenPart8/viz", "Assembly/Cover/Broken/BrokenPart9/viz", "Assembly/Cover/Broken/BrokenPart10/viz", "Assembly/Cover/Broken/BrokenPart11/viz", "Assembly/Cover/Broken/BrokenPart12/viz", "Beam/Intact/viz", "Beam/Swivel/Intact/viz", "Base/Intact/viz", "Assembly/Camera/Intact/viz/Camera_Interior", "Assembly/Camera/Intact/viz/Camera_Rod"];
        internal static ConfigEntry<Color> componentColor;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SecurityCamera), "Start")]
        static void SetupSecurityCamera(SecurityCamera __instance, ref SecurityCameraState ___state)
        {
            if (startAsleepEnabled.Value && UnityEngine.Random.value < startAsleepChance.Value)
            {
			    __instance.light_part.SetTargetLightMode(LightPart.LightMode.Standby, false);
                wakeUpTimers[__instance] = firstWakeupExtraDelay.Value;
                if (sleepyEnabled.Value && UnityEngine.Random.value < sleepyChance.Value)
                {
                    sleepTimers[__instance] = sleepTimeout.Value;
                    if (sleepTimers.Count >= 50)
                    {
                        Plugin.Logger.LogWarning($"sleepy security camera register abnormally large ({sleepTimers.Count}); you have an unusual number of security cameras or they are not being deregistered");
                    }
                    foreach (string s in coloredComponents)
                    {
                        //try 
                        {
                            __instance.transform.Find(s).GetComponent<MeshRenderer>().material.color = componentColor.Value;
                        }
                        //catch
                        {
                            //Plugin.Logger.LogWarning($"cant findd component {s}");
                        }
                    }
                }
                __instance.StartCoroutine(DelayStartAsleep(__instance));
            }
        }

        static IEnumerator<WaitForSeconds> DelayStartAsleep(SecurityCamera camera)
        {
            yield return new WaitForSeconds(1f);
            ReversePatch_TransitionToState(camera, SecurityCameraState.Off);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SecurityCamera), "OnDestroy")]
        static void Deregister(SecurityCamera __instance)
        {
            wakeUpTimers.Remove(__instance);
            sleepTimers.Remove(__instance);
            //Plugin.Logger.LogInfo($"camera timer deregistered; new size {sleepTimers.Count}");
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(SecurityCamera), "TransitionToState")]
        static void ReversePatch_TransitionToState(object instance, SecurityCameraState to_state)
        {
            throw new System.NotImplementedException("method SecurityCamera.TransitionToState was not reverse patched");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SecurityCamera), "OffUpdate")]
        static bool StandbyUpdate(SecurityCamera __instance, ref SecurityCameraState __result)
        {
            if ((bool)CanSeePlayerAccess.Invoke(__instance, []))
            {
                //Plugin.Logger.LogInfo("spotted");
                __result = SecurityCameraState.Idle;
            }
            else
            {
                __result = SecurityCameraState.Off;
            }
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SecurityCamera), "OnExitState")]
        static void OnExitSleep(SecurityCamera __instance, SecurityCameraState ___state)
        {
            if (___state == SecurityCameraState.Off)
            {
                AudioManager.PlayOneShot3D(__instance.sound_events.alert, __instance.transform.position, 1f, 0.66f);
                if (wakeUpTimers.ContainsKey(__instance))
                {
                    wakeUpTimers[__instance] += wakeupDelay.Value;
                }
                else
                {
                    wakeUpTimers[__instance] = wakeupDelay.Value + firstWakeupExtraDelay.Value;
                }
                if (sleepTimers.ContainsKey(__instance))
                {
                    sleepTimers[__instance] = sleepTimeout.Value + wakeUpTimers[__instance];
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SecurityCamera), "IdleUpdate")]
        static bool IdleUpdate(SecurityCamera __instance)
        {
            if (wakeUpTimers.ContainsKey(__instance))
            {
                if ((wakeUpTimers[__instance] -= Time.deltaTime) < 0)
                {
                    wakeUpTimers.Remove(__instance);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SecurityCamera), "IdleUpdate")]
        static void SleepTimer(SecurityCamera __instance, ref SecurityCameraState __result)
        {
            if (sleepyEnabled.Value && __result == SecurityCameraState.Idle)
            {
                if (sleepTimers.ContainsKey(__instance) && (sleepTimers[__instance] -= Time.deltaTime) < 0)
                {
                    __result = SecurityCameraState.Off;
                }
            }
        }
    }

    [HarmonyPatch]
    internal class SecurityCameraLinkedEnemies
    {
        internal static AccessTools.FieldRef<CameraPart, StochasticVision> external_vision_access = AccessTools.FieldRefAccess<CameraPart, StochasticVision>("external_vision");
        internal static ConfigEntry<bool> turretsEnabled;
        internal static ConfigEntry<bool> dronesEnabled;
        internal static ConfigEntry<bool> camerasEnabled;

        private static FieldInfo camera_part_drone_access = AccessTools.Field(typeof(ShockDrone), "camera_part");
        private static FieldInfo camera_part_camera_access = AccessTools.Field(typeof(SecurityCamera), "camera_part");
        private static MethodInfo get_componenet_camera = AccessTools.Method(typeof(GameObject),nameof(GameObject.GetComponent),generics: [typeof(SecurityCamera)]);
        private static FieldInfo enemy_access = AccessTools.Field(typeof(ActiveEnemy), "enemy");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SecurityCamera), "AlertEnemies")]
        static bool AlertCameras(SecurityCamera __instance, ref List<IAlertable> ___alertable_enemies)
        {
            if (camerasEnabled.Value)
            {
                RuntimeTileLevelGenerator instance = RuntimeTileLevelGenerator.instance;
                if (instance)
                {
                    ActiveTile activeTile = instance.GetActiveTile(__instance.transform.position);
                    List<ActiveEnemy> list = new List<ActiveEnemy>();
                    list.AddRange(instance.GetActiveEnemiesInTile(activeTile.tile_position - 1));
                    list.AddRange(instance.GetActiveEnemiesInTile(activeTile.tile_position));
                    list.AddRange(instance.GetActiveEnemiesInTile(activeTile.tile_position + 1));
                    using (List<ActiveEnemy>.Enumerator enumerator = list.GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            ActiveEnemy activeEnemy = enumerator.Current;
                            if (activeEnemy.enemy != null && Vector3.Distance(activeEnemy.enemy.transform.position, __instance.transform.position) <= __instance.alert_radius)
                            {
                                //Plugin.Logger.LogInfo("components");
                                //Plugin.Logger.LogInfo(activeEnemy.enemy.GetComponents<Component>());
                                IAlertable component = activeEnemy.enemy.GetComponent<TurretScript>();
                                IAlertable alertable;
                                if ((alertable = component) == null)
                                {
                                    ShockDrone component2 = activeEnemy.enemy.GetComponent<ShockDrone>();
                                    if (component2 != null)
                                    {
                                        alertable = component2.camera_part;
                                    }
                                    else
                                    {
                                        SecurityCamera sc = activeEnemy.enemy.GetComponent<SecurityCamera>();
                                        if (sc != null) {
                                            //Plugin.Logger.LogInfo("security camera found");
                                            alertable = sc.camera_part;
                                        }
                                    }
                                }
                                IAlertable alertable2 = alertable;
                                if (alertable2 != null)
                                {
                                    ___alertable_enemies.Add(alertable2);
                                }
                            }
                        }
                        goto IL_0136;
                    }
                }
                Debug.LogWarning("SecurityCamera: RuntimeTileLevelGenerator could not be found. Used FindObjectsOfType fallback.");
                ___alertable_enemies = new List<IAlertable>();
                ___alertable_enemies.AddRange(global::UnityEngine.Object.FindObjectsOfType<CameraPart>());
                ___alertable_enemies.AddRange(global::UnityEngine.Object.FindObjectsOfType<TurretScript>());
                IL_0136:
                foreach (IAlertable alertable3 in ___alertable_enemies)
                {
                    alertable3.Alert(__instance.camera_part.Vision, LocalAimHandler.player_instance);
                }
                return false;
            }
            else
            {
                return true;
            }
        }
        /*static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            Label setAlertable2 = il.DefineLabel();
            Label ifCameraValid = il.DefineLabel();

            return new CodeMatcher(instructions)
                .MatchForward(true, new CodeMatch(OpCodes.Ldfld,camera_part_drone_access))
                .AddLabels([setAlertable2])
                .MatchBack(false, new CodeMatch(OpCodes.Ldnull))
                .RemoveInstructions(2)
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_3),
                    new CodeInstruction(OpCodes.Ldfld, enemy_access),
                    new CodeInstruction(OpCodes.Callvirt, get_componenet_camera),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Brtrue_S, ifCameraValid),
                //)
                //.InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldnull),
                    new CodeInstruction(OpCodes.Br_S, setAlertable2),
                //)
                //.Advance(2)
                //.Insert(
                    new CodeInstruction(OpCodes.Ldfld, camera_part_camera_access).WithLabels(ifCameraValid),
                    new CodeInstruction(OpCodes.Br_S, setAlertable2)
                )
                .Instructions();
        }*/

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(TurretScript), "CalculateTargetAim")]
        static Vector2 ReversePatch_CalculateTargetAim(object instance)
        {
            throw new System.NotImplementedException("method TurretScript.CalculateTargetAim was not reverse patched");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), "UpdateMotor")]
        static void TurretAimWithoutCamera(TurretScript __instance, ref float ___target_angle_x, ref float ___target_angle_y)
        {
            if (turretsEnabled.Value && !__instance.camera_alive && SleepyTurrets.state_access(__instance) == AIState.Aiming)
            {
                Vector2 vector = ReversePatch_CalculateTargetAim(__instance);
                ___target_angle_x = vector.x;
                ___target_angle_y = vector.y;
            }
        }

        [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
        [HarmonyPatch(typeof(TurretScript), "UpdateCameraAlive")]
        static void ReversePatch_UpdateCameraAlive(object instance)
        {
            //throw new System.NotImplementedException("method TurretScript.UpdateCameraAlive was not reverse patched");
            #pragma warning disable CS8321 // Local function is declared but never used - reverse patch transpile
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
                return LancerTurrets_LeadAfterVisionLoss.Transpiler(instructions, il);
            }
            #pragma warning restore CS8321 // Local function is declared but never used
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TurretScript), "UpdateCameraDestroyed")]
        static bool BypassDestroyedTurretCamera(TurretScript __instance, StochasticVision ___external_vision)
        {
            if (turretsEnabled.Value && ___external_vision != null)
            {
                //Plugin.Logger.LogInfo("turret camera bypass");
                ReversePatch_UpdateCameraAlive(__instance);
                return false;
            }
            else
            {
                return true;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TurretScript), nameof(TurretScript.IsNeutralized))]
        static bool OverrideIsNeutralized(TurretScript __instance, ref bool __result)
        {
            __result = !__instance.motor_alive || !__instance.battery_alive || !__instance.motion_sensor_alive || !__instance.ammo_alive;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TurretScript), nameof(TurretScript.IsIncapacitated))]
        static bool OverrideIsIncapacitated(TurretScript __instance, ref bool __result, ref bool ___given_up, ref bool ___fell_off_world)
        {
            __result = !__instance.battery_alive || ___given_up || ___fell_off_world || (!__instance.ammo_alive && __instance.bullets == 0);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ShockDrone), "IdleUpdate")]
        static bool BypassDestroyedDroneCamera(ShockDrone __instance, ref ShockDroneState __result)
        {
            if (dronesEnabled.Value && external_vision_access(__instance.camera_part) != null && !__instance.camera_part.CanSeePlayer)
            {
                //Plugin.Logger.LogInfo("drone camera bypass");
                __result = ShockDroneState.Alert;
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}