using System;
using System.Reflection;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace bigInventory
{
    [HarmonyPatch(typeof(CharacterSpawnerRoot), "StartSpawn")]
    public static class CharacterSpawner_Patch
    {
        private static bool isSpawning = false;
        private static MethodInfo cachedStartSpawnMethod;
        private static FieldInfo cachedCreatedField;
        private static FieldInfo cachedPrefabField;
        private static bool reflectionCached = false;
        private static EnemySpawnCoroutineManager coroutineManager;

        [HarmonyPostfix]
        public static void Postfix(CharacterSpawnerRoot __instance)
        {
            if (__instance == null) return;
            if (isSpawning) return;

            try
            {
                if (!BigInventoryConfigManager.Config.EnableMoreEnemys) return;
                if (__instance == null) return;

                // 检查是否在基地地图
                if (LevelManager.Instance != null && LevelManager.Instance.IsBaseLevel)
                {
                    //Debug.Log("[BetterDuckov] 检测到基地地图, 跳过敌人倍增");
                    return;
                }

                // 安全检查：确保游戏完全加载完成
                if (!IsGameFullyLoaded())
                {
                    ModLogger.Log(ModLogger.Level.Regular, $"游戏未完全加载，延迟敌人生成");

                    DelayEnemySpawning(__instance);
                    return;
                }

                if (!reflectionCached)
                {
                    CacheReflectionInfo();
                }

                if (cachedStartSpawnMethod == null || cachedCreatedField == null)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, $"未找到StartSpawn方法或created字段");

                    return;
                }

                // 获取生成倍率
                var spawnInfo = GetSpawnMultiplier(__instance);
                if (spawnInfo.extraSpawns <= 0) return;

                // 初始化协程管理器
                EnsureCoroutineManager();

                // 启动安全生成协程
                coroutineManager.StartCoroutine(SafeSpawnEnemiesOverTime(__instance, spawnInfo.extraSpawns, spawnInfo.isBoss));
            }
            catch (Exception ex)
            {
                isSpawning = false;
                ModLogger.Error(ModLogger.Level.Regular, $"敌人生成失败: {ex}");

            }
        }

        private static bool IsGameFullyLoaded()
        {
            try
            {
                // 检查关键游戏系统是否已初始化
                if (LevelManager.Instance == null) return false;
                if (CharacterMainControl.Main == null) return false;
                if (Time.timeSinceLevelLoad < 2.0f) return false; // 确保关卡加载完成

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void DelayEnemySpawning(CharacterSpawnerRoot spawner)
        {
            if (coroutineManager == null)
            {
                var managerObj = new GameObject("BetterDuckov_EnemySpawnManager");
                coroutineManager = managerObj.AddComponent<EnemySpawnCoroutineManager>();
                GameObject.DontDestroyOnLoad(managerObj);
            }

            coroutineManager.StartCoroutine(DelayedSpawnCoroutine(spawner));
        }

        private static IEnumerator DelayedSpawnCoroutine(CharacterSpawnerRoot spawner)
        {
            // 等待游戏完全加载
            yield return new WaitUntil(() => IsGameFullyLoaded());
            yield return new WaitForSeconds(1.0f); // 额外安全延迟

            // 重新触发生成逻辑
            if (spawner != null && !isSpawning)
            {
                Postfix(spawner);
            }
        }

        private static void CacheReflectionInfo()
        {
            try
            {
                cachedStartSpawnMethod = AccessTools.Method(typeof(CharacterSpawnerRoot), "StartSpawn");
                cachedCreatedField = AccessTools.Field(typeof(CharacterSpawnerRoot), "created");
                cachedPrefabField = AccessTools.Field(typeof(CharacterSpawnerRoot), "prefab");
                reflectionCached = true;
                ModLogger.Log(ModLogger.Level.Regular, $"反射信息缓存成功");

            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"反射信息缓存失败: {ex}");

            }
        }

        private static (int extraSpawns, bool isBoss) GetSpawnMultiplier(CharacterSpawnerRoot spawner)
        {
            bool isBoss = false;
            float targetMultiplier = BigInventoryConfigManager.Config.EnemysMultiplier;

            if (cachedPrefabField != null)
            {
                var prefab = cachedPrefabField.GetValue(spawner) as GameObject;
                if (prefab != null && IsBossUnit(prefab))
                {
                    isBoss = true;
                    if (!BigInventoryConfigManager.Config.EnableMoreBoss)
                    {
                        //Debug.Log($"[BetterDuckov] 跳过BOSS单位倍增: {prefab.name}");
                        return (0, true);
                    }

                    targetMultiplier = BigInventoryConfigManager.Config.BossMultiplier;
                    //Debug.Log($"[BetterDuckov] BOSS单位使用独立倍率 x{targetMultiplier}: {prefab.name}");
                }
            }

            int extraSpawns = Mathf.Max(0, Mathf.RoundToInt(targetMultiplier) - 1);
            return (extraSpawns, isBoss);
        }

        private static bool IsBossUnit(GameObject prefab)
        {
            if (prefab == null) return false;

            string name = prefab.name ?? "";
            if (string.IsNullOrEmpty(name)) return false;

            name = name.ToLower();
            return name.Contains("boss") ||
                   name.Contains("领主") ||
                   name.Contains("首领") ||
                   name.Contains("精英") ||
                   name.Contains("leader") ||
                   name.Contains("Leader") ||
                   name.Contains("elite");
        }

        private static void EnsureCoroutineManager()
        {
            if (coroutineManager == null)
            {
                var managerObj = new GameObject("BetterDuckov_EnemySpawnManager");
                coroutineManager = managerObj.AddComponent<EnemySpawnCoroutineManager>();
                GameObject.DontDestroyOnLoad(managerObj);
                ModLogger.Log(ModLogger.Level.Regular, $"创建敌人生成协程管理器");

            }
        }

        private static IEnumerator SafeSpawnEnemiesOverTime(CharacterSpawnerRoot spawner, int count, bool isBoss)
        {
            if (spawner == null || cachedCreatedField == null || cachedStartSpawnMethod == null)
            {
                ModLogger.Warn(ModLogger.Level.Regular, $"参数为空, 跳过生成", "敌人生成协程");

                yield break;
            }

            string enemyType = isBoss ? "BOSS" : "普通敌人";
            //Debug.Log($"[BetterDuckov] 开始安全生成 {enemyType} x{count}");

            // 等待动画系统稳定
            yield return new WaitForEndOfFrame();

            bool created = (bool)cachedCreatedField.GetValue(spawner);

            // 距离检查
            yield return PerformDistanceCheck(spawner);

            for (int i = 0; i < count; i++)
            {
                if (spawner == null)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, $"敌人生成器已销毁, 停止生成");

                    yield break;
                }

                // 安全检查：确保动画系统就绪
                if (!IsAnimationSystemReady())
                {
                    ModLogger.Warn(ModLogger.Level.Regular, $"动画系统未就绪，等待下一帧");

                    yield return new WaitForEndOfFrame();
                }

                if (created)
                {
                    cachedCreatedField.SetValue(spawner, false);
                }

                isSpawning = true;

                // 使用单独的方法执行生成操作
                var spawnResult = ExecuteSpawnSafely(spawner);
                if (!spawnResult.success)
                {
                    ModLogger.Error(ModLogger.Level.Regular, $"单次敌人生成失败: {spawnResult.exception}");

                }

                isSpawning = false;

                yield return new WaitForSeconds(0.1f);
            }

            // 恢复原始状态
            if (created && spawner != null)
            {
                try
                {
                    cachedCreatedField.SetValue(spawner, true);
                }
                catch (Exception e)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, $"恢复created状态失败: {e}");

                }
            }

            //Debug.Log($"[BetterDuckov] {enemyType}安全分帧生成完成 x{count + 1}");
        }

        // 同步方法处理生成操作
        private static (bool success, Exception exception) ExecuteSpawnSafely(CharacterSpawnerRoot spawner)
        {
            try
            {
                cachedStartSpawnMethod.Invoke(spawner, null);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        private static IEnumerator PerformDistanceCheck(CharacterSpawnerRoot spawner)
        {
            float minDistance = spawner.minDistanceToPlayer;
            bool needDistanceCheck = minDistance > 0f;

            if (needDistanceCheck && CharacterMainControl.Main != null)
            {
                float sqrMinDistance = minDistance * minDistance;
                int distanceCheckCount = 0;
                const int MAX_DISTANCE_CHECK = 50;

                while (distanceCheckCount < MAX_DISTANCE_CHECK &&
                       CharacterMainControl.Main != null &&
                       (CharacterMainControl.Main.transform.position - spawner.transform.position).sqrMagnitude < sqrMinDistance)
                {
                    yield return new WaitForSeconds(0.5f);
                    distanceCheckCount++;
                }

                if (distanceCheckCount >= MAX_DISTANCE_CHECK)
                {
                    ModLogger.Warn(ModLogger.Level.Regular, $"距离检查超时, 强制生成敌人");
                }
            }
        }

        private static bool IsAnimationSystemReady()
        {
            // 简单的动画系统就绪检查
            try
            {
                // 检查是否有活跃的动画器
                var animators = GameObject.FindObjectsOfType<Animator>();
                return animators != null && animators.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerator SafeInvokeStartSpawn(CharacterSpawnerRoot spawner)
        {
            // 在调用前等待一帧，确保Unity系统稳定
            yield return null;

            try
            {
                cachedStartSpawnMethod.Invoke(spawner, null);
            }
            catch (Exception ex)
            {
                ModLogger.Error(ModLogger.Level.Regular, $"StartSpawn调用失败: {ex}");

                throw;
            }
        }

        public static void ClearCache()
        {
            isSpawning = false;
            cachedStartSpawnMethod = null;
            cachedCreatedField = null;
            cachedPrefabField = null;
            reflectionCached = false;
            if (coroutineManager != null)
            {
                coroutineManager.StopAllCoroutines();
                GameObject.Destroy(coroutineManager.gameObject);
                coroutineManager = null;
            }
        }
    }

    public class EnemySpawnCoroutineManager : MonoBehaviour
    {
        void OnDestroy()
        {
            // 清理所有协程
            StopAllCoroutines();
        }
    }
}