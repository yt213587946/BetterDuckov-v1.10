using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ItemStatsSystem.Items;
using ItemStatsSystem;
using Duckov.Utilities;
using SodaCraft.Localizations;
using bigInventory;

namespace WeaponExtraSlotsMod
{
    public static class WeaponExtraSlotsConfig
    {
        public const string ExtraSlotPrefix = "ExtraWeaponSlot_";
        public const string SaveKey = "WeaponExtraSlots_Content";
        public static int ExtraSlotMultiplier = 1; //表示几倍槽位

        // 定义所有枪械应该有的槽位类型
        public static readonly Dictionary<string, SlotTemplate> GunSlotTemplates = new Dictionary<string, SlotTemplate>
        {
            {"Scope", new SlotTemplate { RequireTags = new List<string> {"Scope"}, ExcludeTags = new List<string> {}}},
            {"Muzzle", new SlotTemplate { RequireTags = new List<string> { "Muzzle" }, ExcludeTags = new List<string> {}}},
            {"Grip", new SlotTemplate { RequireTags = new List<string> {"Grip"}, ExcludeTags = new List<string> {}}},
            {"Stock", new SlotTemplate { RequireTags = new List<string> {"Stock"}, ExcludeTags = new List<string> {}}},
            {"TecEquip", new SlotTemplate { RequireTags = new List<string> {"TecEquip"}, ExcludeTags = new List<string> {}}},
            {"Magazine", new SlotTemplate { RequireTags = new List<string> { "Magazine" }, ExcludeTags = new List<string> {}}}
        };
        // 已知Tag的预定义映射
        private static readonly Dictionary<string, string> KnownTagPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Scope", "Tags/WeaponAttachments/Scope"},
            {"Muzzle", "Tags/WeaponAttachments/Muzzle"},
            {"Grip", "Tags/WeaponAttachments/Grip"},
            {"Stock", "Tags/WeaponAttachments/Stock"},
            {"TecEquip", "Tags/WeaponAttachments/TecEquip"},
            {"Magazine", "Tags/WeaponAttachments/Magazine"},
        };
        // 使用Resources.Load直接加载特定路径的Tag
        public static Tag LoadTagByName(string tagName)
        {
            if (KnownTagPaths.TryGetValue(tagName, out string path))
            {
                return Resources.Load<Tag>(path);
            }

            // 回退到名称查找
            return Resources.Load<Tag>($"Tags/{tagName}");
        }

        // Tag缓存相关方法
        private static Dictionary<string, Tag> _tagCache;

        public static Tag GetCachedTag(string tagName)
        {
            if (_tagCache == null)
            {
                InitializeTagCache();
            }

            if (_tagCache != null && _tagCache.TryGetValue(tagName, out Tag tag))
            {
                return tag;
            }
            return null;
        }

        private static void InitializeTagCache()
        {
            _tagCache = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);

            // 先尝试通过已知路径加载
            foreach (var knownTag in KnownTagPaths)
            {
                Tag tag = Resources.Load<Tag>(knownTag.Value);
                if (tag != null && !_tagCache.ContainsKey(knownTag.Key))
                {
                    _tagCache[knownTag.Key] = tag;
                }
            }

            // 再加载所有Tag作为备用
            var allTags = Resources.FindObjectsOfTypeAll<Tag>();
            foreach (var tag in allTags)
            {
                if (tag != null && !_tagCache.ContainsKey(tag.name))
                {
                    _tagCache[tag.name] = tag;
                }
            }

            Debug.Log($"[WeaponExtraSlots] 已缓存 {_tagCache.Count} 个Tag资源");
        }
    }

    public class SlotTemplate
    {
        public List<string> RequireTags { get; set; } = new List<string>();
        public List<string> ExcludeTags { get; set; } = new List<string>();
    }

    [HarmonyPatch(typeof(SlotCollection))]
    public static class WeaponExtraSlotsPatches
    {
        private static readonly HashSet<string> WeaponSlotTypes = new HashSet<string>
        {
            "Scope", "瞄准镜",   // 瞄具
            "Muzzle", "枪口",    // 枪口类
            "Grip", "握把",      // 握把
            "Stock", "枪托",     // 枪托
            "TecEquip", "战术",  // 战术
            "Magazine", "弹夹",  // 弹夹类  
            "Gem", "宝石",       // 宝石
            
        };

        // 使用反射设置私有字段
        private static readonly FieldInfo _forbidItemsWithSameIDField =
            typeof(Slot).GetField("forbidItemsWithSameID", BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPostfix]
        [HarmonyPatch("OnInitialize", MethodType.Normal)]
        public static void SlotCollection_OnInitialize_Postfix(SlotCollection __instance)
        {
            try
            {
                if (!BigInventoryConfigManager.Config.EnableExtraSlots) return;
                if (__instance == null || __instance.list == null) return;

                var master = __instance.Master;
                if (master == null) return;

                // 检查是否为武器
                if (!IsWeaponItem(master))
                    return;

                // 检查是否已经添加过额外槽位
                if (__instance.list.Any(s => s.Key != null && s.Key.StartsWith(WeaponExtraSlotsConfig.ExtraSlotPrefix)))
                {
                    EnsurePersistenceSubscription(__instance);
                    RestoreExtraSlotsContent(__instance);
                    return;
                }

                // 获取武器标签信息
                bool isGun = master.Tags != null && master.Tags.Contains("Gun");
                bool isMeleeWeapon = master.Tags != null && master.Tags.Contains("MeleeWeapon");

                // 获取现有的武器配件槽位
                var originalSlots = __instance.list.Where(IsWeaponAttachmentSlot).ToList();

                if (isGun)
                {
                    // 枪械武器：强制添加所有类型的槽位（除了宝石）
                    AddAllSlotTypesForGun(__instance, originalSlots);
                }
                else if (isMeleeWeapon)
                {
                    // 近战武器：保持原逻辑，复制一份原来的宝石槽
                    AddExtraSlotsForMelee(__instance, originalSlots);
                }
                else if (originalSlots.Count > 0)
                {
                    // 其他武器：保持原逻辑
                    AddExtraSlotsNormal(__instance, originalSlots);
                }

                // 重建字典
                var buildDictInvoker = ReflectionCache.CreateMethodInvoker(typeof(SlotCollection), "BuildDictionary", null);
                buildDictInvoker?.Invoke(__instance, new object[0]);

                EnsurePersistenceSubscription(__instance);
                RestoreExtraSlotsContent(__instance);

                //Debug.Log($"[WeaponExtraSlots] 为武器 {master.DisplayName} 添加了额外配件槽");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponExtraSlots] 添加武器额外槽位时发生错误：{ex}");
            }
        }

        // 为枪械武器强制添加所有类型的槽位（除了宝石）
        private static void AddAllSlotTypesForGun(SlotCollection collection, List<Slot> originalSlots)
        {
            var addedSlotTypes = new HashSet<string>();
            int slotIndex = 0;

            // 为每种预定义的枪械槽位类型创建一个槽位
            foreach (var slotTemplate in WeaponExtraSlotsConfig.GunSlotTemplates)
            {
                string slotType = slotTemplate.Key;

                // 创建新的槽位
                var extraSlot = CreateSlotFromTemplate(slotType, slotTemplate.Value, slotIndex);
                if (extraSlot != null)
                {
                    extraSlot.Initialize(collection);
                    collection.list.Add(extraSlot);
                    addedSlotTypes.Add(slotType);
                    slotIndex++;
                    //Debug.Log($"[WeaponExtraSlots] 为枪械强制添加 {slotType} 类型槽位: {extraSlot.Key}");
                }
            }

            //Debug.Log($"[WeaponExtraSlots] 为枪械强制添加了 {addedSlotTypes.Count} 种不同类型的槽位: {string.Join(", ", addedSlotTypes)}");
        }

        // 从模板创建槽位
        private static Slot CreateSlotFromTemplate(string slotType, SlotTemplate template, int index)
        {
            try
            {
                var extraSlot = new Slot($"{WeaponExtraSlotsConfig.ExtraSlotPrefix}{slotType}_{index}")
                {
                    requireTags = GetTagsFromNames(template.RequireTags),
                    excludeTags = GetTagsFromNames(template.ExcludeTags),
                    SlotIcon = GetDefaultSlotIcon(slotType)
                };

                // 设置 forbidItemsWithSameID
                if (_forbidItemsWithSameIDField != null)
                {
                    _forbidItemsWithSameIDField.SetValue(extraSlot, true);
                }

                return extraSlot;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponExtraSlots] 从模板创建槽位失败: {ex}");
                return null;
            }
        }

        // 根据标签名称列表获取Tag对象列表
        private static List<Tag> GetTagsFromNames(List<string> tagNames)
        {
            var tags = new List<Tag>();
            if (tagNames == null) return tags;

            foreach (string tagName in tagNames)
            {
                // 使用缓存方法
                Tag foundTag = WeaponExtraSlotsConfig.GetCachedTag(tagName);
                if (foundTag != null)
                {
                    tags.Add(foundTag);
                }
                else
                {
                    Debug.LogWarning($"[WeaponExtraSlots] 未找到标签: {tagName}");
                }
            }

            return tags;
        }

        // 获取默认的槽位图标
        private static Sprite GetDefaultSlotIcon(string slotType)
        {
            return null;
        }

        // 为近战武器添加额外槽位（保持原逻辑，只复制现有的槽位）
        private static void AddExtraSlotsForMelee(SlotCollection collection, List<Slot> originalSlots)
        {
            if (originalSlots.Count == 0) return;

            // 近战武器：复制所有现有的槽位
            foreach (var originalSlot in originalSlots)
            {
                for (int i = 0; i < WeaponExtraSlotsConfig.ExtraSlotMultiplier; i++)
                {
                    var extraSlot = CreateExtraSlot(originalSlot, i);
                    if (extraSlot != null)
                    {
                        extraSlot.Initialize(collection);
                        collection.list.Add(extraSlot);
                        //Debug.Log($"[WeaponExtraSlots] 为近战武器添加槽位: {extraSlot.Key}");
                    }
                }
            }
        }

        // 普通添加槽位逻辑（复制所有原始槽位）
        private static void AddExtraSlotsNormal(SlotCollection collection, List<Slot> originalSlots)
        {
            foreach (var originalSlot in originalSlots)
            {
                for (int i = 0; i < WeaponExtraSlotsConfig.ExtraSlotMultiplier; i++)
                {
                    var extraSlot = CreateExtraSlot(originalSlot, i);
                    if (extraSlot != null)
                    {
                        extraSlot.Initialize(collection);
                        collection.list.Add(extraSlot);
                        Debug.Log($"[WeaponExtraSlots] 创建额外槽位: {extraSlot.Key}");
                    }
                }
            }
        }

        // 判断是否为宝石槽位
        private static bool IsGemSlot(string slotKey)
        {
            if (string.IsNullOrEmpty(slotKey)) return false;
            return slotKey.ToLower().Contains("gem");
        }

        // 从槽位键名获取槽位类型
        private static string GetSlotTypeFromKey(string slotKey)
        {
            if (string.IsNullOrEmpty(slotKey)) return "Attachment";

            string keyLower = slotKey.ToLower();

            if (keyLower.Contains("muzzle")) return "Muzzle";
            if (keyLower.Contains("magazine")) return "Magazine";
            if (keyLower.Contains("scope")) return "Scope";
            if (keyLower.Contains("grip")) return "Grip";
            if (keyLower.Contains("stock")) return "Stock";
            if (keyLower.Contains("gem")) return "Gem";
            if (keyLower.Contains("tech") || keyLower.Contains("tec")) return "Tech";

            return "Attachment";
        }

        private static bool IsWeaponItem(Item item)
        {
            if (item == null) return false;

            try
            {
                string itemName = item.DisplayName?.ToLower() ?? "";
                string itemType = item.GetType().Name?.ToLower() ?? "";

                // 武器特征判断
                bool isWeapon =
                    itemName.Contains("gun") || itemName.Contains("rifle") ||
                    itemName.Contains("枪") || itemName.Contains("武器") ||
                    itemType.Contains("weapon") || itemType.Contains("gun") ||
                    // 检查标签
                    (item.Tags != null && (
                        item.Tags.Contains("Gun") ||
                        item.Tags.Contains("MeleeWeapon") ||
                        item.Tags.Contains("Weapon")
                    ));

                return isWeapon;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsWeaponAttachmentSlot(Slot slot)
        {
            if (slot?.Key == null) return false;

            string slotKey = slot.Key.ToLower();

            // 检查是否为武器配件槽位
            foreach (var weaponSlotType in WeaponSlotTypes)
            {
                if (slotKey.Contains(weaponSlotType.ToLower()))
                    return true;
            }

            return false;
        }

        private static Slot CreateExtraSlot(Slot originalSlot, int index)
        {
            try
            {
                var extraSlot = new Slot($"{WeaponExtraSlotsConfig.ExtraSlotPrefix}{originalSlot.Key}_{index}")
                {
                    // 复制原始槽位的属性 - 使用正确的Slot类属性
                    requireTags = originalSlot.requireTags != null ? new List<Tag>(originalSlot.requireTags) : new List<Tag>(),
                    excludeTags = originalSlot.excludeTags != null ? new List<Tag>(originalSlot.excludeTags) : new List<Tag>(),
                    SlotIcon = originalSlot.SlotIcon // 使用正确的属性名
                };

                // 使用反射设置私有字段 forbidItemsWithSameID
                if (_forbidItemsWithSameIDField != null)
                {
                    bool originalValue = (bool)_forbidItemsWithSameIDField.GetValue(originalSlot);
                    _forbidItemsWithSameIDField.SetValue(extraSlot, originalValue);
                }

                return extraSlot;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponExtraSlots] 创建额外槽位失败: {ex}");
                return null;
            }
        }

        private static void EnsurePersistenceSubscription(SlotCollection collection)
        {
            try
            {
                collection.OnSlotContentChanged -= OnSlotContentChangedHandler;
                collection.OnSlotContentChanged += OnSlotContentChangedHandler;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponExtraSlots] EnsurePersistenceSubscription 出错: " + ex);
            }
        }

        private static void OnSlotContentChangedHandler(Slot slot)
        {
            try
            {
                if (slot == null) return;

                var collectionObj = ReflectionCache.CreateFieldGetter(typeof(Slot), "collection")(slot) as SlotCollection;
                if (collectionObj == null) return;

                var master = collectionObj.Master;
                if (master == null) return;

                SaveExtraSlotsContent(collectionObj, master);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponExtraSlots] 保存额外槽内容失败: " + ex);
            }
        }

        private static void SaveExtraSlotsContent(SlotCollection collection, Item master)
        {
            List<string> pairs = new List<string>();
            foreach (var slot in collection.list)
            {
                if (slot?.Key == null) continue;
                if (!slot.Key.StartsWith(WeaponExtraSlotsConfig.ExtraSlotPrefix)) continue;

                if (slot.Content != null)
                {
                    string itemKey = GetItemIdentifier(slot.Content);
                    pairs.Add($"{slot.Key}|{Escape(itemKey)}");
                }
            }

            string serialized = string.Join(";", pairs);

            try
            {
                master.SetString(WeaponExtraSlotsConfig.SaveKey, serialized);
            }
            catch
            {
                // 备用保存方式
                try
                {
                    var getVars = ReflectionCache.CreatePropertyGetter(master.GetType(), "Variables");
                    var varsObj = getVars != null ? getVars(master) : null;
                    if (varsObj != null)
                    {
                        var setMethod = ReflectionCache.CreateMethodInvoker(varsObj.GetType(), "Set", new Type[] { typeof(string), typeof(string) });
                        setMethod(varsObj, new object[] { WeaponExtraSlotsConfig.SaveKey, serialized });
                    }
                }
                catch { }
            }
        }

        private static void RestoreExtraSlotsContent(SlotCollection collection)
        {
            try
            {
                if (collection == null) return;
                var master = collection.Master;
                if (master == null) return;

                string saved = "";
                try
                {
                    saved = master.GetString(WeaponExtraSlotsConfig.SaveKey, "");
                }
                catch
                {
                    try
                    {
                        var getVars = ReflectionCache.CreatePropertyGetter(master.GetType(), "Variables");
                        var varsObj = getVars(master);
                        if (varsObj != null)
                        {
                            var getMethod = ReflectionCache.CreateMethodInvoker(varsObj.GetType(), "GetString", new Type[] { typeof(string), typeof(string) });
                            var res = getMethod(varsObj, new object[] { WeaponExtraSlotsConfig.SaveKey, "" });
                            if (res is string s) saved = s;
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(saved)) return;

                var entries = saved.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (entries.Length == 0) return;

                Item[] allItems = UnityEngine.Object.FindObjectsOfType<Item>(true);

                foreach (var entry in entries)
                {
                    var parts = entry.Split(new[] { '|' }, 2);
                    if (parts.Length != 2) continue;

                    string slotKey = parts[0];
                    string itemIdEscaped = parts[1];
                    string itemId = Unescape(itemIdEscaped);

                    var slot = collection.GetSlot(slotKey);
                    if (slot == null || slot.Content != null) continue;

                    Item foundItem = FindItemByIdentifier(allItems, itemId);
                    if (foundItem != null)
                    {
                        try
                        {
                            foundItem.Detach();
                            Item outItem;
                            slot.Plug(foundItem, out outItem);
                            //Debug.Log($"[WeaponExtraSlots] 恢复配件 {foundItem.DisplayName} 到槽位 {slotKey}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[WeaponExtraSlots] 恢复配件到槽失败: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponExtraSlots] 恢复额外槽内容失败: " + ex);
            }
        }

        private static Item FindItemByIdentifier(Item[] allItems, string identifier)
        {
            foreach (var item in allItems)
            {
                if (item == null) continue;

                string itemId = GetItemIdentifier(item);
                if (!string.IsNullOrEmpty(itemId) && string.Equals(itemId, identifier, StringComparison.OrdinalIgnoreCase))
                    return item;

                string displayName = item.DisplayName?.Trim() ?? "";
                if (!string.IsNullOrEmpty(displayName) && displayName.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return null;
        }

        private static string Escape(string s) => s?.Replace("\\", "\\\\").Replace(";", "\\;").Replace("|", "\\|") ?? "";
        private static string Unescape(string s) => s?.Replace("\\|", "|").Replace("\\;", ";").Replace("\\\\", "\\") ?? "";

        private static readonly Func<object, object> _item_key_field_getter =
            ReflectionCache.CreateFieldGetter(typeof(Item), "key");

        private static string GetItemIdentifier(Item item)
        {
            if (item == null) return "";
            try
            {
                // 尝试多种方式获取物品标识符
                string key = "";
                try { key = item.GetString("Key", null); } catch { }
                if (!string.IsNullOrEmpty(key)) return key.Trim();

                try
                {
                    var stat = item.GetStat("Key");
                    if (stat != null && !string.IsNullOrEmpty(stat.Key)) return stat.Key.Trim();
                }
                catch { }

                try
                {
                    var keyObj = _item_key_field_getter(item);
                    if (keyObj is string s && !string.IsNullOrEmpty(s)) return s.Trim();
                }
                catch { }

                return (item.DisplayName ?? "").Trim();
            }
            catch { return (item.DisplayName ?? "").Trim(); }
        }
    }

    [HarmonyPatch(typeof(Slot), nameof(Slot.CanPlug))]
    public static class Slot_CanPlug_Patch
    {
        static bool Prefix(Slot __instance, Item item, ref bool __result)
        {
            try
            {
                // 如果是额外武器槽位，检查配件兼容性
                if (__instance.Key != null && __instance.Key.StartsWith(WeaponExtraSlotsConfig.ExtraSlotPrefix))
                {
                    // 找到对应的原始槽位
                    var collection = ReflectionCache.CreateFieldGetter(typeof(Slot), "collection")(__instance) as SlotCollection;
                    if (collection != null)
                    {
                        // 从额外槽位名称中提取原始槽位名称
                        var slotNameParts = __instance.Key.Split('_');
                        if (slotNameParts.Length >= 2)
                        {
                            string slotType = slotNameParts[1]; // ExtraWeaponSlot_[SlotType]_Index

                            // 对于强制添加的槽位，需要检查配件是否匹配该槽位类型
                            if (WeaponExtraSlotsConfig.GunSlotTemplates.ContainsKey(slotType))
                            {
                                var template = WeaponExtraSlotsConfig.GunSlotTemplates[slotType];

                                // 检查物品标签是否匹配槽位要求
                                if (item.Tags != null && __instance.requireTags != null)
                                {
                                    bool hasRequiredTag = __instance.requireTags.Any(requiredTag =>
                                        item.Tags.Contains(requiredTag));
                                    bool hasExcludedTag = __instance.excludeTags != null &&
                                        __instance.excludeTags.Any(excludedTag =>
                                            item.Tags.Contains(excludedTag));

                                    __result = hasRequiredTag && !hasExcludedTag;
                                    return false; // 跳过原始方法
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponExtraSlots] Slot.CanPlug 检查出错: " + ex);
                return true;
            }

            return true;
        }
    }

    public static class WeaponExtraSlotsUI
    {
        private static readonly Dictionary<string, string> SlotTypeLocalization = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Muzzle", "UI_Slot_Muzzle"},
            {"Magazine", "UI_Slot_Magazine"},
            {"Scope", "UI_Slot_Scope"},
            {"Grip", "UI_Slot_Grip"},
            {"Stock", "UI_Slot_Stock"},
            {"Gem", "UI_Slot_Gem"},
            {"Tech", "UI_Slot_Tech"},
            {"Attachment", "UI_Slot_Attachment"}
        };

        public static void InstallDynamicPatch(Harmony harmony)
        {
            try
            {
                var postfix = new HarmonyMethod(typeof(WeaponExtraSlotsUI).GetMethod(nameof(PostfixSlotDisplay), BindingFlags.Static | BindingFlags.Public));

                var slotDisplayTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return new Type[0]; }
                    })
                    .Where(t => t.Name.Contains("SlotDisplay"))
                    .ToList();

                int patchedCount = 0;

                foreach (var slotType in slotDisplayTypes)
                {
                    var methods = slotType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name == "Setup" || m.Name == "Refresh" || m.Name == "OnEnable")
                        .ToList();

                    foreach (var method in methods)
                    {
                        try
                        {
                            harmony.Patch(method, postfix: postfix);
                            patchedCount++;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[WeaponExtraSlots] UI补丁注入失败: " + ex);
                        }
                    }
                }

                //Debug.Log($"[WeaponExtraSlots] 成功为 {patchedCount} 个UI方法注入了补丁");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponExtraSlots] UI补丁安装出错: " + ex);
            }
        }

        public static void PostfixSlotDisplay(object __instance)
        {
            try
            {
                if (__instance == null) return;

                var slot = GetBoundSlot(__instance);
                if (slot == null || slot.Key == null || !slot.Key.StartsWith(WeaponExtraSlotsConfig.ExtraSlotPrefix))
                    return;

                // 为额外槽位设置显示名称
                UpdateSlotDisplayName(__instance, slot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponExtraSlots] 槽位显示更新失败: " + ex);
            }
        }

        private static Slot GetBoundSlot(object displayInstance)
        {
            try
            {
                var type = displayInstance.GetType();

                // 尝试获取Slot字段
                var slotField = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(f => f.FieldType == typeof(Slot));
                if (slotField != null)
                    return slotField.GetValue(displayInstance) as Slot;

                // 尝试获取Slot属性
                var slotProperty = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(p => p.PropertyType == typeof(Slot));
                if (slotProperty != null)
                    return slotProperty.GetValue(displayInstance) as Slot;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static void UpdateSlotDisplayName(object displayInstance, Slot slot)
        {
            try
            {
                // 从额外槽位名称中提取原始槽位名称
                var slotNameParts = slot.Key.Split('_');
                if (slotNameParts.Length >= 3)
                {
                    string originalSlotName = slotNameParts[1];

                    // 获取槽位类型
                    string slotType = GetSlotTypeFromKey(originalSlotName);
                    string displayName = GetLocalizedSlotName(slotType) + " (额外)";

                    var gameObject = (displayInstance as Component)?.gameObject;
                    if (gameObject != null)
                    {
                        // 更新UI文本组件
                        UpdateUIText(gameObject, displayName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponExtraSlots] 更新槽位显示名称失败: " + ex);
            }
        }

        private static string GetSlotTypeFromKey(string slotKey)
        {
            string keyLower = slotKey.ToLower();

            if (keyLower.Contains("muzzle")) return "Muzzle";
            if (keyLower.Contains("magazine") || keyLower.Contains("mag")) return "Magazine";
            if (keyLower.Contains("scope") || keyLower.Contains("sight") || keyLower.Contains("optic")) return "Scope";
            if (keyLower.Contains("grip")) return "Grip";
            if (keyLower.Contains("stock")) return "Stock";
            if (keyLower.Contains("gem")) return "Gem";
            if (keyLower.Contains("tech") || keyLower.Contains("tec")) return "Tech";

            return "Attachment";
        }

        private static string GetLocalizedSlotName(string slotType)
        {
            try
            {
                if (SlotTypeLocalization.TryGetValue(slotType, out string localizationKey))
                {
                    // 使用游戏的本地化系统
                    string localizedText = LocalizationManager.GetPlainText(localizationKey);
                    if (!string.IsNullOrEmpty(localizedText) && localizedText != $"*{localizationKey}*")
                    {
                        return localizedText;
                    }
                }

                // 回退到英文名称
                return slotType;
            }
            catch
            {
                return slotType;
            }
        }

        private static void UpdateUIText(GameObject gameObject, string text)
        {
            // 更新所有Text组件
            var textComponents = gameObject.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            foreach (var textComp in textComponents)
            {
                if (textComp != null)
                    textComp.text = text;
            }

            // 更新TextMeshPro组件
            var tmproComponents = gameObject.GetComponentsInChildren<Component>(true)
                .Where(c => c.GetType().FullName?.Contains("TextMeshPro") == true);

            foreach (var tmpro in tmproComponents)
            {
                var textProp = tmpro.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (textProp != null && textProp.CanWrite)
                {
                    textProp.SetValue(tmpro, text);
                }
            }
        }
    }
}
