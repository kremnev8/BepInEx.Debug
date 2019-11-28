﻿using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace StartupProfiler
{
    public class StartupProfiler
    {
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        private static ManualLogSource Logger;
        private static Harmony harmony;

        private static string[] unityMethods = new[] { "Awake", "Start", "Main" };
        private static readonly Dictionary<Type, Stopwatch> timers = new Dictionary<Type, Stopwatch>();

        public static void Patch(AssemblyDefinition ass) { }

        public static void Finish()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource("StartupProfiler");
            harmony = new Harmony("StartupProfiler");

            harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Initialize)),
                          postfix: new HarmonyMethod(typeof(StartupProfiler).GetMethod(nameof(ChainloaderHook))));
        }

        public static void ChainloaderHook()
        {
            harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Start)),
                          transpiler: new HarmonyMethod(typeof(StartupProfiler).GetMethod(nameof(FindPluginTypes))),
                          postfix: new HarmonyMethod(typeof(StartupProfiler).GetMethod(nameof(ChainloaderPost))));
        }

        public static IEnumerable<CodeInstruction> FindPluginTypes(IEnumerable<CodeInstruction> instructions)
        {
            foreach(var code in instructions)
            {
                if(code.opcode == OpCodes.Callvirt && code.operand.ToString().Contains("AddComponent"))
                    yield return new CodeInstruction(OpCodes.Call, typeof(StartupProfiler).GetMethod(nameof(PatchPlugin)));

                yield return code;
            }
        }

        public static Type PatchPlugin(Type type)
        {
            timers[type] = new Stopwatch();

            foreach(var unityMethod in unityMethods)
            {
                var methodInfo = type.GetMethod(unityMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if(methodInfo == null) continue;
                harmony.Patch(methodInfo, new HarmonyMethod(StartTimerMethodInfo), new HarmonyMethod(StopTimerMethodInfo));
                Logger.LogInfo($"Patching method {methodInfo.Name} in type {type}");
            }

            foreach(var methodInfo in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                harmony.Patch(methodInfo, new HarmonyMethod(StartTimerMethodInfo), new HarmonyMethod(StopTimerMethodInfo));
                Logger.LogInfo($"Patching method {methodInfo.Name} in type {type}");
            }

            return type;
        }

        public static MethodInfo StartTimerMethodInfo = typeof(StartupProfiler).GetMethod(nameof(StartTimer));
        public static void StartTimer(object __instance)
        {
            if(timers.TryGetValue(__instance.GetType(), out var watch))
                watch.Start();
        }

        public static MethodInfo StopTimerMethodInfo = typeof(StartupProfiler).GetMethod(nameof(StopTimer));
        public static void StopTimer(object __instance)
        {
            if(timers.TryGetValue(__instance.GetType(), out var watch))
                watch.Stop();
        }

        public static void ChainloaderPost()
        {
            ThreadingHelper.Instance.StartCoroutine(PrintResults());
        }

        public static IEnumerator PrintResults()
        {
            yield return null;

            foreach(var timer in timers.OrderBy(x => x.Key.FullName))
                Logger.LogInfo($"{timer.Key}: {timer.Value.ElapsedMilliseconds}");

            harmony.UnpatchAll();
        }
    }
}
