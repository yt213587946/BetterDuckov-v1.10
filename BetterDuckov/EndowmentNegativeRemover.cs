using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Endowment;
using Duckov.UI;
using HarmonyLib;
using ItemStatsSystem;
using ItemStatsSystem.Stats;
using UnityEngine;

namespace bigInventory
{
    [HarmonyPatch]
    public static class EndowmentNegativeRemover
    {
        private static bool patchesApplied = false;

        public static void InstallPatches(Harmony harmony)
        {
            if (patchesApplied) return;

            try
            {
                var originalMethod = typeof(EndowmentEntry).GetMethod("ApplyModifiers", BindingFlags.NonPublic | BindingFlags.Instance);
                if (originalMethod != null)
                {
                    harmony.Patch(originalMethod, prefix: new HarmonyMethod(typeof(EndowmentNegativeRemover), nameof(ApplyModifiers_Prefix)));
                    //Debug.Log("[EndowmentNegativeRemover] 成功应用天赋负面效果移除补丁");
                }
                else
                {
                    Debug.LogWarning("[EndowmentNegativeRemover] 未找到 ApplyModifiers 方法");
                }

                patchesApplied = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EndowmentNegativeRemover] 应用补丁时出错: {ex}");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EndowmentEntry), "ApplyModifiers")]
        private static bool ApplyModifiers_Prefix(EndowmentEntry __instance)
        {
            if (!BigInventoryConfigManager.Config.EnableEndowment)
                return true; // 继续执行原方法

            try
            {
                if (__instance == null)
                    return true;

                // 获取角色物品
                var characterItem = GetCharacterItem(__instance);
                if (characterItem == null)
                    return true;

                // 获取修饰器数组
                var modifiers = GetModifiers(__instance);
                if (modifiers == null || modifiers.Length == 0)
                    return true;

                // 创建过滤后的修饰器列表
                var filteredModifiers = new List<EndowmentEntry.ModifierDescription>();

                foreach (var modifier in modifiers)
                {
                    if (!IsNegativeModifier(modifier))
                    {
                        filteredModifiers.Add(modifier);
                    }
                    else
                    {
                        //Debug.Log($"[EndowmentNegativeRemover] 移除负面天赋修饰器: {modifier.statKey}, 值: {modifier.value}, 类型: {modifier.type}");
                    }
                }

                // 应用过滤后的修饰器
                ApplyFilteredModifiers(characterItem, filteredModifiers.ToArray(), __instance);

                return false; // 跳过原方法执行
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EndowmentNegativeRemover] 处理天赋修饰器时出错: {ex}");
                return true; // 出错时执行原方法
            }
        }

        private static bool IsNegativeModifier(EndowmentEntry.ModifierDescription modifier)
        {
            try
            {
                // 获取属性极性
                Polarity polarity = StatInfoDatabase.GetPolarity(modifier.statKey);

                bool isNegative = false;

                // 根据极性判断是否为负面效果
                if (polarity != Polarity.Negative)
                {
                    if (polarity <= Polarity.Positive)
                    {
                        // 正极性属性：值为负就是负面
                        isNegative = (modifier.value < 0f);
                    }
                }
                else
                {
                    // 负极性属性：值为正就是负面
                    isNegative = (modifier.value > 0f);
                }

                return isNegative;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EndowmentNegativeRemover] 判断修饰器极性时出错: {ex}");
                return false;
            }
        }

        private static Item GetCharacterItem(EndowmentEntry endowment)
        {
            try
            {
                // 使用反射获取私有属性 CharacterItem
                var characterItemProperty = typeof(EndowmentEntry).GetProperty("CharacterItem",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (characterItemProperty != null)
                {
                    return characterItemProperty.GetValue(endowment) as Item;
                }

                // 备用方法：直接访问主角色
                if (CharacterMainControl.Main != null)
                {
                    return CharacterMainControl.Main.CharacterItem;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EndowmentNegativeRemover] 获取角色物品时出错: {ex}");
                return null;
            }
        }

        private static EndowmentEntry.ModifierDescription[] GetModifiers(EndowmentEntry endowment)
        {
            try
            {
                // 使用公共属性获取修饰器
                return endowment.Modifiers;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EndowmentNegativeRemover] 获取修饰器时出错: {ex}");
                return null;
            }
        }

        private static void ApplyFilteredModifiers(Item characterItem, EndowmentEntry.ModifierDescription[] modifiers, EndowmentEntry endowment)
        {
            try
            {
                // 先移除所有来自这个天赋的修饰器
                characterItem.RemoveAllModifiersFrom(endowment);

                // 应用过滤后的修饰器
                foreach (var modifier in modifiers)
                {
                    characterItem.AddModifier(modifier.statKey,
                        new Modifier(modifier.type, modifier.value, endowment));
                }

                //Debug.Log($"[EndowmentNegativeRemover] 已应用 {modifiers.Length} 个过滤后的天赋修饰器");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EndowmentNegativeRemover] 应用过滤修饰器时出错: {ex}");
            }
        }

        //添加一个后置补丁来记录处理结果
        [HarmonyPostfix]
        [HarmonyPatch(typeof(EndowmentEntry), "ApplyModifiers")]
        private static void ApplyModifiers_Postfix(EndowmentEntry __instance)
        {
            if (!BigInventoryConfigManager.Config.EnableNoNegative)
                return;

            try
            {
                //Debug.Log($"[EndowmentNegativeRemover] 天赋 '{__instance.DisplayName}' 修饰器处理完成");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EndowmentNegativeRemover] 后置处理时出错: {ex}");
            }
        }
    }
}