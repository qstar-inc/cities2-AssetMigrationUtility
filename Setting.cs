using System;
using System.Reflection;
using AssetMigrationUtility.Systems;
using Colossal.Json;
using Colossal.Reflection.Tests;
using Game.Debug;
using Game.Modding;
using Game.Prefabs;
using Game.Settings;
using StarQ.Shared.Extensions;
using StarQ.Shared.Generators;
using Unity.Entities;

namespace AssetMigrationUtility
{
    [GenerateSettingCommonAttribute]
    public partial class Setting : ModSetting
    {
        public override void SetDefaults()
        {
            IsEnabled = true;
            PerObjectLogging = false;
        }

        [SettingsUISection(GeneralTab, GeneralGroup)]
        public bool IsEnabled { get; set; } = true;

        [SettingsUISection(GeneralTab, GeneralGroup)]
        public bool PerObjectLogging { get; set; } = false;

        [SettingsUIButton]
        [SettingsUISection(GeneralTab, GeneralGroup)]
        [SettingsUIDisableByCondition(typeof(WorldHelper), nameof(WorldHelper.IsGame), true)]
        public bool RunOnce
        {
            set
            {
                World
                    .DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<AssetMigration>()
                    .MigrateAssets();
            }
        }

        [SettingsUIButton]
        [SettingsUISection(GeneralTab, GeneralGroup)]
        [SettingsUIDisableByCondition(typeof(WorldHelper), nameof(WorldHelper.IsGame), true)]
        public bool CleanupObsoleteEntities
        {
            set
            {
                try
                {
                    LogHelper.SendLog(
                        "Forwarding cleanup request for obsolete entities to the DebugSystem..."
                    );
                    MethodInfo method = typeof(DebugSystem).GetMethod(
                        "CleanupObsoleteEntities",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    );

                    method.Invoke(WorldHelper.GetSystem<DebugSystem>(), null);
                }
                catch (Exception ex)
                {
                    LogHelper.SendLog(
                        $"Failed to invoke CleanupObsoleteEntities: {ex.Message}",
                        LogLevel.Error
                    );
                }
            }
        }
    }
}
