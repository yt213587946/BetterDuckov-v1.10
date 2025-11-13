using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

namespace bigInventory
{
    public static class ShopModifier
    {
        private static bool patchesApplied = false;
        private static HashSet<object> alreadyModifiedInstances = new HashSet<object>();
        private static HashSet<object> alreadyModifiedRefreshChanceInstances = new HashSet<object>();

        public static void InstallPatches(Harmony harmony)
        {
            if (patchesApplied) return;

            try
            {
                // 使用更直接的方法来查找和修补目标类
                var stockShopType = AccessTools.TypeByName("Duckov.Economy.StockShop");
                if (stockShopType == null)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, "未找到 StockShop 类型", "ShopModifier");
                    return;
                }

                // 查找嵌套的 Entry 类
                var nestedTypes = stockShopType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
                Type entryType = null;
                foreach (var nestedType in nestedTypes)
                {
                    if (nestedType.Name == "Entry")
                    {
                        entryType = nestedType;
                        break;
                    }
                }

                if (entryType == null)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, "未找到 StockShop+Entry 类型", "ShopModifier");

                    return;
                }

                PatchStockMethods(harmony, entryType);

                // 修补黑市刷新时间技能
                PatchBlackMarketRefreshTimeSkill(harmony);

                // 修补黑市刷新次数技能
                PatchBlackMarketRefreshChanceSkill(harmony);

                patchesApplied = true;
                ModLogger.Log(ModLogger.Level.Regular, "成功应用商店修改补丁", "ShopModifier");
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"应用补丁时出错: {ex}", "ShopModifier");

            }
        }

        private static void PatchStockMethods(Harmony harmony, Type entryType)
        {
            try
            {
                // 修补 MaxStock 属性
                var maxStockProperty = entryType.GetProperty("MaxStock", BindingFlags.Public | BindingFlags.Instance);
                if (maxStockProperty != null && maxStockProperty.CanRead)
                {
                    var maxStockGetter = maxStockProperty.GetGetMethod();
                    if (maxStockGetter != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ShopModifier), nameof(MaxStock_Postfix));
                        harmony.Patch(maxStockGetter, postfix: postfix);
                    }
                }
                else
                {
                    ModLogger.Warn(ModLogger.Level.Regular, "未找到 MaxStock 属性或无法读取", "ShopModifier");

                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"修补库存方法时出错: {ex}", "ShopModifier");

            }
        }

        // 修补黑市刷新时间技能
        private static void PatchBlackMarketRefreshTimeSkill(Harmony harmony)
        {
            try
            {
                // 查找 ChangeBlackMarketRefreshTimeFactor 类型
                var refreshSkillType = AccessTools.TypeByName("Duckov.PerkTrees.Behaviours.ChangeBlackMarketRefreshTimeFactor");
                if (refreshSkillType == null)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, "未找到 ChangeBlackMarketRefreshTimeFactor 类型", "ShopModifier");

                    return;
                }

                // 修补 OnAwake 方法，在技能初始化时修改 amount
                var onAwakeMethod = refreshSkillType.GetMethod("OnAwake", BindingFlags.NonPublic | BindingFlags.Instance);
                if (onAwakeMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(ShopModifier), nameof(OnAwake_Postfix_RefreshTime));
                    harmony.Patch(onAwakeMethod, postfix: postfix);
                    //Debug.Log("[ShopModifier] 成功修补 ChangeBlackMarketRefreshTimeFactor.OnAwake 方法");
                }
                else
                {
                    ModLogger.Warn(ModLogger.Level.Regular, "未找到 OnAwake 方法", "ShopModifier");


                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"修补黑市刷新时间技能时出错: {ex}", "ShopModifier");

            }
        }

        // 修补黑市刷新次数技能
        private static void PatchBlackMarketRefreshChanceSkill(Harmony harmony)
        {
            try
            {
                // 查找 AddBlackMarketRefreshChance 类型
                var refreshChanceSkillType = AccessTools.TypeByName("Duckov.PerkTrees.Behaviours.AddBlackMarketRefreshChance");
                if (refreshChanceSkillType == null)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, "未找到 AddBlackMarketRefreshChance 类型", "ShopModifier");

                    return;
                }

                // 修补 OnAwake 方法，在技能初始化时修改 addAmount
                var onAwakeMethod = refreshChanceSkillType.GetMethod("OnAwake", BindingFlags.NonPublic | BindingFlags.Instance);
                if (onAwakeMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(ShopModifier), nameof(OnAwake_Postfix_RefreshChance));
                    harmony.Patch(onAwakeMethod, postfix: postfix);
                    //Debug.Log("[ShopModifier] 成功修补 AddBlackMarketRefreshChance.OnAwake 方法");
                }
                else
                {
                    ModLogger.Warn(ModLogger.Level.Regular, "未找到 AddBlackMarketRefreshChance.OnAwake 方法", "ShopModifier");

                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"修补黑市刷新次数技能时出错: {ex}", "ShopModifier");

            }
        }

        // 最大库存乘以3
        private static void MaxStock_Postfix(ref int __result)
        {
            if (!BigInventoryConfigManager.Config.EnableShopModifier) return;

            try
            {
                int original = __result;
                __result *= BigInventoryConfigManager.Config.MaxStockMultiplier;
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"修改最大库存时出错: {ex}", "ShopModifier");

            }
        }

        // OnAwake 方法的后缀补丁 - 在刷新时间技能初始化时修改 amount 值
        private static void OnAwake_Postfix_RefreshTime(object __instance)
        {
            if (!BigInventoryConfigManager.Config.EnableShopModifier) return;

            try
            {
                // 检查是否已经修改过这个实例
                if (alreadyModifiedInstances.Contains(__instance))
                    return;

                // 使用反射获取 amount 字段
                var instanceType = __instance.GetType();
                var amountField = instanceType.GetField("amount", BindingFlags.NonPublic | BindingFlags.Instance);

                if (amountField != null)
                {
                    int refreshMultiplier = BigInventoryConfigManager.Config.RefreshMultiplier;

                    if (refreshMultiplier > 0)
                    {
                        // 获取原始 amount 值
                        float originalAmount = (float)amountField.GetValue(__instance);

                        // 计算新的 amount 值
                        float newAmount = originalAmount * refreshMultiplier;

                        // 设置新的 amount 值
                        amountField.SetValue(__instance, newAmount);

                        // 标记这个实例已经修改过
                        alreadyModifiedInstances.Add(__instance);

                        //Debug.Log($"[ShopModifier] 在刷新时间技能初始化时修改 amount: {originalAmount} -> {newAmount} (倍率: {refreshMultiplier}x)");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"修改刷新时间技能初始化时出错: {ex}", "ShopModifier");

            }
        }

        // OnAwake 方法的后缀补丁 - 在刷新次数技能初始化时修改 addAmount 值
        private static void OnAwake_Postfix_RefreshChance(object __instance)
        {
            if (!BigInventoryConfigManager.Config.EnableShopModifier) return;

            try
            {
                // 检查是否已经修改过这个实例
                if (alreadyModifiedRefreshChanceInstances.Contains(__instance))
                    return;

                // 使用反射获取 addAmount 字段
                var instanceType = __instance.GetType();
                var addAmountField = instanceType.GetField("addAmount", BindingFlags.NonPublic | BindingFlags.Instance);

                if (addAmountField != null)
                {
                    int refreshMultiplier = BigInventoryConfigManager.Config.RefreshMultiplier;

                    if (refreshMultiplier > 0)
                    {
                        // 获取原始 addAmount 值
                        int originalAddAmount = (int)addAmountField.GetValue(__instance);

                        // 计算新的 addAmount 值
                           int newAddAmount = originalAddAmount * refreshMultiplier;

                        // 设置新的 addAmount 值
                        addAmountField.SetValue(__instance, newAddAmount);

                        // 标记这个实例已经修改过
                        alreadyModifiedRefreshChanceInstances.Add(__instance);

                        //Debug.Log($"[ShopModifier] 在刷新次数技能初始化时修改 addAmount: {originalAddAmount} -> {newAddAmount} (倍率: {refreshMultiplier}x)");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"修改刷新次数技能初始化时出错: {ex}", "ShopModifier");

            }
        }

        // 清理方法
        public static void Uninstall()
        {
            alreadyModifiedInstances.Clear();
            alreadyModifiedRefreshChanceInstances.Clear();
            patchesApplied = false;
            ModLogger.Log(ModLogger.Level.Regular, "已卸载商店修改", "ShopModifier");

        }
    }
}
