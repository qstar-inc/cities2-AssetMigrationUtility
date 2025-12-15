using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Colossal.Entities;
using Colossal.IO.AssetDatabase;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Prefabs.Water;
using StarQ.Shared.Extensions;
using Unity.Collections;
using Unity.Entities;

namespace AssetMigrationUtility.Systems
{
    public partial class AssetMigration : GameSystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
        }

        protected override void OnUpdate() { }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (WorldHelper.IsGame && Mod.m_Setting.IsEnabled)
                MigrateAssets();
        }

        internal void MigrateAssets()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string prefName = string.Empty;
            try
            {
                PrefabSystem prefabSystem = WorldHelper.PrefabSystem;
                var eq3 = SystemAPI.QueryBuilder().WithAll<PrefabRef>().Build();
                var ents3 = eq3.ToEntityArray(Allocator.Temp);

                if (ents3.Length <= 0)
                    return;

                var pm = AssetDatabase.global.GetAssets<PrefabAsset>();
                Dictionary<string, PrefabBase> prefabAssets = new();
                foreach (var pmItem in pm)
                {
                    PrefabBase? prefabBase = pmItem.GetInstance<PrefabBase>();

                    if (
                        prefabBase == null
                        || prefabBase is AssetPackPrefab
                        || prefabBase is ContentPrefab
                        || prefabBase is EffectPrefab
                        || prefabBase is ProcessingRequirementPrefab
                        || prefabBase is RenderPrefab
                        || prefabBase is StrictObjectBuiltRequirementPrefab
                        || prefabBase is UIAssetCategoryPrefab
                        || prefabBase is UIAssetMenuPrefab
                        || prefabBase is WaterRenderSettingsPrefab
                    )
                        continue;
                    prefName = prefabBase.name;

                    string typename =
                        $"{prefabBase.GetType().Name}:{prefabBase.GetPrefabID().GetName()}";

                    if (prefabBase.asset != null && !prefabAssets.ContainsKey(typename))
                        prefabAssets[typename] = prefabBase;

                    if (prefabBase.TryGet(out ObsoleteIdentifiers obs))
                    {
                        if (obs.m_PrefabIdentifiers.Length > 0)
                        {
                            foreach (var item in obs.m_PrefabIdentifiers)
                            {
                                prefabAssets[$"{item.m_Type}:{item.m_Name}"] = prefabBase;
                            }
                        }
                    }
                }

                var sortedDict = prefabAssets
                    .OrderBy(x => x.Key)
                    .ToDictionary(x => x.Key, x => x.Value);

                List<string> notFound = new();

                foreach (var entity in ents3)
                {
                    try
                    {
                        EntityManager.TryGetComponent(entity, out PrefabRef prefabRef);
                        bool isEnabled = EntityManager.IsComponentEnabled<PrefabData>(
                            prefabRef.m_Prefab
                        );
                        if (isEnabled)
                            continue;

                        var obs = prefabSystem.GetObsoleteID(prefabRef.m_Prefab);

                        var reg = Regex.Match(
                            obs.ToString(),
                            "^(.+?):(.+?)(?:\\s+\\(([A-Za-z0-9]{32})\\))?$"
                        );

                        if (!reg.Success)
                            continue;

                        var pType = reg?.Groups[1]?.Value;
                        var pName = reg?.Groups[2]?.Value;

                        if (string.IsNullOrEmpty(pType) || string.IsNullOrEmpty(pName))
                        {
                            LogHelper.SendLog(
                                $"Fail: {obs} (Unable to deduce PrefabName or PrefabType)",
                                LogLevel.Error
                            );
                            continue;
                        }

                        if (notFound.Contains($"{pType}:{pName}"))
                            continue;

                        if (!sortedDict.TryGetValue($"{pType}:{pName}", out PrefabBase pb))
                        {
                            notFound.Add($"{pType}:{pName}");
                            continue;
                        }
                        if (!prefabSystem.TryGetEntity(pb, out Entity prefabEntity))
                        {
                            LogHelper.SendLog(
                                $"Failed search for {pType}:{pName} (Entity not found)",
                                LogLevel.Error
                            );
                            continue;
                        }
                        prefabRef.m_Prefab = prefabEntity;
                        EntityManager.SetComponentData(entity, prefabRef);
                        EntityManager.AddComponent<Updated>(prefabRef.m_Prefab);
                        EntityManager.AddComponent<Updated>(entity);
                        LogHelper.SendLog($"Successfully swapped {pType}:{pName}");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.SendLog(ex, LogLevel.Error);
                    }
                }
                if (notFound.Count > 0)
                    LogHelper.SendLog("PrefabBase not found:\n" + string.Join("\n", notFound));
            }
            catch (Exception ex)
            {
                LogHelper.SendLog($"Stopped on {prefName}");
                LogHelper.SendLog(ex);
            }
            finally
            {
                stopwatch.Stop();

                LogHelper.SendLog(
                    $"Migration process completed in {stopwatch.Elapsed.Duration()}s"
                );
            }
        }
    }
}
