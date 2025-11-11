using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace bigInventory
{
    public class BigInventoryConfigUI : MonoBehaviour
    {
        private static BigInventoryConfigUI _instance;
        private Canvas canvas;
        private GameObject settingsPanel;
        private static Slider activeSlider = null;
        private float lastKeyTime = 0f;
        private GameObject inputBlocker;
        private ScrollRect tabScrollRect;
        private int selectedTab = 0;
        private string[] tabNames = { "MOD配置" };
        private float appliedUIScale = 1.0f;
        private GameObject bottomArea;
        private Button languageToggleButton = null;
        private Button resetButton = null;
        private Button closeButton = null;

        // 输入字段变量
        private string weightFactorInput = "";
        private string stackMultiplierInput = "";
        private string repairLossInput = "";
        private string actionSpeedInput = "";
        private string inventoryMultiplierInput = "";
        private string inventoryExtraInput = "";
        private string storageMultiplierInput = "";
        private string storageExtraInput = "";
        private string messageXInput = "";
        private string messageYInput = "";
        private string minQualityInput = "";
        private string extraTotemInput = "";
        private string uiScaleInput = "";
        private string lootBounsMultiplierInput = "";
        private string maxStockMultiplierInput = "";
        private string refreshMultiplierInput = "";
        private string enemysMultiplierInput = "";
        //private string bossMultiplierInput = "";
        private string durabilityMultiplierInput = "";
        private string pickupRadiusInput = "";
        private string dynamicScanIntervalInput = "";
        private string enemyHealthMultiplierInput = "";

        public static void ToggleUI()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (!sceneName.Contains("Main") && !sceneName.Contains("Menu") && !sceneName.Contains("Lobby") && !sceneName.Contains("Start") && !sceneName.Contains("开始游戏"))
            {
                // 如果不是主菜单场景，强制关闭设置界面
                ForceCloseUI();
                return;
            }
            if (_instance == null)
            {
                var go = new GameObject("BigInventory_ConfigUI");
                _instance = go.AddComponent<BigInventoryConfigUI>();
                DontDestroyOnLoad(go);
            }

            _instance.EnsureCanvas();

            if (_instance.settingsPanel == null)
                _instance.CreateSettingsPanel();

            bool newState = !_instance.settingsPanel.activeSelf;
            _instance.settingsPanel.SetActive(newState);

            if (!newState)
            {
                BigInventoryConfigManager.SaveConfig();
            }

            _instance.ToggleInputBlocker(newState);

            if (!newState)
            {
                Debug.Log("[BigInventory] 设置界面已关闭，保存配置中...");
                BigInventoryConfigManager.SaveConfig();
                ClearActiveSlider();
            }
            //Debug.Log($"[BigInventory] 设置界面 {(newState ? "已打开" : "已关闭")}");
        }

        //强制关闭UI的方法
        public static void ForceCloseUI()
        {
            if (_instance != null && _instance.settingsPanel != null && _instance.settingsPanel.activeSelf)
            {
                Debug.Log("[BigInventory] 强制关闭设置界面");
                _instance.settingsPanel.SetActive(false);
                _instance.ToggleInputBlocker(false);
                BigInventoryConfigManager.SaveConfig();
                ClearActiveSlider();
            }
        }

        //场景变化时检查是否需要关闭UI
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            //场景加载后检查当前是否在主菜单，如果不是则强制关闭UI
            string sceneName = scene.name;
            if (!sceneName.Contains("Main") && !sceneName.Contains("Menu") && !sceneName.Contains("Lobby") && !sceneName.Contains("Start") && !sceneName.Contains("开始游戏"))
            {
                ForceCloseUI();
            }
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;

            try
            {
                Canvas found = FindObjectOfType<Canvas>();
                if (found != null && found.isActiveAndEnabled && found.gameObject.activeInHierarchy)
                {
                    canvas = found;
                    return;
                }

                GameObject canvasObj = new GameObject("BigInventory_Canvas", typeof(RectTransform));
                canvasObj.layer = LayerMask.NameToLayer("UI");

                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
                DontDestroyOnLoad(canvasObj);

                //Debug.Log("[BigInventory] 已强制创建新的 Canvas。");
            }
            catch (Exception ex)
            {
                Debug.LogError("[BigInventory] 创建 Canvas 失败: " + ex);
            }
        }

        private void CreateSettingsPanel()
        {
            try
            {
                if (canvas == null) { EnsureCanvas(); if (canvas == null) return; }

                // 初始化输入文本
                UpdateInputTexts();
                appliedUIScale = BigInventoryConfigManager.Config.UIScale;

                //主面板 
                settingsPanel = new GameObject("SettingsPanel");
                settingsPanel.transform.SetParent(canvas.transform, false);

                // 先添加RectTransform组件
                RectTransform panelRect = settingsPanel.AddComponent<RectTransform>();
                panelRect.localScale = Vector3.one * appliedUIScale;

                Image bgImage = settingsPanel.AddComponent<Image>();
                bgImage.color = new Color(0f, 0f, 0f, 1f);
                bgImage.raycastTarget = true;

                Outline panelOutline = settingsPanel.AddComponent<Outline>();
                panelOutline.effectColor = new Color(1f, 1f, 0f, 1f);
                panelOutline.effectDistance = new Vector2(5, -5);

                VerticalLayoutGroup layout = settingsPanel.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 10f;
                layout.padding = new RectOffset(20, 20, 20, 20);

                panelRect.sizeDelta = new Vector2(900, 900);
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;

                //主标题和版本
                GameObject mainTitleObj = new GameObject("MainTitle");
                mainTitleObj.transform.SetParent(settingsPanel.transform, false);
                TextMeshProUGUI mainTitle = mainTitleObj.AddComponent<TextMeshProUGUI>();
                mainTitle.text = "更好的鸭科夫 v1.10";
                mainTitle.fontSize = 32;
                mainTitle.fontStyle = FontStyles.Bold;
                mainTitle.color = Color.white;
                mainTitle.alignment = TextAlignmentOptions.Center;
                mainTitle.outlineColor = new Color(0.3f, 0.8f, 1f, 1f);
                mainTitle.outlineWidth = 0.35f;

                LayoutElement mainTitleLayout = mainTitleObj.AddComponent<LayoutElement>();
                mainTitleLayout.preferredHeight = 50;

                //Tab 按钮区域
                GameObject tabButtonContainer = new GameObject("TabButtons");
                tabButtonContainer.transform.SetParent(settingsPanel.transform, false);
                HorizontalLayoutGroup tabButtonLayout = tabButtonContainer.AddComponent<HorizontalLayoutGroup>();
                tabButtonLayout.childAlignment = TextAnchor.MiddleCenter;
                tabButtonLayout.spacing = 10f;

                LayoutElement tabButtonContainerLayout = tabButtonContainer.AddComponent<LayoutElement>();
                tabButtonContainerLayout.preferredHeight = 50;

                // 创建 Tab 按钮
                for (int i = 0; i < tabNames.Length; i++)
                {
                    int tabIndex = i;
                    Button tabButton = CreateTabButton(tabNames[i], () => SelectTab(tabIndex));
                    tabButton.transform.SetParent(tabButtonContainer.transform, false);

                    RectTransform tabRect = tabButton.GetComponent<RectTransform>();
                    tabRect.sizeDelta = new Vector2(150, 40);
                }

                //Tab 内容区域（带滚动条）
                GameObject tabContentContainer = new GameObject("TabContentContainer");
                tabContentContainer.transform.SetParent(settingsPanel.transform, false);

                LayoutElement contentLayout = tabContentContainer.AddComponent<LayoutElement>();
                contentLayout.flexibleHeight = 1f;
                contentLayout.preferredHeight = 600;

                // 滚动视图
                GameObject scrollView = new GameObject("ScrollView");
                scrollView.transform.SetParent(tabContentContainer.transform, false);
                tabScrollRect = scrollView.AddComponent<ScrollRect>();
                tabScrollRect.vertical = true;
                tabScrollRect.horizontal = false;
                tabScrollRect.movementType = ScrollRect.MovementType.Clamped;

                RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
                scrollRectTransform.anchorMin = Vector2.zero;
                scrollRectTransform.anchorMax = Vector2.one;
                scrollRectTransform.sizeDelta = Vector2.zero;
                scrollRectTransform.offsetMin = Vector2.zero;
                scrollRectTransform.offsetMax = Vector2.zero;

                // 视口
                GameObject viewport = new GameObject("Viewport");
                viewport.transform.SetParent(scrollView.transform, false);
                RectTransform viewportRect = viewport.AddComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.sizeDelta = Vector2.zero;
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;

                // 将视口背景设为深灰色以与主面板区分
                Image viewportImage = viewport.AddComponent<Image>();
                viewportImage.color = new Color(0.12f, 0.12f, 0.12f, 1f); // 深灰（和主面板黑色区分）
                viewport.AddComponent<Mask>();
                tabScrollRect.viewport = viewportRect;

                // 创建并配置垂直滚动条（白色进度条，位于 ScrollView 右侧）
                GameObject scrollbarObj = new GameObject("VerticalScrollbar");
                scrollbarObj.transform.SetParent(scrollView.transform, false);
                RectTransform sbRect = scrollbarObj.AddComponent<RectTransform>();
                // 将滚动条放置在右侧，宽度 12
                sbRect.anchorMin = new Vector2(1f, 0f);
                sbRect.anchorMax = new Vector2(1f, 1f);
                sbRect.pivot = new Vector2(1f, 0.5f);
                sbRect.sizeDelta = new Vector2(12f, 0f);
                sbRect.anchoredPosition = new Vector2(-6f, 0f);

                Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
                scrollbar.direction = Scrollbar.Direction.BottomToTop;

                // 背景
                GameObject sbBg = new GameObject("Background");
                sbBg.transform.SetParent(scrollbarObj.transform, false);
                Image sbBgImg = sbBg.AddComponent<Image>();
                sbBgImg.color = Color.clear; // 透明背景
                RectTransform sbBgRect = sbBg.GetComponent<RectTransform>();
                sbBgRect.anchorMin = Vector2.zero;
                sbBgRect.anchorMax = Vector2.one;
                sbBgRect.offsetMin = Vector2.zero;
                sbBgRect.offsetMax = Vector2.zero;

                // 滑块
                GameObject handle = new GameObject("Handle");
                handle.transform.SetParent(scrollbarObj.transform, false);
                Image handleImg = handle.AddComponent<Image>();
                handleImg.color = Color.white; // 白色表示滚动进度
                RectTransform handleRect = handle.GetComponent<RectTransform>();
                handleRect.anchorMin = new Vector2(0f, 0f);
                handleRect.anchorMax = new Vector2(1f, 0.1f);
                handleRect.sizeDelta = Vector2.zero;

                scrollbar.targetGraphic = handleImg;
                scrollbar.handleRect = handleRect;

                // 绑定 Scrollbar 到 ScrollRect
                tabScrollRect.verticalScrollbar = scrollbar;
                tabScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                tabScrollRect.vertical = true;

                // 内容区域
                GameObject content = new GameObject("Content");
                content.transform.SetParent(viewport.transform, false);
                RectTransform contentRect = content.AddComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0.5f, 1f);
                contentRect.anchorMax = new Vector2(0.5f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.sizeDelta = new Vector2(750, 0);

                VerticalLayoutGroup contentVertical = content.AddComponent<VerticalLayoutGroup>();
                contentVertical.childAlignment = TextAnchor.UpperCenter;
                contentVertical.spacing = 15f;
                contentVertical.padding = new RectOffset(10, 10, 10, 10);

                ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
                contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                tabScrollRect.content = contentRect;

                // 底部固定区域
                bottomArea = new GameObject("BottomArea");
                bottomArea.transform.SetParent(settingsPanel.transform, false);

                LayoutElement bottomLayout = bottomArea.AddComponent<LayoutElement>();
                bottomLayout.preferredHeight = 150;

                VerticalLayoutGroup bottomVertical = bottomArea.AddComponent<VerticalLayoutGroup>();
                bottomVertical.childAlignment = TextAnchor.MiddleCenter;
                bottomVertical.spacing = 10f;

                // 按钮列
                GameObject buttonRow = new GameObject("ButtonRow");
                buttonRow.transform.SetParent(bottomArea.transform, false);
                HorizontalLayoutGroup buttonRowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
                buttonRowLayout.childAlignment = TextAnchor.MiddleCenter;
                buttonRowLayout.spacing = 20f;

                // 重置和关闭按钮
                Button resetBtn = CreateButton("重置默认值", () =>
                {
                    BigInventoryConfigManager.ResetToDefault();
                    UpdateInputTexts();
                    // 重新创建界面以更新所有值
                    if (settingsPanel != null)
                    {
                        Destroy(settingsPanel);
                        CreateSettingsPanel();
                    }
                    Debug.Log("[BigInventory] 配置已重置为默认值");
                });
                resetBtn.transform.SetParent(buttonRow.transform, false);
                SetButtonSize(resetBtn, 180, 40);

                // 保存引用并设置稳定 GameObject 名称（便于调试/查找）
                resetButton = resetBtn;
                resetBtn.gameObject.name = "Button_Reset";

                Button closeBtn = CreateButton("关闭界面", ForceCloseUI);
                closeBtn.transform.SetParent(buttonRow.transform, false);
                SetButtonSize(closeBtn, 180, 40);

                closeButton = closeBtn;
                closeBtn.gameObject.name = "Button_Close";

                // 创建语言切换按钮并保存引用以便后续直接使用
                Button langBtn = CreateButton("Change to English", () => ToggleLanguage());
                langBtn.transform.SetParent(bottomArea.transform, false);
                SetButtonSize(langBtn, 400, 45);

                // 保存引用，避免后续通过 GetComponentInChildren 误拿到其它按钮（例如重置按钮）
                languageToggleButton = langBtn;
                // 给它一个固定名字，便于调试/查找
                langBtn.gameObject.name = "LangButton";
                // 将语言切换按钮文本颜色设为橙色
                var langBtnText = langBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (langBtnText != null) langBtnText.color = new Color(1f, 0.55f, 0f);

                // 警告文本
                GameObject warningObj = new GameObject("WarningText");
                warningObj.transform.SetParent(bottomArea.transform, false);
                TextMeshProUGUI warningText = warningObj.AddComponent<TextMeshProUGUI>();
                warningText.text = "⚠ 警告: 修改设置后需要重启游戏或重新启用MOD才能完全生效！";
                warningText.fontSize = 24;
                warningText.color = new Color(1f, 0.4f, 0.1f, 1f);
                warningText.alignment = TextAlignmentOptions.Center;

                LayoutElement warningLayout = warningObj.AddComponent<LayoutElement>();
                warningLayout.preferredHeight = 40;

                // 创建 Tab 内容
                CreateModConfigTabContent(content.transform);

                // 默认选择第一个 Tab
                SelectTab(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BigInventory] 创建设置面板时出错: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void CreateModConfigTabContent(Transform parent)
        {
            // UI 缩放设置
            CreateSectionTitle("自定义UI缩放", parent);
            CreateUIScaleControls(parent);
            // 重量减轻设置
            CreateSectionTitle("物品重量减轻", parent);
            CreateToggleWithControls("启用物品减重", BigInventoryConfigManager.Config.EnableWeightReduction,
                v => BigInventoryConfigManager.Config.EnableWeightReduction = v,
                "重量系数", 0.1f, 1.0f, BigInventoryConfigManager.Config.WeightFactor,
                v => { BigInventoryConfigManager.Config.WeightFactor = v; }, weightFactorInput, false, parent);

            // 堆叠倍率设置
            CreateSectionTitle("物品堆叠设置", parent);
            CreateToggleWithControls("启用堆叠倍率增加\n(现已支持部分不可堆叠物)", BigInventoryConfigManager.Config.EnableStackMultiplier,
                v => BigInventoryConfigManager.Config.EnableStackMultiplier = v,
                "堆叠倍率", 1, 200, BigInventoryConfigManager.Config.StackMultiplier,
                v => { BigInventoryConfigManager.Config.StackMultiplier = (int)v; }, stackMultiplierInput, true, parent);

            // 修理损耗设置
            CreateSectionTitle("修理耐久损耗", parent);
            CreateToggleWithControls("启用修理损耗减少", BigInventoryConfigManager.Config.EnableRepairLossReduction,
                v => BigInventoryConfigManager.Config.EnableRepairLossReduction = v,
                "耐久损耗倍率\n低于1即为减少损耗", 0f, 1.0f, BigInventoryConfigManager.Config.RepairLossMultiplier,
                v => { BigInventoryConfigManager.Config.RepairLossMultiplier = v; }, repairLossInput, false, parent);

            CreateToggleWithControls("启用多倍耐久", BigInventoryConfigManager.Config.EnableDurabilityDouble,
                v => BigInventoryConfigManager.Config.EnableDurabilityDouble = v,
                "耐久倍率", 1f, 10.0f, BigInventoryConfigManager.Config.DurabilityMultiplier,
                v => { BigInventoryConfigManager.Config.DurabilityMultiplier = v; }, durabilityMultiplierInput, true, parent);

            // 动作速度设置
            CreateSectionTitle("动作速度设置", parent);
            CreateToggleWithControls("启用动作速度加快\n(使用道具及搜索)", BigInventoryConfigManager.Config.EnableActionSpeed,
                v => BigInventoryConfigManager.Config.EnableActionSpeed = v,
                "速度倍率", 1f, 10.0f, BigInventoryConfigManager.Config.ActionSpeedMultiplier,
                v => { BigInventoryConfigManager.Config.ActionSpeedMultiplier = v; }, actionSpeedInput, false, parent);

            // 背包容量设置
            CreateSectionTitle("背包容量设置", parent);
            CreateToggleWithControls("启用背包容量修改", BigInventoryConfigManager.Config.EnableInventoryCapacityPatch,
                v => BigInventoryConfigManager.Config.EnableInventoryCapacityPatch = v,
                "背包容量倍率", 1, 10, BigInventoryConfigManager.Config.InventoryMultiplier,
                v => { BigInventoryConfigManager.Config.InventoryMultiplier = (int)v; }, inventoryMultiplierInput, true, parent);

            CreateSliderWithInput("背包额外容量", 0, 500, BigInventoryConfigManager.Config.InventoryExtraCapacity,
                v => { BigInventoryConfigManager.Config.InventoryExtraCapacity = (int)v; }, inventoryExtraInput, true, parent);

            // 仓库容量设置
            CreateSectionTitle("仓库容量设置", parent);
            CreateToggleWithControls("启用仓库容量修改", BigInventoryConfigManager.Config.EnablePlayerStoragePatch,
                v => BigInventoryConfigManager.Config.EnablePlayerStoragePatch = v,
                "仓库容量倍率", 1, 10, BigInventoryConfigManager.Config.PlayerStorageMultiplier,
                v => { BigInventoryConfigManager.Config.PlayerStorageMultiplier = (int)v; }, storageMultiplierInput, true, parent);

            CreateSliderWithInput("仓库额外容量", 0, 1000, BigInventoryConfigManager.Config.PlayerStorageExtraAdd,
                v => { BigInventoryConfigManager.Config.PlayerStorageExtraAdd = (int)v; }, storageExtraInput, true, parent);

            // 自动拾取设置
            CreateSectionTitle("自动拾取设置", parent);
            CreateToggleWithControls("启用自动拾取\n(如果卡顿请开启开箱拾取)", BigInventoryConfigManager.Config.EnableAutoCollectBullets,
                v => BigInventoryConfigManager.Config.EnableAutoCollectBullets = v,
                "子弹拾取最低品质", 1, 6, BigInventoryConfigManager.Config.MinAutoCollectQuality,
                v => { BigInventoryConfigManager.Config.MinAutoCollectQuality = (int)v; }, minQualityInput, true, parent);

            CreateToggle("启用开箱拾取模式\n(替代范围拾取)", BigInventoryConfigManager.Config.EnableOpenCollect,
                v => BigInventoryConfigManager.Config.EnableOpenCollect = v, parent);

            CreateToggle("启用心愿单拾取模式\n(替代原拾取逻辑)", BigInventoryConfigManager.Config.EnableWishlistCollect,
                v => BigInventoryConfigManager.Config.EnableWishlistCollect = v, parent);

            CreateToggle("启用战利品自动卸弹", BigInventoryConfigManager.Config.EnableAutoUnloadGuns,
                v => BigInventoryConfigManager.Config.EnableAutoUnloadGuns = v, parent);

            CreateSliderWithInput("自动拾取范围", 5f, 50f, BigInventoryConfigManager.Config.PickupRadius,
                v => { BigInventoryConfigManager.Config.PickupRadius = v; }, pickupRadiusInput, false, parent);

            CreateSliderWithInput("自动拾取扫描间隔", 0.5f, 5f, BigInventoryConfigManager.Config.DynamicScanInterval,
                v => { BigInventoryConfigManager.Config.DynamicScanInterval = v; }, dynamicScanIntervalInput, false, parent);

            CreateSliderWithInput("拾取提示X坐标", -1800, -100, (int)BigInventoryConfigManager.Config.MessageX,
                v => { BigInventoryConfigManager.Config.MessageX = v; }, messageXInput, true, parent);

            CreateSliderWithInput("拾取提示Y坐标", -1200, -100, (int)BigInventoryConfigManager.Config.MessageY,
                v => { BigInventoryConfigManager.Config.MessageY = v; }, messageYInput, true, parent);

            // 图腾及配件设置
            CreateSectionTitle("图腾及配件设置", parent);
            CreateToggleWithControls("启用额外图腾槽位\n(额外槽位滚动鼠标进行查看)", BigInventoryConfigManager.Config.EnableMoreTotemslot,
                v => BigInventoryConfigManager.Config.EnableMoreTotemslot = v,
                "槽位数量", 1, 50, BigInventoryConfigManager.Config.ExtraTotemSlotCount,
                v => { BigInventoryConfigManager.Config.ExtraTotemSlotCount = (int)v; }, extraTotemInput, true, parent);

            CreateToggle("启用图腾及配件无负面效果\n(修改后重进游戏生效)", BigInventoryConfigManager.Config.EnableNoNegative,
                v => BigInventoryConfigManager.Config.EnableNoNegative = v, parent);

            CreateToggle("启用图腾及配件双倍正面效果\n(需启用无负面功能)", BigInventoryConfigManager.Config.EnableDoubleattributes,
               v => BigInventoryConfigManager.Config.EnableDoubleattributes = v, parent);

            CreateToggle("启用额外武器配件槽位\n(修改后重进游戏生效)", BigInventoryConfigManager.Config.EnableExtraSlots,
               v => BigInventoryConfigManager.Config.EnableExtraSlots = v, parent);

            // 额外掉落设置
            CreateSectionTitle("额外掉落设置", parent);
            CreateToggleWithControls("启用额外掉落\n(所有箱子动态生成额外的物品)", BigInventoryConfigManager.Config.EnableLootBouns,
                v => BigInventoryConfigManager.Config.EnableLootBouns = v,
                "额外掉落效果倍率", 1, 5, BigInventoryConfigManager.Config.LootBounsMultiplier,
                v => { BigInventoryConfigManager.Config.LootBounsMultiplier = (int)v; }, lootBounsMultiplierInput, true, parent);

            // 额外敌人设置
            CreateSectionTitle("额外敌人设置", parent);
            CreateToggleWithControls("启用生成额外敌人", BigInventoryConfigManager.Config.EnableMoreEnemys,
                v => BigInventoryConfigManager.Config.EnableMoreEnemys = v,
                "敌人生成倍率", 1f, 10f, BigInventoryConfigManager.Config.EnemysMultiplier,
                v => { BigInventoryConfigManager.Config.EnemysMultiplier = v; }, enemysMultiplierInput, true, parent);

            //CreateToggleWithControls("启用生成额外Boss", BigInventoryConfigManager.Config.EnableMoreBoss,
            //v => BigInventoryConfigManager.Config.EnableMoreBoss = v,
            //"Boss生成倍率", 1f, 10f, BigInventoryConfigManager.Config.BossMultiplier,
            //v => { BigInventoryConfigManager.Config.BossMultiplier = v; }, bossMultiplierInput, true, parent);

            // 角色天赋设置
            CreateSectionTitle("角色天赋设置", parent);
            CreateToggle("启用所有天赋无负面效果", BigInventoryConfigManager.Config.EnableEndowment,
                v => BigInventoryConfigManager.Config.EnableEndowment = v, parent);

            CreateToggle("启用天赋即时解锁\n(无需等待解锁时间)", BigInventoryConfigManager.Config.EnablePerkInstantUnlock,
                v => BigInventoryConfigManager.Config.EnablePerkInstantUnlock = v, parent);

            // 商店修改设置
            CreateSectionTitle("商店修改设置", parent);
            CreateToggleWithControls("启用商店修改", BigInventoryConfigManager.Config.EnableShopModifier,
                v => BigInventoryConfigManager.Config.EnableShopModifier = v,
                 "商店库存倍率", 1, 20, BigInventoryConfigManager.Config.MaxStockMultiplier,
                v => { BigInventoryConfigManager.Config.MaxStockMultiplier = (int)v; }, maxStockMultiplierInput, true, parent);

            CreateSliderWithInput("黑市升级效果倍率", 1, 20, BigInventoryConfigManager.Config.RefreshMultiplier,
               v => { BigInventoryConfigManager.Config.RefreshMultiplier = (int)v; }, refreshMultiplierInput, true, parent);

            CreateToggle("启用购买全部按钮", BigInventoryConfigManager.Config.EnableBuyAll,
                v => BigInventoryConfigManager.Config.EnableBuyAll = v, parent);
            // 敵鴨血量縮放
            CreateSectionTitle("敵鴨血量縮放", parent);
            CreateToggleWithControls("啟用敵鴨血量縮放", BigInventoryConfigManager.Config.EnableEnemyHealthMultiply,
                v => BigInventoryConfigManager.Config.EnableEnemyHealthMultiply = v,
                "血量縮放倍率", 0.01f, 100f, BigInventoryConfigManager.Config.EnemyHealthMultiplier,
                v => { BigInventoryConfigManager.Config.EnemyHealthMultiplier = (float)v; }, enemyHealthMultiplierInput, false, parent);
        }

        private void CreateUIScaleControls(Transform parent)
        {
            GameObject container = new GameObject("UIScaleContainer");
            container.transform.SetParent(parent, false);

            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 15f;

            LayoutElement layoutElem = container.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 50;

            // 标签
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(container.transform, false);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "UI缩放比例:";
            labelText.fontSize = 22;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center; // 修改为居中

            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 120;

            // 数值显示
            GameObject valueObj = new GameObject("ValueText");
            valueObj.transform.SetParent(container.transform, false);
            TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
            valueText.text = appliedUIScale.ToString("F2");
            valueText.fontSize = 22;
            valueText.color = Color.yellow;
            valueText.alignment = TextAlignmentOptions.MidlineRight;

            LayoutElement valueLayout = valueObj.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 50;

            // 滑块
            GameObject sliderObj = new GameObject("UIScaleSlider");
            sliderObj.transform.SetParent(container.transform, false);

            LayoutElement sliderLayout = sliderObj.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.preferredHeight = 30;

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0.5f;
            slider.maxValue = 2.0f;
            slider.value = appliedUIScale;
            slider.wholeNumbers = false;

            // 设置滑块视觉
            SetupSliderVisuals(slider, new Color(0f, 0.8f, 0.1f, 0.9f));

            // 输入框
            GameObject inputObj = new GameObject("UIScaleInput");
            inputObj.transform.SetParent(container.transform, false);

            Image inputBg = inputObj.AddComponent<Image>();
            // 灰白背景
            inputBg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var inputBgOutline = inputObj.AddComponent<Outline>();
            // 淡青描边
            inputBgOutline.effectColor = new Color(0.6f, 1f, 1f, 1f);
            inputBgOutline.effectDistance = new Vector2(1f, -1f);

            TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.text = uiScaleInput;

            // 输入框文本组件
            GameObject inputTextObj = new GameObject("InputText");
            inputTextObj.transform.SetParent(inputObj.transform, false);
            RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(5, 0);
            inputTextRect.offsetMax = new Vector2(-5, 0);

            TextMeshProUGUI inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
            inputText.text = uiScaleInput;
            inputText.fontSize = 18;
            inputText.color = Color.black;
            inputText.alignment = TextAlignmentOptions.Center;// 修改为居中

            inputField.textComponent = inputText;
            inputField.textViewport = inputTextRect;

            RectTransform inputRect = inputObj.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(50, 30);

            LayoutElement inputLayout = inputObj.AddComponent<LayoutElement>();
            inputLayout.preferredWidth = 50;

            // 应用按钮
            Button applyBtn = CreateButton("应用", ApplyUIScale);
            applyBtn.transform.SetParent(container.transform, false);
            SetButtonSize(applyBtn, 80, 30);

            // 事件监听
            slider.onValueChanged.AddListener(value =>
            {
                appliedUIScale = value;
                valueText.text = value.ToString("F2");
                uiScaleInput = value.ToString("F2");
                inputField.text = uiScaleInput;
            });

            inputField.onEndEdit.AddListener(value =>
            {
                if (float.TryParse(value, out float numValue))
                {
                    numValue = Mathf.Clamp(numValue, 0.5f, 2.0f);
                    appliedUIScale = numValue;
                    slider.value = numValue;
                    valueText.text = numValue.ToString("F2");
                    uiScaleInput = numValue.ToString("F2");
                    inputField.text = uiScaleInput;
                }
                else
                {
                    inputField.text = uiScaleInput;
                }
            });
        }

        private void ApplyUIScale()
        {
            BigInventoryConfigManager.Config.UIScale = appliedUIScale;

            // 立即应用缩放效果
            if (settingsPanel != null)
            {
                RectTransform panelRect = settingsPanel.GetComponent<RectTransform>();
                panelRect.localScale = Vector3.one * appliedUIScale;
            }

            Debug.Log($"[BigInventory] UI缩放已应用: {appliedUIScale:F2}x");
            BigInventoryConfigManager.SaveConfig();
        }

        private void CreateSectionTitle(string title, Transform parent)
        {
            // 创建一个容器以便能放置背景、文字底色和文本，并控制高度
            GameObject titleContainer = new GameObject("SectionTitle_" + title);
            titleContainer.transform.SetParent(parent, false);

            // 水平居中容器内的内容
            HorizontalLayoutGroup hl = titleContainer.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childForceExpandHeight = false;
            hl.childForceExpandWidth = false;
            hl.childControlHeight = false;
            hl.childControlWidth = false;

            // 背景图像（银灰色）
            GameObject bg = new GameObject("TitleBackground");
            bg.transform.SetParent(titleContainer.transform, false);
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.75f, 0.75f, 0.78f, 1f); // 银灰色（整体背景）

            // 淡红色分隔长线
            GameObject separatorLine = new GameObject("SeparatorLine");
            separatorLine.transform.SetParent(titleContainer.transform, false);
            Image lineImage = separatorLine.AddComponent<Image>();
            lineImage.color = new Color(1f, 0.6f, 0.6f, 0.4f); // 淡红色，半透明

            LayoutElement lineLayout = separatorLine.AddComponent<LayoutElement>();
            lineLayout.preferredHeight = 12f; // 高度12
            lineLayout.flexibleWidth = 1f; // 宽度覆盖整个内容区

            RectTransform lineRect = separatorLine.GetComponent<RectTransform>();
            lineRect.sizeDelta = new Vector2(0f, 12f);

            // 文本对象
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(titleContainer.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = title;
            titleText.fontSize = 27;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(1f, 0.55f, 0f, 1f); // 橙色文字
            titleText.alignment = TextAlignmentOptions.Center;
            // 使用 TextMeshPro 描边来实现银白色描边
            titleText.outlineColor = new Color(0.92f, 0.94f, 0.96f, 1f);
            titleText.outlineWidth = 0.2f;

            // 计算并设置高度：基于文本 fontSize 并留空 5 像素（近似）
            LayoutElement textLayout = titleObj.AddComponent<LayoutElement>();
            textLayout.preferredHeight = titleText.fontSize + 6f;

            float bgHeight = titleText.fontSize + 5f;
            LayoutElement bgLayout = bg.AddComponent<LayoutElement>();
            bgLayout.preferredHeight = bgHeight;
            bgLayout.minHeight = bgHeight;

            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(0, bgHeight);

            // 确保层级顺序：整体背景在最底，文字在最上
            bg.transform.SetAsFirstSibling();
            separatorLine.transform.SetSiblingIndex(1);
            titleObj.transform.SetSiblingIndex(2);

            // 让容器高度与背景一致
            LayoutElement containerLayout = titleContainer.AddComponent<LayoutElement>();
            containerLayout.preferredHeight = bgHeight;
        }

        private void CreateToggleWithControls(string label, bool currentValue, Action<bool> onToggleChanged,
            string sliderLabel, float min, float max, float current, Action<float> onSliderChanged,
            string inputFieldInitialValue, bool isInteger, Transform parent)
        {
            // 创建开关
            CreateToggle(label, currentValue, onToggleChanged, parent);

            // 始终创建对应的滑块（即使开关未启用也显示滑块）
            CreateSliderWithInput(sliderLabel, min, max, current, onSliderChanged, inputFieldInitialValue, isInteger, parent);
        }

        private void CreateSliderWithInput(string label, float min, float max, float current,
            Action<float> onValueChanged, string inputFieldInitialValue, bool isInteger, Transform parent)
        {
            GameObject container = new GameObject("SliderWithInput_" + label);
            container.transform.SetParent(parent, false);

            VerticalLayoutGroup verticalLayout = container.AddComponent<VerticalLayoutGroup>();
            verticalLayout.childAlignment = TextAnchor.MiddleLeft;
            verticalLayout.spacing = 5f;

            LayoutElement layoutElem = container.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 70;

            // 第一列：标签和输入框
            GameObject topRow = new GameObject("TopRow");
            topRow.transform.SetParent(container.transform, false);
            HorizontalLayoutGroup topLayout = topRow.AddComponent<HorizontalLayoutGroup>();
            topLayout.childAlignment = TextAnchor.MiddleLeft;
            topLayout.spacing = 10f;

            LayoutElement topLayoutElem = topRow.AddComponent<LayoutElement>();
            topLayoutElem.preferredHeight = 30;

            // 标签
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(topRow.transform, false);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label + ":";
            labelText.fontSize = 18;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;

            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 120;

            // 数值显示（靠近标签，而不是输入框）
            GameObject valueObj = new GameObject("ValueText");
            valueObj.transform.SetParent(topRow.transform, false);
            TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
            valueText.text = current.ToString(isInteger ? "F0" : "F2");
            valueText.fontSize = 20;
            valueText.color = Color.yellow;
            // 靠近标签，靠左显示
            valueText.alignment = TextAlignmentOptions.MidlineLeft;

            LayoutElement valueLayout = valueObj.AddComponent<LayoutElement>();
            // 缩小宽度，使其紧靠标签
            valueLayout.preferredWidth = 60;
            valueLayout.minWidth = 30;

            // 输入框（放在右侧）
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(topRow.transform, false);

            Image inputBg = inputObj.AddComponent<Image>();
            // 灰白背景
            inputBg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var inputBgOutline = inputObj.AddComponent<Outline>();
            // 淡青描边
            inputBgOutline.effectColor = new Color(0.6f, 1f, 1f, 1f);
            inputBgOutline.effectDistance = new Vector2(1f, -1f);

            TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.text = inputFieldInitialValue;

            // 输入框文本组件
            GameObject inputTextObj = new GameObject("InputText");
            inputTextObj.transform.SetParent(inputObj.transform, false);
            RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(5, 0);
            inputTextRect.offsetMax = new Vector2(-5, 0);

            TextMeshProUGUI inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
            inputText.text = inputFieldInitialValue;
            inputText.fontSize = 18;
            inputText.color = Color.black;
            inputText.alignment = TextAlignmentOptions.Center; // 保持输入框文本居中

            inputField.textComponent = inputText;
            inputField.textViewport = inputTextRect;

            RectTransform inputRect = inputObj.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(80, 25); // 加宽输入框以便位于右侧

            LayoutElement inputLayout = inputObj.AddComponent<LayoutElement>();
            // 让输入框占据剩余空间（flexibleWidth），以便 ValueText 更靠近标签
            inputLayout.flexibleWidth = 1f;
            inputLayout.preferredWidth = 80;

            // 第二列：滑块
            GameObject sliderRow = new GameObject("SliderRow");
            sliderRow.transform.SetParent(container.transform, false);

            LayoutElement sliderRowLayout = sliderRow.AddComponent<LayoutElement>();
            sliderRowLayout.preferredHeight = 30;

            Slider slider = sliderRow.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = current;
            slider.wholeNumbers = isInteger;

            // 设置滑块视觉
            SetupSliderVisuals(slider, new Color(0f, 0.8f, 0.1f, 0.9f));

            // 使用局部变量来存储当前值
            string currentInputValue = inputFieldInitialValue;

            // 事件监听
            slider.onValueChanged.AddListener(value =>
            {
                string formattedValue = value.ToString(isInteger ? "F0" : "F2");
                valueText.text = formattedValue;
                currentInputValue = formattedValue;
                inputField.text = currentInputValue;
                onValueChanged(value);
            });

            inputField.onEndEdit.AddListener(value =>
            {
                if (float.TryParse(value, out float numValue))
                {
                    numValue = Mathf.Clamp(numValue, min, max);
                    slider.value = numValue;
                    string formattedValue = numValue.ToString(isInteger ? "F0" : "F2");
                    valueText.text = formattedValue;
                    currentInputValue = formattedValue;
                    inputField.text = currentInputValue;
                    onValueChanged(numValue);
                }
                else
                {
                    inputField.text = currentInputValue;
                }
            });
        }

        private void SetupSliderVisuals(Slider slider, Color fillColor)
        {
            // 使用传入的 slider 对象而不是 sliderObj
            GameObject sliderGameObject = slider.gameObject;
            // 背景：亮黄色细长条
            GameObject background = new GameObject("Background");
            background.transform.SetParent(sliderGameObject.transform, false);
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 0f, 0.8f); // 亮黄色，稍微透明
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.4f);    // 垂直居中，更细长
            bgRect.anchorMax = new Vector2(1f, 0.6f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // 刻度尺：黑色细线，在背景上方 
            GameObject tickLine = new GameObject("TickLine");
            tickLine.transform.SetParent(sliderGameObject.transform, false);
            Image tickImage = tickLine.AddComponent<Image>();
            tickImage.color = new Color(1f, 1f, 0f, 0.9f); // 黄色刻度线
            RectTransform tickRect = tickLine.GetComponent<RectTransform>();

            // 设置刻度线尺寸和位置（在背景上方）
            tickRect.anchorMin = new Vector2(0f, 0.4f);
            tickRect.anchorMax = new Vector2(1f, 0.6f);
            tickRect.sizeDelta = new Vector2(0f, 2f); // 细线
            tickRect.anchoredPosition = Vector2.zero;

            // 创建刻度标记
            for (int i = 0; i <= 10; i++)
            {
                GameObject tick = new GameObject($"Tick_{i}");
                tick.transform.SetParent(tickLine.transform, false);
                Image tickMark = tick.AddComponent<Image>();
                tickMark.color = new Color(0f, 0f, 0f, 1f); // 黑色刻度标记

                RectTransform tickRt = tick.GetComponent<RectTransform>();
                tickRt.anchorMin = new Vector2(i / 10f, 0f);
                tickRt.anchorMax = new Vector2(i / 10f, 1f);
                tickRt.sizeDelta = new Vector2(2f, 0f); // 垂直贯穿的细线
                tickRt.anchoredPosition = new Vector2(0, 0);
            }

            // 填充条：亮绿色，在背景和刻度上方
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGameObject.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.4f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.6f);
            fillAreaRect.offsetMin = new Vector2(2f, 2f);   // 稍微内边距
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0f, 0.8f, 0.1f, 0.9f); // 绿色填充
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // 拖动手柄：长方形，在填充条上方
            GameObject handleSlideArea = new GameObject("Handle Slide Area");
            handleSlideArea.transform.SetParent(sliderGameObject.transform, false);
            RectTransform handleAreaRect = handleSlideArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0.4f);
            handleAreaRect.anchorMax = new Vector2(1f, 0.6f);
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleSlideArea.transform, false);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.2f, 1f, 0.2f, 1f); // 绿色手柄
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(12f, 25f); // 细长手柄
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;

            // --- 黑色描边 ---
            Outline outline = handle.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);

            // --- 确保正确的层级顺序 ---
            background.transform.SetAsFirstSibling();    // 背景在最底层
            tickLine.transform.SetSiblingIndex(1);       // 刻度在背景之上
            fillArea.transform.SetSiblingIndex(2);       // 填充在刻度之上
            handleSlideArea.transform.SetAsLastSibling(); // 手柄在最上层

            // 连接滑块组件
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
        }

        private void CreateToggle(string label, bool value, Action<bool> onChanged, Transform parent)
        {
            GameObject row = new GameObject("ToggleRow_" + label);
            row.transform.SetParent(parent, false);

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 210f; // 减少间距，向左移动

            // 不让 HorizontalLayoutGroup 强制控制子对象的宽/高或强制扩展
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // 左侧文字
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            TextMeshProUGUI txt = labelObj.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 20;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Left;
            RectTransform labelRect = txt.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(300, 36); // 基本宽度
            // 确保标签不被父 LayoutGroup 强制拉伸，同时不要把开关推到最右
            var labelLayoutElem = labelObj.AddComponent<LayoutElement>();
            labelLayoutElem.preferredWidth = 200; // 适中宽度，避免占满整列
            labelLayoutElem.minWidth = 50;
            labelLayoutElem.flexibleWidth = 0;

            // 外框背景
            GameObject bgObj = new GameObject("ToggleBG");
            bgObj.transform.SetParent(row.transform, false);
            Image bg = bgObj.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            Outline outline = bgObj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.3f);
            outline.effectDistance = new Vector2(2, -2);

            // Toggle 本体
            Toggle toggle = bgObj.AddComponent<Toggle>();
            toggle.isOn = value;

            // 添加悬浮高亮：使用 EventTrigger 来处理 PointerEnter / PointerExit
            var bgImage = bgObj.GetComponent<Image>();
            Color originalBgColor = bgImage.color;
            Color hoverColor = new Color(Mathf.Min(originalBgColor.r * 1.6f, 1f), Mathf.Min(originalBgColor.g * 1.6f, 1f), Mathf.Min
                (originalBgColor.b * 1.4f, 1f), originalBgColor.a);
            var eventTrigger = bgObj.AddComponent<EventTrigger>();
            var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entryEnter.callback.AddListener((evt) => { bgImage.color = hoverColor; });
            eventTrigger.triggers.Add(entryEnter);
            var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((evt) => { bgImage.color = originalBgColor; });
            eventTrigger.triggers.Add(entryExit);

            // 设置背景的固定尺寸并用 LayoutElement 锁定，避免父 LayoutGroup 强制拉伸
            RectTransform rect = bgObj.GetComponent<RectTransform>();
            // 背景比勾选略大几个像素（可按需调整）
            float bgSize = 18f; // 建议比勾选尺寸大 4-6 像素
            rect.sizeDelta = new Vector2(bgSize, bgSize);
            var bgLayoutElem = bgObj.AddComponent<LayoutElement>();
            bgLayoutElem.preferredWidth = bgSize;
            bgLayoutElem.preferredHeight = bgSize;
            bgLayoutElem.minWidth = bgSize;
            bgLayoutElem.minHeight = bgSize;

            GameObject checkObj = new GameObject("Checkmark");
            checkObj.transform.SetParent(bgObj.transform, false);
            Image ck = checkObj.AddComponent<Image>();
            ck.color = new Color(0.3f, 1f, 0.3f);
            // 勾选图标尺寸（相对于背景略小）
            var ckRect = ck.GetComponent<RectTransform>();
            float ckSize = 12f; // 勾选方块尺寸，可按需调整
            ckRect.sizeDelta = new Vector2(ckSize, ckSize);
            // 保证勾选在背景中心
            ckRect.anchorMin = new Vector2(0.5f, 0.5f);
            ckRect.anchorMax = new Vector2(0.5f, 0.5f);
            ckRect.anchoredPosition = Vector2.zero;

            // 把 checkmark 当作 graphic（勾选的显式图像）
            toggle.graphic = ck;

            toggle.onValueChanged.AddListener((v) =>
            {
                try
                {
                    onChanged?.Invoke(v);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[BigInventory] Toggle onChanged 执行出错: " + ex);
                }
            });
        }

        private Button CreateTabButton(string text, Action onClick)
        {
            GameObject btnObj = new GameObject("TabButton_" + text);
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.4f, 0.8f, 0.8f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = 20;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150, 40);
            return btn;
        }

        private void SelectTab(int tabIndex)
        {
            selectedTab = tabIndex;
            //Debug.Log($"[BigInventory] 切换到Tab: {tabNames[tabIndex]}");
        }

        private void ToggleLanguage()
        {
            // 优先使用创建时保存的引用
            Button langBtn = languageToggleButton;

            // 兼容性回退：如果引用为 null，再尝试基于文本精确查找（只作为后备）
            if (langBtn == null && bottomArea != null)
            {
                // 在 bottomArea 的直接子对象与子孙中查找文本完全匹配的按钮（避免部分匹配）
                var buttons = bottomArea.GetComponentsInChildren<Button>(true);
                foreach (var b in buttons)
                {
                    var t = b.GetComponentInChildren<TextMeshProUGUI>();
                    if (t != null)
                    {
                        var text = t.text.Trim();
                        if (text == "Change to English" || text == "切换中文")
                        {
                            langBtn = b;
                            break;
                        }
                    }
                }
            }

            bool isChinese = false;
            if (langBtn != null)
            {
                var btnText = langBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    // 如果按钮显示"切换中文"，说明当前是英文界面
                    isChinese = btnText.text.Contains("切换中文");
                }
            }

            if (isChinese)
            {
                ApplyLanguage("cn");
                if (langBtn != null)
                {
                    var btnText = langBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (btnText != null) btnText.text = "Change to English";
                }
                //Debug.Log("[BigInventory] 已切换到中文界面");
            }
            else
            {
                ApplyLanguage("en");
                if (langBtn != null)
                {
                    var btnText = langBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (btnText != null) btnText.text = "切换中文";
                }
            }
        }

        private void ApplyLanguage(string lang)
        {
            bool english = lang == "en";

            // 主标题
            var mainTitle = settingsPanel?.transform.Find("MainTitle")?.GetComponent<TextMeshProUGUI>();
            if (mainTitle != null)
                mainTitle.text = english ? "Better Duckov v1.10" : "更好的鸭科夫 v1.10";

            // 警告文本
            var warningText = bottomArea?.transform.Find("WarningText")?.GetComponent<TextMeshProUGUI>();
            if (warningText != null)
                warningText.text = english
                    ? "⚠ Warning: Changes require game restart or MOD re-enable to take effect!"
                    : "⚠ 警告: 修改设置后需要重启游戏或重新启用MOD才能完全生效！";

            // Tab 按钮
            var tabButtons = settingsPanel?.GetComponentsInChildren<Button>();
            if (tabButtons != null)
            {
                foreach (var tabBtn in tabButtons)
                {
                    var tabText = tabBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (tabText != null && tabBtn.name.Contains("TabButton"))
                    {
                        if (tabText.text.Contains("MOD配置") || tabText.text.Contains("MOD Settings"))
                            tabText.text = english ? "MOD Settings" : "MOD配置";
                    }
                }
            }

            // 更新重置与关闭按钮文本（使用保存的引用）
            if (resetButton != null)
            {
                var txt = resetButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = english ? "Reset to Default" : "重置默认值";
            }
            if (closeButton != null)
            {
                var txt = closeButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = english ? "Close Panel" : "关闭界面";
            }

            Button langBtn = languageToggleButton;
            if (langBtn != null)
            {
                var langText = langBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (langText != null)
                {
                    langText.text = english ? "切换中文" : "Change to English";
                    langText.color = new Color(1f, 0.55f, 0f);
                }
            }

            // 所有文本内容的切换
            if (settingsPanel != null)
            {
                foreach (var text in settingsPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    string old = text.text.Trim();

                    // 跳过按钮文本
                    if (text.transform.parent?.GetComponent<Button>() != null)
                        continue;

                    // 跳过语言切换按钮文本
                    if (langBtn != null && text.transform.IsChildOf(langBtn.transform))
                        continue;

                    if (english)
                    {
                        // 章节标题
                        if (old.Contains("自定义UI缩放")) text.text = "Custom UI Scale";
                        else if (old.Contains("物品重量减轻")) text.text = "Item Weight Reduction";
                        else if (old.Contains("物品堆叠设置")) text.text = "Item Stack Settings";
                        else if (old.Contains("修理耐久损耗")) text.text = "Repair Durability Loss";
                        else if (old.Contains("动作速度设置")) text.text = "Action Speed Settings";
                        else if (old.Contains("背包容量设置")) text.text = "Backpack Capacity Settings";
                        else if (old.Contains("仓库容量设置")) text.text = "Storage Capacity Settings";
                        else if (old.Contains("自动拾取设置")) text.text = "Auto Pickup Settings";
                        else if (old.Contains("图腾及配件设置")) text.text = "Totem And Accessory Settings";
                        else if (old.Contains("额外掉落设置")) text.text = "Extra Loot Settings";
                        else if (old.Contains("额外敌人设置")) text.text = "Extra Enemies Settings";
                        else if (old.Contains("角色天赋设置")) text.text = "Character Talent Settings";
                        else if (old.Contains("商店修改设置")) text.text = "Shop Modification Settings";
                        else if (old.Contains("敵鴨血量縮放")) text.text = "Enemy Health Scaling";

                        // 开关标签
                        else if (old.Contains("启用物品减重")) text.text = "Enable Item Weight Reduction";
                        else if (old.Contains("启用堆叠倍率增加\n(现已支持部分不可堆叠物)")) text.text = "Enable Stack Multiplier\n(Now supports some non-stackable items)";
                        else if (old.Contains("启用修理损耗减少")) text.text = "Enable Repair Loss Reduction";
                        else if (old.Contains("启用多倍耐久")) text.text = "Enable Multiplier Durability";
                        else if (old.Contains("启用耐久倍率")) text.text = "Enable Durability Multiplier";
                        else if (old.Contains("启用动作速度加快\n(使用道具及搜索)")) text.text = "Enable Action Speed Boost\n(Item usage and searching)";
                        else if (old.Contains("启用背包容量修改")) text.text = "Enable Backpack Capacity Modification";
                        else if (old.Contains("启用仓库容量修改")) text.text = "Enable Storage Capacity Modification";
                        else if (old.Contains("启用自动拾取\n(如果卡顿请开启开箱拾取)")) text.text = "Enable Auto Pickup\n(If there is any lag, please enable Pickup on open box)";
                        else if (old.Contains("启用开箱拾取模式\n(替代范围拾取)")) text.text = "Enable Pickup on open box\n(replacing proximity pickup)";
                        else if (old.Contains("启用心愿单拾取模式\n(替代原拾取逻辑)")) text.text = "Enable Wishlist Pickup Mode\n(Replaces original pickup logic)";
                        else if (old.Contains("启用战利品自动卸弹")) text.text = "Enable Auto Unload Guns";
                        else if (old.Contains("启用额外图腾槽位\n(额外槽位滚动鼠标进行查看)")) text.text = "Enable Extra Totem Slots\n(Use the scroll mouse to view additional slots)";
                        else if (old.Contains("启用图腾及配件无负面效果\n(修改后重进游戏生效)")) text.text =
                                "Enable Totems and accessories has no negative effects\n(The changes will take effect after re-entering the game)";
                        else if (old.Contains("启用图腾及配件双倍正面效果\n(需启用无负面功能)")) text.text =
                                "Enable Totems and accessories double positive effects\n(No negative functions should be activated)";
                        else if (old.Contains("启用额外武器配件槽位\n(修改后重进游戏生效)")) text.text =
                                "Enable Extra Weapon Attachment Slots\n(The changes will take effect after re-entering the game)";
                        else if (old.Contains("启用额外掉落")) text.text = "Enable Extra Loot\n(All containers dynamically generate extra items)";
                        else if (old.Contains("启用生成额外敌人\n(包括Boss)")) text.text = "Enable Extra Enemy Spawns\n(Including Boss)";
                        else if (old.Contains("启用所有天赋无负面效果")) text.text = "Enable All talents without negative effects";
                        else if (old.Contains("启用天赋即时解锁\n(无需等待解锁时间)")) text.text = "Enable Instant Perk Unlock\n(No waiting time required)";
                        else if (old.Contains("启用商店修改")) text.text = "Enable Shop Modification";
                        else if (old.Contains("启用购买全部按钮")) text.text = "Enable Buy All Button";
                        else if (old.Contains("啟用敵鴨血量縮放")) text.text = "Enable Enemy Health Scaling";




                        // 滑块标签
                        else if (old.Contains("重量系数")) text.text = "Weight Factor";
                        else if (old.Contains("堆叠倍率")) text.text = "Stack Multiplier";
                        else if (old.Contains("耐久损耗倍率")) text.text = "Durability Loss Multiplier";
                        else if (old.Contains("耐久倍率")) text.text = "Durability Multiplier";
                        else if (old.Contains("速度倍率")) text.text = "Speed Multiplier";
                        else if (old.Contains("背包容量倍率")) text.text = "Backpack Capacity Multiplier";
                        else if (old.Contains("背包额外容量")) text.text = "Backpack Extra Capacity";
                        else if (old.Contains("仓库容量倍率")) text.text = "Storage Capacity Multiplier";
                        else if (old.Contains("仓库额外容量")) text.text = "Storage Extra Capacity";
                        else if (old.Contains("子弹拾取最低品质")) text.text = "Minimum Pickup Quality";
                        else if (old.Contains("拾取范围")) text.text = "Pickup Radius";
                        else if (old.Contains("扫描间隔")) text.text = "Scan Interval";
                        else if (old.Contains("拾取提示X坐标")) text.text = "Pickup Message X Position";
                        else if (old.Contains("拾取提示Y坐标")) text.text = "Pickup Message Y Position";
                        else if (old.Contains("槽位数量")) text.text = "Slot Count";
                        else if (old.Contains("额外掉落效果倍率")) text.text = "Extra Loot Effect Multiplier";
                        else if (old.Contains("敌人生成倍率")) text.text = "Enemy Spawn Multiplier";
                        else if (old.Contains("UI缩放比例")) text.text = "UI Scale";
                        else if (old.Contains("商店库存倍率")) text.text = "Shop Stock Multiplier";
                        else if (old.Contains("黑市升级效果倍率")) text.text = "Black Market Upgrade Multiplier";
                        else if (old.Contains("血量縮放倍率")) text.text = "Health Scaling Factor";
                    }
                    else
                    {
                        // 章节标题
                        if (old.Contains("Custom UI Scale")) text.text = "自定义UI缩放";
                        else if (old.Contains("Item Weight Reduction")) text.text = "物品重量减轻";
                        else if (old.Contains("Item Stack Settings")) text.text = "物品堆叠设置";
                        else if (old.Contains("Repair Durability Loss")) text.text = "修理耐久损耗";
                        else if (old.Contains("Action Speed Settings")) text.text = "动作速度设置";
                        else if (old.Contains("Backpack Capacity Settings")) text.text = "背包容量设置";
                        else if (old.Contains("Storage Capacity Settings")) text.text = "仓库容量设置";
                        else if (old.Contains("Auto Pickup Settings")) text.text = "自动拾取设置";
                        else if (old.Contains("Totem And Accessory Settings")) text.text = "图腾及配件设置";
                        else if (old.Contains("Extra Loot Settings")) text.text = "额外掉落设置";
                        else if (old.Contains("Extra Enemies Settings")) text.text = "额外敌人设置";
                        else if (old.Contains("Character Talent Settings")) text.text = "角色天赋设置";
                        else if (old.Contains("Shop Modification Settings")) text.text = "商店修改设置";
                        else if (old.Contains("Enemy Health Scaling")) text.text = "敵鴨血量縮放";


                        // 开关标签
                        else if (old.Contains("Enable Item Weight Reduction")) text.text = "启用物品减重";
                        else if (old.Contains("Enable Stack Multiplier\n(Now supports some non-stackable items)")) text.text = "启用堆叠倍率增加\n(现已支持部分不可堆叠物)";
                        else if (old.Contains("Enable Repair Loss Reduction")) text.text = "启用修理损耗减少";
                        else if (old.Contains("Enable Multiplier Durability")) text.text = "启用多倍耐久";
                        else if (old.Contains("Enable Durability Multiplier")) text.text = "启用耐久倍率";
                        else if (old.Contains("Enable Action Speed Boost\n(Item usage and searching)")) text.text = "启用动作速度加快\n(使用道具及搜索)";
                        else if (old.Contains("Enable Backpack Capacity Modification")) text.text = "启用背包容量修改";
                        else if (old.Contains("Enable Storage Capacity Modification")) text.text = "启用仓库容量修改";
                        else if (old.Contains("Enable Auto Pickup\n(If there is any lag, please enable Pickup on open box)")) text.text = "启用自动拾取\n(如果卡顿请开启开箱拾取)";
                        else if (old.Contains("Enable Pickup on open box\n(replacing proximity pickup)")) text.text = "启用开箱拾取模式\n(替代范围拾取)";
                        else if (old.Contains("Enable Wishlist Pickup Mode\n(Replaces original pickup logic)")) text.text = "启用心愿单拾取模式\n(替代原拾取逻辑)";
                        else if (old.Contains("Enable Auto Unload Guns")) text.text = "启用战利品自动卸弹";
                        else if (old.Contains("Enable Extra Totem Slots\n(Use the scroll mouse to view additional slots)")) text.text = "启用额外图腾槽位\n(额外槽位滚动鼠标进行查看)";
                        else if (old.Contains("Enable Totems and accessories has no negative effects\n(The changes will take effect after re-entering the game)"))
                            text.text = "启用图腾及配件无负面效果\n(修改后重进游戏生效)";
                        else if (old.Contains("Enable Totems and accessories double positive effects\n(No negative functions should be activated)"))
                            text.text = "启用图腾及配件双倍正面效果\n(需启用无负面功能)";
                        else if (old.Contains("Enable Extra Weapon Attachment Slots\n(The changes will take effect after re-entering the game)"))
                            text.text = "启用额外武器配件槽位\n(修改后重进游戏生效)";
                        else if (old.Contains("Enable Extra Loot\n(All containers dynamically generate extra items)")) text.text = "启用额外掉落\n(所有箱子动态生成额外的物品)";
                        else if (old.Contains("Enable Extra Enemy Spawns\n(Including Boss)")) text.text = "启用生成额外敌人\n(包括Boss)";
                        else if (old.Contains("Enable All talents without negative effects")) text.text = "启用所有天赋无负面效果";
                        else if (old.Contains("Enable Instant Perk Unlock\n(No waiting time required)")) text.text = "启用天赋即时解锁\n(无需等待解锁时间)";
                        else if (old.Contains("Enable Shop Modification")) text.text = "启用商店修改";
                        else if (old.Contains("Enable Buy All Button")) text.text = "启用购买全部按钮";
                        else if (old.Contains("Enable Enemy Health Scaling")) text.text = "啟用敵鴨血量縮放";


                        // 滑块标签
                        else if (old.Contains("Weight Factor")) text.text = "重量系数";
                        else if (old.Contains("Stack Multiplier")) text.text = "堆叠倍率";
                        else if (old.Contains("Durability Loss Multiplier")) text.text = "耐久损耗倍率";
                        else if (old.Contains("Durability Multiplier")) text.text = "耐久倍率";
                        else if (old.Contains("Speed Multiplier")) text.text = "速度倍率";
                        else if (old.Contains("Backpack Capacity Multiplier")) text.text = "背包容量倍率";
                        else if (old.Contains("Backpack Extra Capacity")) text.text = "背包额外容量";
                        else if (old.Contains("Storage Capacity Multiplier")) text.text = "仓库容量倍率";
                        else if (old.Contains("Storage Extra Capacity")) text.text = "仓库额外容量";
                        else if (old.Contains("Minimum Pickup Quality")) text.text = "子弹拾取最低品质";
                        else if (old.Contains("Pickup Radius")) text.text = "拾取范围";
                        else if (old.Contains("Scan Interval")) text.text = "扫描间隔";
                        else if (old.Contains("Message X Position")) text.text = "拾取提示X坐标";
                        else if (old.Contains("Message Y Position")) text.text = "拾取提示Y坐标";
                        else if (old.Contains("Slot Count")) text.text = "槽位数量";
                        else if (old.Contains("Extra Loot Effect Multiplier")) text.text = "额外掉落效果倍率";
                        else if (old.Contains("Enemy Spawn Multiplier")) text.text = "敌人生成倍率";
                        else if (old.Contains("UI Scale")) text.text = "UI缩放比例";
                        else if (old.Contains("Shop Stock Multiplier")) text.text = "商店库存倍率";
                        else if (old.Contains("Black Market Upgrade Multiplier")) text.text = "黑市升级效果倍率";
                        else if (old.Contains("Health Scaling Factor")) text.text = "血量縮放倍率";

                    }
                }
            }

            // 应用按钮
            if (settingsPanel != null)
            {
                var applyBtns = settingsPanel.GetComponentsInChildren<Button>();
                foreach (var btn in applyBtns)
                {
                    var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (btnText != null && (btnText.text.Contains("应用") || btnText.text.Contains("Apply")))
                        btnText.text = english ? "Apply" : "应用";
                }
            }
        }

        //输入框中的文本显示
        private void UpdateInputTexts()
        {
            var config = BigInventoryConfigManager.Config;
            weightFactorInput = config.WeightFactor.ToString("F2");
            stackMultiplierInput = config.StackMultiplier.ToString("F0");
            repairLossInput = config.RepairLossMultiplier.ToString("F2"); //F0：保留2位小数
            actionSpeedInput = config.ActionSpeedMultiplier.ToString("F1");  //F1：保留1位小数
            inventoryMultiplierInput = config.InventoryMultiplier.ToString("F0");  //F0：保留0位小数
            inventoryExtraInput = config.InventoryExtraCapacity.ToString("F0");
            storageMultiplierInput = config.PlayerStorageMultiplier.ToString("F0");
            storageExtraInput = config.PlayerStorageExtraAdd.ToString("F0");
            messageXInput = config.MessageX.ToString("F0");
            messageYInput = config.MessageY.ToString("F0");
            minQualityInput = config.MinAutoCollectQuality.ToString("F0");
            extraTotemInput = config.ExtraTotemSlotCount.ToString("F0");
            lootBounsMultiplierInput = config.LootBounsMultiplier.ToString("F0");
            maxStockMultiplierInput = config.MaxStockMultiplier.ToString("F0");
            refreshMultiplierInput = config.RefreshMultiplier.ToString("F0");
            enemysMultiplierInput = config.EnemysMultiplier.ToString("F0");
            //bossMultiplierInput = config.BossMultiplier.ToString("F0");
            durabilityMultiplierInput = config.DurabilityMultiplier.ToString("F0");
            pickupRadiusInput = config.PickupRadius.ToString("F1");
            dynamicScanIntervalInput = config.DynamicScanInterval.ToString("F1");
            uiScaleInput = config.UIScale.ToString("F2");
        }

        private Button CreateButton(string text, Action onClick)
        {
            GameObject btnObj = new GameObject("Button_" + text);
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.6f, 1f, 0.8f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = 22;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(50, 40);
            return btn;
        }

        private void SetButtonSize(Button button, float width, float height)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void ClearActiveSlider()
        {
            try
            {
                activeSlider = null;
                if (_instance != null)
                {
                    _instance.lastKeyTime = 0f;
                }
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BigInventory] ClearActiveSlider 出错: " + ex);
            }
        }

        private void ToggleInputBlocker(bool enable)
        {
            if (canvas == null) return;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (enable)
            {
                if (inputBlocker == null)
                {
                    inputBlocker = new GameObject("InputBlocker");
                    inputBlocker.transform.SetParent(canvas.transform, false);
                    var img = inputBlocker.AddComponent<Image>();
                    img.color = new Color(0f, 0f, 0f, 0.4f);

                    RectTransform rect = inputBlocker.GetComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;

                    var blocker = inputBlocker.AddComponent<Button>();
                    blocker.onClick.AddListener(() => { });
                }

                inputBlocker.SetActive(true);
                settingsPanel.transform.SetAsLastSibling();
            }
            else
            {
                if (inputBlocker != null)
                    inputBlocker.SetActive(false);
            }
        }

        private void Update()
        {
            // 实时检查当前场景，如果不是主菜单则强制关闭UI
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                string sceneName = SceneManager.GetActiveScene().name;
                if (!sceneName.Contains("Main") && !sceneName.Contains("Menu") && !sceneName.Contains("Lobby") && !sceneName.Contains("Start") && !sceneName.Contains("开始游戏"))
                {
                    ForceCloseUI();
                    return;
                }
            }

            if (settingsPanel == null || !settingsPanel.activeSelf)
            {
                activeSlider = null;
                lastKeyTime = 0f;
                return;
            }

            if (activeSlider == null) return;

            float now = Time.unscaledTime;
            bool leftPressed = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            bool rightPressed = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);

            if ((leftPressed || rightPressed) && now - lastKeyTime >= 0.4f)
            {
                float step = activeSlider.wholeNumbers ? 1f : 0.05f;

                if (leftPressed)
                    activeSlider.value = Mathf.Clamp(activeSlider.value - step, activeSlider.minValue, activeSlider.maxValue);
                else if (rightPressed)
                    activeSlider.value = Mathf.Clamp(activeSlider.value + step, activeSlider.minValue, activeSlider.maxValue);

                lastKeyTime = now;
            }
        }
    }
}