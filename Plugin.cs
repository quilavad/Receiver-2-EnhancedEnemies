using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EnhancedEnemies.Patches;
using HarmonyLib;

namespace EnhancedEnemies;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private static Harmony _harmony;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        ShotgunTurrets.enabled = Config.Bind(
            new ConfigDefinition("Shotgun Turrets", "Enabled"),
            true,
            new ConfigDescription("Enables turrets to shoot multiple shotgun pellets instead of singular bullets")
        );
        ShotgunTurrets.spread = Config.Bind(
            new ConfigDefinition("Shotgun Turrets", "Pellet spread"),
            15f,
            new ConfigDescription("Angle, in degrees, of pellet spread cone", 
                new AcceptableValueRange<float>(0f, 180f))
        );
        ShotgunTurrets.pellets = Config.Bind(
            new ConfigDefinition("Shotgun Turrets", "Pellet count"),
            9,
            new ConfigDescription("Number of pellets per shot", 
                new AcceptableValueRange<int>(1, 300))
        );
        ShotgunTurrets.chance = Config.Bind(
            new ConfigDefinition("Shotgun Turrets", "Chance"),
            0.5f,
            new ConfigDescription("Determines the proportion of turrets that are shotgun turrets. 0 means only regular turrets, 1 mean all shotgun turrets",
                new AcceptableValueRange<float>(0f, 1f))
        );
        ShotgunTurrets.fireDelayMod = Config.Bind(
            new ConfigDefinition("Shotgun Turrets", "Fire delay multiplier"),
            4f,
            new ConfigDescription("Fire delay for shotgun turrets is multiplied by this factor. 2 means half fire rate, 0.5 means double", 
                new AcceptableValueRange<float>(0, 10))
        );
        ShotgunTurrets.alertDelayMod = Config.Bind(
            new ConfigDefinition("Shotgun Turrets", "Reaction time multiplier"),
            1.5f,
            new ConfigDescription("Delay for the first shot after being spotting for shotgun turrets is multiplied by this factor. 2 means double time to react, 0.5 means half", 
                new AcceptableValueRange<float>(0, 10))
        );
/////////////////////////////////////////////////////////////////////////////////////////////////
        SleepyTurrets.overrideLevelStartAsleepChance = Config.Bind(
            new ConfigDefinition("Turret Sleep Control", "Override level start asleep chance"),
            false,
            new ConfigDescription("Enable override of levels' start asleep chance, using \"Start asleep chance\" instead")
        );
        SleepyTurrets.startAsleepChance = Config.Bind(
            new ConfigDefinition("Turret Sleep Control", "Start asleep chance"),
            0.25f,
            new ConfigDescription("Determines the proportion of turrets that start asleep. \"Override level start asleep chance\" must be enabled to work. 0 means no turrets start asleep, 1 mean all. Vanilla values range 0 - 0.3",
                new AcceptableValueRange<float>(0f, 1f))
        );
        SleepyTurrets.wakeupDelay = Config.Bind(
            new ConfigDefinition("Turret Sleep Control", "Wakeup delay"),
            0.5f,
            new ConfigDescription("Time, in seconds, it takes before turrets can react after waking from sleep. Vanilla value is 1.5", 
                new AcceptableValueRange<float>(0f, 60f))
        );
        SleepyTurrets.firstWakeupExtraDelay = Config.Bind(
            new ConfigDefinition("Turret Sleep Control", "First wakeup extra delay"),
            1f,
            new ConfigDescription("Additional time, in seconds, it takes before turrets can react after waking from sleep for the first time. Vanilla value is 0", 
                new AcceptableValueRange<float>(0f, 60f))
        );
/////////////////////////////////////////////////////////////////////////////////////////////////
        SleepyTurrets.enabled = Config.Bind(
            new ConfigDefinition("Sleepy Turrets", "Enabled"),
            true,
            new ConfigDescription("Enables turrets that start asleep to go back to sleep")
        );
        SleepyTurrets.sleepTimeout = Config.Bind(
            new ConfigDefinition("Sleepy Turrets", "Sleep timeout"),
            10f,
            new ConfigDescription("Time, in seconds, it takes for sleepy turrets to sleep after not spotting the player", 
                new AcceptableValueRange<float>(0.01f, 60f))
        );
        SleepyTurrets.sleepyChance = Config.Bind(
            new ConfigDefinition("Sleepy Turrets", "Sleepy chance"),
            0.5f,
            new ConfigDescription("Determines the proportion of turrets which start asleep that can go back to sleep. 0 mean no sleepy turrets, 1 mean all sleepy turrets", 
                new AcceptableValueRange<float>(0f, 1f))
        );
/////////////////////////////////////////////////////////////////////////////////////////////////
        SleepyDrones.startAsleepEnabled = Config.Bind(
            new ConfigDefinition("Sleeping Drones", "Enabled"),
            true,
            new ConfigDescription("Enables drones to start asleep")
        );
        SleepyDrones.startAsleepChance = Config.Bind(
            new ConfigDefinition("Sleeping Drones", "Start asleep chance"),
            0.25f,
            new ConfigDescription("Determines the proportion of drones that start asleep. 0 means no drones start asleep, 1 mean all. Vanilla values for turrets range 0 - 0.3",
                new AcceptableValueRange<float>(0f, 1f))
        );
        SleepyDrones.wakeupDelay = Config.Bind(
            new ConfigDefinition("Sleeping Drones", "Wakeup delay"),
            0.5f,
            new ConfigDescription("Time, in seconds, it takes before drones can react after waking from sleep. Vanilla value for turrets is 1.5", 
                new AcceptableValueRange<float>(0f, 60f))
        );
        SleepyDrones.firstWakeupExtraDelay = Config.Bind(
            new ConfigDefinition("Sleeping Drones", "First wakeup extra delay"),
            1f,
            new ConfigDescription("Additional time, in seconds, it takes before drones can react after waking from sleep for the first time. Vanilla value for turrets is 0", 
                new AcceptableValueRange<float>(0f, 60f))
        );
        /*SleepyDrones.firstSleepDelay = Config.Bind(
            new ConfigDefinition("Sleeping Drones", "First sleep delay"),
            3f,
            new ConfigDescription("testing variable", 
                new AcceptableValueRange<float>(0f, 60f))
        );*/
        SleepyDrones.groundedChance = Config.Bind(
            new ConfigDefinition("Sleeping Drones", "Grounded sleep chance"),
            0.5f,
            new ConfigDescription("Determines the proportions of sleeping drones that will attempt to land and turn off its rotors. 0 means all sleeping drones will still fly, 1 mean all will attempt to land", 
                new AcceptableValueRange<float>(0f, 1f))
        );
        SleepyDrones.sleepVolumeMod = Config.Bind(
            new ConfigDefinition("Sleeping Drones", "Rotor sleep volume multiplier"),
            0.5f,
            new ConfigDescription("The volume of the rotors of non-grounded sleeping drone are multiplied by this factor. 0.5 means half as loud, 2 means twice as loud", 
                new AcceptableValueRange<float>(0f, 10f))
        );
        /*SleepyDrones.wakeupAngle = Config.Bind(
            new ConfigDefinition("Sleeping Drones", "Wakeup angle detection"),
            360f,
            new ConfigDescription("The angle, in degrees, in which sleeping drones can detected the player in order to wake up", 
                new AcceptableValueRange<float>(0f, 360f))
        );
        SleepyDrones.wakeUpRange = Config.Bind(
            new ConfigDefinition("Sleeping Drones", "Wakeup range detection"),
            10f,
            new ConfigDescription("The range at which sleeping drones can detected the player in order to wake up. The range at which drones can see in vanilla is 20", 
                new AcceptableValueRange<float>(0f, 360f))
        );*/
/////////////////////////////////////////////////////////////////////////////////////////////////
        SleepyDrones.sleepyEnabled = Config.Bind(
            new ConfigDefinition("Sleepy Drones", "Enabled"),
            true,
            new ConfigDescription("Enables drones to that start asleep to go back to sleep. \"Sleeping Drones\" must be enabled to work")
        );
        SleepyDrones.sleepTimeout = Config.Bind(
            new ConfigDefinition("Sleepy Drones", "Sleep timeout"),
            10f,
            new ConfigDescription("Time, in seconds, it takes for sleepy drones to sleep after not spotting the player", 
                new AcceptableValueRange<float>(0.01f, 60f))
        );
        SleepyDrones.sleepyChance = Config.Bind(
            new ConfigDefinition("Sleepy Drones", "Sleepy chance"),
            0.5f,
            new ConfigDescription("Determines the proportion of drones which start asleep that can go back to sleep. 0 mean no sleepy drones, 1 mean all sleepy drones",
                new AcceptableValueRange<float>(0f, 1f))
        );
/////////////////////////////////////////////////////////////////////////////////////////////////
        SleepySecurityCameras.startAsleepEnabled = Config.Bind(
            new ConfigDefinition("Sleeping Security Cameras", "Enabled"),
            true,
            new ConfigDescription("Enables security cameras to start asleep")
        );
        SleepySecurityCameras.startAsleepChance = Config.Bind(
            new ConfigDefinition("Sleeping Security Cameras", "Start asleep chance"),
            0.25f,
            new ConfigDescription("Determines the proportion of security cameras that start asleep. 0 means no security cameras start asleep, 1 mean all. Vanilla values for turrets range 0 - 0.3",
                new AcceptableValueRange<float>(0f, 1f))
        );
        SleepySecurityCameras.wakeupDelay = Config.Bind(
            new ConfigDefinition("Sleeping Security Cameras", "Wakeup delay"),
            0.5f,
            new ConfigDescription("Time, in seconds, it takes before security cameras can react after waking from sleep. Vanilla value for turrets is 1.5", 
                new AcceptableValueRange<float>(0f, 60f))
        );
        SleepySecurityCameras.firstWakeupExtraDelay = Config.Bind(
            new ConfigDefinition("Sleeping Security Cameras", "First wakeup extra delay"),
            1f,
            new ConfigDescription("Additional time, in seconds, it takes before security cameras can react after waking from sleep for the first time. Vanilla value for turrets is 0", 
                new AcceptableValueRange<float>(0f, 60f))
        );
/////////////////////////////////////////////////////////////////////////////////////////////////
        SleepySecurityCameras.sleepyEnabled = Config.Bind(
            new ConfigDefinition("Sleepy Security Cameras", "Enabled"),
            true,
            new ConfigDescription("Enables security cameras to that start asleep to go back to sleep. \"Sleeping Security Cameras\" must be enabled to work")
        );
        SleepySecurityCameras.sleepTimeout = Config.Bind(
            new ConfigDefinition("Sleepy Security Cameras", "Sleep timeout"),
            10f,
            new ConfigDescription("Time, in seconds, it takes for sleepy security cameras to sleep after not spotting the player", 
                new AcceptableValueRange<float>(0.01f, 60f))
        );
        SleepySecurityCameras.sleepyChance = Config.Bind(
            new ConfigDefinition("Sleepy Security Cameras", "Sleepy chance"),
            0.5f,
            new ConfigDescription("Determines the proportion of security cameras which start asleep that can go back to sleep. 0 mean no sleepy security cameras, 1 mean all sleepy security cameras",
                new AcceptableValueRange<float>(0f, 1f))
        );
/////////////////////////////////////////////////////////////////////////////////////////////////
        SecurityCameraLinkedEnemies.turretsEnabled = Config.Bind(
            new ConfigDefinition("Security Camera Link", "Turrets enabled"),
            true,
            new ConfigDescription("Enables turrets to activate through security cameras even if its camera is destroyed")
        );
        SecurityCameraLinkedEnemies.dronesEnabled = Config.Bind(
            new ConfigDefinition("Security Camera Link", "Drones enabled"),
            true,
            new ConfigDescription("Enables drones to activate through security cameras even if its camera is destroyed")
        );
        SecurityCameraLinkedEnemies.camerasEnabled = Config.Bind(
            new ConfigDefinition("Security Camera Link", "Cameras enabled"),
            true,
            new ConfigDescription("Enables security cameras to activate through other security cameras (they still need their own camera though)")
        );

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        //_harmony.PatchAll(typeof(MyopicTurrets));
        _harmony.PatchAll(typeof(ShotgunTurrets));
        _harmony.PatchAll(typeof(SleepyTurrets));
        _harmony.PatchAll(typeof(SleepyDrones));
        _harmony.PatchAll(typeof(SleepySecurityCameras));
        _harmony.PatchAll(typeof(SecurityCameraLinkedEnemies));
    }
}
