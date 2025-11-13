using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace bigInventory
{
    public static class PerkInstantUnlock
    {
        private static bool patchesApplied = false;

        public static void InstallPatches(Harmony harmony)
        {
            if (patchesApplied) return;

            try
            {
                var perkType = AccessTools.TypeByName("Duckov.PerkTrees.Perk");
                if (perkType == null)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, $"未找到 Perk 类型", "PerkInstantUnlock");

                    return;
                }

                // 补丁 GetRemainingTime 方法，使其总是返回 TimeSpan.Zero
                var getRemainingTimeMethod = perkType.GetMethod("GetRemainingTime", BindingFlags.Public | BindingFlags.Instance);
                if (getRemainingTimeMethod != null)
                {
                    harmony.Patch(getRemainingTimeMethod,
                        prefix: new HarmonyMethod(typeof(PerkInstantUnlock), nameof(GetRemainingTime_Prefix)));
                    //Debug.Log("[PerkInstantUnlock] 成功修补 GetRemainingTime 方法");
                }

                // 补丁 GetProgress01 方法，使其总是返回 1（100%进度）
                var getProgress01Method = perkType.GetMethod("GetProgress01", BindingFlags.Public | BindingFlags.Instance);
                if (getProgress01Method != null)
                {
                    harmony.Patch(getProgress01Method,
                        prefix: new HarmonyMethod(typeof(PerkInstantUnlock), nameof(GetProgress01_Prefix)));
                   //Debug.Log("[PerkInstantUnlock] 成功修补 GetProgress01 方法");
                }

                // 补丁 ConfirmUnlock 方法，使其跳过时间检查
                var confirmUnlockMethod = perkType.GetMethod("ConfirmUnlock", BindingFlags.Public | BindingFlags.Instance);
                if (confirmUnlockMethod != null)
                {
                    harmony.Patch(confirmUnlockMethod,
                        prefix: new HarmonyMethod(typeof(PerkInstantUnlock), nameof(ConfirmUnlock_Prefix)));
                    //Debug.Log("[PerkInstantUnlock] 成功修补 ConfirmUnlock 方法");
                }

                patchesApplied = true;
                ModLogger.Log(ModLogger.Level.Regular, $"成功应用天赋即时解锁补丁", "PerkInstantUnlock");


            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"应用补丁时出错: {ex}", "PerkInstantUnlock");
            }
        }

        [HarmonyPrefix]
        private static bool GetRemainingTime_Prefix(ref TimeSpan __result)
        {
            if (!BigInventoryConfigManager.Config.EnablePerkInstantUnlock)
                return true;

            // 总是返回0剩余时间
            __result = TimeSpan.Zero;
            return false; // 跳过原方法
        }

        [HarmonyPrefix]
        private static bool GetProgress01_Prefix(ref float __result)
        {
            if (!BigInventoryConfigManager.Config.EnablePerkInstantUnlock)
                return true;

            // 总是返回100%进度
            __result = 1f;
            return false; // 跳过原方法
        }

        [HarmonyPrefix]
        private static bool ConfirmUnlock_Prefix(object __instance, ref bool __result)
        {
            if (!BigInventoryConfigManager.Config.EnablePerkInstantUnlock)
                return true;

            try
            {
                // 使用反射获取实例的字段和方法
                var unlockedField = __instance.GetType().GetField("Unlocked", BindingFlags.Public | BindingFlags.Instance);
                var unlockedProperty = __instance.GetType().GetProperty("Unlocked", BindingFlags.Public | BindingFlags.Instance);
                var unlockingField = __instance.GetType().GetField("unlocking", BindingFlags.NonPublic | BindingFlags.Instance);
                var masterField = __instance.GetType().GetField("master", BindingFlags.NonPublic | BindingFlags.Instance);

                // 检查是否已解锁
                bool isUnlocked = false;
                if (unlockedField != null)
                {
                    isUnlocked = (bool)unlockedField.GetValue(__instance);
                }
                else if (unlockedProperty != null)
                {
                    isUnlocked = (bool)unlockedProperty.GetValue(__instance);
                }

                if (isUnlocked)
                {
                    __result = false;
                    return false;
                }

                // 检查是否在解锁过程中
                bool isUnlocking = unlockingField != null && (bool)unlockingField.GetValue(__instance);
                if (!isUnlocking)
                {
                    __result = false;
                    return false;
                }

                // 立即完成解锁
                if (unlockedField != null)
                {
                    unlockedField.SetValue(__instance, true);
                }
                else if (unlockedProperty != null)
                {
                    unlockedProperty.SetValue(__instance, true);
                }

                if (unlockingField != null)
                {
                    unlockingField.SetValue(__instance, false);
                }

                // 通知状态变化
                if (masterField != null)
                {
                    var master = masterField.GetValue(__instance);
                    var notifyMethod = master?.GetType().GetMethod("NotifyChildStateChanged", BindingFlags.Public | BindingFlags.Instance);
                    notifyMethod?.Invoke(master, new object[] { __instance });
                }

                // 触发解锁确认事件
                var onPerkUnlockConfirmedField = __instance.GetType().GetField("OnPerkUnlockConfirmed", BindingFlags.Public | BindingFlags.Static);
                if (onPerkUnlockConfirmedField != null)
                {
                    var onPerkUnlockConfirmed = onPerkUnlockConfirmedField.GetValue(null) as Action<object>;
                    onPerkUnlockConfirmed?.Invoke(__instance);
                }

                __result = true;
                return false; // 跳过原方法
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"ConfirmUnlock 前缀出错: {ex}", "PerkInstantUnlock");

                return true; // 出错时执行原方法
            }
        }
    }
}