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
                //Debug.Log("[BigInventory] F8 按键被检测到，尝试打开设置界面。");
                BigInventoryConfigUI.ToggleUI();
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                //Debug.Log("[BigInventory] Q键被检测到，尝试打开设置界面。");
                BigInventoryConfigUI.ToggleUI();
                return;
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                //Debug.Log("[BigInventory]E键被检测到，尝试打开设置界面。");
                BigInventoryConfigUI.ToggleUI();
                return;
            }
        }
    }
}
