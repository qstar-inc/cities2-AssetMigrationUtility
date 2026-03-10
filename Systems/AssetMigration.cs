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
using Game.Routes;
using StarQ.Shared.Extensions;
using Unity.Collections;
using Unity.Entities;

namespace AssetMigrationUtility.Systems
{
    public partial class AssetMigration : GameSystemBase
    {
        static readonly Regex PrefabRegex = new(
            "^(.+?):(.+?)(?:\\s+\\(([A-Fa-f0-9]{32})\\))?$",
            RegexOptions.Compiled
        );

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
                EntityQuery eq1 = SystemAPI.QueryBuilder().WithAll<PrefabRef>().Build();
                NativeArray<Entity> ents1 = eq1.ToEntityArray(Allocator.Temp);

                EntityQuery eq2 = SystemAPI.QueryBuilder().WithAll<VehicleModel>().Build();
                NativeArray<Entity> ents2 = eq2.ToEntityArray(Allocator.Temp);

                if (ents1.Length <= 0 && ents2.Length <= 0)
                    return;

                IEnumerable<PrefabAsset> pm = AssetDatabase.global.GetAssets<PrefabAsset>();
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

                Dictionary<string, PrefabBase> sortedDict = prefabAssets
                    .OrderBy(x => x.Key)
                    .ToDictionary(x => x.Key, x => x.Value);

                List<string> notFound = new();

                foreach (Entity entity in ents1)
                {
                    try
                    {
                        EntityManager.TryGetComponent(entity, out PrefabRef prefabRef);
                        bool isEnabled = EntityManager.IsComponentEnabled<PrefabData>(
                            prefabRef.m_Prefab
                        );
                        if (isEnabled)
                            continue;

                        PrefabID obs = prefabSystem.GetObsoleteID(prefabRef.m_Prefab);

                        Match reg = PrefabRegex.Match(obs.ToString());

                        if (!reg.Success)
                            continue;

                        string pType = reg.Groups[1].Value;
                        string pName = reg.Groups[2].Value;

                        if (string.IsNullOrEmpty(pType) || string.IsNullOrEmpty(pName))
                        {
                            LogHelper.SendLog(
                                $"Fail: {obs} (Unable to deduce PrefabName or PrefabType)",
                                LogLevel.Error
                            );
                            continue;
                        }
                        string prefabKey = $"{pType}:{pName}";

                        if (notFound.Contains(prefabKey))
                            continue;

                        if (!sortedDict.TryGetValue(prefabKey, out PrefabBase pb))
                        {
                            notFound.Add(prefabKey);
                            continue;
                        }
                        if (!prefabSystem.TryGetEntity(pb, out Entity prefabEntity))
                        {
                            LogHelper.SendLog(
                                $"Failed search for {prefabKey} (Entity not found)",
                                LogLevel.Error
                            );
                            continue;
                        }
                        prefabRef.m_Prefab = prefabEntity;
                        EntityManager.SetComponentData(entity, prefabRef);
                        EntityManager.AddComponent<Updated>(prefabRef.m_Prefab);
                        EntityManager.AddComponent<Updated>(entity);
                        LogHelper.SendLog($"Successfully swapped {prefabKey}");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.SendLog(ex, LogLevel.Error);
                    }
                }

                LogHelper.SendLog($"Starting VehicleModel migration on {ents2.Length} entities.");
                foreach (Entity entity in ents2)
                {
                    try
                    {
                        EntityManager.TryGetBuffer(
                            entity,
                            false,
                            out DynamicBuffer<VehicleModel> vehicleModel
                        );

                        LogHelper.SendLog(
                            $"Found {vehicleModel.Length} VehicleModel entries on entity {entity}"
                        );

                        for (int i = vehicleModel.Length - 1; i >= 0; i--)
                        {
                            VehicleModel model = vehicleModel[i];
                            bool removed = false;

                            for (int routeIndex = 0; routeIndex < 2; routeIndex++)
                            {
                                Entity routeModel =
                                    routeIndex == 0
                                        ? model.m_PrimaryPrefab
                                        : model.m_SecondaryPrefab;

                                if (routeModel == Entity.Null)
                                    continue;

                                bool isEnabled = EntityManager.IsComponentEnabled<PrefabData>(
                                    routeModel
                                );
                                if (isEnabled)
                                    continue;

                                PrefabID obs = prefabSystem.GetObsoleteID(routeModel);

                                Match reg = PrefabRegex.Match(obs.ToString());

                                if (!reg.Success)
                                {
                                    vehicleModel.RemoveAt(i);
                                    removed = true;
                                    break;
                                }

                                string pType = reg.Groups[1].Value;
                                string pName = reg.Groups[2].Value;

                                if (string.IsNullOrEmpty(pType) || string.IsNullOrEmpty(pName))
                                {
                                    LogHelper.SendLog(
                                        $"Fail: {obs} (Unable to deduce PrefabName or PrefabType)",
                                        LogLevel.Error
                                    );

                                    vehicleModel.RemoveAt(i);
                                    removed = true;
                                    break;
                                }

                                string prefabKey = $"{pType}:{pName}";

                                if (notFound.Contains(prefabKey))
                                {
                                    vehicleModel.RemoveAt(i);
                                    removed = true;
                                    break;
                                }

                                if (!sortedDict.TryGetValue(prefabKey, out PrefabBase pb))
                                {
                                    notFound.Add(prefabKey);

                                    vehicleModel.RemoveAt(i);
                                    removed = true;
                                    break;
                                }

                                if (!prefabSystem.TryGetEntity(pb, out Entity prefabEntity))
                                {
                                    LogHelper.SendLog(
                                        $"Failed search for {prefabKey} (Entity not found)",
                                        LogLevel.Error
                                    );

                                    vehicleModel.RemoveAt(i);
                                    removed = true;
                                    break;
                                }

                                if (routeIndex == 0)
                                    model.m_PrimaryPrefab = prefabEntity;
                                else
                                    model.m_SecondaryPrefab = prefabEntity;

                                LogHelper.SendLog($"Successfully swapped on routes {prefabKey}");
                            }

                            if (removed)
                                continue;
                            vehicleModel[i] = model;
                        }
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
