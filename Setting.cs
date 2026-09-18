using System;
using System.Reflection;
using AssetMigrationUtility.Systems;
using Game.Debug;
using Game.Modding;
using Game.Settings;
using StarQ.Shared.Extensions;
using StarQ.Shared.Generators;
using Unity.Entities;

namespace AssetMigrationUtility
{
    [GenerateSettingCommonAttribute]
    [SettingsUIShowGroupName(DebugGroup, AboutModGroup)]
    public partial class Setting : ModSetting
    {
        public override void SetDefaults()
        {
            IsEnabled = true;
            PerObjectLogging = false;
        }

        public const string DebugGroup = "DebugGroup";

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
        [SettingsUISection(GeneralTab, DebugGroup)]
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
                    return;
                }

                LogHelper.SendLog("Done");
            }
        }

        [SettingsUIButton]
        [SettingsUISection(GeneralTab, DebugGroup)]
        [SettingsUIDisableByCondition(typeof(WorldHelper), nameof(WorldHelper.IsGame), true)]
        public bool RemoveExtraCompanies
        {
            set
            {
                try
                {
                    LogHelper.SendLog(
                        "Forwarding cleanup request for extra companies to the DebugSystem..."
                    );
                    EconomyDebugSystem.RemoveExtraCompanies();
                }
                catch (Exception ex)
                {
                    LogHelper.SendLog(
                        $"Failed to invoke RemoveExtraCompanies: {ex.Message}",
                        LogLevel.Error
                    );
                    return;
                }

                LogHelper.SendLog("Done");
            }
        }
    }
}
