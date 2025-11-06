using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Duckov.Utilities;
using ItemStatsSystem;
using Duckov.ItemUsage;
using System.Collections.Concurrent;

namespace bigInventory
{
    [HarmonyPatch]
    public static class LootPatch_Main
    {
        [HarmonyPatch(typeof(LootSpawner), "Setup")]
        [HarmonyPrefix]
        public static void LootSpawner_Setup_Prefix(LootSpawner __instance)
        {
            try
            {
                if (!BigInventoryConfigManager.Config.EnableLootBouns) return;
                ModifyLootCount(__instance, "LootSpawner");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Patch Error] LootSpawner 修改失败: {e}");
            }
        }

        [HarmonyPatch(typeof(LootBoxLoader), "Setup")]
        [HarmonyPrefix]
        public static void LootBoxLoader_Setup_Prefix(LootBoxLoader __instance)
        {
            try
            {
                if (!BigInventoryConfigManager.Config.EnableLootBouns) return;
                ModifyLootCount(__instance, "LootBoxLoader");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Patch Error] LootBoxLoader 修改失败: {e}");
            }
        }

        // 缓存 Field getter/setter
        private static readonly ConcurrentDictionary<Type, Func<object, object>> RandomCountGetterCache =
            new ConcurrentDictionary<Type, Func<object, object>>();
        private static readonly ConcurrentDictionary<Type, Action<object, object>> RandomCountSetterCache =
            new ConcurrentDictionary<Type, Action<object, object>>();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> RandomPoolGetterCache =
            new ConcurrentDictionary<Type, Func<object, object>>();

        private static void ModifyLootCount(object instance, string tag)
        {
            if (instance == null) return;
            var type = instance.GetType();

            try
            {
                // 获取 randomCount getter/setter
                if (!RandomCountGetterCache.TryGetValue(type, out var getter))
                {
                    getter = ReflectionCache.CreateFieldGetter(type, "randomCount");
                    RandomCountGetterCache[type] = getter;
                }

                if (!RandomCountSetterCache.TryGetValue(type, out var setter))
                {
                    setter = ReflectionCache.CreateFieldSetter(type, "randomCount");
                    RandomCountSetterCache[type] = setter;
                }

                if (getter == null || setter == null)
                {
                    Debug.LogWarning($"[LootPatch] {tag}: 找不到 randomCount 字段。");
                    return;
                }

                var oldValueObj = getter(instance);
                if (!(oldValueObj is Vector2Int oldValue))
                {
                    Debug.LogWarning($"[LootPatch] {tag}: randomCount 类型不符。");
                    return;
                }

                int oldMin = oldValue.x;
                int oldMax = oldValue.y;
                int newMin = oldMin;
                int newMax = oldMax;

                // 按规则动态提升掉落量
                if (oldMax >= 6)
                {
                    newMax += 3 * BigInventoryConfigManager.Config.LootBounsMultiplier;
                    newMin += 3 * BigInventoryConfigManager.Config.LootBounsMultiplier;
                }
                else if (oldMax >= 3)
                {
                    newMax += 2 * BigInventoryConfigManager.Config.LootBounsMultiplier;
                    newMin += 2 * BigInventoryConfigManager.Config.LootBounsMultiplier; 
                }
                else
                {
                    newMax += 1 * BigInventoryConfigManager.Config.LootBounsMultiplier; 
                    newMin += 1 * BigInventoryConfigManager.Config.LootBounsMultiplier; 
                }

                // 强制掉落数量 = 最大值（不再使用范围随机）
                newMin = newMax;

                // 写回修改后的值
                setter(instance, new Vector2Int(newMin, newMax));

                //Debug.Log($"[LootPatch] {tag}: 掉落数量已修改为固定 {newMax} 个（原范围: {oldMin}-{oldMax}）");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModifyLootCount] {tag} 出错: {ex}");
            }
        }
    }
}
