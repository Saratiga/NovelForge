using System;
using System.Collections.Generic;
using NovelForge.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovelForge.Editor
{
    // Scripts are validated against the set of DSL commands at import time. Registering that
    // set as a custom dependency makes adding or removing a [NovelCommand] class reimport
    // every .nfscript, so a stale "Unknown command" import error clears by itself.
    [InitializeOnLoad]
    public static class NovelCommandDependency
    {
        public const string Name = "NovelForge/CommandNames";

        private const string LastHashKey = "NovelForge.CommandNamesHash";

        static NovelCommandDependency()
        {
            Hash128 hash = ComputeHash();
            AssetDatabase.RegisterCustomDependency(Name, hash);
            if (SessionState.GetString(LastHashKey, string.Empty) == hash.ToString())
                return;
            SessionState.SetString(LastHashKey, hash.ToString());
            EditorApplication.delayCall += AssetDatabase.Refresh;
        }

        public static Hash128 ComputeHash() => ComputeHash(CommandRegistry.CreateDefault().RegisteredNames);

        public static Hash128 ComputeHash(IEnumerable<string> commandNames)
        {
            var sorted = new List<string>(commandNames);
            sorted.Sort(StringComparer.Ordinal);
            return Hash128.Compute(string.Join("\n", sorted));
        }
    }
}
