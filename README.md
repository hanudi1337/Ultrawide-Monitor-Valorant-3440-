# Valorant Ultrawide Stretch Helper

Streckt das 16:9-Bild von Valorant per **GPU-Skalierung** auf die volle Breite eines
3440 × 1440 (21:9) Ultrawide-Monitors – ohne schwarze Balken.

**Vanguard-sicher:** Das Tool ändert ausschließlich Treiber-/Windows-Anzeigeeinstellungen
(dieselben, die man auch in der NVIDIA-Systemsteuerung setzen könnte). Es fasst weder
Spieldateien noch den Valorant-Prozess an. Es ist reines Bild-Strecken – **kein FOV-Vorteil**.

---

## Starten

Fertig gebaut liegt das Tool hier:

```
bin\ValorantStretchHelper.exe
```

Einfach doppelklicken (keine Adminrechte, keine Installation nötig).
Wichtig: `NvAPIWrapper.dll` muss im selben Ordner wie die EXE liegen (ist sie bereits).

### Neu bauen (optional)

`build.bat` doppelklicken. Es wird nur der C#-Compiler des in Windows enthaltenen
.NET Framework 4.x benutzt – es muss nichts installiert werden.

---

## Nutzung

1. **„Stretch aktivieren“** klicken. Das Tool
   - setzt die GPU-Skalierung des primären Monitors per NvAPI auf
     **„Vollbild“ (Skalierung auf GPU ausführen)** – das ist der Stretch-Modus, und
   - stellt (empfohlen, abschaltbar) die Desktop-Auflösung auf die gewählte
     16:9-Auflösung um (Standard: 2560 × 1440).
2. **Valorant starten** und in den Videoeinstellungen prüfen:
   - **Anzeigemodus: „Vollbild“** (NICHT „Randlos (Fenster)“ – sonst greift die GPU-Skalierung nicht!)
   - **Auflösung: dieselbe 16:9-Auflösung**, die im Tool gewählt ist (z. B. 2560 × 1440).
3. Nach dem Spielen: **„Zurücksetzen“** klicken – oder das Tool einfach über das
   Tray-Menü beenden: Beim Beenden werden Skalierung **und** Auflösung automatisch
   auf die Ausgangswerte zurückgestellt.

Das „X“ am Fenster minimiert nur in den Infobereich (Tray). Beenden geht über
**Rechtsklick auf das Tray-Icon → „Beenden (und zurücksetzen)“**.

---

## Robustheit / Absturzschutz

- Beim Aktivieren werden die Originalwerte zusätzlich in
  `%APPDATA%\ValorantStretchHelper\original_settings.txt` gesichert.
  Wurde das Tool nicht sauber beendet (Absturz, Prozess abgeschossen), bietet es beim
  nächsten Start automatisch an, die Originaleinstellungen wiederherzustellen.
- Der Auflösungswechsel erfolgt dynamisch (nicht in die Registry geschrieben) –
  spätestens ein Neustart stellt die native Auflösung ohnehin wieder her.
- Ohne NVIDIA-Treiber/NvAPI stürzt nichts ab: Das Tool zeigt eine verständliche Meldung
  plus eine Schritt-für-Schritt-Anleitung, wie man die GPU-Skalierung bei
  **AMD (Adrenalin)** bzw. **Intel** einmalig von Hand setzt. Auflösungswechsel und
  Zurücksetzen funktionieren dann trotzdem (Windows-API); beim Umschalten wird
  zusätzlich das Windows-Stretch-Flag (`DMDFO_STRETCH`) gesetzt.

---

## Troubleshooting

| Problem | Lösung |
|---|---|
| Bild bleibt mit Balken | In Valorant „Vollbild“ (nicht „Randlos“) und die 16:9-Auflösung aus dem Tool wählen; einmal Alt+Tab oder Neustart des Spiels. |
| Stretch nur im Spiel, Desktop soll nativ bleiben | Haken „Desktop-Auflösung mit umstellen“ entfernen. Dann ggf. einmalig in der NVIDIA-Systemsteuerung → „Desktop-Größe und -Position anpassen“ → „Skalierungsmodus von Spielen und Programmen überschreiben“ anhaken. |
| „NvAPI nicht verfügbar“ trotz NVIDIA-Karte | NVIDIA-Treiber (nicht nur Windows-Basistreiber) installieren; `NvAPIWrapper.dll` muss neben der EXE liegen. |
| Desktop blieb nach Absturz gestreckt | Tool erneut starten → Wiederherstellungs-Abfrage mit „Ja“ beantworten. |

---

## Technik

- C# / .NET Framework 4.8, WinForms (`src\`)
- NVIDIA: [NvAPIWrapper.Net 0.8.1](https://github.com/falahati/NvAPIWrapper) –
  `PathInfo.SetDisplaysConfig` mit `Scaling.ToNative` (= `NV_SCALING_GPU_SCALING_TO_NATIVE`,
  „Vollbild, auf GPU ausführen“) auf dem GDI-primären Display
- Auflösung: `EnumDisplaySettingsW` / `ChangeDisplaySettingsExW` (user32)
- GPU-Erkennung: WMI (`Win32_VideoController`) + NvAPI-Probe
