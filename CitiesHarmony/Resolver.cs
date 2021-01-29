﻿namespace CitiesHarmony {
    using System;
    using System.Linq;
    using System.Reflection;

    public static class Resolver {
        private static readonly Version MinHarmonyVersionToHandle = new Version(2, 0, 0, 8);
        const string HarmonyName = "0Harmony";
        public static void InstallHarmonyResolver()
        {
            UnityEngine.Debug.Log($"[CitiesHarmony] InstallHarmonyResolver() called ...");
            var mCSResolver = 
                typeof(BuildConfig)
                .GetMethod("CurrentDomain_AssemblyResolve", BindingFlags.NonPublic | BindingFlags.Static);
            ResolveEventHandler dCSResolver =
                (ResolveEventHandler)Delegate.CreateDelegate(typeof(ResolveEventHandler), mCSResolver);

            AppDomain.CurrentDomain.AssemblyResolve -= dCSResolver;
            AppDomain.CurrentDomain.TypeResolve -= dCSResolver;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveHarmony;
            AppDomain.CurrentDomain.TypeResolve += ResolveHarmony;
            AppDomain.CurrentDomain.AssemblyResolve += dCSResolver;
            AppDomain.CurrentDomain.TypeResolve += dCSResolver;

            UnityEngine.Debug.Log($"[CitiesHarmony] InstallHarmonyResolver() successfull!");
        }

        public static Assembly ResolveHarmony(object sender, ResolveEventArgs args)
        {
            try {
                if(IsHarmony2(new AssemblyName(args.Name))) {
#if DEBUG
                    UnityEngine.Debug.Log($"[CitiesHarmony] resolving '{args.Name}' ...");
#endif
                    var ret = GetHarmony2();
                    UnityEngine.Debug.Log($"[CitiesHarmony] Resolved '{args.Name}' to {ret}");
                    return ret;
                }
            } catch(Exception e) {
                    UnityEngine.Debug.LogException(e);
            }

            return null;
        }

        public static bool IsHarmony2(AssemblyName assemblyName)
        {
            return assemblyName.Name == HarmonyName &&
                   assemblyName.Version >= MinHarmonyVersionToHandle;
        }

        public static Assembly GetHarmony2()
        {
            return typeof(HarmonyLib.Harmony).Assembly;
        }
    }
}