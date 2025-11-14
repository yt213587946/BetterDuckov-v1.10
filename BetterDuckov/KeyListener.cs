using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace bigInventory
{
    public class BigInventoryKeyListener : MonoBehaviour
    {
        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F8))
            {
                ModLogger.Log(ModLogger.Level.Test, $"F8 按键被检测到，尝试打开设置界面。", "BigInventory");

                BigInventoryConfigUI.ToggleUI();
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                ModLogger.Log(ModLogger.Level.Test, $"Q键被检测到，尝试打开设置界面。", "BigInventory");

                BigInventoryConfigUI.ToggleUI();
                return;
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                ModLogger.Log(ModLogger.Level.Test, $"E键被检测到，尝试打开设置界面。", "BigInventory");

                BigInventoryConfigUI.ToggleUI();
                return;
            }
        }
    }
}
