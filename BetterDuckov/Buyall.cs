using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Duckov.Economy.UI;
using Duckov.Economy;
using Duckov;
using ItemStatsSystem;
using bigInventory;

namespace BigInventory.Patches
{
    [HarmonyPatch(typeof(StockShopView))]
    public class StockShopViewPatch
    {
        private static GameObject buyAllButton;
        private static TextMeshProUGUI buyAllText;
        private static Button buyAllButtonComponent;

        //存储金钱图标的TMP_SpriteAsset
        private static TMP_SpriteAsset moneyIconSpriteAsset;
        private static string moneyIconSpriteName;

        // 存储每个物品类型的价格缓存
        private static Dictionary<int, int> itemPriceCache = new Dictionary<int, int>();

        // 使用反射缓存字段和方法
        private static FieldInfo interactionButtonField;
        private static FieldInfo buttonColorInteractableField;
        private static FieldInfo buttonColorNotInteractableField;
        private static MethodInfo refreshInteractionButtonMethod;
        private static MethodInfo refreshStockTextMethod;
        private static FieldInfo targetField;
        private static FieldInfo priceTextField;

        [HarmonyPatch("Setup")]
        [HarmonyPostfix]
        private static void Setup_Postfix(StockShopView __instance)
        {
            try
            {
                if (!BigInventoryConfigManager.Config.EnableBuyAll) return;
                InitializeReflectionCache(__instance);
                ExtractMoneyIconFromPriceText(__instance);
                CreateBuyAllButton(__instance);
                UpdateBuyAllButtonState(__instance);
            }
            catch (Exception e)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"Setup error: {e.Message}", "BuyAllPatch");


            }
        }

        private static void InitializeReflectionCache(StockShopView __instance)
        {
            if (interactionButtonField == null)
            {
                interactionButtonField = typeof(StockShopView).GetField("interactionButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (buttonColorInteractableField == null)
            {
                buttonColorInteractableField = typeof(StockShopView).GetField("buttonColor_Interactable",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (buttonColorNotInteractableField == null)
            {
                buttonColorNotInteractableField = typeof(StockShopView).GetField("buttonColor_NotInteractable",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (refreshInteractionButtonMethod == null)
            {
                refreshInteractionButtonMethod = typeof(StockShopView).GetMethod("RefreshInteractionButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (refreshStockTextMethod == null)
            {
                refreshStockTextMethod = typeof(StockShopView).GetMethod("RefreshStockText",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (targetField == null)
            {
                targetField = typeof(StockShopView).GetField("target",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (priceTextField == null)
            {
                priceTextField = typeof(StockShopView).GetField("priceText",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        //从原价格文本中提取金钱图标信息
        private static void ExtractMoneyIconFromPriceText(StockShopView __instance)
        {
            try
            {
                TextMeshProUGUI priceText = priceTextField?.GetValue(__instance) as TextMeshProUGUI;
                if (priceText != null && priceText.text != null)
                {
                    // 分析原价格文本，查找sprite标签
                    string originalText = priceText.text;

                    // 查找sprite标签格式：<sprite name="XXX">
                    int spriteStart = originalText.IndexOf("<sprite");
                    if (spriteStart >= 0)
                    {
                        int nameStart = originalText.IndexOf("name=\"", spriteStart) + 6;
                        int nameEnd = originalText.IndexOf("\"", nameStart);

                        if (nameStart >= 6 && nameEnd > nameStart)
                        {
                            moneyIconSpriteName = originalText.Substring(nameStart, nameEnd - nameStart);
                            moneyIconSpriteAsset = priceText.spriteAsset;

                            ModLogger.Log(ModLogger.Level.Regular, $"找到金钱图标: {moneyIconSpriteName}", "BuyAllPatch");

                        }
                    }

                    // 如果没找到sprite标签，尝试直接获取第一个字符（可能是sprite）
                    if (string.IsNullOrEmpty(moneyIconSpriteName) && priceText.textInfo != null)
                    {
                        priceText.ForceMeshUpdate();
                        if (priceText.textInfo.characterCount > 0)
                        {
                            TMP_CharacterInfo firstChar = priceText.textInfo.characterInfo[0];
                            if (firstChar.elementType == TMP_TextElementType.Sprite)
                            {
                                moneyIconSpriteAsset = priceText.spriteAsset;
                                // 获取sprite名称可能需要更多处理
                                moneyIconSpriteName = "Icon_Money"; // 默认名称，可能需要调整
                                ModLogger.Log(ModLogger.Level.Regular, "找到sprite类型的金钱图标", "BuyAllPatch");

                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"提取金钱图标失败: {e.Message}", "BuyAllPatch");

            }
        }

        [HarmonyPatch("RefreshInteractionButton")]
        [HarmonyPostfix]
        private static void RefreshInteractionButton_Postfix(StockShopView __instance)
        {
            try
            {
                if (!BigInventoryConfigManager.Config.EnableBuyAll) return;
                UpdateBuyAllButtonState(__instance);
            }
            catch (Exception e)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"Refresh error: {e.Message}", "BuyAllPatch");

            }
        }

        [HarmonyPatch("OnInteractionButtonClicked")]
        [HarmonyPrefix]
        private static void CreateBuyAllButton(StockShopView __instance)
        {
            if (!BigInventoryConfigManager.Config.EnableBuyAll) return;
            if (buyAllButton != null)
            {
                UnityEngine.Object.Destroy(buyAllButton);
            }

            // 使用反射获取原交互按钮
            Button originalButton = interactionButtonField?.GetValue(__instance) as Button;
            if (originalButton == null)
            {
                ModLogger.Error(ModLogger.Level.Regular, "无法获取原交互按钮", "BuyAllPatch");

                return;
            }

            // 创建新的按钮对象，不复制原按钮的子对象
            buyAllButton = new GameObject("BuyAllButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buyAllButton.transform.SetParent(originalButton.transform.parent);

            // 复制RectTransform属性
            RectTransform originalRect = originalButton.GetComponent<RectTransform>();
            RectTransform newRect = buyAllButton.GetComponent<RectTransform>();

            newRect.anchorMin = originalRect.anchorMin;
            newRect.anchorMax = originalRect.anchorMax;
            newRect.pivot = originalRect.pivot;
            newRect.sizeDelta = originalRect.sizeDelta;
            newRect.anchoredPosition = new Vector2(
                originalRect.anchoredPosition.x,
                originalRect.anchoredPosition.y - originalRect.rect.height - 10f
            );

            // 复制Image属性
            Image originalImage = originalButton.GetComponent<Image>();
            Image newImage = buyAllButton.GetComponent<Image>();
            newImage.sprite = originalImage.sprite;
            newImage.color = originalImage.color;
            newImage.type = originalImage.type;
            newImage.preserveAspect = originalImage.preserveAspect;

            // 获取按钮组件
            buyAllButtonComponent = buyAllButton.GetComponent<Button>();

            // 复制Button属性
            buyAllButtonComponent.targetGraphic = newImage;
            buyAllButtonComponent.transition = originalButton.transition;
            buyAllButtonComponent.colors = originalButton.colors;
            buyAllButtonComponent.spriteState = originalButton.spriteState;
            buyAllButtonComponent.animationTriggers = originalButton.animationTriggers;
            buyAllButtonComponent.navigation = originalButton.navigation;

            // 创建文本对象
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buyAllButton.transform);

            // 设置文本的RectTransform
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.localScale = Vector3.one;

            // 设置文本属性
            buyAllText = textObject.GetComponent<TextMeshProUGUI>();
            buyAllText.text = "购买全部";
            buyAllText.alignment = TextAlignmentOptions.Center;
            buyAllText.enableAutoSizing = true;
            buyAllText.fontSizeMin = 16;
            buyAllText.fontSizeMax = 28;
            buyAllText.color = Color.black;

            // 尝试从原按钮复制字体
            TextMeshProUGUI originalText = originalButton.GetComponentInChildren<TextMeshProUGUI>();
            if (originalText != null)
            {
                buyAllText.font = originalText.font;
                buyAllText.fontStyle = originalText.fontStyle;
                buyAllText.fontSize = originalText.fontSize;

                // 设置sprite asset以显示图标
                if (moneyIconSpriteAsset != null)
                {
                    buyAllText.spriteAsset = moneyIconSpriteAsset;
                }
            }

            // 添加点击事件
            buyAllButtonComponent.onClick.AddListener(() => BuyAllSelectedItem(__instance));

            // 初始隐藏
            buyAllButton.SetActive(false);
        }

        private static void UpdateBuyAllButtonState(StockShopView __instance)
        {
            if (!BigInventoryConfigManager.Config.EnableBuyAll)
                if (buyAllButton == null) return;

            var selection = __instance.GetSelection();

            if (selection == null || !selection.IsUnlocked())
            {
                buyAllButton.SetActive(false);
                return;
            }

            StockShop.Entry targetEntry = selection.Target;
            if (targetEntry == null || targetEntry.CurrentStock <= 0)
            {
                buyAllButton.SetActive(false);
                return;
            }

            // 显示购买全部按钮
            buyAllButton.SetActive(true);

            // 计算总价格
            Item item = selection.GetItem();
            if (item == null) return;

            int singlePrice = GetItemPrice(__instance, item, false);
            int totalPrice = singlePrice * targetEntry.CurrentStock;

            // 检查是否有足够金钱
            bool hasEnoughMoney = new Cost((long)totalPrice).Enough;

            // 更新按钮状态
            buyAllButtonComponent.interactable = hasEnoughMoney && targetEntry.CurrentStock > 0;

            // 更新文本显示（包含金钱图标）
            if (buyAllText != null)
            {
                string moneyIconTag = "";
                if (!string.IsNullOrEmpty(moneyIconSpriteName))
                {
                    moneyIconTag = $"<sprite name=\"{moneyIconSpriteName}\"> ";
                }

                if (hasEnoughMoney)
                {
                    buyAllText.text = $"购买全部 ({targetEntry.CurrentStock}个)\n<size=80%>{moneyIconTag}{totalPrice:n0}</size>";
                }
                else
                {
                    buyAllText.text = $"<color=red>购买全部 ({targetEntry.CurrentStock}个)\n<size=80%>{moneyIconTag}{totalPrice:n0} (资金不足)</size></color>";
                }
            }

            // 使用反射获取颜色并更新按钮颜色
            if (buttonColorInteractableField != null && buttonColorNotInteractableField != null)
            {
                Color interactableColor = (Color)buttonColorInteractableField.GetValue(__instance);
                Color notInteractableColor = (Color)buttonColorNotInteractableField.GetValue(__instance);

                Image buttonImage = buyAllButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = hasEnoughMoney ? interactableColor : notInteractableColor;
                }
            }
        }

        private static void BuyAllSelectedItem(StockShopView __instance)
        {
            if (!BigInventoryConfigManager.Config.EnableBuyAll) return;
            var selection = __instance.GetSelection();
            if (selection == null || !selection.IsUnlocked())
            {
                return;
            }

            StockShop.Entry targetEntry = selection.Target;
            if (targetEntry == null || targetEntry.CurrentStock <= 0)
            {
                return;
            }

            Item item = selection.GetItem();
            if (item == null) return;

            int singlePrice = GetItemPrice(__instance, item, false);
            int totalPrice = singlePrice * targetEntry.CurrentStock;

            // 检查金钱是否足够
            if (!new Cost((long)totalPrice).Enough)
            {
                ModLogger.Log(ModLogger.Level.Regular, "资金不足，无法购买全部", "BuyAllPatch");

                return;
            }

            // 获取购买方法的私有访问
            MethodInfo buyTaskMethod = typeof(StockShopView).GetMethod("BuyTask",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (buyTaskMethod == null)
            {
                ModLogger.Error(ModLogger.Level.Regular, "无法找到 BuyTask 方法", "BuyAllPatch");

                return;
            }

            // 一次性购买所有库存
            int itemsToBuy = targetEntry.CurrentStock;
            int successfulPurchases = 0;

            for (int i = 0; i < itemsToBuy; i++)
            {
                try
                {
                    // 调用原购买方法
                    var task = buyTaskMethod.Invoke(__instance, new object[] { targetEntry.ItemTypeID });
                    successfulPurchases++;

                    // 更新缓存价格（因为购买后价格可能会变化）
                    if (itemPriceCache.ContainsKey(targetEntry.ItemTypeID))
                    {
                        itemPriceCache.Remove(targetEntry.ItemTypeID);
                    }
                }
                catch (Exception e)
                {
                    ModLogger.Error(ModLogger.Level.Regular, $"购买失败: {e.Message}", "BuyAllPatch");

                    break;
                }
            }

            ModLogger.Log(ModLogger.Level.Regular, $"成功购买 {successfulPurchases} 个物品", "BuyAllPatch");


            // 使用反射调用刷新方法
            refreshInteractionButtonMethod?.Invoke(__instance, null);
            refreshStockTextMethod?.Invoke(__instance, null);

            // 播放音效
            AudioManager.Post("UI/buy");
        }

        private static int GetItemPrice(StockShopView __instance, Item item, bool selling)
        {
            int itemTypeID = item.TypeID;

            // 使用缓存避免重复计算
            if (!itemPriceCache.ContainsKey(itemTypeID))
            {
                // 通过反射调用私有方法获取价格
                MethodInfo getPriceMethod = typeof(StockShopView).GetMethod(
                    "<RefreshInteractionButton>g__GetPrice|71_0",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (getPriceMethod != null)
                {
                    int price = (int)getPriceMethod.Invoke(__instance, new object[] { item, selling });
                    itemPriceCache[itemTypeID] = price;
                }
                else
                {
                    // 备用方法：通过反射获取target然后调用ConvertPrice
                    StockShop targetShop = targetField?.GetValue(__instance) as StockShop;
                    if (targetShop != null)
                    {
                        itemPriceCache[itemTypeID] = targetShop.ConvertPrice(item, selling);
                    }
                    else
                    {
                        itemPriceCache[itemTypeID] = 0;
                    }
                }
            }

            return itemPriceCache[itemTypeID];
        }

        [HarmonyPatch("OnDisable")]
        [HarmonyPostfix]
        private static void OnDisable_Postfix()
        {
            // 清理缓存
            itemPriceCache.Clear();
            if (!BigInventoryConfigManager.Config.EnableBuyAll && buyAllButton != null)
            {
                UnityEngine.Object.Destroy(buyAllButton);
                buyAllButton = null;
            }
        }
    }
}