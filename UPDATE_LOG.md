# UPDATE_LOG.md — steamboxV3.0

Update log for tracking changes to steamboxV3.0. Add new entries at the TOP of the log for future work.

> **Version sync rule:** every release must keep these in sync:
> 1. `UPDATE_LOG.md` entry heading version (e.g. `## 2026-08-08 — v3.1.2 — ...`),
> 2. `Form1.cs` constants `app_version` and `changelog_id`,
> 3. `Properties/AssemblyInfo.cs` `AssemblyVersion` / `AssemblyFileVersion`.
> The app shows this on `label17` (short) and in the `richTextBox_status` "Build Info" block (long).

---

## 2026-08-08 — v3.2.0 — 30-steambox HMI layout: 2 racks × 15, top log tabs, shared Run/Stop handler

### 1. Grid now supports all 30 steambox units (`Form1.Designer.cs` + `Form1.cs`)
- **What:** the unit grid was expanded from 15 to **30 rows**, arranged as **2 racks × 15** so all
  units are visible at a glance (HMI-style). Rack A = SB 1–15 (left), Rack B = SB 16–30 (right),
  each with its own RoyalBlue column-header row. Rows are 38 px tall and alternate
  `Lavender` / `(245,245,255)` backgrounds.
- **Why:** the final installation has 30 steamboxes; the old fixed 15-row layout could not show them.
- **How:** all 30 row panels (`tableLayoutPanelSB1..30`) and their `lbl_*1..30` / `btn_*1..30`
  controls are declared in the Designer; `InitializeUIArrays(30)` and `sbmax = 31` pick them up so
  polling, scanning, MQTT publish, and UI updates all cover IDs 1–30.
- **Form size:** `ClientSize` is **1920 × 964** (same height as the pre-3.2.0 build) so it fits a
  24"/1080p monitor; the whole 30-unit grid is always visible without scrolling.

### 2. Log windows moved to a top TabControl (`Form1.Designer.cs`)
- **What:** the 4 logs now live in a `TabControl` positioned at the **top-right** (900 × 210 at
  `(924, 14)`, above Rack B / SB 16–30) with tabs "Sistem Status" (`richTextBox_status`),
  "System Error" (`richTextBox1`), "MQTT Traffic" (`richTextBox2`), "Action" (`richTextBox3`),
  and "Heartbeat" (`richTextBox4`). The app title (`label15`), version (`label17`), and Scan
  button (`btn_scanSb`) sit in the **top-left** (above Rack A / SB 1–15). The old right-side
  labels (`label10/11/12/14`) were removed and the logs are `ReadOnly`.
- **Why:** keeps the app title and the log tabs in the header band while giving the full form
  width/height to the 30-unit grid (top row = title left + tabs right; bottom row = SB 1–15 and
  SB 16–30 side by side), still short enough for 1080p.

### 3. Removed debug controls (`Form1.Designer.cs` + `Form1.cs`)
- **What:** deleted `textBox1`, `button1` (MQTT publish test), `button2` (AL1H writer),
  `splitter1`, and their `button1_Click` / `button2_Click` handlers.
- **Why:** dev leftovers that clutter a production HMI screen.

### 4. Shared Run/Stop handler (`Form1.Designer.cs` + `Form1.cs`)
- **What:** all 30 `btn_statusN` buttons now wire to a single `btn_status_Click` handler that reads
  the unit id from the button `Tag`. The 15 per-unit `btn_status1..15_Click` handlers were removed.
- **Why:** same behavior, no per-unit boilerplate for the doubled row count.

### 5. Form width trimmed to fit content (`Form1.Designer.cs`)
- **What:** `ClientSize` width reduced from **1920 → 1836** px. Content (title/tab, racks, headers)
  ends at X = 1824, so the ~96 px dead strip on the right edge is gone.
- **Why:** unused empty space on a fixed-size HMI screen.

### 6. Heartbeat log capped to the last 10 rows (`Form1.cs`)
- **What:** new `hb_append()` helper keeps the "Heartbeat Log:" header plus at most the **last 10**
  data lines. New entries push the oldest off the top (ring-buffer style). All heartbeat writes
  (`[SCAN]`, `[TICK]`, MQTT run/stop blocks) now go through it.
- **Why:** previously the log could grow until a 5000-char reset; a fixed 10-row window keeps the
  heartbeat tab readable at a glance.

### 7. Faster steambox rescan (`Form1.cs` + `App.config`)
- **What:** `scan_sb()` probe speed-up:
  - read timeout lowered to `scan_timeout` (new App.config key, default **100 ms**) during the scan,
    restored to 250 ms afterwards (in `try/finally`);
  - per-ID fixed delay cut `Sleep(50)` → `Sleep(10)` (Modbus RTU only needs ~4 ms gap at 9600 baud);
  - **absent** units (no answer → `System.TimeoutException`) no longer trigger a disconnect/reconnect
    — the line is quiet, nothing to clear — so an inactive unit costs ~110 ms instead of ~460 ms;
  - the disconnect+`Sleep(20)`+reconnect bus reset is kept only for real bus errors
    (CRC/garbage/port-not-open);
  - the whole loop now holds `modbusLock` once (run/stop buttons are briefly unresponsive during a
    scan — a startup/button action).
- **Why:** a typical mixed rescan took ~8 s and an all-off scan ~13–15 s; now ~2–3 s / ~3.5 s.

### 8. Version bump
- `app_version` / `changelog_id` (`Form1.cs`), `AssemblyVersion` / `AssemblyFileVersion`
  (`Properties/AssemblyInfo.cs`), and `UPDATE_LOG.md` heading bumped to **3.2.0**.

---

## 2026-08-08 — v3.1.2 — FIX: stale resep/durasi + MQTT robustness + version info

### 5. Version / build info on the UI (`Form1.cs` + `Properties/AssemblyInfo.cs` + this file)
- **What:** `label17` ("Versi 3.1.2" under "Aplikasi Bridge Steambox - Server") now shows
  `Versi {app_version} · Build {exe write time}` (e.g. `Versi 3.1.2 · Build 08/08/2026 14:30`).
  The `richTextBox_status` window shows a longer "Build Info" block: Versi, Changelog date,
  Build Time, and Assembly version.
- **Why:** so the client admin can identify which build is running and look it up in this log.
- **Maintenance:** build time is read from the exe file's write date automatically (no manual step).
  Bump `app_version` / `changelog_id` (Form1.cs) + AssemblyInfo versions per release — see the
  version sync rule at the top.

### 1. Resep & Durasi no longer show the previous batch after stop (`Form1.cs`)
- **Problem:** After a steambox was stopped (GUI button, MQTT command, or force-stop), the
  `lbl_resep` and `lbl_durasi` labels kept showing the recipe/duration of the last batch
  ("resep and durasi filled from previously").
- **Fix:** `stop(byte id)` now clears `mq_resep[id] = "-"` and `mq_durasi[id] = 0`.
  Because every stop path (GUI, MQTT, `pemasakan_off`) routes through `stop()`, all of them reset.
- **Deliberate behavior kept:** the *elapsed cooking time* (pemasakan) label is NOT cleared on stop;
  it is reset on the next start inside `run(id)`.
- **Also fixed** a small log bug in `run_stop()`'s stop branch that printed the recipe name as
  "Durasi". It now logs the real resep and durasi, captured *before* `stop()` clears them.

### 2. MQTT publish now sends exactly IDs 1–15 (`Form1.cs` `mqtt_pub()`)
- **Problem:** the publish loop iterated `1..30` (`data_pub.Length` = 31) and published 30 units,
  even though the site only has up to 15 steamboxes.
- **Fix:** loop and comma logic now use `sbmax` (1..15), matching the actual steambox count.
  Note: `data_pub` / `run_pub` / `temp_pub` / `com_pub` arrays keep size 31 (unchanged).

### 3. MQTT auto-reconnect (`Form1.cs` `bacaconfig()` + new `MqttClient_ConnectionClosed`)
- **Problem:** the app connected to MQTT only once at startup. If the broker dropped the session
  (PC restart, network blip), it never reconnected and publishing silently stopped.
- **Fix:** subscribe to `mqClient.ConnectionClosed`. On loss, create a fresh `MqttClient`
  (a closed M2Mqtt client cannot be reused), reconnect with a 3 s backoff (up to 5 attempts),
  and re-subscribe `sb/req`. A `isMqttReconnecting` flag prevents concurrent reconnects.

### 4. Configurable MQTT client ID (`Form1.cs` + `App.config`)
- **Fix:** new optional `mqtt_clientid` key. If empty/missing it falls back to the legacy `id`,
  so existing deployments are unchanged. Give each PC a unique ID so multiple machines on the
  same broker don't kick each other off (they previously all used `steamboxV3.0`).

### Important note for the client-PC MQTT issue
The JSON observed in MQTT Explorer after a client-PC restart contained fields this app **never
sends**: `network_state` and `lastUpdate`. The desktop app only publishes
`[{"id":1,"run":0,"temp":25.3,"com":1},...]` on `sb/data`. That `network_state`/`lastUpdate`
payload is produced by the **web backend / another MQTT client**, not by this app. When debugging
the client PC, check MQTT Explorer on topic `sb/data` specifically, and confirm whether another
instance of the app (e.g. an old copy on another machine) is also publishing to the same broker
with the same client ID.

---

## Template for future entries

```md
## YYYY-MM-DD — SHORT_TITLE

### Change heading (`file`)
- **Problem:** what was wrong / why the change was needed.
- **Fix:** what was changed and where.
- **Testing:** how it was verified (build, runtime, device/backend).
```
