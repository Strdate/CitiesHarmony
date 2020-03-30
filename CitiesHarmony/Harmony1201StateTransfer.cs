﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CitiesHarmony {
    internal class Harmony1201StateTransfer {
        private Harmony harmony;
        private Assembly assembly;

        private MethodInfo HarmonySharedState_GetPatchedMethods;
        private MethodInfo HarmonySharedState_GetPatchInfo;

        private FieldInfo PatchInfo_prefixed;
        private FieldInfo PatchInfo_postfixes;
        private FieldInfo PatchInfo_transpilers;

        private FieldInfo Patch_owner;
        private FieldInfo Patch_priority;
        private FieldInfo Patch_before;
        private FieldInfo Patch_after;
        private FieldInfo Patch_patch;

        private Type harmonyInstanceType;
        private MethodInfo HarmonyInstance_Create;
        private MethodInfo HarmonyInstance_UnpatchAll;

        public Harmony1201StateTransfer(Harmony harmony, Assembly assembly) {
            this.harmony = harmony;
            this.assembly = assembly;

            UnityEngine.Debug.Log($"Transferring Harmony {assembly.GetName().Version} state ({assembly.FullName})");

            var sharedStateType = assembly.GetType("Harmony.HarmonySharedState");
            HarmonySharedState_GetPatchedMethods = sharedStateType.GetMethodOrThrow("GetPatchedMethods", BindingFlags.NonPublic | BindingFlags.Static);
            HarmonySharedState_GetPatchInfo = sharedStateType.GetMethodOrThrow("GetPatchInfo", BindingFlags.NonPublic | BindingFlags.Static);

            var patchInfoType = assembly.GetType("Harmony.PatchInfo");
            PatchInfo_prefixed = patchInfoType.GetFieldOrThrow("prefixes");
            PatchInfo_postfixes = patchInfoType.GetFieldOrThrow("postfixes");
            PatchInfo_transpilers = patchInfoType.GetFieldOrThrow("transpilers");

            var patchType = assembly.GetType("Harmony.Patch");
            Patch_owner = patchType.GetFieldOrThrow("owner");
            Patch_priority = patchType.GetFieldOrThrow("priority");
            Patch_before = patchType.GetFieldOrThrow("before");
            Patch_after = patchType.GetFieldOrThrow("after");
            Patch_patch = patchType.GetFieldOrThrow("patch");

            harmonyInstanceType = assembly.GetType("Harmony.HarmonyInstance") ?? throw new Exception("HarmonyInstance type not found");
            HarmonyInstance_Create = harmonyInstanceType.GetMethodOrThrow("Create", BindingFlags.Public | BindingFlags.Static);
            HarmonyInstance_UnpatchAll = harmonyInstanceType.GetMethodOrThrow("UnpatchAll", new Type[] { typeof(string) });
        }

        public void Patch() {
            var patchedMethods = new List<MethodBase>((HarmonySharedState_GetPatchedMethods.Invoke(null, new object[0]) as IEnumerable<MethodBase>));

            UnityEngine.Debug.Log($"{patchedMethods.Count} patched methods found.");

            var processors = new List<PatchProcessor>();

            foreach (var method in patchedMethods) {
                var patchInfo = HarmonySharedState_GetPatchInfo.Invoke(null, new object[] { method });
                if (patchInfo == null) continue;

                var prefixes = (object[])PatchInfo_prefixed.GetValue(patchInfo);
                foreach (var patch in prefixes) {
                    processors.Add(CreateHarmony(patch)
                        .CreateProcessor(method)
                        .AddPrefix(CreateHarmonyMethod(patch)));
                }

                var postfixes = (object[])PatchInfo_postfixes.GetValue(patchInfo);
                foreach (var patch in postfixes) {
                    processors.Add(CreateHarmony(patch)
                        .CreateProcessor(method)
                        .AddPostfix(CreateHarmonyMethod(patch)));
                }

                var transpilers = (object[])PatchInfo_transpilers.GetValue(patchInfo);
                foreach (var patch in transpilers) {
                    processors.Add(CreateHarmony(patch)
                        .CreateProcessor(method)
                        .AddTranspiler(CreateHarmonyMethod(patch)));
                }
            }

            UnityEngine.Debug.Log($"Reverting patches...");
            var oldInstance = HarmonyInstance_Create.Invoke(null, new object[] { "CitiesHarmony" });
            HarmonyInstance_UnpatchAll.Invoke(oldInstance, new object[] { null });

            // Reset shared state
            var sharedStateAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.Contains("HarmonySharedState"));
            if (sharedStateAssembly != null) {
                var stateField = sharedStateAssembly.GetType("HarmonySharedState")?.GetField("state");
                if (stateField != null) {
                    UnityEngine.Debug.Log("Resetting HarmonySharedState...");
                    stateField.SetValue(null, null);
                }
            }

            // Apply patches to old harmony
            Harmony1201SelfPatcher.Apply(harmony, assembly);

            foreach (var processor in processors) {
                processor.Patch();
            }
        }

        private Harmony CreateHarmony(object patch) {
            var owner = (string)Patch_owner.GetValue(patch);
            return new Harmony(owner);
        }

        private HarmonyMethod CreateHarmonyMethod(object patch) {
            var priority = (int)Patch_priority.GetValue(patch);
            var before = (string[])Patch_before.GetValue(patch);
            var after = (string[])Patch_after.GetValue(patch);
            var method = (MethodInfo)Patch_patch.GetValue(patch);
            return new HarmonyMethod(method, priority, before, after);
        }
    }
}