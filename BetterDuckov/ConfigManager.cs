using System;
using Saves;
using UnityEngine;

namespace bigInventory
{
    public static class BigInventoryConfigManager
    {
        private const string CONFIG_KEY = "BigInventory_Config";
        private static ModConfig config;

        // UI 可订阅的事件：当配置保存/更新时触发，参数是配置实例
        public static event Action<ModConfig> OnConfigChanged;

        //当前配置实例（自动创建）
        public static ModConfig Config
        {
            get
            {
                if (config == null)
                    config = new ModConfig();
                return config;
            }
        }

        // 从全局存档中加载配置
        public static void LoadConfig()
        {
            try
            {
                string json = SavesSystem.LoadGlobal<string>(CONFIG_KEY, "");
                if (string.IsNullOrEmpty(json))
                {
                    config = new ModConfig();
                    Debug.Log("[BigInventory] 未找到配置文件，已创建默认配置。");
                    return;
                }

                config = JsonUtility.FromJson<ModConfig>(json);

                // 防止解析失败或字段缺失时出错
                if (config == null)
                {
                    config = new ModConfig();
                    Debug.LogWarning("[BigInventory] 配置文件损坏或不可读取，使用默认配置。");
                }
                else
                {
                    //Debug.Log("[BigInventory] 配置加载成功。");
                }
            }
            catch (Exception ex)
            {
                config = new ModConfig();
                Debug.LogError("[BigInventory] 配置加载异常，使用默认配置: " + ex.Message);
            }
        }

        // 保存当前配置到全局存档
        public static void SaveConfig()
        {
            try
            {
                if (config == null)
                    config = new ModConfig();

                string json = JsonUtility.ToJson(config, true);
                SavesSystem.SaveGlobal<string>(CONFIG_KEY, json);
                Debug.Log("[BigInventory] 配置保存成功。");

                if (OnConfigChanged != null)
                    OnConfigChanged(config);
            }
            catch (Exception ex)
            {
                Debug.LogError("[BigInventory] 配置保存异常: " + ex.Message);
            }
        }

        // 在代码中更新配置并自动保存
        public static void UpdateConfig(Action<ModConfig> modifyAction)
        {
            if (modifyAction == null)
                return;

            if (config == null)
                config = new ModConfig();

            modifyAction(config);
            SaveConfig();
        }

        // 重置为默认配置
        public static void ResetToDefault()
        {
            config = new ModConfig();
            SaveConfig();
            Debug.Log("[BigInventory] 配置已重置为默认值。");
        }
    }
}
