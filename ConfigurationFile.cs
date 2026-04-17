using BepInEx.Configuration;
using BepInEx;
using ServerSync;
using System;
using System.IO;

namespace BetterCraftBonus
{
    internal class ConfigurationFile
    {
        private static ConfigEntry<bool> _serverConfigLocked = null;
        public static ConfigEntry<bool> debug;
        
        public static ConfigEntry<int> craftBonusAmount;
        public static ConfigEntry<float> craftBonusChance;
        public static ConfigEntry<int> multiCraftAmount;

        public static ConfigFile configFile;
        private static readonly string ConfigFileName = BetterCraftBonus.GUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        public static readonly ConfigSync ConfigSync = new ConfigSync(BetterCraftBonus.GUID)
        {
            DisplayName = BetterCraftBonus.NAME,
            CurrentVersion = BetterCraftBonus.VERSION,
            MinimumRequiredVersion = BetterCraftBonus.VERSION
        };

        internal static void LoadConfig(BaseUnityPlugin plugin)
        {
            {
                configFile = plugin.Config;

                _serverConfigLocked = config("1 - General", "Lock Configuration", true, new ConfigDescription("If on, the configuration is locked and can be changed by server admins only."));
                _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
                debug = config("1 - General", "DebugMode", false, new ConfigDescription("Enabling/Disabling the debugging in the console (default = false)"), false);
                
                craftBonusAmount = config("2 - Config", "Craft Bonus Multiplier", 2, new ConfigDescription("Multiplier to apply when the bonus is triggered (default = 2)"));
                craftBonusChance = config("2 - Config", "Craft Bonus Chance", 0.25f, new ConfigDescription("Probability of craft bonus to happen against your current skill level (default = 0.25, which is 25% chance at skill = 100)", new AcceptableValueRange<float>(0.01f, 1)));
                multiCraftAmount = config("2 - Config", "Multi Craft Amount", 5, new ConfigDescription("Set up the amount to craft when Shift is pressed while crafting in craft stations (default = 5 as in vanilla)"));
                
                SetupWatcher();
            }
        }

        private static void SetupWatcher()
        {
            FileSystemWatcher watcher = new FileSystemWatcher(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private static void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                Logger.Log("Attempting to reload configuration...");
                configFile.Reload();
                SettingsChanged(null, null);
            }
            catch
            {
                Logger.LogError($"There was an issue loading {ConfigFileName}");
            }
        }

        private static void SettingsChanged(object sender, EventArgs e)
        {
            InventoryGui.instance.m_multiCraftAmount = Math.Max(1, multiCraftAmount.Value);
            InventoryGui.instance.m_craftBonusChance = Math.Max(0.01f, craftBonusChance.Value);
        }

        private static ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private static ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new ConfigDescription(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = configFile.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }
    }
}
