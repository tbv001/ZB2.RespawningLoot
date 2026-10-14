using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RespawningLoot.Components;

namespace RespawningLoot;

[BepInPlugin(PluginGuid, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class RespawningLoot : BaseUnityPlugin
{
    internal const string PluginGuid = "com.theblackvoid.respawningloot";
    internal new static ManualLogSource Logger;
    public static ConfigEntry<int> RespawnInterval;
    public static ConfigEntry<bool> RandomizeLootTypes;

    private void Awake()
    {
        Logger = base.Logger;
        try
        {
            Configure();
            gameObject.AddComponent<LootRespawnManager>();
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private void Configure()
    {
        RespawnInterval = Config.Bind(new ConfigDefinition("Settings", "Respawn Interval"), 300,
            new ConfigDescription("Item respawning interval in seconds",
                new AcceptableValueRange<int>(1, int.MaxValue)));
        RandomizeLootTypes = Config.Bind(new ConfigDefinition("Settings", "Randomize Loot Types"), false,
            new ConfigDescription("Whether to randomize the loot types every respawn"));
    }
}
