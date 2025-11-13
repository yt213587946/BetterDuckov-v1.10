using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Effects;
using Duckov.Modding;
using Duckov.UI;
using ItemStatsSystem;
using UnityEngine;
using Duckov.Utilities;

namespace bigInventory
{
    public static class TotemNegativeRemover
    {
        private static FieldInfo modDescValueField;
        private static FieldInfo modDescDisplayField;
        private static HashSet<string> SettedKeys;
        private static HashSet<string> DoubledPositiveKeys;

        // Tag
        private static readonly HashSet<string> bannedTags = new HashSet<string>
        {
            "Grip", "Magazine", "Muzzle", "Scope", "Stock", "TecEquip", "Gem", "Helmat"
        };

        // 对象池管理
        private static GameObject coroutineRunner;
        private static bool isInitialized;

        public static void Initialize()
        {
            if (isInitialized) return;

            modDescValueField = typeof(ModifierDescription).GetField("value", BindingFlags.Instance | BindingFlags.NonPublic);
            modDescDisplayField = typeof(ModifierDescription).GetField("display", BindingFlags.Instance | BindingFlags.NonPublic);
            SettedKeys = new HashSet<string>();
            DoubledPositiveKeys = new HashSet<string>();

            ExecuteAfterFrame(UpdateValue);
            isInitialized = true;
        }

        private static void ExecuteAfterFrame(Action action)
        {
            if (coroutineRunner == null)
            {
                coroutineRunner = new GameObject("TempCoroutineRunner_TotemFix");
                var runner = coroutineRunner.AddComponent<CoroutineRunner>();
                UnityEngine.Object.DontDestroyOnLoad(coroutineRunner);
            }

            coroutineRunner.GetComponent<CoroutineRunner>().StartCoroutine(DelayedExecute(action));
        }

        private static IEnumerator DelayedExecute(Action action)
        {
            yield return new WaitForEndOfFrame();
            action?.Invoke();
        }

        private static void UpdateValue()
        {
            if (!BigInventoryConfigManager.Config.EnableNoNegative) return;

            ItemAssetsCollection instance = ItemAssetsCollection.Instance;
            if (instance == null) return;

            List<ItemAssetsCollection.Entry> entries = instance.entries;
            if (entries == null) return;

            int totemProcessedCount = 0;
            int bannedKeywordsProcessedCount = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                ItemAssetsCollection.Entry entry = entries[i];
                ItemMetaData data = entry.metaData;

                // 提前检查数据有效性
                if (data.Equals(default(ItemMetaData)) || string.IsNullOrEmpty(data.Name))
                    continue;

                bool isTotem = data.Name.Contains("Totem");
                bool isBannedItem = false;
                bool isHelmat = false;

                // 检查是否是 bannedTags 中的物品
                Item prefab = entry.prefab;
                if (prefab != null && prefab.Tags != null)
                {
                    // 使用预定义的bannedTags集合进行快速检查
                    foreach (string tag in bannedTags)
                    {
                        if (prefab.Tags.Contains(tag))
                        {
                            isBannedItem = true;
                            // 特别检查是否为Helmat类型
                            if (tag == "Helmat")
                            {
                                isHelmat = true;
                            }
                            break;
                        }
                    }
                }

                // 如果不是图腾也不是配件物品，跳过
                if (!isTotem && !isBannedItem) continue;
                if (prefab == null) continue;

                // 处理组件移除
                ProcessComponents(prefab, isTotem, isBannedItem, ref totemProcessedCount, ref bannedKeywordsProcessedCount);

                // 处理属性描述修改
                ProcessModifierDescriptions(entry, prefab, isTotem, isBannedItem, isHelmat, ref totemProcessedCount, ref bannedKeywordsProcessedCount);
            }
        }

        private static void ProcessComponents(Item prefab, bool isTotem, bool isBannedItem,
            ref int totemProcessedCount, ref int bannedKeywordsProcessedCount)
        {
            // 移除 DamageAction 组件
            DamageAction[] damageActions = prefab.GetComponentsInChildren<DamageAction>();
            if (damageActions.Length > 0)
            {
                for (int j = 0; j < damageActions.Length; j++)
                {
                    UnityEngine.Object.DestroyImmediate(damageActions[j], true);
                    UpdateCounters(isTotem, isBannedItem, ref totemProcessedCount, ref bannedKeywordsProcessedCount);
                }

                // 只有在有DamageAction被移除时才处理其他组件
                RemoveComponents<AddBuffAction>(prefab, isTotem, isBannedItem, ref totemProcessedCount, ref bannedKeywordsProcessedCount);
                RemoveComponents<TickTrigger>(prefab, isTotem, isBannedItem, ref totemProcessedCount, ref bannedKeywordsProcessedCount);
                RemoveComponents<Effect>(prefab, isTotem, isBannedItem, ref totemProcessedCount, ref bannedKeywordsProcessedCount);
            }
        }

        private static void RemoveComponents<T>(Item prefab, bool isTotem, bool isBannedItem,
            ref int totemProcessedCount, ref int bannedKeywordsProcessedCount) where T : UnityEngine.Object
        {
            T[] components = prefab.GetComponentsInChildren<T>();
            for (int j = 0; j < components.Length; j++)
            {
                try
                {
                    UnityEngine.Object.DestroyImmediate(components[j], true);
                    UpdateCounters(isTotem, isBannedItem, ref totemProcessedCount, ref bannedKeywordsProcessedCount);
                }
                catch (Exception ex)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, $"无法移除 {typeof(T).Name} 组件: {ex.Message}", "TotemNegativeRemover");

                }
            }
        }

        private static void ProcessModifierDescriptions(ItemAssetsCollection.Entry entry, Item prefab,
            bool isTotem, bool isBannedItem, bool isHelmat, ref int totemProcessedCount, ref int bannedKeywordsProcessedCount)
        {
            ModifierDescriptionCollection[] modifierCollections = prefab.GetComponentsInChildren<ModifierDescriptionCollection>();
            for (int j = 0; j < modifierCollections.Length; j++)
            {
                foreach (ModifierDescription desc in modifierCollections[j])
                {
                    string keyID = $"{entry.typeID}_{desc.Key}";

                    // 使用HashSet的快速查找
                    if (SettedKeys.Contains(keyID)) continue;

                    SettedKeys.Add(keyID);
                    ProcessSingleModifier(desc, keyID, isTotem, isBannedItem, isHelmat, ref totemProcessedCount, ref bannedKeywordsProcessedCount);
                }
            }
        }

        private static void ProcessSingleModifier(ModifierDescription desc, string keyID,
            bool isTotem, bool isBannedItem, bool isHelmat, ref int totemProcessedCount, ref int bannedKeywordsProcessedCount)
        {
            Polarity polarity = StatInfoDatabase.GetPolarity(desc.Key);
            bool isPositive = IsPositiveEffect(polarity, desc.Value);

            if (isPositive)
            {
                // Helmat类型不进行正面效果翻倍
                if (isHelmat) return;

                if (!BigInventoryConfigManager.Config.EnableDoubleattributes) return;

                string doubleKeyID = keyID + "_DOUBLED";
                if (!DoubledPositiveKeys.Contains(doubleKeyID))
                {
                    // 正面效果翻倍
                    float newValue = desc.Value * 2f;
                    modDescValueField.SetValue(desc, newValue);
                    DoubledPositiveKeys.Add(doubleKeyID);
                    UpdateCounters(isTotem, isBannedItem, ref totemProcessedCount, ref bannedKeywordsProcessedCount);
                }
            }
            else
            {
                // 负面效果设为0并隐藏
                modDescValueField.SetValue(desc, 0f);
                modDescDisplayField.SetValue(desc, false);
                UpdateCounters(isTotem, isBannedItem, ref totemProcessedCount, ref bannedKeywordsProcessedCount);
            }
        }

        private static bool IsPositiveEffect(Polarity polarity, float value)
        {
            return polarity != Polarity.Negative
                ? (polarity <= Polarity.Positive && value > 0f)
                : (value < 0f);
        }

        private static void UpdateCounters(bool isTotem, bool isBannedItem,
            ref int totemProcessedCount, ref int bannedKeywordsProcessedCount)
        {
            if (isTotem) totemProcessedCount++;
            if (isBannedItem) bannedKeywordsProcessedCount++;
        }

        // 协程运行器辅助类
        private class CoroutineRunner : MonoBehaviour { }
    }

    // 专门的MonoBehaviour组件
    public class TotemNegativeRemoverComponent : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(DelayedInitialize());
        }

        private IEnumerator DelayedInitialize()
        {
            yield return new WaitForEndOfFrame();
            TotemNegativeRemover.Initialize();
        }
    }
}
