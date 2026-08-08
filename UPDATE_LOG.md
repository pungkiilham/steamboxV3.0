# UPDATE_LOG.md — steamboxV3.0

Update log for tracking changes to steamboxV3.0. Add new entries at the TOP of the log for future work.

> **Version sync rule:** every release must keep these in sync:
> 1. `UPDATE_LOG.md` entry heading version (e.g. `## 2026-08-08 — v3.1.2 — ...`),
> 2. `Form1.cs` constants `app_version` and `changelog_id`,
> 3. `Properties/AssemblyInfo.cs` `AssemblyVersion` / `AssemblyFileVersion`.
> The app shows this on `label17` (short) and in the `richTextBox_status` "Build Info" block (long).

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
