using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ValorantStretchHelper
{
    public struct DisplayMode
    {
        public int Width;
        public int Height;
        public int Frequency;

        public override string ToString()
        {
            string aspect = "";
            if (Width * 9 == Height * 16) aspect = "   (16:9)";
            else if (Width * 10 == Height * 16) aspect = "   (16:10)";
            else if (Width * 3 == Height * 4) aspect = "   (4:3)";
            else if (Width * 4 == Height * 5) aspect = "   (5:4)";
            else if (Width * 9 == Height * 21) aspect = "   (21:9)";
            return string.Format("{0} × {1}  @ {2} Hz{3}", Width, Height, Frequency, aspect);
        }
    }

    /// <summary>
    /// Auflösungswechsel über die Windows-API (EnumDisplaySettings / ChangeDisplaySettingsEx).
    /// Änderungen erfolgen dynamisch (nicht in die Registry geschrieben) – nach einem
    /// Neustart ist automatisch wieder die Originalauflösung aktiv.
    /// </summary>
    public static class DisplayHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettingsW(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsExW(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        private const int ENUM_CURRENT_SETTINGS = -1;

        private const int DM_BITSPERPEL = 0x40000;
        private const int DM_PELSWIDTH = 0x80000;
        private const int DM_PELSHEIGHT = 0x100000;
        private const int DM_DISPLAYFREQUENCY = 0x400000;
        private const int DM_DISPLAYFIXEDOUTPUT = 0x20000000;

        private const int DMDFO_STRETCH = 1;

        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const int DISP_CHANGE_RESTART = 1;
        private const int DISP_CHANGE_FAILED = -1;
        private const int DISP_CHANGE_BADMODE = -2;
        private const int DISP_CHANGE_NOTUPDATED = -3;
        private const int DISP_CHANGE_BADFLAGS = -4;
        private const int DISP_CHANGE_BADPARAM = -5;

        private static DEVMODE NewDevMode()
        {
            var dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            dm.dmDriverExtra = 0;
            return dm;
        }

        public static DisplayMode GetCurrentMode(string deviceName)
        {
            var dm = NewDevMode();
            var mode = new DisplayMode();
            if (EnumDisplaySettingsW(deviceName, ENUM_CURRENT_SETTINGS, ref dm))
            {
                mode.Width = dm.dmPelsWidth;
                mode.Height = dm.dmPelsHeight;
                mode.Frequency = dm.dmDisplayFrequency;
            }
            return mode;
        }

        /// <summary>
        /// Alle vom Treiber gemeldeten 32-Bit-Modi, je Auflösung nur die höchste
        /// Bildwiederholrate, absteigend sortiert.
        /// </summary>
        public static List<DisplayMode> GetModes(string deviceName)
        {
            var best = new Dictionary<long, DisplayMode>();
            var dm = NewDevMode();
            int i = 0;
            while (EnumDisplaySettingsW(deviceName, i, ref dm))
            {
                i++;
                if (dm.dmBitsPerPel != 32) continue;
                if (dm.dmPelsWidth < 1024 || dm.dmPelsHeight < 720) continue;

                long key = (long)dm.dmPelsWidth * 100000L + dm.dmPelsHeight;
                DisplayMode existing;
                if (!best.TryGetValue(key, out existing) || dm.dmDisplayFrequency > existing.Frequency)
                {
                    var m = new DisplayMode();
                    m.Width = dm.dmPelsWidth;
                    m.Height = dm.dmPelsHeight;
                    m.Frequency = dm.dmDisplayFrequency;
                    best[key] = m;
                }
            }

            var list = new List<DisplayMode>(best.Values);
            list.Sort(delegate(DisplayMode a, DisplayMode b)
            {
                if (a.Width != b.Width) return b.Width.CompareTo(a.Width);
                return b.Height.CompareTo(a.Height);
            });
            return list;
        }

        /// <summary>
        /// Setzt die Auflösung. Gibt null bei Erfolg zurück, sonst eine Fehlermeldung.
        /// withStretchFlag setzt zusätzlich das Windows-"Stretch"-Flag (DMDFO_STRETCH) –
        /// hilfreich als Best-Effort-Streckung bei AMD/Intel ohne Treiber-API.
        /// </summary>
        public static string SetMode(string deviceName, int width, int height, int frequency, bool withStretchFlag)
        {
            var dm = NewDevMode();
            if (!EnumDisplaySettingsW(deviceName, ENUM_CURRENT_SETTINGS, ref dm))
                return "Der aktuelle Anzeigemodus konnte nicht gelesen werden.";

            dm.dmPelsWidth = width;
            dm.dmPelsHeight = height;
            dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT;
            if (frequency > 0)
            {
                dm.dmDisplayFrequency = frequency;
                dm.dmFields |= DM_DISPLAYFREQUENCY;
            }
            if (withStretchFlag)
            {
                dm.dmDisplayFixedOutput = DMDFO_STRETCH;
                dm.dmFields |= DM_DISPLAYFIXEDOUTPUT;
            }

            // dwflags = 0  =>  dynamischer Wechsel, wird nicht dauerhaft gespeichert
            int result = ChangeDisplaySettingsExW(deviceName, ref dm, IntPtr.Zero, 0, IntPtr.Zero);

            if (result != DISP_CHANGE_SUCCESSFUL && withStretchFlag)
            {
                // Manche Treiber akzeptieren das Fixed-Output-Flag nicht – ohne erneut versuchen
                dm.dmDisplayFixedOutput = 0;
                dm.dmFields &= ~DM_DISPLAYFIXEDOUTPUT;
                result = ChangeDisplaySettingsExW(deviceName, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
            }

            if (result != DISP_CHANGE_SUCCESSFUL && frequency > 0)
            {
                // Notfalls ohne feste Bildwiederholrate versuchen
                dm.dmFields &= ~DM_DISPLAYFREQUENCY;
                result = ChangeDisplaySettingsExW(deviceName, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
            }

            return result == DISP_CHANGE_SUCCESSFUL ? null : Describe(result, width, height);
        }

        private static string Describe(int code, int width, int height)
        {
            switch (code)
            {
                case DISP_CHANGE_BADMODE:
                    return string.Format("Der Modus {0}×{1} wird von diesem Monitor/Treiber nicht unterstützt.", width, height);
                case DISP_CHANGE_RESTART:
                    return "Für diese Änderung wäre ein Neustart nötig – Wechsel abgebrochen.";
                case DISP_CHANGE_NOTUPDATED:
                    return "Die Einstellungen konnten nicht geschrieben werden.";
                case DISP_CHANGE_BADFLAGS:
                case DISP_CHANGE_BADPARAM:
                    return "Ungültige Parameter beim Auflösungswechsel.";
                case DISP_CHANGE_FAILED:
                default:
                    return string.Format("Der Grafiktreiber hat den Wechsel auf {0}×{1} abgelehnt (Code {2}).", width, height, code);
            }
        }
    }
}
