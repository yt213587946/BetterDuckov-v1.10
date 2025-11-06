using System;
using Duckov.Modding;
using UnityEngine;
using System.Reflection;
using ItemStatsSystem;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using HarmonyLib;
using Duckov.UI;
using System.IO;
using Duckov.Utilities;
using Duckov.Weathers;
using TMPro;
using Cysharp.Threading.Tasks;
using Duckov;

namespace bigInventory
{
    [Serializable]
    public class ModConfig
    {
        public bool EnableWeightReduction = true;
        public float WeightFactor = 0.2f;

        public bool EnableStackMultiplier = true;
        public int StackMultiplier = 3;

        public bool EnableRepairLossReduction = true;
        public bool EnableDurabilityDouble = false;
        public float DurabilityMultiplier = 2f;
        public float RepairLossMultiplier = 0.25f;

        public bool EnableActionSpeed = false;
        public float ActionSpeedMultiplier = 1.5f;

        public bool EnableInventoryCapacityPatch = true;
        public int InventoryMultiplier = 2;
        public int InventoryExtraCapacity = 0;

        public bool EnablePlayerStoragePatch = true;
        public int PlayerStorageMultiplier = 5;
        public int PlayerStorageExtraAdd = 300;

        public bool EnableAutoCollectBullets = false;
        public bool EnableOpenCollect = true;
        public bool EnableAutoUnloadGuns = false;
        public bool EnableWishlistCollect = false;
        public float MessageX = -400f;
        public float MessageY = -200f;
        public int MinAutoCollectQuality = 1;
        public float PickupRadius = 12f;
        public float DynamicScanInterval = 2f;

        public bool EnableLootBouns = false;    
        public int LootBounsMultiplier = 1;

        public bool EnableMoreEnemys = false;
        public float EnemysMultiplier = 2f;
        public bool EnableMoreBoss = false;
        public float BossMultiplier = 2f;

        public bool EnableMoreTotemslot = false;
        public bool EnableNoNegative = false;
        public bool EnableDoubleattributes = false;
        public bool EnableExtraSlots = false;
        public int ExtraTotemSlotCount = 3;

        public bool EnableEndowment = false;
        public bool EnablePerkInstantUnlock = false;

        public bool EnableShopModifier = false;
        public bool EnableBuyAll = false;
        public int MaxStockMultiplier = 3;
        public int RefreshMultiplier = 5;

        public float UIScale = 1f;

        public static void Load()
        {
            BigInventoryConfigManager.LoadConfig();
        }
    }

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private Harmony harmony;
        private const string HARMONY_ID = "com.violet.lightweightmod";

        private static bool patchesApplied = false;
        //应用退出时的清理标志
        private static bool isApplicationQuitting = false;

        protected override void OnAfterSetup()
        {
            base.OnAfterSetup();
            try
            {
                if (patchesApplied)
                {
                    Debug.Log("更好的鸭科夫mod补丁已存在，跳过重复加载。");
                    return;
                }

                // 注册应用退出事件
                Application.quitting += OnApplicationQuitting;

                // 加载配置
                BigInventoryConfigManager.LoadConfig();
                // 应用 Harmony 补丁
                harmony = new Harmony("com.violet.lightweightmod");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                EndowmentNegativeRemover.InstallPatches(harmony);
                ShopModifier.InstallPatches(harmony);
                PerkInstantUnlock.InstallPatches(harmony);
                CapacityPatchManager.InitializeAll();

                ForceSlotDisplayNamePatch.InstallDynamicPatch(harmony);
                SlotContentRestrictionDynamic.InstallSlotContentPatches(harmony);
                ExtraTotemSlotsInstaller.Install(harmony);

                GameObject go = new GameObject("BigInventory_UIListener");
                go.AddComponent<BigInventoryKeyListener>();
                UnityEngine.Object.DontDestroyOnLoad(go);

                // 初始化图腾负面效果移除
                InitializeTotemNegativeRemoval();

                // 只创建一次按键监听器
                if (GameObject.Find("BigInventory_UIListener") == null)
                {
                    GameObject listener = new GameObject("BigInventory_UIListener");
                    listener.AddComponent<BigInventoryKeyListener>();
                    UnityEngine.Object.DontDestroyOnLoad(listener);
                }

                patchesApplied = true;
                Debug.Log("更好的鸭科夫mod已注册并标记为已加载。");
            }
            catch (Exception ex)
            {
                Debug.LogError("初始化失败:" + ex);
            }
        }

        //应用退出时的处理
        private void OnApplicationQuitting()
        {
            Debug.Log("[BigInventory] 检测到应用退出，开始清理...");
            isApplicationQuitting = true;
            ForceCleanup();
        }

        //强制清理方法
        private void ForceCleanup()
        {
            try
            {
                //Debug.Log("[BigInventory] 强制清理开始...");

                // 保存配置
                BigInventoryConfigManager.SaveConfig();
                Debug.Log("[BigInventory] 配置已保存");

                // 卸载Harmony补丁
                if (harmony != null)
                {
                    harmony.UnpatchAll(HARMONY_ID);
                    harmony = null;
                    Debug.Log("[BigInventory] Harmony补丁已卸载");
                }

                patchesApplied = false;

                // 清理UI实例
                BigInventoryConfigUI uiInstance = FindObjectOfType<BigInventoryConfigUI>();
                if (uiInstance != null)
                {
                    DestroyImmediate(uiInstance.gameObject);
                    //Debug.Log("[BigInventory] UI实例已清理");
                }

                // 清理按键监听器
                BigInventoryKeyListener keyListener = FindObjectOfType<BigInventoryKeyListener>();
                if (keyListener != null)
                {
                    DestroyImmediate(keyListener.gameObject);
                    //Debug.Log("[BigInventory] 按键监听器已清理");
                }

                // 清理图腾负面效果移除器
                TotemNegativeRemoverComponent totemRemover = FindObjectOfType<TotemNegativeRemoverComponent>();
                if (totemRemover != null)
                {
                    DestroyImmediate(totemRemover.gameObject);
                    //Debug.Log("[BigInventory] 图腾负面效果移除器已清理");
                }

                Debug.Log("[BigInventory] 清理完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BigInventory] 强制清理过程中出错: {ex}");
            }
        }

        protected override void OnBeforeDeactivate()
        {
            base.OnBeforeDeactivate();
            // 如果应用正在退出，跳过清理（因为已经在OnApplicationQuitting中处理了）
            if (isApplicationQuitting)
            {
                return;
            }

            try
            {
                //Debug.Log("[BigInventory] 卸载前保存配置...");
                BigInventoryConfigManager.SaveConfig();

                if (harmony != null)
                {
                    harmony.UnpatchAll(HARMONY_ID);
                    harmony = null;
                    patchesApplied = false; // 解除标志
                    Debug.Log("更好的鸭科夫mod 所有补丁已卸载。");
                }
                // 清理 UI 实例
                BigInventoryConfigUI uiInstance = FindObjectOfType<BigInventoryConfigUI>();
                if (uiInstance != null)
                {
                    Destroy(uiInstance.gameObject);
                }

                // 清理按键监听器
                BigInventoryKeyListener keyListener = FindObjectOfType<BigInventoryKeyListener>();
                if (keyListener != null)
                {
                    Destroy(keyListener.gameObject);
                }

                // 清理图腾负面效果移除器
                TotemNegativeRemoverComponent totemRemover = FindObjectOfType<TotemNegativeRemoverComponent>();
                if (totemRemover != null)
                {
                    Destroy(totemRemover.gameObject);
                }
                // 取消注册事件
                Application.quitting -= OnApplicationQuitting;
            }
            catch (Exception ex)
            {
                Debug.LogError("更好的鸭科夫mod OnBeforeDeactivate error: " + ex);
            }
        }

        //OnDestroy作为最终保障
        private void OnDestroy()
        {
            if (!isApplicationQuitting)
            {
                Debug.Log("[BigInventory] OnDestroy被调用，执行最终清理");
                ForceCleanup();
            }
        }

        private void InitializeTotemNegativeRemoval()
        {
            // 创建永久存在的GameObject来管理图腾负面效果移除
            GameObject totemManager = new GameObject("TotemNegativeEffectManager");
            totemManager.AddComponent<TotemNegativeRemoverComponent>();
            UnityEngine.Object.DontDestroyOnLoad(totemManager);
            //Debug.Log("[BigInventory] 图腾负面效果移除器已初始化");
        }

        [HarmonyPatch(typeof(Item), nameof(Item.RecalculateTotalWeight))]
        public static class Item_RecalculateTotalWeight_Patch
        {
            // 缓存 _cachedTotalWeight 字段 setter
            private static readonly Action<object, object> _cachedTotalWeightSetter =
                ReflectionCache.CreateFieldSetter(typeof(Item), "_cachedTotalWeight");

            [HarmonyPostfix]
            public static void Postfix(Item __instance, ref float __result)
            {
                try
                {
                    if (!BigInventoryConfigManager.Config.EnableWeightReduction) return;
                    if (__instance == null) return;

                    // 只作用于普通物品（忽略随机容器内的权重计算）
                    if (__instance.GetType().FullName?.Contains("RandomContainer") == true)
                        return;

                    // 减轻重量系数（可配置化）
                    float weightFactor = BigInventoryConfigManager.Config.WeightFactor;

                    // 应用减重
                    __result *= weightFactor;

                    // 同步写回缓存字段，防止 UI 显示原始值
                    try
                    {
                        _cachedTotalWeightSetter(__instance, (float?)__result);
                    }
                    catch { /* 安全回退 */ }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"更好的鸭科夫mod 修改物品重量时出错: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(Item))]
        public static class Item_MaxStackCount_Patch
        {
            // 使用textkey集合进行识别
            private static readonly HashSet<string> drinkTextKeys = new HashSet<string>
            {
                "Item_Cola", "Item_ProteinPowder","Item_Soda", "Item_CocoMilk", "Item_Vodka", "Item_Whisky", "Item_MoonShine", "Item_Lv",
                "Item_Compass", "Item_Watch", "Item_Heat","Item_Propane", "Item_GasCanL", "Item_Pen","Item_Walkie_Talkie02","Item_FloppyDisk","Item_PressionSencer",
                "Item_Note_BlackMarket", "Item_UDisk", "Item_InformationFiles", "Item_PowerBank","Item_Newspaper","Item_ContinerS","Item_ContinerM","Item_ContinerL",
                "Item_Tissue", "Item_DuctTape", "Item_UniversalGlueA", "Item_UniversalGlueB", "Item_Clock", "Item_uPhone", "Item_TOMSUNGPhone","Item_ChineseKnot",
                "Item_Trophy","Item_Trumpet","Item_Heart","Item_Lipstick","Item_Ring","Item_Robot","Item_PowerOfWishlist","Item_WD40","Item_CarBattery","Item_Bone",
                "Item_AncientCoins","Item_ChineseLantern","Item_Radio","Item_Tambourine","Item_CDDriver","Item_CircuitBoard","Item_Note","Item_Drum4","Item_Lantern",
                "Item_Dice_2","Item_Book","Item_BicyleToy","Item_Ball","Item_Cannon","Item_AirplaneToy","Item_RingToy","Item_Cable","Item_HardDrive","Item_CrystalBattery",
                "Item_Dice","Item_Fish","Item_Rocket","Item_Yarn","Item_Xylophone","Item_Skull","Item_Scissors","Item_Accelerator","Item_CrystalBooster","Item_CrystalFiltrationUnit",
                "Item_Fish_Purple_Damselfish","Item_Fish_Red_Groupere","Item_Fish_Grey_Catshark","Item_Fish_Redfin_Snapper","Item_Fish_Green_Triggerfish","Item_Fish_Blue_Jack_Mackerel",
                "Item_Fish_Blue_Tang","Item_Fish_Green_Cod","Item_Fish_Blue_Damselfish","Item_Fish_Brown_Sardine","Item_Fish_Orange_Squirrelfish","Item_Fish_White_Cod",
                "Item_Fish_White_Perch","Item_Fish_Blue_Sailfish","Item_Fish_Red_Spotted_Grouper","Item_Fish_Yellow_Porcupinefish","Item_Fish_Red_Banded_Fish","Item_Fish_Blue_Mackerel",
                "Item_Fish_Orange_Greenfin_Fish","Item_Fish_Black_Snapper","Item_Fish_Green_Yellow_Porcupinefish","Item_Fish_Redfin_Flamefish","Item_Fish_Brown_Mullet",
                "Item_Fish_Blue_Marlin","Item_Fish_White_Angelfish","Item_Fish_Red_Goldfish","Item_Fish_Yellow_Green_Snapper","Item_Fish_Pink_Clownfish","Item_LEDX",
                "Item_Fish_Brown_and_White_Grunt","Item_Fish_Golden_Fish","Item_Kazoo","Item_Detergent","Item_Zippo","Item_Bleach","Item_ConcentratedSlurry",
            };

            private static readonly HashSet<string> grenadeTextKeys = new HashSet<string>
            {
                "Item_Grenade", "Item_Dynamite", "Item_FireGrenade","Item_DynamiteMultiple",
                "Item_ToxGrenade", "Item_SmokeGrenade", "Item_ElecGrenade","Item_FlashGrenade"
            };

            [HarmonyPatch("MaxStackCount", MethodType.Getter)]
            [HarmonyPostfix]
            public static void Postfix(Item __instance, ref int __result)
            {
                try
                {
                    if (!BigInventoryConfigManager.Config.EnableStackMultiplier)
                        return;

                    // 直接使用DisplayNameRaw获取textkey
                    string textKey = __instance.DisplayNameRaw;

                    if (string.IsNullOrEmpty(textKey))
                    {
                        // 备用方案：如果textkey为空，回退到使用DisplayName
                        textKey = __instance.DisplayName?.ToLowerInvariant().Replace(" ", "_") ?? "";
                    }

                    // 使用textkey进行识别
                    bool isDrinkItem = drinkTextKeys.Contains(textKey);
                    bool isGrenadeItem = grenadeTextKeys.Contains(textKey);

                    // ✅ 对饮品、手雷进行堆叠优化
                    bool shouldOptimizeStack = isDrinkItem || isGrenadeItem;

                    if (shouldOptimizeStack)
                    {
                        if (__result < 5)
                            __result = 5;
                    }

                    // 应用堆叠倍数
                    if (__result > 1 || shouldOptimizeStack)
                    {
                        int oldValue = __result;
                        __result *= BigInventoryConfigManager.Config.StackMultiplier;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BigInventory] 修改堆叠数量时发生错误: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(CharacterMainControl), "UpdateAction")]
        public static class CharacterMainControl_UpdateAction_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(CharacterMainControl __instance, ref float deltaTime)
            {
                try
                {
                    if (__instance == null)
                        return true;

                    // 获取当前角色正在执行的动作
                    var currentAction = __instance.CurrentAction;

                    // 仅当当前动作是 CA_UseItem或CA_Interact时才加速
                    if (currentAction != null && (currentAction is CA_UseItem || currentAction is CA_Interact))
                    {
                        if (BigInventoryConfigManager.Config.EnableActionSpeed)
                        {
                            deltaTime *= BigInventoryConfigManager.Config.ActionSpeedMultiplier;
                            //Debug.Log($"[BigInventory] 加速生效: x{BigInventoryConfigManager.Config.ActionSpeedMultiplier}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"更好的鸭科夫mod 动作加速补丁出错: {ex}");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Item))]
        public static class Item_DurabilityLoss_Patch
        {
            [HarmonyPatch("DurabilityLoss", MethodType.Getter)]
            [HarmonyPostfix]
            public static void Postfix(Item __instance, ref float __result)
            {
                try
                {
                    if (!BigInventoryConfigManager.Config.EnableRepairLossReduction) return;
                    // 仅对可修理装备生效
                    if (__instance != null && __instance.UseDurability && __instance.Tags.Contains("Repairable"))
                    {
                        float oldValue = __result;
                        __result *= 1;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"更好的鸭科夫mod 修改 DurabilityLoss 时出错: {ex}");
                }
            }
        }



        [HarmonyPatch(typeof(ItemUtilities))]
        public static class ItemUtilities_GetRepairLossRatio_Patch
        {
            static MethodBase TargetMethod()
            {
                try
                {
                    // 查找静态扩展方法：public static float GetRepairLossRatio(this Item item)
                    var target = AccessTools.Method(typeof(ItemUtilities), "GetRepairLossRatio", new[] { typeof(Item) });
                    if (target == null)
                    {
                        Debug.LogWarning("[BigInventory] 警告：未找到 ItemUtilities.GetRepairLossRatio(Item)，跳过此补丁。");
                        return null;
                    }

                    //Debug.Log("[BigInventory] 已找到并准备补丁 ItemUtilities.GetRepairLossRatio(Item)。");
                    return target;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[BigInventory] 获取 GetRepairLossRatio 方法时出错：" + ex);
                    return null;
                }
            }

            // Postfix 补丁：修改返回值（__result）
            static void Postfix(Item item, ref float __result)
            {
                try
                {
                    if (!BigInventoryConfigManager.Config.EnableRepairLossReduction)
                        return;

                    // 按配置倍率调整修理损耗比
                    __result *= Mathf.Clamp(BigInventoryConfigManager.Config.RepairLossMultiplier, 0f, 1f);

                    //Debug.Log($"[BigInventory] 修理损耗比已修改为 {__result:P2}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BigInventory] 修改修理损耗比时出错: {ex}");
                }
            }
        }
    }
}
