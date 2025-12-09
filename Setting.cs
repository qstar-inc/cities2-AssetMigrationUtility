using System.Collections.Generic;
using AssetMigrationUtility.Systems;
using Colossal;
using Colossal.IO.AssetDatabase;
using Colossal.Json;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using StarQ.Shared.Extensions;
using Unity.Entities;

namespace AssetMigrationUtility
{
    [FileLocation("ModsSettings\\StarQ\\" + nameof(AssetMigrationUtility))]
    [SettingsUITabOrder(GeneralTab, AboutTab, LogTab)]
    public class Setting : ModSetting
    {
        public Setting(IMod mod)
            : base(mod) => SetDefaults();

        public const string GeneralTab = "GeneralTab";
        public const string GeneralGroup = "GeneralGroup";

        public const string AboutTab = "AboutTab";
        public const string InfoGroup = "InfoGroup";

        public const string LogTab = "LogTab";

        [Exclude]
        [SettingsUIHidden]
        public bool IsInGame => WorldHelper.IsGame;

        [SettingsUISection(GeneralTab, GeneralGroup)]
        public bool IsEnabled { get; set; } = true;

        [SettingsUIButton]
        [SettingsUISection(GeneralTab, GeneralGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsInGame), true)]
        public bool RunOnce
        {
            set
            {
                World
                    .DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<AssetMigration>()
                    .MigrateAssets();
            }
        }

        public override void SetDefaults()
        {
            IsEnabled = true;
        }

        [SettingsUISection(AboutTab, InfoGroup)]
        public string NameText => Mod.Name;

        [SettingsUISection(AboutTab, InfoGroup)]
        public string VersionText => VariableHelper.AddDevSuffix(Mod.Version);

        [SettingsUISection(AboutTab, InfoGroup)]
        public string AuthorText => VariableHelper.StarQ;

        [SettingsUIButton]
        [SettingsUIButtonGroup("Social")]
        [SettingsUISection(AboutTab, InfoGroup)]
        public bool BMaCLink
        {
            set => VariableHelper.OpenBMAC();
        }

        //[SettingsUIButton]
        //[SettingsUIButtonGroup("Social")]
        //[SettingsUISection(AboutTab, InfoGroup)]
        //public bool Discord
        //{
        //    set => VariableHelper.OpenDiscord();
        //}

        [SettingsUIMultilineText]
        [SettingsUIDisplayName(typeof(LogHelper), nameof(LogHelper.LogText))]
        [SettingsUISection(LogTab, "")]
        public string LogText => string.Empty;

        [Exclude]
        [SettingsUIHidden]
        public bool IsLogMissing
        {
            get => VariableHelper.CheckLog(Mod.Id);
        }

        [SettingsUIButton]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsLogMissing))]
        [SettingsUISection(LogTab, "")]
        public bool OpenLog
        {
            set => VariableHelper.OpenLog(Mod.Id);
        }
    }
}
