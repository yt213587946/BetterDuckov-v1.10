using HarmonyLib;
using ItemStatsSystem;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace bigInventory
{
    [HarmonyPatch]
    public class DurabilityMultiplierPatch
    {
        // 使用更高效的数据结构
        private static HashSet<int> processedItems = new HashSet<int>();
        private static Dictionary<int, DurabilityData> itemDurabilityData = new Dictionary<int, DurabilityData>();

        // 要处理的标签哈希（避免字符串比较）
        private static readonly HashSet<int> targetTagHashes = new HashSet<int>
        {
            "Gun".GetHashCode(),
            "Helmat".GetHashCode(),
            "Armor".GetHashCode()
        };

        private class DurabilityData
        {
            public float OriginalMaxDurability;
            public float Multiplier;
            public bool IsAdjusted;
            public bool IsInitialized;
        }

        [HarmonyPatch(typeof(Item), "get_MaxDurability")]
        public class ItemMaxDurabilityPatch
        {
            static void Postfix(Item __instance, ref float __result)
            {
                if (!BigInventoryConfigManager.Config.EnableDurabilityDouble) return;

                int instanceId = __instance.GetInstanceID();

                try
                {
                    // 快速标签检查
                    if (!HasTargetTag(__instance)) return;

                    // 确保耐久数据已初始化
                    if (!itemDurabilityData.ContainsKey(instanceId))
                    {
                        InitializeDurabilityData(__instance, instanceId);
                    }

                    // 应用双倍耐久
                    if (itemDurabilityData.TryGetValue(instanceId, out var data) && data.IsInitialized)
                    {
                        __result = data.OriginalMaxDurability * data.Multiplier;
                    }
                }
                catch (Exception e)
                {
                    ModLogger.Error(ModLogger.Level.Regular, $"在修改物品最大耐久时发生错误: {e.Message}", "DurabilityMultiplierPatch");

                }
            }
        }

        [HarmonyPatch(typeof(Item), "get_Durability")]
        public class ItemCurrentDurabilityPatch
        {
            static void Postfix(Item __instance, ref float __result)
            {
                if (!BigInventoryConfigManager.Config.EnableDurabilityDouble) return;

                int instanceId = __instance.GetInstanceID();

                // 只处理有耐久数据的物品
                if (!itemDurabilityData.TryGetValue(instanceId, out var data) || !data.IsAdjusted) return;

                try
                {
                    float maxDurability = __instance.MaxDurability;
                    if (__result > maxDurability)
                    {
                        __result = maxDurability;
                    }
                }
                catch (Exception e)
                {
                    ModLogger.Error(ModLogger.Level.Regular, $"在调整当前耐久值时发生错误: {e.Message}", "DurabilityMultiplierPatch");

                }
            }
        }

        [HarmonyPatch(typeof(Item), "Initialize")]
        public class ItemInitializePatch
        {
            static void Postfix(Item __instance)
            {
                if (!BigInventoryConfigManager.Config.EnableDurabilityDouble) return;

                try
                {
                    // 只在初始化时处理一次
                    int instanceId = __instance.GetInstanceID();

                    // 移除旧的记录（如果有）
                    processedItems.Remove(instanceId);
                    itemDurabilityData.Remove(instanceId);

                    if (HasTargetTag(__instance) && __instance.UseDurability)
                    {
                        InitializeDurabilityData(__instance, instanceId);
                        processedItems.Add(instanceId);
                    }
                }
                catch (Exception e)
                {
                    ModLogger.Error(ModLogger.Level.Regular, $"在物品初始化时应用耐久修改发生错误: {e.Message}", "DurabilityMultiplierPatch");

                }
            }
        }

        [HarmonyPatch(typeof(Item), "OnDestroy")]
        public class ItemOnDestroyPatch
        {
            static void Prefix(Item __instance)
            {
                int instanceId = __instance.GetInstanceID();
                processedItems.Remove(instanceId);
                itemDurabilityData.Remove(instanceId);
            }
        }

        // 辅助方法 - 快速标签检查
        private static bool HasTargetTag(Item item)
        {
            if (item?.Tags == null) return false;

            foreach (var tag in item.Tags)
            {
                if (tag != null && targetTagHashes.Contains(tag.name.GetHashCode()))
                {
                    return true;
                }
            }
            return false;
        }

        // 辅助方法 - 初始化耐久数据
        private static void InitializeDurabilityData(Item item, int instanceId)
        {
            if (itemDurabilityData.ContainsKey(instanceId)) return;

            float originalDurability = GetOriginalMaxDurability(item);
            if (originalDurability <= 0f) return;

            var data = new DurabilityData
            {
                OriginalMaxDurability = originalDurability,
                Multiplier = BigInventoryConfigManager.Config.DurabilityMultiplier,
                IsAdjusted = false,
                IsInitialized = true
            };

            itemDurabilityData[instanceId] = data;

            // 调整当前耐久（只执行一次）
            AdjustCurrentDurability(item, data);
            data.IsAdjusted = true;

            //Debug.Log($"已初始化物品耐久度: {item.name}, 原始耐久: {originalDurability}, 新耐久: {originalDurability * data.Multiplier}");
        }

        // 辅助方法 - 调整当前耐久
        private static void AdjustCurrentDurability(Item item, DurabilityData data)
        {
            try
            {
                float currentDurability = item.Durability;
                float newMaxDurability = data.OriginalMaxDurability * data.Multiplier;

                if (Mathf.Approximately(currentDurability, data.OriginalMaxDurability))
                {
                    item.Durability = newMaxDurability;
                }
                else if (data.OriginalMaxDurability > 0)
                {
                    float durabilityPercentage = currentDurability / data.OriginalMaxDurability;
                    item.Durability = newMaxDurability * durabilityPercentage;
                }

                //Debug.Log($"调整当前耐久度: {item.name}, 当前: {currentDurability} -> {item.Durability}, 最大: {newMaxDurability}");
            }
            catch (Exception e)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"调整当前耐久值时发生错误: {e.Message}", "DurabilityMultiplierPatch");

            }
        }

        // 辅助方法 - 获取原始最大耐久
        private static float GetOriginalMaxDurability(Item item)
        {
            try
            {
                return item.Constants.GetFloat("MaxDurability".GetHashCode(), 0f);
            }
            catch
            {
                return 0f;
            }
        }
    }

    // 简化的管理器
    public class DurabilityPatchManager : MonoBehaviour
    {
        private static DurabilityPatchManager instance;
        public static DurabilityPatchManager Instance => instance;

        public static void CreateManager()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("DurabilityPatchManager");
                instance = go.AddComponent<DurabilityPatchManager>();
                DontDestroyOnLoad(go);
            }
        }

        void Awake()
        {
            // 确保在场景切换时不清除数据
            ModLogger.Log(ModLogger.Level.Regular, $"耐久度补丁管理器已初始化", "DurabilityPatchManager");

        }
    }
}