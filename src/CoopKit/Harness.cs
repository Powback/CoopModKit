using System;
using System.Reflection;
using HarmonyLib;

namespace CoopKit
{
    /// <summary>Mod-harness discipline that pays off on every game.</summary>
    public static class Harness
    {
        /// <summary>
        /// Patch class by class: after a game update, one unresolvable target
        /// costs that feature, not the whole mod. Returns the failure count.
        /// </summary>
        public static int PatchAllIsolated(Harmony harmony, Assembly assembly, Action<string> logError)
        {
            var failures = 0;
            foreach (var type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), inherit: true).Length == 0) continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    failures++;
                    logError?.Invoke($"Patch '{type.Name}' failed, continuing without it: {e.Message}");
                }
            }
            return failures;
        }

        /// <summary>
        /// Refuse to run against a game build the mod was never verified on —
        /// loudly, at load, instead of confusingly at play time.
        /// </summary>
        public static bool VersionGate(
            string current, string target, bool ignoreMismatch,
            Action<string> logError, Action<string> logWarning)
        {
            if (current == target) return true;
            if (ignoreMismatch)
            {
                logWarning?.Invoke($"Game version '{current}' != target '{target}'; loading anyway.");
                return true;
            }
            logError?.Invoke(
                $"This build targets game version {target} but the game reports '{current}'. " +
                "Refusing to load. Set the ignore flag in the config to try anyway.");
            return false;
        }
    }

    /// <summary>A single bad frame must not become 60 log lines per second.</summary>
    public sealed class ThrottledLog
    {
        private readonly double _intervalSeconds;
        private readonly Action<string> _sink;
        private double _last = double.MinValue;

        public ThrottledLog(double intervalSeconds, Action<string> sink)
        {
            _intervalSeconds = intervalSeconds;
            _sink = sink;
        }

        /// <param name="now">Caller supplies its clock (e.g. unscaled game time).</param>
        public void Log(double now, string message)
        {
            if (now - _last < _intervalSeconds) return;
            _last = now;
            _sink?.Invoke(message);
        }
    }
}
