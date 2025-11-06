using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using ItemStatsSystem;
using Duckov.UI;
using FMOD;
using Duckov.Utilities;
using TMPro;
using System.Reflection;
using Duckov;

namespace bigInventory
{
    [HarmonyPatch]
    public static class AutoPickupSystem
    {
        private static CharacterMainControl player;
        private static float pickupRadius = BigInventoryConfigManager.Config.PickupRadius;  //拾取范围
        private static bool initialized = false;
        private static bool sceneFullyLoaded = false;

        private static List<InteractableLootbox> cachedLootboxes = new List<InteractableLootbox>();
        private static float cacheUpdateTimer = 0f;
        private static float cacheUpdateInterval = 5f; // 更新缓存
        private static float cacheClearTimer = 0f;
        private static readonly float cacheClearInterval = 90f; // 90秒缓存清理间隔

        private static HashSet<InteractableLootbox> processedLootboxes = new HashSet<InteractableLootbox>();
        private static bool EnableOpenCollect => BigInventoryConfigManager.Config.EnableOpenCollect;
        // 限制"背包已满"提示频率
        private static float lastFullMessageTime = -999f;
        private static readonly float fullMessageCooldown = 20f;

        // 记录已经执行过卸弹的枪械（基于库存索引）
        private static HashSet<string> unloadedGunRecords = new HashSet<string>();

        //LevelManager 初始化完成事件处理
        private static void OnLevelFullyInitialized()
        {
            sceneFullyLoaded = true;
            UnityEngine.Debug.Log("[AutoPickupSystem] 场景完全初始化完成，开始自动拾取扫描");
        }

        // 清理事件订阅，在场景切换时清空缓存
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelManager), "OnDestroy")]
        public static void OnLevelManagerDestroy(LevelManager __instance)
        {
            if (!__instance.gameObject.scene.isLoaded)
                return; // 场景卸载时才清理
            LevelManager.OnAfterLevelInitialized -= OnLevelFullyInitialized;
            sceneFullyLoaded = false;
            player = null;
            initialized = false;
            cachedLootboxes.Clear(); // 清空缓存
            processedLootboxes.Clear(); // 清理已处理记录
            cacheUpdateTimer = 0f;
            cacheClearTimer = 0f; // 重置清理定时器
            ClearUnloadedGunsRecord(); // 清理卸弹记录
            UnityEngine.Debug.Log("[AutoPickupSystem] 清理完成");
        }

        // 自动拾取禁用Tag
        private static readonly HashSet<string> bannedTags = new HashSet<string>
        {
             "Grip", "Magazine", "Muzzle", "Scope", "Stock", "TecEquip"
        };
        // 使用textkey集合进行识别
        private static readonly HashSet<string> autoCollectTextKeys = new HashSet<string>
        {
            "Item_Cash", "Item_ColdCore", "Item_FlamingCore","Item_Feather", "Item_BlackDogTag", "Item_SpaceCrystal",
        };

        // 初始化入口,使用 LevelManager 现有的静态事件
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelManager), "Start")]
        public static void OnLevelStart()
        {
            if (initialized) return;
            initialized = true;
            sceneFullyLoaded = false;

            // 订阅 LevelManager 的初始化完成事件
            LevelManager.OnAfterLevelInitialized += OnLevelFullyInitialized;

            GameObject host = new GameObject("[AutoPickupSystem]");
            GameObject.DontDestroyOnLoad(host);
            host.AddComponent<AutoPickupRunner>();
            UnityEngine.Debug.Log("[AutoPickupSystem] 初始化完成");
        }

        private static CharacterMainControl FindPlayer()
        {
            var lm = LevelManager.Instance;
            if (lm?.MainCharacter != null)
                return lm.MainCharacter;
            return UnityEngine.Object.FindObjectOfType<CharacterMainControl>();
        }

        // 主逻辑：执行一次扫描与拾取
        public static void PerformAutoPickup()
        {
            if (EnableOpenCollect) return;
            if (!BigInventoryConfigManager.Config.EnableAutoCollectBullets &&!BigInventoryConfigManager.Config.EnableWishlistCollect)
                return;

            // 只有在场景完全初始化后才开始扫描
            if (!sceneFullyLoaded)
                return;

            // 检查是否在基地地图
            if (LevelManager.Instance != null && LevelManager.Instance.IsBaseLevel)
            {
                return;
            }

            if (player == null)
            {
                player = FindPlayer();
                if (player == null)
                {
                    sceneFullyLoaded = false; // 重置状态
                    return;
                }
            }

            // 更新箱子缓存（降低频率但确保新箱子能被检测到）
            cacheUpdateTimer += Time.deltaTime;
            if (cacheUpdateTimer >= cacheUpdateInterval || cachedLootboxes.Count == 0)
            {
                cacheUpdateTimer = 0f;
                UpdateLootboxCache();
            }

            // 定时清理缓存和处理记录（每90秒）
            cacheClearTimer += Time.deltaTime;
            if (cacheClearTimer >= cacheClearInterval)
            {
                ClearAllCaches();
                cacheClearTimer = 0f;
            }

            // 使用缓存的箱子列表，同时检查是否有新箱子需要添加
            var currentLootboxes = UnityEngine.Object.FindObjectsOfType<InteractableLootbox>();
            if (currentLootboxes != null && currentLootboxes.Length > cachedLootboxes.Count)
            {
                // 有新箱子出现，更新缓存
                UpdateLootboxCache();
            }

            // 使用缓存的箱子列表
            for (int i = cachedLootboxes.Count - 1; i >= 0; i--)
            {
                var lootbox = cachedLootboxes[i];
                if (lootbox == null)
                {
                    // 移除已销毁的箱子
                    cachedLootboxes.RemoveAt(i);
                    continue;
                }

                // 跳过已处理的箱子（额外安全检查）
                if (processedLootboxes.Contains(lootbox))
                {
                    cachedLootboxes.RemoveAt(i);
                    continue;
                }

                if (lootbox.Inventory == null)
                    continue;

                var inventory = lootbox.Inventory;

                //跳过仓库、基地储物柜等非战利品容器
                string boxName = lootbox.name.ToLowerInvariant();
                if (boxName.Contains("storage") || boxName.Contains("base") || boxName.Contains("Tomb")
                    || boxName.Contains("tomb") || boxName.Contains("pet"))
                    continue;

                // 检查距离（靠近自动拾取）- 使用平方距离优化性能
                float distSqr = (player.transform.position - lootbox.transform.position).sqrMagnitude;
                if (distSqr > pickupRadius * pickupRadius)
                    continue;

                //添加卸弹逻辑
                if (BigInventoryConfigManager.Config.EnableAutoUnloadGuns)
                {
                    ProcessGunUnloading(lootbox.Inventory);
                }

                // 执行拾取操作，只要执行过就从缓存中移除
                bool performedPickup = TryPickupFromInventory(lootbox.Inventory);

                // 只要执行过自动拾取操作，就延时0.5s后从缓存中移除该箱子
                if (performedPickup)
                {
                    StartCoroutine(DelayedRemoveLootbox(lootbox, i));
                    continue;
                }
            }
        }

        private static void UpdateLootboxCache()
        {
            cachedLootboxes.Clear();
            var allLootboxes = UnityEngine.Object.FindObjectsOfType<InteractableLootbox>();
            if (allLootboxes != null && allLootboxes.Length > 0)
            {
                // 只添加未处理过的箱子
                foreach (var lootbox in allLootboxes)
                {
                    if (lootbox != null && !processedLootboxes.Contains(lootbox))
                    {
                        cachedLootboxes.Add(lootbox);
                    }
                }
            }
        }

        // 延时移除箱子的协程
        private static System.Collections.IEnumerator DelayedRemoveLootbox(InteractableLootbox lootbox, int index)
        {
            yield return new WaitForSeconds(0.5f);

            // 将箱子添加到已处理集合，确保不再加入缓存
            processedLootboxes.Add(lootbox);

            // 从当前缓存中移除
            if (cachedLootboxes.Count > index && cachedLootboxes[index] == lootbox)
            {
                cachedLootboxes.RemoveAt(index);
            }
            else
            {
                cachedLootboxes.Remove(lootbox);
            }
        }

        // 清理所有缓存和处理记录
        private static void ClearAllCaches()
        {
            int previousCacheCount = cachedLootboxes.Count;
            int previousProcessedCount = processedLootboxes.Count;

            cachedLootboxes.Clear();
            processedLootboxes.Clear();

            //UnityEngine.Debug.Log($"[AutoPickupSystem] 定时清理缓存完成 - 清理前: 缓存箱{previousCacheCount}个, 已处理箱{previousProcessedCount}个, 清理后: 缓存箱0个, 已处理箱0个");

            //立即重新扫描一次，确保不会漏掉当前存在的箱子
            UpdateLootboxCache();
        }

        // 检查物品是否包含禁用Tag
        private static bool HasBannedTag(Item item)
        {
            if (item?.Tags == null) return false;

            foreach (string bannedTag in bannedTags)
            {
                // 使用TagCollection的Contains方法检查Tag
                if (item.Tags.Contains(bannedTag))
                    return true;
            }
            return false;
        }

        // 获取心愿单物品的类型ID
        private static int GetItemTypeID(Item item)
        {
            if (item == null) return -1;
            return item.TypeID;
        }

        // 执行拾取逻辑，返回是否执行了拾取操作
        private static bool TryPickupFromInventory(Inventory targetInv)
        {
            if (targetInv == null) return false;
            var playerInv = player?.CharacterItem?.Inventory;
            if (playerInv == null) return false;

            List<Item> items = new List<Item>(targetInv);
            bool performedPickup = false;

            foreach (var item in items)
            {
                if (item == null) continue;

                string name = item.DisplayName?.Trim()?.ToLowerInvariant() ?? "";
                bool shouldCollect = false;
                bool isWishlistMode = BigInventoryConfigManager.Config.EnableWishlistCollect;

                if (isWishlistMode)
                {
                    //心愿单模式：仅拾取心愿单中的物品
                    int itemId = GetItemTypeID(item);
                    if (itemId > 0)
                    {
                        var wishlistInfo = ItemWishlist.GetWishlistInfo(itemId);
                        shouldCollect = wishlistInfo.isManuallyWishlisted;
                    }

                    // 非心愿单物品直接跳过
                    if (!shouldCollect)
                        continue;
                }
                else
                {
                    //原自动拾取逻辑（仅在心愿单关闭时生效）
                    string textKey = item.DisplayNameRaw;
                    bool isBullet = item.GetBool("IsBullet");

                    if (isBullet) shouldCollect = true;
                    if (HasBannedTag(item)) continue;
                    if (autoCollectTextKeys.Contains(textKey)) shouldCollect = true;

                    if (isBullet && item.Quality < BigInventoryConfigManager.Config.MinAutoCollectQuality)
                        continue;

                    if (!shouldCollect)
                        continue;
                }

                //判断是否能拾取（包含堆叠检测）
                if (!HasFreeSlot(playerInv))
                {
                    // 背包已满，尝试堆叠逻辑
                    if (!CanStackIntoExistingSlots(playerInv, item))
                    {
                        // 无法堆叠才真正停止
                        if (Time.time - lastFullMessageTime >= fullMessageCooldown)
                        {
                            PickupNotifier.ShowMessage("<color=yellow>背包已满，无法继续自动拾取！</color>");
                            lastFullMessageTime = Time.time;
                        }
                        break;
                    }
                }
                // 执行拾取
                int count = item.StackCount;
                if (ItemUtilities.SendToPlayerCharacterInventory(item, false))
                {
                    string message = isWishlistMode
                        ? $"<color=green>心愿单拾取</color> {item.DisplayName}"
                        : $"<color=yellow>自动拾取</color> {item.DisplayName}";

                    PickupNotifier.ShowMessage(message, count);
                    performedPickup = true; // 标记为执行了拾取操作
                }
            }
            // 返回是否执行了任何拾取操作
            return performedPickup;
        }

        // 启动协程的辅助方法
        private static void StartCoroutine(System.Collections.IEnumerator coroutine)
        {
            if (AutoPickupRunner.Instance != null)
            {
                AutoPickupRunner.Instance.StartCoroutine(coroutine);
            }
        }

        // 辅助函数：检测空位
        private static int lastFreeSlotHint = 0;
        private static bool HasFreeSlot(Inventory inv)
        {
            if (inv == null) return false;

            try
            {
                if (inv.Capacity > inv.GetItemCount())
                {
                    int start = Math.Min(lastFreeSlotHint, inv.Capacity - 1);
                    for (int i = start; i < inv.Capacity; i++)
                    {
                        if (inv.lockedIndexes != null && inv.lockedIndexes.Contains(i))
                            continue;

                        var item = inv.GetItemAt(i);
                        if (item == null)
                        {
                            lastFreeSlotHint = i;
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool CanStackIntoExistingSlots(Inventory inv, Item targetItem)
        {
            if (inv == null || targetItem == null) return false;
            if (!targetItem.Stackable) return false;

            try
            {
                int targetType = targetItem.TypeID;
                int remainingCount = targetItem.StackCount;

                // 遍历玩家背包中的物品
                for (int i = 0; i < inv.Capacity; i++)
                {
                    var existing = inv.GetItemAt(i);
                    if (existing == null) continue;

                    // 只考虑同类型物品
                    if (existing.TypeID != targetType) continue;
                    if (!existing.Stackable) continue;

                    int canAdd = existing.MaxStackCount - existing.StackCount;
                    if (canAdd <= 0) continue;

                    remainingCount -= canAdd;
                    if (remainingCount <= 0)
                    {
                        // 可以完全堆叠进背包
                        return true;
                    }
                }

                // 部分可堆叠时也返回 true（代表有空间接收）
                return remainingCount < targetItem.StackCount;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AutoPickupSystem] 堆叠检测异常: {ex}");
            }

            return false;
        }

        // Harmony Patch：在打开箱子时自动拾取
        [HarmonyPatch(typeof(LootView))]
        public static class LootView_AutoCollectBullets_Patch
        {
            [HarmonyPostfix]
            [HarmonyPatch("OnOpen")]
            public static void Postfix_AutoCollectBullets(LootView __instance)
            {
                if (!BigInventoryConfigManager.Config.EnableAutoCollectBullets || !BigInventoryConfigManager.Config.EnableOpenCollect)
                {
                    return;
                }

                // 启动延迟协程执行
                __instance.StartCoroutine(AutoCollectAfterDelay(__instance));
            }

            private static IEnumerator AutoCollectAfterDelay(LootView lootView)
            {
                yield return new WaitForSeconds(0.1f);

                try
                {
                    Inventory targetInv = lootView.TargetInventory;
                    if (targetInv == null || targetInv == PlayerStorage.Inventory) yield break;

                    var lm = LevelManager.Instance;
                    if (lm?.MainCharacter?.CharacterItem?.Inventory == null) yield break;

                    var playerInv = lm.MainCharacter.CharacterItem.Inventory;

                    List<Item> items = new List<Item>(targetInv);
                    int collectedCount = 0;

                    if (BigInventoryConfigManager.Config.EnableAutoUnloadGuns)
                    {
                        ProcessGunUnloading(targetInv);
                    }

                    foreach (var item in items)
                    {
                        if (item == null) continue;

                        bool shouldCollect = false;

                        // 如果开启了心愿单拾取，只拾取心愿单中的物品
                        if (BigInventoryConfigManager.Config.EnableWishlistCollect)
                        {
                            int itemId = GetItemTypeID(item);
                            if (itemId > 0)
                            {
                                var wishlistInfo = ItemWishlist.GetWishlistInfo(itemId);
                                shouldCollect = wishlistInfo.isManuallyWishlisted;
                                // 如果不在心愿单中，直接跳过
                                if (!shouldCollect) continue;
                            }
                        }
                        else
                        {
                            // 原有的拾取判断逻辑
                            string name = item.DisplayName?.Trim()?.ToLowerInvariant() ?? "";
                            string textKey = item.DisplayNameRaw;
                            bool isBullet = false;

                            if (item.GetBool("IsBullet")) { shouldCollect = true; isBullet = true; }

                            if (HasBannedTag(item))
                                continue;

                            if (autoCollectTextKeys.Contains(textKey))
                            {
                                shouldCollect = true;
                            }

                            if (isBullet && item.Quality < BigInventoryConfigManager.Config.MinAutoCollectQuality)
                                continue;
                        }

                        if (!shouldCollect) continue;

                        // 判断是否能拾取（包含堆叠检测）
                        if (!HasFreeSlot(playerInv))
                        {
                            // 背包已满，尝试堆叠逻辑
                            if (!CanStackIntoExistingSlots(playerInv, item))
                            {
                                // 无法堆叠才真正停止
                                if (Time.time - lastFullMessageTime >= fullMessageCooldown)
                                {
                                    PickupNotifier.ShowMessage("<color=yellow>背包已满，无法继续自动拾取！</color>");
                                    lastFullMessageTime = Time.time;
                                }
                                break;
                            }
                        }

                        int count = item.StackCount;
                        if (ItemUtilities.SendToPlayerCharacterInventory(item, false))
                        {
                            string message = BigInventoryConfigManager.Config.EnableWishlistCollect ?
                                $"<color=green>心愿单拾取</color> {item.DisplayName}" :
                                $"<color=yellow>自动拾取</color> {item.DisplayName}";
                            PickupNotifier.ShowMessage(message, count);
                            collectedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[AutoPickupSystem] 开箱自动拾取异常: {ex}");
                }
            }
        }

        // ========== 自动卸弹相关方法 ==========

        // 检查物品是否为枪械
        private static bool IsGun(Item item)
        {
            if (item == null) return false;

            bool isGun = item.Tags != null && item.Tags.Contains("Gun");
            return isGun;
        }

        // 获取物品在库存中的槽位索引
        private static int? GetItemSlotIndex(Item item)
        {
            if (item?.InInventory == null) return null;

            try
            {
                var inventory = item.InInventory;
                for (int i = 0; i < inventory.Capacity; i++)
                {
                    var slotItem = inventory.GetItemAt(i);
                    if (slotItem == item)
                    {
                        return i;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AutoPickupSystem] 获取物品槽位索引失败: {ex}");
            }

            return null;
        }

        // 基于库存索引生成枪械的唯一记录键
        private static string GetGunInventoryKey(Item gun)
        {
            if (gun == null) return null;

            // 如果枪械在某个库存中，使用库存+索引作为唯一标识
            if (gun.InInventory != null)
            {
                int? slotIndex = GetItemSlotIndex(gun);
                if (slotIndex.HasValue)
                {
                    string recordKey = $"{gun.InInventory.GetInstanceID()}_{slotIndex.Value}";
                    //UnityEngine.Debug.Log($"[AutoPickupSystem] 生成记录键: {recordKey} (库存: {gun.InInventory.GetInstanceID()}, 槽位: {slotIndex.Value})");
                    return recordKey;
                }
            }

            //后备方案：使用完整标识符
            string fallbackKey = GetGunFallbackIdentifier(gun);
            //UnityEngine.Debug.Log($"[AutoPickupSystem] 使用后备记录键: {fallbackKey}");
            return fallbackKey;
        }

        // 后备方案：生成完整的唯一标识符
        private static string GetGunFallbackIdentifier(Item gun)
        {
            if (gun == null) return null;

            List<string> identifiers = new List<string>();

            if (!string.IsNullOrEmpty(gun.DisplayName))
            {
                identifiers.Add($"ID:{gun.DisplayName}");
            }

            if (gun.gameObject != null)
            {
                identifiers.Add($"Instance:{gun.gameObject.GetInstanceID()}");
            }

            if (gun.transform != null)
            {
                Vector3 position = gun.transform.position;
                identifiers.Add($"Pos:{position.x:F2},{position.y:F2},{position.z:F2}");
            }

            return string.Join("|", identifiers);
        }

        // 检查枪械是否已经卸弹
        private static bool IsGunAlreadyUnloaded(Item gun)
        {
            if (gun == null) return false;

            string recordKey = GetGunInventoryKey(gun);
            if (string.IsNullOrEmpty(recordKey)) return false;

            bool alreadyUnloaded = unloadedGunRecords.Contains(recordKey);

            return alreadyUnloaded;
        }

        // 记录已卸弹的枪械
        private static void MarkGunAsUnloaded(Item gun)
        {
            if (gun == null) return;

            string recordKey = GetGunInventoryKey(gun);
            if (!string.IsNullOrEmpty(recordKey))
            {
                unloadedGunRecords.Add(recordKey);
                //UnityEngine.Debug.Log($"[AutoPickupSystem] 记录枪械到卸弹列表: {gun.DisplayName} (Key: {recordKey})");
            }
        }

        // 清理已卸弹记录
        private static void ClearUnloadedGunsRecord()
        {
            int previousCount = unloadedGunRecords.Count;
            unloadedGunRecords.Clear();
            //UnityEngine.Debug.Log($"[AutoPickupSystem] 已清理卸弹记录，之前记录数: {previousCount}");
        }

        // 执行枪械卸弹操作
        private static void UnloadGun(Item gun)
        {
            if (gun == null) return;

            try
            {
                // 检查是否已经卸弹过
                if (IsGunAlreadyUnloaded(gun))
                {
                    return;
                }

                string recordKey = GetGunInventoryKey(gun);
                //UnityEngine.Debug.Log($"[AutoPickupSystem] 开始卸弹处理: {gun.DisplayName} (记录键: {recordKey})");

                // 获取枪械设置组件
                var gunSetting = gun.GetComponent<ItemSetting_Gun>();
                if (gunSetting == null)
                {
                    UnityEngine.Debug.LogWarning($"[AutoPickupSystem] 无法找到ItemSetting_Gun组件，跳过卸弹");
                    return;
                }

                // 检查枪械库存中的子弹数量
                var gunInventory = gun.Inventory;
                int bulletCount = 0;
                if (gunInventory != null)
                {
                    List<Item> bullets = new List<Item>(gunInventory);
                    bulletCount = bullets.Count;
                   //UnityEngine.Debug.Log($"[AutoPickupSystem] 枪械内子弹数量: {bulletCount}");
                }

                // 如果没有子弹，直接标记为已卸弹并返回
                if (bulletCount == 0)
                {
                    //UnityEngine.Debug.Log($"[AutoPickupSystem] 枪械 {gun.DisplayName} 没有子弹，无需卸弹");
                    MarkGunAsUnloaded(gun);
                    return;
                }

                // 播放卸弹音效
                //AudioManager.Post("SFX/Combat/Gun/unload");

                // 执行卸弹操作
                gunSetting.TakeOutAllBullets();

                // 记录已卸弹
                MarkGunAsUnloaded(gun);

                //UnityEngine.Debug.Log($"[AutoPickupSystem] 已为枪械 {gun.DisplayName} 执行卸弹操作，清除 {bulletCount} 发子弹 (记录键: {recordKey})");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[AutoPickupSystem] 卸弹操作失败: {ex}");
            }
        }

        // 检查并处理容器内的所有枪械卸弹
        private static void ProcessGunUnloading(Inventory inventory)
        {
            if (inventory == null) return;

            List<Item> items = new List<Item>(inventory);
            int gunCount = 0;
            int unloadedCount = 0;

            foreach (var item in items)
            {
                if (item == null) continue;

                // 如果是枪械，执行卸弹
                if (IsGun(item))
                {
                    gunCount++;
                    UnloadGun(item);
                    unloadedCount++;
                }
            }
        }

        // Runner自动检测失效并恢复
        private class AutoPickupRunner : MonoBehaviour
        {
            public static AutoPickupRunner Instance { get; private set; }

            private float timer = 0f;
            private float performanceTimer = 0f;
            private float cacheClearTimer = 0f;
            private float baseScanInterval;
            private float dynamicScanInterval;
            private bool lastActiveState = false;

            void Awake()
            {
                Instance = this;
                baseScanInterval = BigInventoryConfigManager.Config.DynamicScanInterval;
                dynamicScanInterval = baseScanInterval;
            }

            void Update()
            {
                // 动态更新配置值
                float configScanInterval = BigInventoryConfigManager.Config.DynamicScanInterval;
                if (baseScanInterval != configScanInterval)
                {
                    baseScanInterval = configScanInterval;
                    dynamicScanInterval = baseScanInterval;
                }

                bool shouldRun = BigInventoryConfigManager.Config.EnableAutoCollectBullets
                                 && !BigInventoryConfigManager.Config.EnableOpenCollect;

                if (!shouldRun && BigInventoryConfigManager.Config.EnableWishlistCollect)
                {
                    shouldRun = true;
                }

                // 开关变化检测
                if (shouldRun != lastActiveState)
                {
                    lastActiveState = shouldRun;
                    if (!shouldRun)
                    {
                        AutoPickupSystem.cachedLootboxes.Clear();
                        AutoPickupSystem.processedLootboxes.Clear();
                        timer = 0f;
                        performanceTimer = 0f;
                        cacheClearTimer = 0f;
                        return;
                    }
                }

                if (!shouldRun)
                    return;

                if (!AutoPickupSystem.sceneFullyLoaded && LevelManager.Instance != null)
                {
                    AutoPickupSystem.OnLevelStart();
                    return;
                }

                timer += Time.deltaTime;
                performanceTimer += Time.deltaTime;
                cacheClearTimer += Time.deltaTime;

                // 每90秒清理缓存
                if (cacheClearTimer >= 90f)
                {
                    AutoPickupSystem.ClearAllCaches();
                    cacheClearTimer = 0f;
                }

                // 每10秒检测一次性能
                if (performanceTimer >= 10f)
                {
                    performanceTimer = 0f;
                    // 根据战利品箱数量动态调整间隔，设置上限
                    if (AutoPickupSystem.cachedLootboxes.Count > 150)
                    {
                        dynamicScanInterval = Mathf.Min(baseScanInterval * 2f, 10f); // 最大10秒
                    }
                    else
                    {
                        dynamicScanInterval = baseScanInterval;
                    }
                }

                if (timer >= dynamicScanInterval)
                {
                    timer = 0f;
                    AutoPickupSystem.PerformAutoPickup();
                }
            }
        }


        public class PickupNotifier : MonoBehaviour
        {
            private static PickupNotifier _instance;
            private readonly List<TextMeshProUGUI> _activeTexts = new List<TextMeshProUGUI>();
            private RectTransform _container;

            private float _verticalSpacing = 42f;
            private float _fadeInTime = 0.25f;
            private float _displayTime = 3.5f;
            private float _fadeOutTime = 0.75f;

            // 外部调用接口（AutoPickupSystem 使用）
            public static void ShowMessage(string itemName, int count = 1)
            {
                if (string.IsNullOrEmpty(itemName))
                    return;

                if (_instance == null)
                {
                    GameObject go = new GameObject("PickupNotifier");
                    _instance = go.AddComponent<PickupNotifier>();
                    DontDestroyOnLoad(go);
                }

                _instance.StartCoroutine(_instance.WaitAndCreateMessage($"+ {itemName} × {count}"));
            }

            private IEnumerator WaitAndCreateMessage(string text)
            {
                // 等待 HUDManager / Canvas 加载
                float timer = 0f;
                while (_container == null && timer < 3f)
                {
                    TryInitializeCanvas();
                    timer += Time.deltaTime;
                    yield return null;
                }

                if (_container == null)
                {
                    UnityEngine.Debug.LogWarning("[PickupNotifier] 无法找到 Canvas，跳过文本显示。");
                    yield break;
                }

                CreateMessage(text);
            }

            private void TryInitializeCanvas()
            {
                if (_container != null) return;

                Canvas lootCanvas = UnityEngine.Object.FindObjectOfType<Canvas>();
                var parent = (lootCanvas != null)
                    ? lootCanvas.transform
                    : UnityEngine.Object.FindObjectOfType<HUDManager>()?.transform;

                if (parent == null)
                    return;

                GameObject containerObj = new GameObject("PickupMessageContainer");
                _container = containerObj.AddComponent<RectTransform>();
                _container.SetParent(parent);
                _container.anchorMin = new Vector2(1f, 1f);
                _container.anchorMax = new Vector2(1f, 1f);
                _container.pivot = new Vector2(1f, 1f);

                // 从配置读取位置或使用默认
                float x = 0f;
                float y = -100f;
                try
                {
                    x = BigInventoryConfigManager.Config.MessageX;
                    y = BigInventoryConfigManager.Config.MessageY;
                }
                catch { }

                _container.anchoredPosition = new Vector2(x, y);
            }

            private void CreateMessage(string text)
            {
                if (_container == null)
                {
                    UnityEngine.Debug.LogWarning("[PickupNotifier] 容器未初始化。");
                    return;
                }

                GameObject textObj = new GameObject("PickupText");
                textObj.transform.SetParent(_container);
                var tmp = textObj.AddComponent<TextMeshProUGUI>();

                var template = GameplayDataSettings.UIStyle.TemplateTextUGUI;
                if (template != null)
                {
                    tmp.font = template.font;
                    tmp.fontSize = 28f;
                    tmp.alignment = TextAlignmentOptions.Right;
                    tmp.color = new Color(0.3f, 0.9f, 1f);
                    tmp.richText = true;
                }

                tmp.text = text;

                RectTransform rect = tmp.GetComponent<RectTransform>();
                rect.localScale = Vector3.one;
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);

                float yOffset = _activeTexts.Count * _verticalSpacing;
                rect.anchoredPosition = new Vector2(0, -yOffset);

                _activeTexts.Add(tmp);
                StartCoroutine(FadeAndRemove(tmp));
            }

            private IEnumerator FadeAndRemove(TextMeshProUGUI tmp)
            {
                float timer = 0f;
                Color c = tmp.color;
                c.a = 0f;
                tmp.color = c;

                // 淡入
                while (timer < _fadeInTime)
                {
                    timer += Time.deltaTime;
                    c.a = Mathf.Clamp01(timer / _fadeInTime);
                    tmp.color = c;
                    yield return null;
                }

                // 停留
                yield return new WaitForSeconds(_displayTime);

                // 淡出
                timer = 0f;
                while (timer < _fadeOutTime)
                {
                    timer += Time.deltaTime;
                    c.a = Mathf.Clamp01(1f - (timer / _fadeOutTime));
                    tmp.color = c;
                    yield return null;
                }

                _activeTexts.Remove(tmp);
                Destroy(tmp.gameObject);

                // 调整剩余文本位置
                for (int i = 0; i < _activeTexts.Count; i++)
                {
                    var t = _activeTexts[i];
                    if (t == null) continue;
                    var rt = t.GetComponent<RectTransform>();
                    if (rt != null)
                        rt.anchoredPosition = new Vector2(0, -i * _verticalSpacing);
                }
            }
        }

    }
}
