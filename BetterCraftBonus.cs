using BepInEx;
using HarmonyLib;

namespace BetterCraftBonus
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class BetterCraftBonus : BaseUnityPlugin
    {
        public const string GUID = "Turbero.BetterCraftBonus";
        public const string NAME = "Better Craft Bonus";
        public const string VERSION = "1.0.0";

        private readonly Harmony harmony = new Harmony(GUID);

        void Awake()
        {
            ConfigurationFile.LoadConfig(this);

            harmony.PatchAll();
        }

        void onDestroy()
        {
            harmony.UnpatchSelf();
        }
    }
}
