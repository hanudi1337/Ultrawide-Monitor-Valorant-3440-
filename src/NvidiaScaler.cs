using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NvAPIWrapper;
using NvAPIWrapper.Display;
using NvAPIWrapper.Native.Display;

namespace ValorantStretchHelper
{
    /// <summary>
    /// Kapselt sämtliche NvAPI-Zugriffe (über NvAPIWrapper). Alle öffentlichen Methoden
    /// sind exception-sicher: Ohne NVIDIA-Treiber/NvAPI liefern sie false bzw. -1 und
    /// setzen LastError, statt die Anwendung zum Absturz zu bringen.
    /// Die eigentlichen NvAPI-Aufrufe stecken in nicht-inlinebaren Core-Methoden, damit
    /// eine fehlende NvAPIWrapper.dll erst beim Aufruf (und damit im try/catch) auffällt.
    /// </summary>
    public class NvidiaScaler
    {
        public bool IsAvailable;
        public string LastError = "";

        // NV_SCALING-Wert für "Vollbild, Skalierung auf GPU ausführen" (Stretch)
        public const int ScalingFullScreenGpu = 2;

        private Dictionary<uint, uint> _originalScaling;

        public bool Initialize()
        {
            try
            {
                InitializeCore();
                IsAvailable = true;
                return true;
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                LastError = DescribeInitError(ex);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeCore()
        {
            NVIDIA.Initialize();
            PathInfo.GetDisplaysConfig(); // Probe: Displaykonfiguration muss lesbar sein
        }

        /// <summary>Aktuelle Skalierung aller Displays als Original merken.</summary>
        public bool SnapshotOriginal()
        {
            if (!IsAvailable) return false;
            try
            {
                _originalScaling = SnapshotCore();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Dictionary<uint, uint> SnapshotCore()
        {
            var result = new Dictionary<uint, uint>();
            PathInfo[] paths = PathInfo.GetDisplaysConfig();
            foreach (PathInfo path in paths)
                foreach (PathTargetInfo target in path.TargetsInfo)
                    result[target.DisplayDevice.DisplayId] = (uint)target.Scaling;
            return result;
        }

        public Dictionary<uint, uint> GetOriginalScaling()
        {
            return _originalScaling != null ? _originalScaling : new Dictionary<uint, uint>();
        }

        /// <summary>
        /// Setzt die GPU-Skalierung des primären Displays auf
        /// "Vollbild / Full-screen, auf GPU ausführen" (NV_SCALING_GPU_SCALING_TO_NATIVE).
        /// </summary>
        public bool EnableStretch()
        {
            if (!IsAvailable)
            {
                LastError = "NvAPI ist nicht verfügbar.";
                return false;
            }
            try
            {
                if (_originalScaling == null)
                    _originalScaling = SnapshotCore();
                EnableStretchCore();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void EnableStretchCore()
        {
            PathInfo[] paths = PathInfo.GetDisplaysConfig();

            bool primaryFound = false;
            foreach (PathInfo path in paths)
            {
                if (!path.IsGDIPrimary) continue;
                primaryFound = true;
                foreach (PathTargetInfo target in path.TargetsInfo)
                    target.Scaling = Scaling.ToNative; // Vollbild, auf GPU ausgeführt
            }

            if (!primaryFound)
            {
                foreach (PathInfo path in paths)
                    foreach (PathTargetInfo target in path.TargetsInfo)
                        target.Scaling = Scaling.ToNative;
            }

            PathInfo.SetDisplaysConfig(paths, DisplayConfigFlags.SaveToPersistence);
        }

        /// <summary>Stellt die gemerkte Original-Skalierung je DisplayId wieder her.</summary>
        public bool RestoreScaling(Dictionary<uint, uint> scalingByDisplayId)
        {
            if (!IsAvailable)
            {
                LastError = "NvAPI ist nicht verfügbar.";
                return false;
            }
            if (scalingByDisplayId == null || scalingByDisplayId.Count == 0) return true;
            try
            {
                RestoreCore(scalingByDisplayId);
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RestoreCore(Dictionary<uint, uint> scalingByDisplayId)
        {
            PathInfo[] paths = PathInfo.GetDisplaysConfig();
            bool changed = false;
            foreach (PathInfo path in paths)
            {
                foreach (PathTargetInfo target in path.TargetsInfo)
                {
                    uint original;
                    if (scalingByDisplayId.TryGetValue(target.DisplayDevice.DisplayId, out original))
                    {
                        target.Scaling = (Scaling)original;
                        changed = true;
                    }
                }
            }
            if (changed)
                PathInfo.SetDisplaysConfig(paths, DisplayConfigFlags.SaveToPersistence);
        }

        /// <summary>Aktueller Skalierungsmodus des primären Displays (-1 = unbekannt).</summary>
        public int GetPrimaryScalingValue()
        {
            if (!IsAvailable) return -1;
            try
            {
                return GetPrimaryScalingCore();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return -1;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GetPrimaryScalingCore()
        {
            PathInfo[] paths = PathInfo.GetDisplaysConfig();
            PathInfo primary = null;
            foreach (PathInfo path in paths)
            {
                if (path.IsGDIPrimary)
                {
                    primary = path;
                    break;
                }
            }
            if (primary == null && paths.Length > 0) primary = paths[0];
            if (primary == null || primary.TargetsInfo.Length == 0) return -1;
            return (int)primary.TargetsInfo[0].Scaling;
        }

        public static string ScalingText(int value)
        {
            switch (value)
            {
                case 0: return "Standard (Anzeige entscheidet)";
                case 1: return "Vollbild (ausbalanciert)";
                case 2: return "Vollbild – GPU  ➜  STRETCH";
                case 3: return "Keine Skalierung (GPU)";
                case 5: return "Seitenverhältnis (GPU)";
                case 6: return "Seitenverhältnis (ausbalanciert)";
                case 7: return "Keine Skalierung (ausbalanciert)";
                case -1: return "unbekannt";
                default: return string.Format("unbekannt ({0})", value);
            }
        }

        private static string DescribeInitError(Exception ex)
        {
            string typeName = ex.GetType().Name;
            string innerName = ex.InnerException != null ? ex.InnerException.GetType().Name : "";

            if (ex is DllNotFoundException || typeName == "NVIDIAApiException" || innerName == "NVIDIAApiException" ||
                typeName == "NVIDIANotSupportedException")
                return "NvAPI ist nicht verfügbar – vermutlich ist kein NVIDIA-Treiber installiert. (" + ex.Message + ")";

            if (ex is System.IO.FileNotFoundException || typeName == "FileLoadException")
                return "NvAPIWrapper.dll fehlt oder konnte nicht geladen werden. Die DLL muss im selben Ordner wie die EXE liegen.";

            return ex.Message;
        }
    }
}
