using HarmonyLib;
using Receiver2;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Configuration;
using System.Reflection;
using System.Dynamic;
using System;
using System.CodeDom;

namespace EnhancedEnemies.Patches
{
    [HarmonyPatch]
    internal static class MyopicTurrets
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
    }

    [HarmonyPatch]
    internal static class ShotgunTurrets
    {
        internal static ConfigEntry<bool> enabled;
        internal static ConfigEntry<float> spread;
        internal static ConfigEntry<int> pellets;
        internal static ConfigEntry<float> fireDelayMod;
        internal static ConfigEntry<float> alertDelayMod;
        internal static ConfigEntry<float> chance;
        public static CartridgeSpec _00Pellet = Init00Pellet();

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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), nameof(TurretScript.Start))]
        static void TurretSetup(TurretScript __instance, ref CartridgeSpec ___cartridge_spec)
        {
            if (UnityEngine.Random.value < chance.Value)
            {
                ___cartridge_spec = _00Pellet;
                __instance.fire_interval *= fireDelayMod.Value;
                __instance.blind_fire_interval *= fireDelayMod.Value;
            }
        }

        /*[HarmonyPostfix]
        [HarmonyPatch(typeof(TurretScript), nameof(TurretScript.Start))]
        static void ModFireDelay(TurretScript __instance)
        {
            __instance.fire_interval *= fireDelayMod.Value;
            __instance.blind_fire_interval *= fireDelayMod.Value;
        }*/

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
			if (__instance.CanSeePlayer())
			{
				if (__state == AIState.Idle)
				{
					___alert_delay *= alertDelayMod.Value;
				}
			}
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BulletTrajectoryManager), nameof(BulletTrajectoryManager.ExecuteTrajectory))]
        static bool TurretShotgunShot(BulletTrajectory trajectory)
        {
            //Plugin.Logger.LogInfo("ExecuteTrajectory");
            if (enabled.Value && trajectory.cartridge_spec.Equals(_00Pellet) &&
                (trajectory.bullet_source_entity_type == ReceiverEntityType.Turret ||
                trajectory.bullet_source_entity_type == ReceiverEntityType.CeilingTurret))
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
                    VanillaExecuteTrajectory(bt);
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        static void VanillaExecuteTrajectory(BulletTrajectory trajectory)
		{
			ReceiverEvents.TriggerEvent(ReceiverEventTypeBulletTrajectory.Created, trajectory);
			BulletTrajectoryManager.active_trajectories.Add(trajectory);
		}
    }

    [HarmonyPatch]
    internal static class SleepyTurrets
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
            if (__instance.kds.identifier == "")
            {
                __instance.kds.identifier = UnityEngine.Random.Range(int.MinValue, int.MaxValue).ToString();
            }
            int seed;
            if (!int.TryParse(__instance.kds.identifier, out seed))
            {
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }
            System.Random random = new System.Random(seed);
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
                        Plugin.Logger.LogWarning($"turret register abnormally large ({sleepTimers.Count}); you have an unusual number of turrets or they are not being deregistered");
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
                }
                if (UnityEngine.Random.value < groundedChance.Value)
                {
                    grounded.Add(__instance);
                }
                __instance.StartCoroutine(DelayStartAsleep(__instance));
            }
            if (cameraToDrone.Count >= 50)
            {
                Plugin.Logger.LogWarning($"drone register abnormally large ({cameraToDrone.Count}); you have an unusual number of drones or they are not being deregistered");
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
                        Plugin.Logger.LogWarning($"security camera register abnormally large ({sleepTimers.Count}); you have an unusual number of security cameras or they are not being deregistered");
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
                Plugin.Logger.LogInfo("spotted");
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

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(TurretScript), "UpdateCameraAlive")]
        static void ReversePatch_UpdateCameraAlive(object instance)
        {
            throw new System.NotImplementedException("method TurretScript.UpdateCameraAlive was not reverse patched");
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