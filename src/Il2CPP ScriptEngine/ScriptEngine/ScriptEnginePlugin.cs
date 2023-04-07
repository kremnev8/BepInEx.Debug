using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ScriptEngine
{
    [BepInPlugin(MODGUID, MODNAME, VERSION)]
    public class ScriptEnginePlugin : BasePlugin
    {
        public const string MODNAME = "IL2CPP Script Engine";
        public const string MODGUID = "com.bepis.bepinex.il2cpp-scriptengine";
        public const string VERSION = "1.0.0";

        public static ManualLogSource logger;

        internal static ConfigEntry<bool> LoadOnStart { get; set; }
        internal static ConfigEntry<KeyCode> ReloadKey { get; set; }

        public override void Load()
        {
            logger = Log;
            ClassInjector.RegisterTypeInIl2Cpp<ScriptEngineBehaviour>();

            LoadOnStart = Config.Bind("General", "LoadOnStart", false,
                new ConfigDescription("Load all plugins from the scripts folder when starting the application"));
            ReloadKey = Config.Bind("General", "ReloadKey", KeyCode.F6,
                new ConfigDescription("Press this key to reload all the plugins from the scripts folder"));

            ScriptEngineBehaviour behaviour = AddComponent<ScriptEngineBehaviour>();
            
            if (LoadOnStart.Value)
                behaviour.ReloadPlugins();
            logger.LogInfo("Script Engine loaded!");
        }
    }
}