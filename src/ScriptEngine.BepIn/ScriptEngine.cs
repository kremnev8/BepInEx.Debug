﻿using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace ScriptEngine
{
    [BepInPlugin(GUID: GUID, Name: "Script Engine", Version: Version)]
    public class ScriptEngine : BaseUnityPlugin
    {
        public const string GUID = "com.bepis.bepinex.scriptengine";
        public const string Version = "2.0";

        public string ScriptDirectory => Path.Combine(Paths.PluginPath, "scripts");

        GameObject scriptManager;

        ConfigWrapper<bool> LoadOnStart { get; set; }
        SavedKeyboardShortcut ReloadKey { get; set; }

        void Awake()
        {
            LoadOnStart = new ConfigWrapper<bool>("LoadOnStart", this, true);
            ReloadKey = new SavedKeyboardShortcut("ReloadKey", this, new KeyboardShortcut(KeyCode.RightAlt));

            if(LoadOnStart.Value)
                ReloadPlugins();
        }

        void Update()
        {
            if(ReloadKey.IsDown())
                ReloadPlugins();
        }

        void ReloadPlugins()
        {
            Destroy(scriptManager);
            scriptManager = new GameObject($"ScriptEngine_{DateTime.Now.Ticks}");
            DontDestroyOnLoad(scriptManager);

            var files = Directory.GetFiles(ScriptDirectory, "*.dll");
            if(files.Length > 0)
            {
                foreach(string path in Directory.GetFiles(ScriptDirectory, "*.dll"))
                    LoadDLL(path, scriptManager);

                Logger.Log(LogLevel.Message, "Reloaded script plugins!");
            }
            else
            {
                Logger.Log(LogLevel.Message, "No plugins to reload");
            }
        }

        void LoadDLL(string path, GameObject obj)
        {
            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(ScriptDirectory);
            defaultResolver.AddSearchDirectory(Paths.ManagedPath);

            AssemblyDefinition dll = AssemblyDefinition.ReadAssembly(path, new ReaderParameters
            {
                AssemblyResolver = defaultResolver
            });

            dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

            using(var ms = new MemoryStream())
            {
                dll.Write(ms);
                var assembly = Assembly.Load(ms.ToArray());

                foreach(Type type in assembly.GetTypes())
                {
                    if(typeof(BaseUnityPlugin).IsAssignableFrom(type))
                        obj.AddComponent(type);
                }
            }
        }
    }
}
