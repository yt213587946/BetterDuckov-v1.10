using System;
using UnityEngine;

namespace bigInventory
{
    internal static class ModLogger
    {
        private static string ModName => ModBehaviour.modName;
        internal static readonly bool _switch = false;

        public enum Level { Regular, Test }

        private static bool ShouldLog(Level lvl)
        {
            return lvl == Level.Regular || (lvl == Level.Test && _switch);
        }

        private static string Format(string message, string subMod = null)
        {
            string sub = subMod != null ? $"[{subMod}]" : "";
            return $"[{DateTime.Now:HH:mm:ss}][{ModName}]{sub}: {message}";
        }
        public static void Log(Level lvl, string message, string subMod = null)
        {
            if (!ShouldLog(lvl)) return;

            Debug.Log(Format(message, subMod));
        }

        public static void Error(Level lvl, string message, string subMod = null)
        {
            if (!ShouldLog(lvl)) return;

            Debug.LogError("> !!! <" + Format(message, subMod));
        }

        public static void Warn(Level lvl, string message, string subMod = null)
        {
            if (!ShouldLog(lvl)) return;

            Debug.LogWarning("> !!!!! <" + Format(message, subMod));
        }
    }
}
