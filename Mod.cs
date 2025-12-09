using System.Collections.Generic;
using System.Reflection;
using AssetMigrationUtility.Systems;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using StarQ.Shared.Extensions;
using Unity.Entities;

namespace AssetMigrationUtility
{
    public class Mod : IMod
    {
        public static string Id = nameof(AssetMigrationUtility);
        public static string Name = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyTitleAttribute>()
            .Title;
        public static string Version = Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version.ToString(3);

        public static ILog log = LogManager.GetLogger($"{Id}").SetShowsErrorsInUI(false);
        public static Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            LogHelper.Init(Id, log);
            LocaleHelper.Init(Id, Name, GetReplacements);

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();

            AssetDatabase.global.LoadSettings(
                nameof(AssetMigrationUtility),
                m_Setting,
                new Setting(this)
            );
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<AssetMigration>();
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }

        public static Dictionary<string, string> GetReplacements()
        {
            return new() { { "X", "Y" } };
        }
    }
}
