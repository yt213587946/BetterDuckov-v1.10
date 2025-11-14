using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace bigInventory
{
    internal static class EnemyHealthMultiplier
    {
        private static Harmony Harmony => ModBehaviour.harmony;

        private static ModConfig Config => BigInventoryConfigManager.Config;

        // 用不到，遊戲中不會有調整血量導致的血量差異
        //internal static readonly HashSet<Health> processedEnemies = new HashSet<Health>();

        [HarmonyPatch(typeof(Health), "MaxHealth", MethodType.Getter)]
        public class Patch_Health_MaxHealth
        {
            private static void Postfix(Health __instance, ref float __result)
            {
                if (!Config.EnableEnemyHealthMultiply) return;
                var owner = __instance.GetComponent<CharacterMainControl>();

                if (owner != null && owner != LevelManager.Instance.MainCharacter)
                {
                    __result *= Config.EnemyHealthMultiplier;   

                    // 用不到
                    //if (!processedEnemies.Contains(__instance))
                    //{
                    //    __instance.CurrentHealth = __result;
                    //    processedEnemies.Add(__instance);
                    //}
                }
            }
        }
    }
}
