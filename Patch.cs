using System;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterCraftBonus
{
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    public class DoCraftingPatch
    {
        private static int amountBeforeCrafting;
        
        [UsedImplicitly]
        public static void Prefix(InventoryGui __instance, Player player)
        {
            __instance.m_multiCraftAmount = Math.Max(1, ConfigurationFile.multiCraftAmount.Value);
            __instance.m_craftBonusChance = Math.Max(0.01f, ConfigurationFile.craftBonusChance.Value);
            
            Recipe craftRecipe = (Recipe) GameManager.GetPrivateValue(__instance, "m_craftRecipe");
            string recipeSharedName = craftRecipe.m_item.m_itemData.m_shared.m_name;
            amountBeforeCrafting = player.GetInventory().CountItems(recipeSharedName);
        }

        [UsedImplicitly]
        public static void Postfix(InventoryGui __instance, Player player)
        {
            //Compare current amount after DoCrafting
            Recipe craftRecipe = (Recipe) GameManager.GetPrivateValue(__instance, "m_craftRecipe");
            
            string recipeSharedName = craftRecipe.m_item.m_itemData.m_shared.m_name;
            int currentAmountInInventory = player.GetInventory().CountItems(recipeSharedName);
            bool isMultiCrafting = (bool) GameManager.GetPrivateValue(__instance, "m_multiCrafting");
            int multiCraftingAmount = isMultiCrafting ? __instance.m_multiCraftAmount : 1;

            Logger.Log($"recipeSharedName: {recipeSharedName}, currentAmountInInventory: {currentAmountInInventory}, amountBeforeCrafting: {amountBeforeCrafting}, craftRecipe.m_amount: {craftRecipe.m_amount}, multiCraftingAmount: {multiCraftingAmount}");
            
            if (currentAmountInInventory - amountBeforeCrafting > craftRecipe.m_amount * multiCraftingAmount)
            {
                // there was some vanilla +1 --> complement until xN times
                int craftedInVanilla = currentAmountInInventory - amountBeforeCrafting;
                int targetAmount = craftRecipe.m_amount * Math.Max(1, ConfigurationFile.craftBonusAmount.Value) * multiCraftingAmount;
                int pendingAmountToAdd = targetAmount - craftedInVanilla;
                Logger.Log($"Preparing extras generation | craftedInVanilla: {craftedInVanilla}, targetAmount: {targetAmount}, pendingAmountToAdd: {pendingAmountToAdd}");
                
                int craftVariant = (int) GameManager.GetPrivateValue(InventoryGui.instance, "m_craftVariant");
                long playerId = player.GetPlayerID();
                string playerName = player.GetPlayerName();
                string recipeName = craftRecipe.m_item.gameObject.name;
                    
                // Add items to match the pending amount to create
                for (int i = 0; i < pendingAmountToAdd; i++)
                {
                    Inventory inventory = player.GetInventory();
                    if (inventory.CanAddItem(craftRecipe.m_item.gameObject, 1))
                    {
                        //Add to inventory
                        ItemDrop.ItemData crafted = inventory.AddItem(recipeName, 1, 1, craftVariant, playerId, playerName);
                        Logger.Log("Extra item created: " + crafted);
                    }
                    else
                    {
                        //Drop on the floor with RPC to be able to see it everyone
                        ZRoutedRpc.instance.InvokeRoutedRPC("RPC_DropItemFloor", recipeName, player.transform.position);
                    }
                }
            }
            else
            {
                Logger.Log("No luck!");
            }
        }
    }
    
    [HarmonyPatch(typeof(DamageText), "ShowText", typeof(DamageText.TextType), typeof(Vector3), typeof(string), typeof(bool))]
    public class DamageTextPatch
    {
        [UsedImplicitly]
        public static void Prefix(DamageText __instance, DamageText.TextType type, Vector3 pos, ref string text, bool player)
        {
            Logger.Log("ShowText Prefix");
            
            if (type == DamageText.TextType.Bonus && player && Player.m_localPlayer != null)
            {
                Player localPlayer = Player.m_localPlayer;
                CraftingStation currentCraftingStation = localPlayer.GetCurrentCraftingStation();
                Recipe craftRecipe = (Recipe) GameManager.GetPrivateValue(InventoryGui.instance, "m_craftRecipe");
                
                Logger.Log($"ShowText | Crafting: {craftRecipe.name}, skill: {currentCraftingStation.m_craftingSkill}, maxStackSize: {craftRecipe.m_item.m_itemData.m_shared.m_maxStackSize}");
                if (currentCraftingStation != null && currentCraftingStation.m_craftingSkill != Skills.SkillType.None &&
                    craftRecipe != null && craftRecipe.m_item.m_itemData.m_shared.m_maxStackSize > 1)
                {
                    text = $"x{ConfigurationFile.craftBonusAmount.Value}";
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(Game), "Start")]
    public class RPCsPatch
    {
        [UsedImplicitly]
        private static void Prefix()
        {
            ZRoutedRpc.instance.Register("RPC_DropItemFloor", new Action<long, string, Vector3>(RPC_DropItemFloor));
        }

        private static void RPC_DropItemFloor(long sender, string recipeName, Vector3 playerPosition)
        {
            if (!ZNet.instance.IsServer()) return;

            var prefab = ZNetScene.instance.GetPrefab(recipeName);
            if (prefab == null) return;

            var go = Object.Instantiate(prefab, playerPosition, Quaternion.identity);
            var zNetView = go.GetComponent<ZNetView>();
            if (zNetView != null)
            {
                var zdo = zNetView.GetZDO();
                zdo.Set(ZDOVars.s_creator, sender);
            }
            
            Logger.Log("Extra item "+recipeName+" created on the floor due to full inventory.");
        }
    }
}
