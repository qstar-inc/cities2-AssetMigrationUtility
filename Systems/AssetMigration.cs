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
                foreach (PrefabAsset pmItem in pm)
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
                            foreach (PrefabIdentifierInfo item in obs.m_PrefabIdentifiers)
                            {
                                prefabAssets[$"{item.m_Type}:{item.m_Name}"] = prefabBase;
                            }
                        }
                    }
                }

                HashSet<string> notFound = new();

                int skipped = 0;

                foreach (Entity entity in ents1)
                {
                    try
                    {
                        if (!EntityManager.TryGetComponent(entity, out PrefabRef prefabRef))
                            continue;
                        if (!EntityManager.Exists(prefabRef.m_Prefab))
                            continue;

                        if (
                            !TryResolvePrefab(
                                prefabRef.m_Prefab,
                                prefabAssets,
                                notFound,
                                prefabSystem,
                                out Entity resolvedEntity,
                                out PrefabBase pb,
                                out string prefabKey
                            )
                        )
                            continue;

                        if (pb is SurfacePrefab)
                        {
                            if (EntityManager.TryGetComponent(entity, out Owner owner))
                            {
                                LogHelper.SendLog(
                                    $"Skipping {prefabKey} because it is owned by {owner.m_Owner}"
                                );
                                skipped++;
                                continue;
                            }
                        }

                        if (Mod.m_Setting.PerObjectLogging)
                            LogHelper.SendLog(
                                $"Swapping {prefabRef.m_Prefab} with {resolvedEntity} for {prefabKey}",
                                LogLevel.DEV
                            );

                        prefabRef.m_Prefab = resolvedEntity;
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
                        if (
                            !EntityManager.TryGetBuffer(
                                entity,
                                false,
                                out DynamicBuffer<VehicleModel> vehicleModel
                            )
                        )
                        {
                            LogHelper.SendLog(
                                $"Failed to get VehicleModel buffer on {entity}",
                                LogLevel.Error
                            );
                            continue;
                        }

                        LogHelper.SendLog(
                            $"Found {vehicleModel.Length} VehicleModel entries on {entity}",
                            LogLevel.DEVD
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

                                if (
                                    !TryResolvePrefab(
                                        routeModel,
                                        prefabAssets,
                                        notFound,
                                        prefabSystem,
                                        out Entity resolvedEntity,
                                        out PrefabBase pb,
                                        out string prefabKey
                                    )
                                )
                                {
                                    vehicleModel.RemoveAt(i);
                                    removed = true;
                                    break;
                                }

                                if (routeIndex == 0)
                                    model.m_PrimaryPrefab = resolvedEntity;
                                else
                                    model.m_SecondaryPrefab = resolvedEntity;

                                if (Mod.m_Setting.PerObjectLogging)
                                    LogHelper.SendLog(
                                        $"Successfully swapped on routes {prefabKey}"
                                    );
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
                    LogHelper.SendLog(
                        "PrefabBase not found:\n" + string.Join("\n", notFound.OrderBy(x => x))
                    );
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

        private bool TryResolvePrefab(
            Entity source,
            Dictionary<string, PrefabBase> prefabAssets,
            HashSet<string> notFound,
            PrefabSystem prefabSystem,
            out Entity resolvedEntity,
            out PrefabBase prefabBase,
            out string prefabKey
        )
        {
            resolvedEntity = Entity.Null;
            prefabBase = null;
            prefabKey = null;

            if (EntityManager.IsComponentEnabled<PrefabData>(source))
                return false;

            PrefabID obs = prefabSystem.GetObsoleteID(source);
            Match reg = PrefabRegex.Match(obs.ToString());

            if (!reg.Success)
                return false;

            string pType = reg.Groups[1].Value;
            string pName = reg.Groups[2].Value;

            if (string.IsNullOrEmpty(pType) || string.IsNullOrEmpty(pName))
            {
                LogHelper.SendLog(
                    $"Fail: {obs} (Unable to deduce PrefabName or PrefabType)",
                    LogLevel.Error
                );
                return false;
            }

            prefabKey = $"{pType}:{pName}";

            if (notFound.Contains(prefabKey))
                return false;

            if (!prefabAssets.TryGetValue(prefabKey, out prefabBase))
            {
                notFound.Add(prefabKey);
                return false;
            }

            if (!prefabSystem.TryGetEntity(prefabBase, out resolvedEntity))
            {
                LogHelper.SendLog(
                    $"Failed search for {prefabKey} (Entity not found)",
                    LogLevel.Error
                );
                return false;
            }

            return true;
        }
    }
}
