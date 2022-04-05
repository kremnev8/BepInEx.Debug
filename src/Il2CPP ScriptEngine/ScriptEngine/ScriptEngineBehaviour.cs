using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.IL2CPP.Utils.Collections;
using BepInEx.Logging;
using Mono.Cecil;
using UnityEngine;
using Directory = Il2CppSystem.IO.Directory;
using Input = UnityEngine.Input;
using Path = Il2CppSystem.IO.Path;

namespace ScriptEngine
{
    public class ScriptEngineBehaviour : MonoBehaviour
    {
        public ScriptEngineBehaviour(IntPtr ptr) : base(ptr) { }

        public string ScriptDirectory => Path.Combine(Paths.BepInExRootPath, "scripts");

        public List<BasePlugin> loadedPlugins = new List<BasePlugin>();

        private void Update()
        {
            if (Input.GetKeyDown(ScriptEnginePlugin.ReloadKey.Value))
                ReloadPlugins();
        }

        internal void ReloadPlugins()
        {
            ScriptEnginePlugin.logger.Log(LogLevel.Info, "Unloading old plugin instances");
            foreach (BasePlugin plugin in loadedPlugins)
            {
                plugin.Unload();
            }

            loadedPlugins.Clear();

            var files = Directory.GetFiles(ScriptDirectory, "*.dll");
            if (files.Length > 0)
            {
                foreach (string path in Directory.GetFiles(ScriptDirectory, "*.dll"))
                    LoadDLL(path);

                ScriptEnginePlugin.logger.LogMessage("Reloaded all plugins!");
            }
            else
            {
                ScriptEnginePlugin.logger.LogMessage("No plugins to reload");
            }
        }

        private void LoadDLL(string path)
        {
            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(ScriptDirectory);
            defaultResolver.AddSearchDirectory(Paths.ManagedPath);
            defaultResolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);

            ScriptEnginePlugin.logger.Log(LogLevel.Info, $"Loading plugins from {path}");

            using var dll = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {AssemblyResolver = defaultResolver});
            dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

            using var ms = new MemoryStream();

            dll.Write(ms);
            var ass = Assembly.Load(ms.ToArray());

            foreach (Type type in GetTypesSafe(ass))
            {
                try
                {
                    if (typeof(BasePlugin).IsAssignableFrom(type))
                    {
                        var metadata = MetadataHelper.GetMetadata(type);
                        if (metadata != null)
                        {
                            var typeDefinition = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                            var pluginInfo = IL2CPPChainloader.ToPluginInfo(typeDefinition, path);
                            IL2CPPChainloader.Instance.Plugins[metadata.GUID] = pluginInfo;

                            ScriptEnginePlugin.logger.Log(LogLevel.Info, $"Loading {metadata.GUID}");
                            StartCoroutine(DelayAction(() =>
                            {
                                try
                                {
                                    TryRunModuleCtor(pluginInfo, ass);
                                    MethodInfo info = typeof(BepInEx.PluginInfo).GetProperty(nameof(BepInEx.PluginInfo.Instance)).GetSetMethod(true);

                                    var inst = IL2CPPChainloader.Instance.LoadPlugin(pluginInfo, ass);
                                    info.Invoke(pluginInfo, new object[] {inst});
                                    loadedPlugins.Add(inst);
                                }
                                catch (Exception e)
                                {
                                    ScriptEnginePlugin.logger.LogError($"Failed to load plugin {metadata.GUID} because of exception: {e}");
                                }
                            }).WrapToIl2Cpp());
                        }
                    }
                }
                catch (Exception e)
                {
                    ScriptEnginePlugin.logger.LogError($"Failed to load plugin {type.Name} because of exception: {e}");
                }
            }
        }

        private static void TryRunModuleCtor(BepInEx.PluginInfo plugin, Assembly assembly)
        {
            try
            {
                RuntimeHelpers.RunModuleConstructor(assembly.GetType(plugin.TypeName).Module.ModuleHandle);
            }
            catch (Exception e)
            {
                ScriptEnginePlugin.logger.Log(LogLevel.Warning,
                    $"Couldn't run Module constructor for {assembly.FullName}::{plugin.TypeName}: {e}");
            }
        }

        private static IEnumerable<Type> GetTypesSafe(Assembly ass)
        {
            try
            {
                return ass.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var sbMessage = new StringBuilder();
                sbMessage.AppendLine("\r\n-- LoaderExceptions --");
                foreach (var l in ex.LoaderExceptions)
                    sbMessage.AppendLine(l.ToString());
                sbMessage.AppendLine("\r\n-- StackTrace --");
                sbMessage.AppendLine(ex.StackTrace);
                ScriptEnginePlugin.logger.LogError(sbMessage.ToString());
                return ex.Types.Where(x => x != null);
            }
        }

        private static IEnumerator DelayAction(Action action)
        {
            yield return null;
            action();
        }
    }
}