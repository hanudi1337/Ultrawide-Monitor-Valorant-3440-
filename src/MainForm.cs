using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Management;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ValorantStretchHelper
{
    public class MainForm : Form
    {
        private readonly NvidiaScaler _nv = new NvidiaScaler();

        private string _deviceName = "";
        private string _gpuName = "Unbekannte Grafikkarte";
        private string _gpuVendor = "";
        private DisplayMode _originalMode;
        private BackupData _startupBackup;
        private bool _stretchActive;
        private bool _restoredAtExit;
        private bool _reallyExit;
        private bool _trayHintShown;

        private Label _lblGpu;
        private Label _lblRes;
        private Label _lblScaling;
        private Label _lblState;
        private CheckBox _chkChangeRes;
        private ComboBox _cmbRes;
        private Button _btnEnable;
        private Button _btnRestore;
        private TextBox _txtVendorInfo;
        private GroupBox _grpVendorInfo;
        private NotifyIcon _tray;
        private Timer _timer;
        private Icon _appIcon;

        public MainForm()
        {
            BuildUi();
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            _appIcon = CreateAppIcon();

            Text = "Valorant Ultrawide Stretch Helper";
            Icon = _appIcon;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(640, 610);

            var lblTitle = new Label();
            lblTitle.Text = "Valorant Ultrawide Stretch Helper";
            lblTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(18, 12);
            Controls.Add(lblTitle);

            var lblSub = new Label();
            lblSub.Text = "16:9-Bild per GPU-Skalierung auf die volle 21:9-Breite strecken (3440 × 1440)";
            lblSub.ForeColor = Color.DimGray;
            lblSub.AutoSize = true;
            lblSub.Location = new Point(20, 42);
            Controls.Add(lblSub);

            // -------- Status
            var grpStatus = new GroupBox();
            grpStatus.Text = "Status";
            grpStatus.Bounds = new Rectangle(18, 70, 604, 118);
            Controls.Add(grpStatus);

            _lblGpu = MakeStatusLabel(grpStatus, 24, "GPU: wird erkannt …");
            _lblRes = MakeStatusLabel(grpStatus, 46, "Desktop-Auflösung: –");
            _lblScaling = MakeStatusLabel(grpStatus, 68, "GPU-Skalierung: –");
            _lblState = MakeStatusLabel(grpStatus, 90, "Stretch: inaktiv");
            _lblState.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            // -------- Einstellungen
            var grpSettings = new GroupBox();
            grpSettings.Text = "Einstellungen";
            grpSettings.Bounds = new Rectangle(18, 196, 604, 92);
            Controls.Add(grpSettings);

            _chkChangeRes = new CheckBox();
            _chkChangeRes.Text = "Desktop-Auflösung mit umstellen (empfohlen – dann greift der Stretch zuverlässig)";
            _chkChangeRes.Checked = true;
            _chkChangeRes.AutoSize = true;
            _chkChangeRes.Location = new Point(14, 24);
            grpSettings.Controls.Add(_chkChangeRes);

            _cmbRes = new ComboBox();
            _cmbRes.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbRes.Bounds = new Rectangle(14, 52, 250, 26);
            grpSettings.Controls.Add(_cmbRes);

            var lblResHint = new Label();
            lblResHint.Text = "→ Dieselbe Auflösung auch in Valorant einstellen!";
            lblResHint.AutoSize = true;
            lblResHint.ForeColor = Color.FromArgb(180, 90, 0);
            lblResHint.Location = new Point(276, 56);
            grpSettings.Controls.Add(lblResHint);

            // -------- Buttons
            _btnEnable = new Button();
            _btnEnable.Text = "Stretch aktivieren";
            _btnEnable.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _btnEnable.Bounds = new Rectangle(18, 298, 295, 44);
            _btnEnable.BackColor = Color.FromArgb(220, 245, 225);
            _btnEnable.Click += OnEnableClick;
            Controls.Add(_btnEnable);

            _btnRestore = new Button();
            _btnRestore.Text = "Zurücksetzen (Original wiederherstellen)";
            _btnRestore.Font = new Font("Segoe UI", 10F);
            _btnRestore.Bounds = new Rectangle(327, 298, 295, 44);
            _btnRestore.Click += OnRestoreClick;
            Controls.Add(_btnRestore);

            // -------- Hinweise
            var grpHints = new GroupBox();
            grpHints.Text = "Wichtige Hinweise";
            grpHints.Bounds = new Rectangle(18, 352, 604, 146);
            Controls.Add(grpHints);

            var lblHints = new Label();
            lblHints.Bounds = new Rectangle(14, 22, 578, 116);
            lblHints.Text =
                "•  Valorant muss auf Anzeigemodus „Vollbild“ stehen (nicht „Randlos (Fenster)“),\r\n" +
                "    sonst greift die GPU-Skalierung nicht.\r\n" +
                "•  In Valorant dieselbe 16:9-Auflösung wählen, die oben eingestellt ist (z. B. 2560 × 1440).\r\n" +
                "•  Dies ist reines Bild-Strecken: Es verändert nichts am Spiel, an Spieldateien oder am\r\n" +
                "    Prozess und gibt keinen FOV-Vorteil.\r\n" +
                "•  Beim Beenden dieses Tools werden die Originaleinstellungen automatisch wiederhergestellt.\r\n" +
                "•  Reihenfolge: erst „Stretch aktivieren“, dann Valorant starten.";
            grpHints.Controls.Add(lblHints);

            // -------- AMD/Intel-Anleitung (nur sichtbar, wenn NvAPI fehlt)
            _grpVendorInfo = new GroupBox();
            _grpVendorInfo.Text = "GPU-Skalierung manuell einstellen (kein NvAPI verfügbar)";
            _grpVendorInfo.Bounds = new Rectangle(18, 508, 604, 190);
            _grpVendorInfo.Visible = false;
            Controls.Add(_grpVendorInfo);

            _txtVendorInfo = new TextBox();
            _txtVendorInfo.Multiline = true;
            _txtVendorInfo.ReadOnly = true;
            _txtVendorInfo.ScrollBars = ScrollBars.Vertical;
            _txtVendorInfo.BackColor = Color.FromArgb(250, 250, 240);
            _txtVendorInfo.Bounds = new Rectangle(14, 22, 578, 156);
            _grpVendorInfo.Controls.Add(_txtVendorInfo);

            // -------- Tray
            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Fenster öffnen", null, OnTrayOpen);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Stretch aktivieren", null, OnEnableClick);
            trayMenu.Items.Add("Zurücksetzen", null, OnRestoreClick);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Beenden (und zurücksetzen)", null, OnTrayExit);

            _tray = new NotifyIcon();
            _tray.Icon = _appIcon;
            _tray.Text = "Valorant Stretch Helper – inaktiv";
            _tray.ContextMenuStrip = trayMenu;
            _tray.Visible = true;
            _tray.DoubleClick += OnTrayOpen;

            _timer = new Timer();
            _timer.Interval = 2500;
            _timer.Tick += delegate { UpdateStatus(); };
        }

        private static Label MakeStatusLabel(GroupBox parent, int y, string text)
        {
            var lbl = new Label();
            lbl.Text = text;
            lbl.AutoSize = true;
            lbl.Location = new Point(14, y);
            parent.Controls.Add(lbl);
            return lbl;
        }

        private static Icon CreateAppIcon()
        {
            // 21:9-Rechteck mit zwei "Streck-Pfeilen" – rein programmatisch, keine Icon-Datei nötig
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var back = new SolidBrush(Color.FromArgb(28, 32, 48)))
                    g.FillRectangle(back, 0, 8, 32, 16);
                using (var pen = new Pen(Color.FromArgb(0, 205, 130), 2f))
                    g.DrawRectangle(pen, 1, 9, 29, 14);
                using (var arrow = new SolidBrush(Color.FromArgb(0, 205, 130)))
                {
                    g.FillPolygon(arrow, new[] { new Point(11, 12), new Point(11, 20), new Point(4, 16) });
                    g.FillPolygon(arrow, new[] { new Point(21, 12), new Point(21, 20), new Point(28, 16) });
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        // ------------------------------------------------------------------ Startlogik

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _deviceName = Screen.PrimaryScreen.DeviceName;
            DetectGpu();
            _nv.Initialize();

            // Übrig gebliebenes Backup = letzter Lauf wurde nicht sauber beendet
            if (BackupStore.Exists())
            {
                var answer = MessageBox.Show(this,
                    "Beim letzten Beenden wurden die Originaleinstellungen offenbar nicht\r\n" +
                    "zurückgesetzt (Absturz oder Prozess abgebrochen?).\r\n\r\n" +
                    "Jetzt die gespeicherten Originaleinstellungen wiederherstellen?",
                    "Wiederherstellung", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                    RestoreAll(true);
            }

            // Ausgangszustand JETZT einfrieren (nach evtl. Wiederherstellung)
            _originalMode = DisplayHelper.GetCurrentMode(_deviceName);
            _nv.SnapshotOriginal();

            _startupBackup = new BackupData();
            _startupBackup.DeviceName = _deviceName;
            _startupBackup.Width = _originalMode.Width;
            _startupBackup.Height = _originalMode.Height;
            _startupBackup.Frequency = _originalMode.Frequency;
            _startupBackup.Scaling = _nv.GetOriginalScaling();

            FillResolutionList();

            _lblGpu.Text = string.Format("GPU: {0}   ({1})", _gpuName,
                _nv.IsAvailable ? "NvAPI verbunden" : "NvAPI nicht verfügbar");

            if (!_nv.IsAvailable)
                ShowVendorFallback();

            SystemEvents.SessionEnding += OnSessionEnding;

            UpdateStatus();
            _timer.Start();
        }

        private void DetectGpu()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, AdapterCompatibility FROM Win32_VideoController"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string name = Convert.ToString(mo["Name"]);
                        string vendor = Convert.ToString(mo["AdapterCompatibility"]);
                        if (string.IsNullOrEmpty(name)) continue;
                        _gpuName = name;
                        _gpuVendor = vendor == null ? "" : vendor;
                        if (_gpuVendor.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                            break; // bei Hybrid-Systemen den NVIDIA-Eintrag bevorzugen
                    }
                }
            }
            catch
            {
                // WMI nicht erreichbar – Name bleibt "Unbekannte Grafikkarte"
            }
        }

        private void FillResolutionList()
        {
            _cmbRes.Items.Clear();
            List<DisplayMode> modes = DisplayHelper.GetModes(_deviceName);
            int preferredIndex = -1;
            int fallbackIndex = -1;

            foreach (DisplayMode m in modes)
            {
                // Nur Modi, die schmaler als die native Breite sind (sonst gibt es nichts zu strecken)
                if (m.Width >= _originalMode.Width) continue;
                if (m.Height > _originalMode.Height) continue;

                int index = _cmbRes.Items.Add(m);
                if (m.Width == 2560 && m.Height == 1440) preferredIndex = index;
                if (m.Width == 1920 && m.Height == 1080 && fallbackIndex < 0) fallbackIndex = index;
            }

            if (_cmbRes.Items.Count == 0)
            {
                _chkChangeRes.Checked = false;
                _chkChangeRes.Enabled = false;
                _cmbRes.Enabled = false;
                return;
            }

            if (preferredIndex >= 0) _cmbRes.SelectedIndex = preferredIndex;
            else if (fallbackIndex >= 0) _cmbRes.SelectedIndex = fallbackIndex;
            else _cmbRes.SelectedIndex = 0;
        }

        private void ShowVendorFallback()
        {
            bool isAmd = _gpuVendor.IndexOf("Advanced Micro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         _gpuVendor.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         _gpuVendor.IndexOf("ATI", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isIntel = _gpuVendor.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0;

            string text =
                "NvAPI wurde nicht gefunden (" + _nv.LastError + ")\r\n\r\n" +
                "Die GPU-Skalierung muss deshalb EINMALIG von Hand im Grafiktreiber gesetzt werden.\r\n" +
                "Auflösungswechsel und Zurücksetzen über dieses Tool funktionieren trotzdem –\r\n" +
                "beim Umschalten wird zusätzlich das Windows-Stretch-Flag gesetzt (Best-Effort).\r\n\r\n";

            if (isAmd || !isIntel)
            {
                text +=
                    "AMD (Adrenalin Software):\r\n" +
                    "  1. Rechtsklick auf den Desktop  →  „AMD Software: Adrenalin Edition“\r\n" +
                    "  2. Zahnrad (Einstellungen)  →  Reiter „Anzeige“\r\n" +
                    "  3. „GPU-Skalierung“ = EIN\r\n" +
                    "  4. „Skalierungsmodus“ = „Vollbild“ (Full Panel)\r\n\r\n";
            }
            if (isIntel || !isAmd)
            {
                text +=
                    "Intel (Grafik-Kontrollzentrum / Arc Control):\r\n" +
                    "  1. Intel Grafik-Kontrollzentrum öffnen  →  „Anzeige“\r\n" +
                    "  2. Skalierung auf „Bildschirm strecken“ (Stretch) stellen\r\n";
            }

            _txtVendorInfo.Text = text;
            _grpVendorInfo.Visible = true;
            ClientSize = new Size(640, 712);
        }

        // ------------------------------------------------------------------ Aktionen

        private void OnEnableClick(object sender, EventArgs e)
        {
            var errors = new List<string>();

            // Originalwerte sichern, BEVOR etwas geändert wird (Absturzschutz).
            // Nur wenn noch kein Backup existiert – das älteste ist das echte Original.
            if (!BackupStore.Exists() && _startupBackup != null)
                BackupStore.Save(_startupBackup);

            // 1) GPU-Skalierung: Vollbild (Stretch), auf GPU ausgeführt
            if (_nv.IsAvailable)
            {
                if (!_nv.EnableStretch())
                    errors.Add("GPU-Skalierung konnte nicht gesetzt werden:\r\n" + _nv.LastError);
            }

            // 2) Optional: Desktop-Auflösung auf gewählte 16:9-Auflösung umstellen
            if (_chkChangeRes.Checked && _cmbRes.SelectedItem is DisplayMode)
            {
                var mode = (DisplayMode)_cmbRes.SelectedItem;
                // Bei AMD/Intel zusätzlich das Windows-Stretch-Flag mitgeben
                string err = DisplayHelper.SetMode(_deviceName, mode.Width, mode.Height, mode.Frequency,
                    !_nv.IsAvailable);
                if (err != null)
                    errors.Add("Auflösungswechsel fehlgeschlagen:\r\n" + err);
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(this, string.Join("\r\n\r\n", errors.ToArray()),
                    "Stretch aktivieren – Probleme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            bool anythingWorked = errors.Count == 0 ||
                                  (_nv.IsAvailable && errors.Count < 2);
            if (anythingWorked)
            {
                _stretchActive = true;
                _tray.Text = "Valorant Stretch Helper – AKTIV";
                _tray.BalloonTipTitle = "Stretch aktiv";
                _tray.BalloonTipText = "Jetzt Valorant starten (Vollbild + passende 16:9-Auflösung).";
                _tray.ShowBalloonTip(3000);
            }
            UpdateStatus();
        }

        private void OnRestoreClick(object sender, EventArgs e)
        {
            RestoreAll(true);
            UpdateStatus();
        }

        /// <summary>
        /// Stellt Skalierung und Auflösung wieder her. Quelle ist bevorzugt die
        /// Backup-Datei (überlebt Abstürze), sonst der beim Start gemerkte Zustand.
        /// </summary>
        private void RestoreAll(bool showErrors)
        {
            BackupData data = BackupStore.Load();
            if (data == null) data = _startupBackup;
            if (data == null) return;

            var errors = new List<string>();

            if (_nv.IsAvailable && data.Scaling.Count > 0)
            {
                if (!_nv.RestoreScaling(data.Scaling))
                    errors.Add("GPU-Skalierung: " + _nv.LastError);
            }

            string device = string.IsNullOrEmpty(data.DeviceName) ? _deviceName : data.DeviceName;
            DisplayMode current = DisplayHelper.GetCurrentMode(device);
            if (current.Width != data.Width || current.Height != data.Height ||
                current.Frequency != data.Frequency)
            {
                string err = DisplayHelper.SetMode(device, data.Width, data.Height, data.Frequency, false);
                if (err != null)
                    errors.Add("Auflösung: " + err);
            }

            if (errors.Count == 0)
            {
                BackupStore.Delete();
                _stretchActive = false;
                _tray.Text = "Valorant Stretch Helper – inaktiv";
            }
            else if (showErrors)
            {
                MessageBox.Show(this,
                    "Beim Zurücksetzen gab es Probleme:\r\n\r\n" + string.Join("\r\n", errors.ToArray()),
                    "Zurücksetzen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Wird beim Beenden (und als Sicherheitsnetz aus Main) aufgerufen.</summary>
        public void RestoreOriginalSettingsIfNeeded(bool showErrors)
        {
            if (_restoredAtExit) return;
            _restoredAtExit = true;
            if (_stretchActive || BackupStore.Exists())
                RestoreAll(showErrors);
        }

        // ------------------------------------------------------------------ Status

        private void UpdateStatus()
        {
            DisplayMode current = DisplayHelper.GetCurrentMode(_deviceName);
            _lblRes.Text = string.Format("Desktop-Auflösung: {0} × {1}  @ {2} Hz   (Original: {3} × {4})",
                current.Width, current.Height, current.Frequency,
                _originalMode.Width, _originalMode.Height);

            int scaling = _nv.GetPrimaryScalingValue();
            _lblScaling.Text = "GPU-Skalierung: " +
                (_nv.IsAvailable ? NvidiaScaler.ScalingText(scaling) : "NvAPI nicht verfügbar – siehe Anleitung unten");

            bool scalingActive = scaling == NvidiaScaler.ScalingFullScreenGpu;
            bool active = _stretchActive || scalingActive;

            if (active)
            {
                _lblState.Text = "Stretch: AKTIV – Valorant im Vollbild mit passender 16:9-Auflösung starten";
                _lblState.ForeColor = Color.FromArgb(0, 140, 60);
            }
            else
            {
                _lblState.Text = "Stretch: inaktiv";
                _lblState.ForeColor = Color.DimGray;
            }
        }

        // ------------------------------------------------------------------ Tray & Beenden

        private void OnTrayOpen(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void OnTrayExit(object sender, EventArgs e)
        {
            _reallyExit = true;
            Close();
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            RestoreOriginalSettingsIfNeeded(false);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // "X" minimiert nur in den Tray – beendet wird über das Tray-Menü
            if (e.CloseReason == CloseReason.UserClosing && !_reallyExit)
            {
                e.Cancel = true;
                Hide();
                if (!_trayHintShown)
                {
                    _trayHintShown = true;
                    _tray.BalloonTipTitle = "Läuft im Hintergrund weiter";
                    _tray.BalloonTipText = "Beenden (mit automatischem Zurücksetzen) über Rechtsklick auf das Tray-Icon.";
                    _tray.ShowBalloonTip(3000);
                }
                base.OnFormClosing(e);
                return;
            }

            _timer.Stop();
            RestoreOriginalSettingsIfNeeded(e.CloseReason == CloseReason.UserClosing);
            SystemEvents.SessionEnding -= OnSessionEnding;
            _tray.Visible = false;
            _tray.Dispose();
            base.OnFormClosing(e);
        }
    }
}
