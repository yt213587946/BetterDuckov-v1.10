using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ItemStatsSystem.Items;
using ItemStatsSystem;
using Duckov.Utilities;
using System.Collections.Concurrent;
using SodaCraft.Localizations;
using UnityEngine.UI;
using Duckov.UI;
using MonoMod.Utils;
using UnityEngine.EventSystems;

namespace bigInventory
{
    public static class ExtraTotemSlotsConfig
    {
        public const string ExtraTotemSlotPrefix = "ExtraTotemSlot_";
        public const string SaveKey = "BigInventory_ExtraTotemSlots";
    }

    [HarmonyPatch(typeof(SlotCollection))]

    public static class ExtraTotemSlotsPatches
    {
        private static readonly HashSet<int> _processedUIDisplays = new HashSet<int>();
        private class ExtraTotemScrollMarker : MonoBehaviour { }

        [HarmonyPostfix]
        [HarmonyPatch("OnInitialize", MethodType.Normal)]
        public static void SlotCollection_OnInitialize_Postfix(SlotCollection __instance)
        {
            try
            {
                if (!BigInventoryConfigManager.Config.EnableMoreTotemslot) return;
                if (__instance == null || __instance.list == null) return;

                var master = __instance.Master;
                if (master == null) return;

                // 检查是否为武器 - 武器不添加额外图腾槽
                if (IsWeaponItem(master))
                {
                    //Debug.Log($"[ExtraTotemSlots] 跳过武器物品: {master.DisplayName}，不添加额外图腾槽");
                    return;
                }

                // 检查预制体名称或类型
                string prefabName = master.name?.ToLower() ?? "";
                string masterType = master.GetType().Name?.ToLower() ?? "";

                // 常见的玩家角色标识符
                bool isPlayerCharacter =
                    prefabName.Contains("player") ||
                    prefabName.Contains("character") ||
                    masterType.Contains("player") ||
                    masterType.Contains("character") ||
                    prefabName.Contains("躯壳") ||
                    prefabName.Contains("shell");

                if (!isPlayerCharacter)
                    return;

                // 如果已有额外槽位，则恢复内容并返回
                if (__instance.list.Any(s => s.Key != null && s.Key.StartsWith(ExtraTotemSlotsConfig.ExtraTotemSlotPrefix)))
                {
                    EnsurePersistenceSubscription(__instance);
                    RestoreExtraSlotsContent(__instance);
                    return;
                }

                // 添加额外图腾槽
                for (int i = 0; i < BigInventoryConfigManager.Config.ExtraTotemSlotCount; i++)
                {
                    var slot = new Slot(ExtraTotemSlotsConfig.ExtraTotemSlotPrefix + i)
                    {
                        requireTags = new List<Tag>(),
                        excludeTags = new List<Tag>()
                    };

                    slot.Initialize(__instance);
                    __instance.list.Add(slot);
                }

                var buildDictInvoker = ReflectionCache.CreateMethodInvoker(typeof(SlotCollection), "BuildDictionary", null);
                buildDictInvoker?.Invoke(__instance, new object[0]);

                EnsurePersistenceSubscription(__instance);
                RestoreExtraSlotsContent(__instance);
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"添加图腾槽位时发生错误：{ex}", "ExtraTotemSlots");

            }
        }

        // 添加武器检测方法
        private static bool IsWeaponItem(Item item)
        {
            if (item == null) return false;

            try
            {
                // 检查武器标签
                if (item.Tags != null && (
                    item.Tags.Contains("Gun") ||
                    item.Tags.Contains("MeleeWeapon") ||
                    item.Tags.Contains("Weapon") ||
                    item.Tags.Contains("RangedWeapon") ||
                    item.Tags.Contains("Firearm")))
                {
                    return true;
                }

                // 检查物品名称中的武器关键词
                string itemName = item.DisplayName?.ToLower() ?? "";
                string itemType = item.GetType().Name?.ToLower() ?? "";

                bool isWeaponByName =
                    itemName.Contains("gun") || itemName.Contains("rifle") ||
                    itemName.Contains("shotgun") || itemName.Contains("sniper") ||
                    itemName.Contains("pistol") || itemName.Contains("revolver") ||
                    itemName.Contains("smg") || itemName.Contains("assault") ||
                    itemName.Contains("枪") || itemName.Contains("武器") ||
                    itemName.Contains("sword") || itemName.Contains("blade") ||
                    itemName.Contains("knife") || itemName.Contains("axe") ||
                    itemName.Contains("hammer") || itemName.Contains("spear") ||
                    itemType.Contains("weapon") || itemType.Contains("gun");

                return isWeaponByName;
            }
            catch
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(ItemSlotCollectionDisplay), "Setup")]
        [HarmonyPostfix]
        public static void ItemSlotCollectionDisplay_Setup_Postfix(ItemSlotCollectionDisplay __instance, Item target, bool movable)
        {
            try
            {
                if (__instance == null || target == null) return;

                // 检查是否已经处理过这个显示实例
                int instanceId = __instance.GetInstanceID();
                if (_processedUIDisplays.Contains(instanceId))
                    return;

                //立即开始处理，不延迟
                ImmediateEnsureScrollUI(__instance, target);
                _processedUIDisplays.Add(instanceId);
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"UI 显示设置失败: {ex}", "ExtraTotemSlots");
            }
        }

        //立即处理滚动UI
        private static void ImmediateEnsureScrollUI(ItemSlotCollectionDisplay display, Item target)
        {
            try
            {
                var slotCollections = target.GetComponentsInChildren<SlotCollection>(true);
                bool hasTotemSlots = slotCollections.Any(col =>
                    col.list?.Any(s => s.Key?.StartsWith(ExtraTotemSlotsConfig.ExtraTotemSlotPrefix) == true) == true);

                if (hasTotemSlots)
                {
                    // 立即处理，不延迟
                    TryEnsureScrollableUI(display);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"立即确保滚动 UI 失败: {ex}", "ExtraTotemSlots");
            }
        }


        //为 UI 显示添加滚动支持
        private static void TryEnsureScrollableUI(ItemSlotCollectionDisplay display)
        {
            if (display == null) return;

            try
            {
                GameObject displayGO = display.gameObject;
                if (displayGO == null) return;

                //Debug.Log($"[ExtraTotemSlots] 开始为 {displayGO.name} 处理滚动 UI");

                // 查找可能的网格布局或容器
                Transform container = FindUISlotContainer(displayGO.transform);
                if (container == null)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, $"未找到 {displayGO.name} 的 UI 容器", "ExtraTotemSlots");

                    return;
                }

                // 确保容器有 RectTransform
                RectTransform containerRect = container as RectTransform;
                if (containerRect == null)
                {
                    containerRect = container.gameObject.AddComponent<RectTransform>();
                }

                // 设置滚动
                SetupUIScroll(containerRect, display);

                //Debug.Log($"[ExtraTotemSlots] 成功为 {displayGO.name} 设置滚动 UI");

            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"设置 UI 滚动失败: {ex}", "ExtraTotemSlots");
            }
        }

        //查找 UI 槽位容器
        private static Transform FindUISlotContainer(Transform start)
        {
            if (start == null) return null;

            // 查找常见的 UI 容器组件
            Transform container = FindComponentInChildren<UnityEngine.UI.LayoutGroup>(start)?.transform;
            if (container != null) return container;

            container = FindComponentInChildren<UnityEngine.UI.GridLayoutGroup>(start)?.transform;
            if (container != null) return container;

            container = FindComponentInChildren<UnityEngine.UI.VerticalLayoutGroup>(start)?.transform;
            if (container != null) return container;

            container = FindComponentInChildren<UnityEngine.UI.HorizontalLayoutGroup>(start)?.transform;
            if (container != null) return container;

            // 通过名称查找
            container = FindTransformByName(start, "Grid");
            if (container != null) return container;

            container = FindTransformByName(start, "Layout");
            if (container != null) return container;

            container = FindTransformByName(start, "Container");
            if (container != null) return container;

            container = FindTransformByName(start, "Slots");
            if (container != null) return container;

            // 返回第一个有多个子级的对象
            foreach (Transform child in start)
            {
                if (child.childCount > 1)
                    return child;
            }

            return start; // 返回自身作为备用
        }

        //在子级中查找组件
        private static T FindComponentInChildren<T>(Transform parent) where T : Component
        {
            if (parent == null) return null;

            T component = parent.GetComponent<T>();
            if (component != null) return component;

            foreach (Transform child in parent)
            {
                component = child.GetComponent<T>();
                if (component != null) return component;

                T grandChildComponent = FindComponentInChildren<T>(child);
                if (grandChildComponent != null) return grandChildComponent;
            }

            return null;
        }

        //通过名称查找变换
        private static Transform FindTransformByName(Transform parent, string name)
        {
            if (parent == null) return null;

            if (parent.name.Contains(name))
                return parent;

            foreach (Transform child in parent)
            {
                if (child.name.Contains(name))
                    return child;

                Transform found = FindTransformByName(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        //为 UI 容器设置滚动
        private static void SetupUIScroll(RectTransform container, ItemSlotCollectionDisplay display)
        {
            try
            {
                // 检查是否已经设置了滚动
                if (container.GetComponent<ExtraTotemScrollMarker>() != null)
                {
                    //Debug.Log($"[ExtraTotemSlots] 容器 {container.name} 已经设置了滚动");
                    return;
                }
                // 添加或获取 ScrollRect
                ScrollRect scrollRect = container.GetComponent<ScrollRect>();
                if (scrollRect == null)
                {
                    scrollRect = container.gameObject.AddComponent<ScrollRect>();
                }

                // 查找真正的内容区域（包含所有槽位的容器）
                RectTransform content = FindActualContentForScroll(container, display);
                if (content == null)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, $"未找到内容区域，使用容器自身", "ExtraTotemSlots");

                    content = container;
                }

                // 设置 ScrollRect - 降低灵敏度
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.viewport = container;
                scrollRect.content = content;
                scrollRect.scrollSensitivity = 0.4f; // 降低灵敏度
                scrollRect.inertia = false;
                scrollRect.decelerationRate = 0f; // 降低惯性

                //立即设置滚动位置到顶部
                scrollRect.verticalNormalizedPosition = 1f; // 1 = 顶部，0 = 底部
                // 添加 RectMask2D - 这会自动隐藏超出视口的内容
                if (container.GetComponent<RectMask2D>() == null)
                {
                    container.gameObject.AddComponent<RectMask2D>();
                }

                // 设置容器和内容的布局
                SetupScrollLayout(container, content, display);
                //添加一个透明的 Image 组件到视口，让整个区域都能响应鼠标事件
                MakeViewportScrollable(container);
                //再次确保滚动位置正确
                DelayEnsureScrollPosition(scrollRect, content);
                // 标记已处理
                container.gameObject.AddComponent<ExtraTotemScrollMarker>();

                //Debug.Log($"[ExtraTotemSlots] 为 UI 容器 {container.name} 设置滚动，内容: {content.name}");
                DebugScrollComponents(container, content, scrollRect);

            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"设置 UI 滚动失败: {ex}", "ExtraTotemSlots");
            }
        }

        //延迟确保滚动位置
        private static async void DelayEnsureScrollPosition(ScrollRect scrollRect, RectTransform content)
        {
            try
            {
                // 等待几帧确保UI完全初始化
                for (int i = 0; i < 3; i++)
                {
                    await System.Threading.Tasks.Task.Delay(16); // 约1帧时间
                    if (scrollRect == null || content == null) return;

                    // 每次等待后都强制设置到顶部
                    scrollRect.verticalNormalizedPosition = 1f;
                    content.anchoredPosition = new Vector2(0, 0);
                }

                //Debug.Log($"[ExtraTotemSlots] 延迟确保滚动位置完成");
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"延迟确保滚动位置失败: {ex}", "ExtraTotemSlots");

            }
        }
        //让整个视口区域都能响应滚动
        private static void MakeViewportScrollable(RectTransform viewport)
        {
            try
            {
                // 检查是否已经有 Image 组件
                UnityEngine.UI.Image existingImage = viewport.GetComponent<UnityEngine.UI.Image>();
                if (existingImage != null)
                {
                    // 如果已有 Image，确保它不阻挡射线
                    existingImage.raycastTarget = true;
                    return;
                }

                // 添加一个完全透明的 Image 组件
                UnityEngine.UI.Image viewportImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
                viewportImage.color = new Color(0, 0, 0, 0); // 完全透明
                viewportImage.raycastTarget = true; // 启用射线检测

                //Debug.Log($"[ExtraTotemSlots] 为视口 {viewport.name} 添加透明背景以支持滚动");
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"设置视口可滚动失败: {ex}", "ExtraTotemSlots");

            }
        }
        //确保视口可以接收射线
        private static void EnsureViewportRaycast(RectTransform viewport)
        {
            try
            {
                // 确保有 CanvasGroup 或合适的设置
                CanvasGroup canvasGroup = viewport.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = viewport.gameObject.AddComponent<CanvasGroup>();
                }
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;

                // 确保视口本身可以接收事件
                viewport.gameObject.layer = LayerMask.NameToLayer("UI");
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"确保视口射线检测失败: {ex}", "ExtraTotemSlots");

            }
        }

        //内容区域查找
        private static RectTransform FindActualContentForScroll(RectTransform container, ItemSlotCollectionDisplay display)
        {
            if (container == null || display == null) return null;

            try
            {
                // 方法1：查找包含 GridLayoutGroup 的容器
                var gridLayout = FindComponentInChildren<GridLayoutGroup>(container);
                if (gridLayout != null)
                {
                    ModLogger.Log(ModLogger.Level.Regular, $"找到 GridLayoutGroup: {gridLayout.name}", "ExtraTotemSlots");

                    return gridLayout.transform as RectTransform;
                }

                // 方法2：查找包含多个槽位显示的子对象
                Transform contentTransform = null;
                int maxChildCount = 0;

                foreach (Transform child in container)
                {
                    int activeChildren = 0;
                    foreach (Transform grandChild in child)
                    {
                        if (grandChild.gameObject.activeInHierarchy)
                            activeChildren++;
                    }

                    if (activeChildren > maxChildCount)
                    {
                        maxChildCount = activeChildren;
                        contentTransform = child;
                    }
                }

                if (contentTransform != null && maxChildCount >= 2)
                {
                    ModLogger.Log(ModLogger.Level.Regular, $"找到内容区域: {contentTransform.name}，包含 {maxChildCount} 个活跃子对象", "ExtraTotemSlots");

                    return contentTransform as RectTransform;
                }

                return container; // 返回自身作为备用
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"查找内容区域失败: {ex}", "ExtraTotemSlots");
                return container;
            }
        }

        //设置滚动布局
        private static void SetupScrollLayout(RectTransform viewport, RectTransform content, ItemSlotCollectionDisplay display)
        {
            try
            {
                // 设置视口
                viewport.anchorMin = new Vector2(0, 0);
                viewport.anchorMax = new Vector2(1, 1);
                viewport.pivot = new Vector2(0.5f, 0.5f);

                // 设置内容区域
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;

                //立即设置内容位置到顶部，隐藏超出部分
                content.anchoredPosition = new Vector2(0, 0);
                // 计算视口高度（显示2列）
                float viewportHeight = CalculateViewportHeight(content);

                // 设置视口高度
                SetupViewportHeight(viewport, viewportHeight);

                // 设置内容高度（保持原有计算）
                float contentHeight = CalculateContentHeight(content, display);
                content.sizeDelta = new Vector2(content.sizeDelta.x, contentHeight);
                //强制刷新布局，确保内容位置正确
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);

                DelayEnsureInitialPosition(viewport, content);
                //确保视口可以接收鼠标事件
                EnsureViewportRaycast(viewport);

                //Debug.Log($"[ExtraTotemSlots] 设置滚动布局 - 视口高度: {viewportHeight}, 内容高度: {contentHeight}");

            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"设置滚动布局失败: {ex}", "ExtraTotemSlots");

            }
        }

        //延迟确保初始位置
        private static async void DelayEnsureInitialPosition(RectTransform viewport, RectTransform content)
        {
            try
            {
                // 等待一帧让Unity完成布局
                await System.Threading.Tasks.Task.Delay(10);

                if (viewport == null || content == null) return;

                //强制设置内容位置到顶部
                content.anchoredPosition = new Vector2(0, 0);

                // 再次刷新布局
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);

                //Debug.Log($"[ExtraTotemSlots] 延迟确保初始位置完成");
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"延迟确保初始位置失败: {ex}", "ExtraTotemSlots");

            }
        }
        private static float CalculateContentHeight(RectTransform content, ItemSlotCollectionDisplay display)
        {
            try
            {
                // 基于实际子对象计算高度
                if (content.childCount > 0)
                {
                    // 获取 GridLayoutGroup 设置
                    GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
                    float cellHeight = grid != null ? grid.cellSize.y : 80f;
                    float spacing = grid != null ? grid.spacing.y : 10f;
                    float paddingTop = grid != null ? grid.padding.top : 5f;
                    float paddingBottom = grid != null ? grid.padding.bottom : 5f;

                    // 计算行数（每行5个槽位）
                    int totalSlots = content.childCount;
                    int rows = Mathf.CeilToInt(totalSlots / 5f);

                    // 计算总高度：内边距 + 所有行高度 + 行间距
                    float totalHeight = paddingTop + paddingBottom + (rows * cellHeight) + Mathf.Max(0, rows - 1) * spacing;

                    //Debug.Log($"[ExtraTotemSlots] 计算内容高度 - 槽位数量: {totalSlots}, 行数: {rows}, 总高度: {totalHeight}");

                    return totalHeight;
                }

                // 备用计算
                return EstimateSlotCount(display) * 120f;
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"计算内容高度失败: {ex}", "ExtraTotemSlots");

                return 400f;
            }
        }

        //计算视口高度（显示2行）
        private static float CalculateViewportHeight(RectTransform content)
        {
            try
            {
                if (content.childCount > 0)
                {
                    // 获取第一个子对象的高度作为参考
                    RectTransform firstChild = content.GetChild(0) as RectTransform;
                    if (firstChild != null)
                    {
                        float slotHeight = firstChild.rect.height;

                        // 获取 GridLayoutGroup 的间距设置
                        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
                        float spacing = grid != null ? grid.spacing.y : 10f;
                        float paddingTop = grid != null ? grid.padding.top : 5f;
                        float paddingBottom = grid != null ? grid.padding.bottom : 5f;

                        // 计算2行的高度：内边距 + 2个槽位高度 + 1个间距
                        float twoRowHeight = paddingTop + paddingBottom + (2 * slotHeight) + spacing;

                       //Debug.Log($"[ExtraTotemSlots] 计算视口高度 - 槽位高度: {slotHeight}, 间距: {spacing}, 2行高度: {twoRowHeight}");
                        return twoRowHeight;
                    }
                }

                // 备用值
                return 200f;
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"计算视口高度失败: {ex}", "ExtraTotemSlots");

                return 200f;
            }
        }

        //设置视口高度
        private static void SetupViewportHeight(RectTransform viewport, float height)
        {
            try
            {
                // 设置 LayoutElement 控制视口高度
                LayoutElement layout = viewport.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = viewport.gameObject.AddComponent<LayoutElement>();
                }

                layout.minHeight = height;
                layout.preferredHeight = height;
                layout.flexibleHeight = 0f;

                // 如果视口有固定锚点，也调整大小
                if (Mathf.Approximately(viewport.anchorMin.y, viewport.anchorMax.y))
                {
                    Vector2 size = viewport.sizeDelta;
                    size.y = height;
                    viewport.sizeDelta = size;
                }

                //Debug.Log($"[ExtraTotemSlots] 设置视口高度: {height}");
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"设置视口高度失败: {ex}", "ExtraTotemSlots");

            }
        }

        //添加调试方法，检查滚动组件状态
        private static void DebugScrollComponents(RectTransform viewport, RectTransform content, ScrollRect scrollRect)
        {
            try
            {
                //Debug.Log($"[ExtraTotemSlots] 滚动组件调试信息:");
                //Debug.Log($"[ExtraTotemSlots] - 视口: {viewport.name} ({(viewport != null ? "有效" : "无效")})");
                //Debug.Log($"[ExtraTotemSlots] - 内容: {content.name} ({(content != null ? "有效" : "无效")})");
                //Debug.Log($"[ExtraTotemSlots] - ScrollRect: {(scrollRect != null ? "有效" : "无效")}");

                if (scrollRect != null)
                {
                    //Debug.Log($"[ExtraTotemSlots] - 垂直滚动: {scrollRect.vertical}");
                    //Debug.Log($"[ExtraTotemSlots] - 水平滚动: {scrollRect.horizontal}");
                    //Debug.Log($"[ExtraTotemSlots] - 视口引用: {(scrollRect.viewport != null ? "设置" : "未设置")}");
                    //Debug.Log($"[ExtraTotemSlots] - 内容引用: {(scrollRect.content != null ? "设置" : "未设置")}");
                }

                if (content != null)
                {
                    //Debug.Log($"[ExtraTotemSlots] - 内容子对象数量: {content.childCount}");
                    //Debug.Log($"[ExtraTotemSlots] - 内容尺寸: {content.sizeDelta}");
                    //Debug.Log($"[ExtraTotemSlots] - 内容锚点: {content.anchorMin} -> {content.anchorMax}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ExtraTotemSlots] 调试滚动组件失败: {ex}");
            }
        }

        //设置 UI 容器尺寸
        private static void SetupUIContainerSize(RectTransform container, ItemSlotCollectionDisplay display)
        {
            try
            {
                // 估算槽位数量（基于显示组件）
                int estimatedSlotCount = EstimateSlotCount(display);
                float slotHeight = 100f; // 估算的槽位高度
                float spacing = 10f;
                float padding = 20f;

                int rows = Mathf.CeilToInt(estimatedSlotCount / 5f); // 每行5个槽位
                float calculatedHeight = padding * 2 + rows * slotHeight + Mathf.Max(0, rows - 1) * spacing;
                float finalHeight = Mathf.Min(calculatedHeight, 500f); // 限制最大高度

                // 设置 LayoutElement
                LayoutElement layout = container.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = container.gameObject.AddComponent<LayoutElement>();
                }

                layout.minHeight = finalHeight;
                layout.preferredHeight = finalHeight;
                layout.flexibleHeight = 0f;

                // 调整容器大小
                if (Mathf.Approximately(container.anchorMin.y, container.anchorMax.y))
                {
                    Vector2 size = container.sizeDelta;
                    size.y = finalHeight;
                    container.sizeDelta = size;
                }

                //Debug.Log($"[ExtraTotemSlots] UI 容器 {container.name} 高度设置为: {finalHeight}");

            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"设置 UI 容器尺寸失败: {ex}", "ExtraTotemSlots");

            }
        }

        //估算槽位数量
        private static int EstimateSlotCount(ItemSlotCollectionDisplay display)
        {
            try
            {
                // 通过显示组件的子对象数量估算
                if (display.transform != null)
                {
                    int childCount = 0;
                    foreach (Transform child in display.transform)
                    {
                        if (child.gameObject.activeInHierarchy)
                            childCount++;
                    }
                    return Mathf.Max(childCount, 10); // 至少10个槽位
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"估算槽位数量失败: {ex}", "ExtraTotemSlots");

            }

            return 10; // 默认值
        }

        //补丁 InventoryView 的 OnOpen 方法，确保在打开时处理
        [HarmonyPatch(typeof(InventoryView), "OnOpen")]
        [HarmonyPostfix]
        public static void InventoryView_OnOpen_Postfix(InventoryView __instance)
        {
            try
            {
                if (__instance == null) return;

                // 延迟处理所有相关的显示组件
                DelayProcessInventoryView(__instance);
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"InventoryView 打开处理失败: {ex}", "ExtraTotemSlots");

            }
        }

        private static async void DelayProcessInventoryView(InventoryView view)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(200);

                // 使用反射获取私有字段
                var slotDisplayField = typeof(InventoryView).GetField("slotDisplay",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var characterItemProperty = typeof(InventoryView).GetProperty("CharacterItem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (slotDisplayField != null && characterItemProperty != null)
                {
                    var slotDisplay = slotDisplayField.GetValue(view) as ItemSlotCollectionDisplay;
                    var item = characterItemProperty.GetValue(view) as Item;

                    if (slotDisplay != null && item != null)
                    {
                        // 手动调用 Setup 方法的后缀补丁
                        ItemSlotCollectionDisplay_Setup_Postfix(slotDisplay, item, true);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"延迟处理 InventoryView 失败: {ex}", "ExtraTotemSlots");

            }
        }

        private static void EnsurePersistenceSubscription(SlotCollection collection)
        {
            try
            {
                // 无需反射获取事件字段名，直接订阅
                collection.OnSlotContentChanged -= OnSlotContentChangedHandler;
                collection.OnSlotContentChanged += OnSlotContentChangedHandler;
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"EnsurePersistenceSubscription 出错: {ex}", "ExtraTotemSlots");

            }
        }

        private static void OnSlotContentChangedHandler(Slot slot)
        {
            try
            {
                if (slot == null) return;

                // 使用 cached field getter 获取 slot.collection
                var collectionObj = ReflectionCache.CreateFieldGetter(typeof(Slot), "collection")(slot) as SlotCollection;
                if (collectionObj == null) return;

                var master = collectionObj.Master;
                if (master == null) return;

                List<string> pairs = new List<string>();
                foreach (var s in collectionObj.list)
                {
                    if (s?.Key == null) continue;
                    if (!s.Key.StartsWith(ExtraTotemSlotsConfig.ExtraTotemSlotPrefix)) continue;

                    if (s.Content != null)
                    {
                        string itemKey = GetItemIdentifier(s.Content);
                        pairs.Add($"{s.Key}|{Escape(itemKey)}");
                    }
                }

                string serialized = string.Join(";", pairs);

                // 优先尝试公开 SetString
                try
                {
                    master.SetString(ExtraTotemSlotsConfig.SaveKey, serialized);
                }
                catch
                {
                    // 尝试通过 Variables 属性与其 Set 方法（反射缓存）
                    try
                    {
                        var getVars = ReflectionCache.CreatePropertyGetter(master.GetType(), "Variables");
                        var varsObj = getVars != null ? getVars(master) : null;
                        if (varsObj != null)
                        {
                            var setMethod = ReflectionCache.GetMethodCached(varsObj.GetType(), "Set", new Type[] { typeof(string), typeof(string) });
                            if (setMethod != null)
                            {
                                var inv = ReflectionCache.CreateMethodInvoker(varsObj.GetType(), "Set", new Type[] { typeof(string), typeof(string) });
                                inv(varsObj, new object[] { ExtraTotemSlotsConfig.SaveKey, serialized });
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"保存额外槽内容失败: {ex}", "ExtraTotemSlots");

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
                    saved = master.GetString(ExtraTotemSlotsConfig.SaveKey, "");
                }
                catch
                {
                    try
                    {
                        var getVars = ReflectionCache.CreatePropertyGetter(master.GetType(), "Variables");
                        var varsObj = getVars(master);
                        if (varsObj != null)
                        {
                            var getMethod = ReflectionCache.GetMethodCached(varsObj.GetType(), "GetString", new Type[] { typeof(string), typeof(string) });
                            if (getMethod != null)
                            {
                                var inv = ReflectionCache.CreateMethodInvoker(varsObj.GetType(), "GetString", new Type[] { typeof(string), typeof(string) });
                                var res = inv(varsObj, new object[] { ExtraTotemSlotsConfig.SaveKey, "" });
                                if (res is string s) saved = s;
                            }
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(saved)) return;

                var entries = saved.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (entries.Length == 0) return;

                Item[] allItems = UnityEngine.Object.FindObjectsOfType<Item>(true);

                foreach (var e in entries)
                {
                    var parts = e.Split(new[] { '|' }, 2);
                    if (parts.Length != 2) continue;
                    string slotKey = parts[0];
                    string itemIdEscaped = parts[1];
                    string itemId = Unescape(itemIdEscaped);

                    var slot = collection.GetSlot(slotKey);
                    if (slot == null) continue;
                    if (slot.Content != null) continue;

                    Item found = null;

                    foreach (var it in allItems)
                    {
                        if (it == null) continue;
                        string id = GetItemIdentifier(it);
                        if (!string.IsNullOrEmpty(id) && string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase))
                        {
                            found = it;
                            break;
                        }
                    }

                    if (found == null)
                    {
                        foreach (var it in allItems)
                        {
                            if (it == null) continue;
                            string dn = it.DisplayName?.Trim() ?? "";
                            if (!string.IsNullOrEmpty(dn) && dn.Equals(itemId, StringComparison.OrdinalIgnoreCase))
                            {
                                found = it;
                                break;
                            }
                        }
                    }

                    if (found != null)
                    {
                        try
                        {
                            found.Detach();
                            Item outItem;
                            slot.Plug(found, out outItem);
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Warn(ModLogger.Level.Regular, $"恢复单个图腾到槽失败: {ex}", "ExtraTotemSlots");

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"恢复额外槽内容失败: {ex}", "ExtraTotemSlots");

            }
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace(";", "\\;").Replace("|", "\\|");
        }

        private static string Unescape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\|", "|").Replace("\\;", ";").Replace("\\\\", "\\");
        }

        // GetItemIdentifier 使用缓存 field-getter
        private static readonly Func<object, object> _item_key_field_getter = ReflectionCache.CreateFieldGetter(typeof(Item), "key");

        private static string GetItemIdentifier(Item item)
        {
            if (item == null) return "";
            try
            {
                string key = "";
                try
                {
                    key = item.GetString("Key", null);
                }
                catch { key = null; }

                if (!string.IsNullOrEmpty(key)) return key.Trim();

                try
                {
                    var stat = item.GetStat("Key");
                    if (stat != null && !string.IsNullOrEmpty(stat.Key)) return stat.Key.Trim();
                }
                catch { }

                try
                {
                    var o = _item_key_field_getter(item);
                    if (o is string s && !string.IsNullOrEmpty(s)) return s.Trim();
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
                if (__instance.Key != null && __instance.Key.StartsWith(ExtraTotemSlotsConfig.ExtraTotemSlotPrefix))
                {
                    bool isTotem = SlotContentRestrictionDynamic.IsTotemItem(item);
                    __result = isTotem;
                    return false;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"Slot.CanPlug 检查出错: {ex}", "ExtraTotemSlots");

                return true;
            }

            return true;
        }
    }

    public static class ForceSlotDisplayNamePatch
    {
        private const string ExtraTotemSlotPrefix = ExtraTotemSlotsConfig.ExtraTotemSlotPrefix;

        // 使用游戏中的图腾本地化键
        private const string TotemLocalizationKey = "ItemFilter_Totem";

        private static readonly ConcurrentDictionary<Type, Func<object, object>> TypeToSlotGetter = new ConcurrentDictionary<Type, Func<object, object>>();

        public static void PostfixForceText(object __instance)
        {
            try
            {
                if (__instance == null) return;
                var instanceType = __instance.GetType();
                if (!instanceType.Name.Contains("SlotDisplay")) return;

                Func<object, object> slotGetter;
                if (!TypeToSlotGetter.TryGetValue(instanceType, out slotGetter))
                {
                    // 尝试找到 Slot 字段
                    var slotField = instanceType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .FirstOrDefault(f => f.FieldType == typeof(Slot));
                    if (slotField != null)
                    {
                        slotGetter = ReflectionCache.CreateFieldGetter(instanceType, slotField.Name);
                    }
                    else
                    {
                        var slotProp = instanceType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                            .FirstOrDefault(p => p.PropertyType == typeof(Slot));
                        if (slotProp != null)
                        {
                            slotGetter = ReflectionCache.CreatePropertyGetter(instanceType, slotProp.Name);
                        }
                    }

                    if (slotGetter == null)
                        slotGetter = (o) => null;

                    TypeToSlotGetter[instanceType] = slotGetter;
                }

                var boundSlot = slotGetter(__instance) as Slot;
                if (boundSlot == null || boundSlot.Key == null || !boundSlot.Key.StartsWith(ExtraTotemSlotPrefix))
                    return;

                // 使用游戏的本地化系统获取图腾名称
                string displayText = GetLocalizedTotemName();

                GameObject go = (__instance as Component)?.gameObject;
                if (go == null) return;

                // 更新UI文本
                UpdateUIText(go, displayText);
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"修改槽位显示名称失败: {ex}", "ExtraTotemSlots");
            }
        }

        private static string GetLocalizedTotemName()
        {
            try
            {
                // 直接使用游戏的 LocalizationManager 获取本地化文本
                string localizedText = LocalizationManager.GetPlainText(TotemLocalizationKey);

                if (!string.IsNullOrEmpty(localizedText) && localizedText != $"*{TotemLocalizationKey}*")
                {
                    return localizedText;
                }

                ModLogger.Warn(ModLogger.Level.Regular, $"未找到本地化键: {TotemLocalizationKey}", "ExtraTotemSlots");

            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"获取本地化文本失败: {ex}", "ExtraTotemSlots");
            }

            // 回退方案：基于系统语言的简单映射
            return GetFallbackTotemName();
        }

        private static string GetFallbackTotemName()
        {
            // 基于系统语言的简单映射作为回退
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                    return "图腾";
                case SystemLanguage.Japanese:
                    return "トーテム";
                case SystemLanguage.Korean:
                    return "토템";
                case SystemLanguage.Russian:
                    return "Тотем";
                case SystemLanguage.French:
                    return "Totem";
                case SystemLanguage.German:
                    return "Totem";
                case SystemLanguage.Spanish:
                    return "Tótem";
                case SystemLanguage.Portuguese:
                    return "Totem";
                default:
                    return "Totem"; // 默认英语
            }
        }

        private static void UpdateUIText(GameObject go, string displayText)
        {
            var texts = go.GetComponentsInChildren<UnityEngine.UI.Text>(true).Cast<Component>().ToList();
            var monos = go.GetComponentsInChildren<MonoBehaviour>(true).ToList();

            foreach (var txt in texts)
            {
                if (txt == null) continue;
                (txt as UnityEngine.UI.Text).text = displayText;
            }
            foreach (var mono in monos)
            {
                if (mono == null) continue;
                var t = mono.GetType();
                if (t.FullName != null && t.FullName.Contains("TMPro.TextMeshProUGUI"))
                {
                    var prop = ReflectionCache.GetPropertyCached(t, "text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.CanWrite)
                    {
                        var setter = ReflectionCache.CreatePropertySetter(t, "text");
                        setter(mono, displayText);
                    }
                }
            }
        }

        // 原有的 InstallDynamicPatch 方法保持不变
        public static void InstallDynamicPatch(Harmony harmony)
        {
            try
            {
                var postfix = new HarmonyMethod(typeof(ForceSlotDisplayNamePatch).GetMethod(nameof(PostfixForceText), BindingFlags.Static | BindingFlags.Public));

                var slotDisplayTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return new Type[0]; }
                    })
                    .Where(t => t.Name.Contains("SlotDisplay"))
                    .ToList();

                int patchedCount = 0;

                foreach (var t in slotDisplayTypes)
                {
                    var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m =>
                            m.Name == "Setup" ||
                            m.Name.StartsWith("Setup") ||
                            m.Name == "Refresh" ||
                            m.Name == "OnEnable" ||
                            m.Name == "UpdateContent")
                        .ToList();

                    foreach (var m in methods)
                    {
                        try
                        {
                            harmony.Patch(m, postfix: postfix);
                            patchedCount++;
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Warn(ModLogger.Level.Regular, $"注入补丁失败: {ex}", "ExtraTotemSlots");

                        }
                    }
                }

                ModLogger.Log(ModLogger.Level.Regular, $"成功为 {patchedCount} 个方法注入了槽位名称补丁", "ExtraTotemSlots");

            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"InstallDynamicPatch 出错: {ex}", "ExtraTotemSlots");

            }
        }
    }

    public static class SlotContentRestrictionDynamic
    {
        private static readonly string[] CandidateMethodNames = { "SetContent", "Plug", "TrySetContent", "SetItem" };

        public static void InstallSlotContentPatches(Harmony harmony)
        {
            try
            {
                var slotType = typeof(Slot);
                var methods = slotType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => CandidateMethodNames.Contains(m.Name))
                    .ToList();

                var prefixMethod = typeof(SlotContentRestrictionDynamic).GetMethod(nameof(SlotSetContentPrefix), BindingFlags.Static | BindingFlags.NonPublic);

                foreach (var m in methods)
                {
                    try
                    {
                        harmony.Patch(m, prefix: new HarmonyMethod(prefixMethod));
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Warn(ModLogger.Level.Regular, $"patch 方法失败: {ex}", "SlotContentRestrictionDynamic");

                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"Install 出错: {ex}", "SlotContentRestrictionDynamic");
            }
        }

        private static bool SlotSetContentPrefix(Slot __instance, params object[] __args)
        {
            try
            {
                if (__instance == null)
                    return true;

                // 仅限制额外图腾槽
                if (__instance.Key == null || !__instance.Key.StartsWith(ExtraTotemSlotsConfig.ExtraTotemSlotPrefix))
                    return true;

                // 尝试提取第一个参数
                Item targetItem = null;
                if (__args != null && __args.Length > 0)
                    targetItem = __args[0] as Item;

                // 如果没有传入 Item，就放行
                if (targetItem == null)
                    return true;

                bool ok = IsTotemItem(targetItem);

                if (!ok)
                {
                    ModLogger.Log(ModLogger.Level.Regular, $"阻止非图腾物品 {targetItem.DisplayName} 放入图腾槽 {__instance.Key}", "SlotContentRestrictionDynamic");

                }

                return ok; // true 表示允许继续原方法，false 表示阻止
            }
            catch (Exception ex)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"SlotSetContentPrefix 出错: {ex}", "SlotContentRestrictionDynamic");

                return true; // 安全回退
            }
        }

        // IsTotemItem 的判定逻辑保留原样
        public static bool IsTotemItem(Item item)
        {
            if (item == null) return false;
            try
            {
                //判断标签或关键字
                if (item.Tags != null && item.Tags.Contains("Totem")) return true;
                var name = item.DisplayName ?? "";
                if (name.Contains("图腾") || name.Contains("Totem")) return true;
            }
            catch { }
            return false;
        }
    }

    public static class ExtraTotemSlotsInstaller
    {
        public static void Install(Harmony harmony)
        {
        }
    }
}
