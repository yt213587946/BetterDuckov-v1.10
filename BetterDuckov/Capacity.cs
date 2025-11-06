using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using UnityEngine;

namespace bigInventory
{
    public static class EquipmentEventDetector
    {
        private static bool isInitialized = false;
        private static readonly HashSet<string> equipmentSlotKeys = new HashSet<string>
        {
            "Armor", "Totem", "Backpack"
        };

        public static event Action<CharacterMainControl> OnEquipmentChanged;

        public static void Initialize()
        {
            if (isInitialized) return;

            try
            {
                CharacterMainControl.OnMainCharacterSlotContentChangedEvent += OnSlotContentChanged;
                isInitialized = true;
                //Debug.Log("[EquipmentEventDetector] 装备变化检测器初始化成功");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentEventDetector] 初始化失败: {ex}");
            }
        }

        public static void Uninitialize()
        {
            if (!isInitialized) return;

            try
            {
                CharacterMainControl.OnMainCharacterSlotContentChangedEvent -= OnSlotContentChanged;
                isInitialized = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentEventDetector] 卸载失败: {ex}");
            }
        }

        private static void OnSlotContentChanged(CharacterMainControl character, Slot slot)
        {
            if (character == null || slot == null) return;

            try
            {
                if (IsCapacityAffectingSlot(slot))
                {
                    //Debug.Log($"[EquipmentEventDetector] 容量相关槽位变化: {slot.Key}, 物品: {slot.Content?.DisplayName}");
                    OnEquipmentChanged?.Invoke(character);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentEventDetector] 处理槽位变化时出错: {ex}");
            }
        }

        private static bool IsCapacityAffectingSlot(Slot slot)
        {
            if (string.IsNullOrEmpty(slot.Key)) return false;
            string slotKey = slot.Key;
            return equipmentSlotKeys.Any(equipmentKey =>
                slotKey.Equals(equipmentKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    // 基于哈希值的精确缓存方案
    [HarmonyPatch(typeof(CharacterMainControl))]
    public static class CharacterMainControl_InventoryCapacity_Patch
    {
        private static float cachedCapacity = -1f;
        private static int lastEquipmentHash = 0;
        private static bool isPatched = false;
        private static int totalCalls = 0;
        private static int cacheHits = 0;

        public static void Initialize()
        {
            if (isPatched) return;

            try
            {
                EquipmentEventDetector.Initialize();
                EquipmentEventDetector.OnEquipmentChanged += OnEquipmentChanged;
                isPatched = true;
               // Debug.Log("[CharacterMainControl_InventoryCapacity_Patch] 背包容量补丁初始化成功（哈希缓存方案）");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterMainControl_InventoryCapacity_Patch] 初始化失败: {ex}");
            }
        }

        public static void Uninitialize()
        {
            if (!isPatched) return;

            try
            {
                EquipmentEventDetector.OnEquipmentChanged -= OnEquipmentChanged;
                EquipmentEventDetector.Uninitialize();
                isPatched = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterMainControl_InventoryCapacity_Patch] 卸载失败: {ex}");
            }
        }

        private static void OnEquipmentChanged(CharacterMainControl character)
        {
            if (character == null) return;

            // 装备变化时立即清空缓存，强制下次重新计算
            cachedCapacity = -1f;
            lastEquipmentHash = 0;
            //Debug.Log("[CharacterMainControl_InventoryCapacity_Patch] 装备变化，清空缓存");
        }

        [HarmonyPatch("InventoryCapacity", MethodType.Getter)]
        [HarmonyPostfix]
        public static void Postfix(CharacterMainControl __instance, ref float __result)
        {
            try
            {
                if (!BigInventoryConfigManager.Config.EnableInventoryCapacityPatch)
                    return;

                totalCalls++;

                // 计算当前装备状态的哈希值
                int currentHash = CalculateEquipmentHash(__instance);

                // 如果装备状态未变化且缓存有效，直接返回缓存值
                if (currentHash == lastEquipmentHash && cachedCapacity >= 0f)
                {
                    cacheHits++;
                    __result = cachedCapacity;

                    // 每1000次输出一次缓存命中率
                    if (totalCalls % 1000 == 0)
                    {
                        float hitRate = (float)cacheHits / totalCalls * 100f;
                        //Debug.Log($"[CharacterMainControl_InventoryCapacity_Patch] 缓存统计: 总调用{totalCalls}次, 命中{cacheHits}次, 命中率{hitRate:F1}%");
                    }
                    return;
                }

                // 需要重新计算
                float oldValue = __result;
                float extra = BigInventoryConfigManager.Config.InventoryExtraCapacity;
                float mul = BigInventoryConfigManager.Config.InventoryMultiplier;
                float newCapacity = __result * mul + extra;

                // 更新缓存
                cachedCapacity = newCapacity;
                lastEquipmentHash = currentHash;
                __result = newCapacity;

                //Debug.Log($"[CharacterMainControl_InventoryCapacity_Patch] 背包容量重新计算: {oldValue} -> {newCapacity}, 装备哈希: {currentHash}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"更好的鸭科夫mod 修改背包容量时发生错误: {ex}");
            }
        }

        // 计算装备状态哈希值
        private static int CalculateEquipmentHash(CharacterMainControl character)
        {
            if (character?.CharacterItem?.Slots == null) return 0;

            unchecked
            {
                int hash = 17;

                // 只计算影响容量的装备槽位
                string[] capacitySlots = { "Armor", "Totem", "Backpack" };

                foreach (string slotKey in capacitySlots)
                {
                    var slot = character.CharacterItem.Slots.GetSlot(slotKey);
                    var item = slot?.Content;

                    // 组合槽位名称和物品ID的哈希
                    hash = hash * 31 + slotKey.GetHashCode();
                    hash = hash * 31 + (item?.GetInstanceID() ?? 0);
                    hash = hash * 31 + (item?.StackCount ?? 0);
                }

                return hash;
            }
        }

        // 场景变化时重置
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelManager), "OnDestroy")]
        public static void OnSceneUnload()
        {
            cachedCapacity = -1f;
            lastEquipmentHash = 0;
            totalCalls = 0;
            cacheHits = 0;
            //Debug.Log("[CharacterMainControl_InventoryCapacity_Patch] 场景卸载，重置所有缓存");
        }

        // 配置变化时重新计算
        public static void OnConfigChanged()
        {
            cachedCapacity = -1f;
            lastEquipmentHash = 0;
            //Debug.Log("[CharacterMainControl_InventoryCapacity_Patch] 配置变化，重置缓存");
        }
    }

    // 保持原有的储物箱容量补丁（不做修改）
    [HarmonyPatch(typeof(PlayerStorage))]
    public static class PlayerStorage_Capacity_Patch
    {
        // 缓存可能的 SetCapacity 方法调用器 & inventory 字段 getter
        private static readonly Func<object, object[], object> _setCapacityInvoker =
            ReflectionCache.CreateMethodInvoker(typeof(PlayerStorage), "SetCapacity", new Type[] { typeof(int) });

        private static readonly Func<object, object> _inventoryFieldGetter =
            ReflectionCache.CreateFieldGetter(typeof(PlayerStorage), "inventory");

        [HarmonyPatch("RecalculateStorageCapacity")]
        [HarmonyPostfix]
        public static void Postfix(ref int __result)
        {
            try
            {
                if (!BigInventoryConfigManager.Config.EnablePlayerStoragePatch) return;
                int originalFinalCap = __result;
                float StorageMultiplier = BigInventoryConfigManager.Config.PlayerStorageMultiplier;
                float StorageExtraAdd = BigInventoryConfigManager.Config.PlayerStorageExtraAdd;
                int newCap = (int)((originalFinalCap * StorageMultiplier) + StorageExtraAdd);

                // 强制设置 inventory 的容量为我们想要的值
                var instance = PlayerStorage.Instance;
                if (instance != null)
                {
                    try
                    {
                        // 优先通过 SetCapacity 调用（缓存 invoker）
                        if (_setCapacityInvoker != null)
                        {
                            _setCapacityInvoker(instance, new object[] { newCap });
                        }
                        else
                        {
                            // 回退：通过 inventory 字段并调用 Inventory.SetCapacity
                            var invObj = _inventoryFieldGetter(instance);
                            if (invObj is Inventory inv)
                            {
                                inv.SetCapacity(newCap);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[更好的鸭科夫mod] 通过反射设置储物箱容量失败: " + ex);
                    }
                }
                else
                {
                    Debug.LogWarning("[更好的鸭科夫mod] PlayerStorage.Instance 为 null，无法强制设置容量，稍后会重试。");
                }

                __result = newCap;

                //Debug.Log($"[更好的鸭科夫mod] RecalculateStorageCapacity 修改：原始={originalFinalCap} -> 新={newCap}");
            }
            catch (Exception ex)
            {
                Debug.LogError("[更好的鸭科夫mod] 储物箱容量Postfix出错: " + ex);
            }
        }
    }

    // 在ModBehaviour中的集成
    public static class CapacityPatchManager
    {
        public static void InitializeAll()
        {
            // 只初始化背包容量补丁
            CharacterMainControl_InventoryCapacity_Patch.Initialize();
        }

        public static void UninitializeAll()
        {
            // 只卸载背包容量补丁
            CharacterMainControl_InventoryCapacity_Patch.Uninitialize();
        }

        public static void OnConfigChanged()
        {
            CharacterMainControl_InventoryCapacity_Patch.OnConfigChanged();
            // 储物箱容量保持原有逻辑，会在下次访问时自动重新计算
        }
    }
}