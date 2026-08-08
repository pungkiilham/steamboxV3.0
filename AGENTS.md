# AGENTS.md — steamboxV3.0 (VIBE SANDBOX)

Context file for AI agents working on this repository.

## Repo Roles & Workflow (IMPORTANT)

This folder (`steamboxV3.0-vibe`) is the **VIBE SANDBOX** — an independent git clone of the original
project, checked out on its own **`vibe-coding` branch** that exists only on GitHub (pushed as a
separate branch; the original `master` is untouched).

- **Original:** `D:\Project lain-lain\Project MVP-Demo\CSharp\steamboxV3.0` — branch `master`, the
  released app. Changes here are treated as production.
- **Vibe (this folder):** `D:\Project lain-lain\Project MVP-Demo\CSharp\steamboxV3.0-vibe` — branch
  `vibe-coding`. Use this folder to prototype / experiment freely. Nothing here touches `master`.
- **Workflow:** do all experimental work in THIS vibe folder on `vibe-coding`. When an experiment is
  approved, promote the specific change to the original repo (`git cherry-pick` / manual apply) and
  bump the version there. Never rewrite the original's history.
- **Sync point:** both repos currently sit at the same commit `b6d053a` (v3.1.2). `origin` for this
  clone points at the same GitHub repo (`pungkiilham/steamboxV3.0`), so `git push origin vibe-coding`
  keeps the sandbox branch on GitHub.
- **README/warnings:** all project facts below describe the shared codebase and apply to both repos
  unless noted. Build time is read from the exe write timestamp, so the sandbox build shows its own
  build time — this is expected.

## Project Overview

**steamboxV3.0** is a Windows Forms desktop application that monitors and controls up to **15 steambox units** in an industrial steam-cooking setup. Each unit is driven by an **Autonics TK4M** PID temperature controller, which the app talks to over **Modbus RTU** (RS485 serial). The app reports live status to a cloud server over **MQTT** and accepts run/stop commands from a web backend over the same MQTT channel.

The GUI shows one row panel per steambox (labels + status lamps + run/stop buttons) plus 4 scrolling log windows (system errors, MQTT traffic, actions, heartbeat) and a status readout.

## Tech Stack

- **Language / Runtime:** C#, .NET Framework **4.7.2**, C# language version **7.3** (`/langversion:7.3`)
- **UI:** Windows Forms (WinForms), single-form app
- **Build:** MSBuild via Visual Studio 2022 (see `build_log.txt`), `Debug|AnyCPU` / `Release|AnyCPU`
- **Target:** WinExe → `bin\Debug\steamboxV3.0.exe` (`.exe.config` copied from `App.config`)

### NuGet Packages (see `packages.config`)
| Package | Version | Purpose |
|---|---|---|
| EasyModbusTCP | 5.6.0 | Modbus RTU/TCP client over serial (`EasyModbus.ModbusClient`) |
| M2Mqtt | 4.3.0.0 | MQTT client (`uPLibrary.Networking.M2Mqtt`) |
| Newtonsoft.Json | 13.0.1 | Referenced but JSON is built/parsed manually as strings |
| JsonConverter.Abstractions | 0.7.1 | Transitive dependency |

## File Map

| File | Role |
|---|---|
| `Program.cs` | Entry point; runs `Form1` |
| `Form1.cs` | **All business logic (~1087 lines)**: config load, Modbus polling, MQTT pub/sub, cooking timers, run/stop, UI updates |
| `Form1.Designer.cs` | Designer-generated UI: 15 unit panels, header, 1 TextBox, 5 RichTextBox logs, 1 Timer |
| `Form1.resx` | Form resources (icons, etc.) |
| `App.config` | All runtime configuration (appSettings) |
| `Properties/` | `AssemblyInfo.cs`, settings/resources designers |
| `steamboxv3.0_original.txt` | **Reference copy of the old `Form1.cs` (2229 lines)** — used to compare/revert logic |
| `build_log.txt` | Captured MSBuild output from a successful Debug build |
| `UPDATE_LOG.md` | Dated change log — add new entries at the top |
| `packages.config` | NuGet package manifest (packages restored into `packages/`) |

## Configuration (`App.config` → `ConfigurationManager.AppSettings`)

| Key | Default | Meaning |
|---|---|---|
| `comport` | `COM17` | Modbus serial port |
| `baudrate` | `9600` | Serial baud rate |
| `timer_tick` | `100` | Poll timer interval (ms) |
| `timeout` | `50` | (loaded but unused in polling) |
| `ip` / `port` / `id` / `user` / `pass` | `103.175.220.42` / `1884` / `steamboxV3.0` / `sbMQTT` / `sbMQTT1234` | MQTT broker + credentials. **Note: `port` config key is unused — port is hardcoded to `1884` in `bacaconfig()`. Credentials are plaintext.** |
| `mqtt_clientid` | `steamboxV3.0` | MQTT client ID used for `Connect()`. Falls back to `id` if empty/missing. Give each PC a **unique** value so multiple instances on the same broker don't kick each other off. |
| `start_pemasakan` | `1000` | PV threshold (x0.1 °C, i.e. 100.0 °C) at which cooking timer starts |
| `selisih_pemasakan` | `1` | Allowed overtime (minutes) before forced stop |
| `sv_on` | `900` | SV setpoint (x0.1, i.e. 90.0 °C) written on run |
| `alarm_on` | `-10` | AL1H setpoint (x0.1, i.e. -1.0) written when running |
| `alarm_off` | `500` | AL1H setpoint (x0.1, i.e. 50.0) written when stopped |
| `active_ids` | `1,2,3` | Present but **unused** (device detection is done by live scanning instead) |

## Modbus Register Map

Client config: `COM17`, `9600` baud, `StopBits.Two`, `Parity.None`, `ConnectionTimeout=250`. Unit identifier = steambox ID (1–15). All Modbus access is serialized with `modbusLock`.

### Read (per unit ID)
| Register | Type | Meaning |
|---|---|---|
| `50` (holding, read 4) | `status_flag` | 0 = stop, 1 = run |
| `1000` (input) | `pv_val` | Process value / temperature (x0.1 °C) |
| `1003` (input) | `sv_val` | Set value (x0.1 °C) |
| `3` (discrete input) | `out1_flag` | Output 1 state |
| `9` (discrete input) | `alarm1_flag` | Alarm 1 state |
| `54` (holding, read 4) | `al1h_val` | AL1H alarm high setpoint (x0.1) |

### Write (per unit ID)
| Register | Value | Meaning |
|---|---|---|
| `addr_run = 50` | `0` = run, `1` = stop | Run/stop command |
| `addr_sv = 0` | `val_sv` (from `sv_on`) | SV setpoint |
| `addr_alarm = 54` | `val_alarmOn` / `val_alarmOff` | AL1H alarm setpoint |

`cek_al1h()` re-writes SV and AL1H each poll if the device value drifted from configured values.

## MQTT Protocol

- **Publish topic:** `sb/data`
- **Subscribe topic:** `sb/req`
- Client: `new MqttClient(ip, 1884, false, null, null, MqttSslProtocols.TLSv1_2)`, connect with `mqtt_clientid`/`user`/`pass` (falls back to `id`).
- **Auto-reconnect:** `mqClient.ConnectionClosed` → `MqttClient_ConnectionClosed` creates a fresh client, reconnects (3 s backoff, up to 5 attempts), and re-subscribes `sb/req`. A closed M2Mqtt client cannot be reused, so a new one is built on each attempt.

### Publish payload (`mqtt_pub()`), sent on every tick
```json
[{"id":1,"run":0,"temp":25.3,"com":1},{"id":2,"run":1,"temp":90.0,"com":1}, ...]
```
Fields: `id` = unit, `run` = running(0)/stopped(1), `temp` = PV (x0.1 → decimal), `com` = `sb_aktif` (1 = present).
**Publish loop is bounded to IDs `1..15` (`sbmax`)** — it no longer emits units 16–30.

### Subscribe payload (`sb/req`), parsed by `MQClient_MqttMsgPublishReceived`
```json
{"id":1,"resep":"RECIPE_NAME","durasi":45,"run":1}
```
Parsed by splitting on `: , }`. `resep` truncated to 11 chars; `durasi` is **minutes**, converted to seconds (`*60`) into `mq_durasi`. Sets `mq_flag[id]=1` so the next poll applies it via `mqrun_stop(id)`.

## Key Logic Flows

### Startup (`Form1` ctor)
`InitializeComponent()` → `bacaconfig()` (load config + connect MQTT) → `bacaPort()` (open serial, connect Modbus, start timer, `scan_sb()`) → `mqtt_sub()`.

### Poll loop (`timer1_Tick`, every 100 ms)
1. Skips if `isProcessing || isScanning` to avoid overlap.
2. Offloads to `Task.Run`: for each ID `1..15` with `sb_aktif==1`, calls `ProcessSteambox(id)` (which calls `readval_single(id)`), 50 ms gap between units.
3. After the cycle, `mqtt_pub()`.
4. Finally clears `isProcessing`.

### Unit detection (`scan_sb`)
Reads holding register `50` (4 regs) for IDs `1..15`; on success marks `sb_aktif[i]=1`, on failure marks `sb_aktif[i]=0` and **hard-resets the Modbus connection** (disconnect, sleep, reconnect) to clear bus noise. Triggered at startup and via `btn_scanSb`.

### Per-unit processing (`ProcessSteambox(id)` / `readval_single(id)`)
- Reads status, PV, SV, OUT1, ALARM, AL1H.
- On exception: marks `sb_connected[id]=false`, **never sets `sb_aktif[id]=0`** (keeps retrying), and bounces the Modbus connection.
- Applies pending MQTT command (`mq_flag` → `mqrun_stop`).
- Cooking timer: if `pv_val >= start_pemasakan` and status==stop → `mulai_pemasakan(id)` records `start_time_pemasakan[id]`, then `update_timer_pemasakan(id)` on subsequent ticks.
- `pemasakan_off(id)` forces a stop if elapsed > `mq_durasi + selisih_pemasakan*60`.
- Updates UI via `this.Invoke(...)`: recipe, duration (+ estimated stop time), SV, PV, AL1H, pemasakan time, connection/status button colors.

### Run/Stop (`run_stop()`, `run(id)`, `stop(id)`)
- Button handlers (`btn_status1..15`) set `id_sb` and call `run_stop()`.
- `run_stop()` reads status; if stopped → `run(id)` (writes run=0, SV, AL1H-on) and resets cooking timer; if running → `stop(id)` (writes run=1, SV, AL1H-off).
- `mqrun_stop(id)` applies the same via MQTT commands.
- **`stop(id)` clears the recipe/duration (`mq_resep[id]="-"`, `mq_durasi[id]=0`)** so labels never show a previous batch. The elapsed cooking-time label is intentionally kept on stop and reset on the next `run(id)`.
- `UPDATE_LOG.md` records dated change entries — add new entries at the top.

## Versioning

- The app shows a version label (`label17`, under the "Aplikasi Bridge Steambox - Server" title) as `Versi {app_version} · Build {exe write time}`.
- The `richTextBox_status` ("Sistem Status") window prints a longer **Build Info** block: Versi, Changelog date, Build Time, Assembly version.
- **Single source of truth:** `Form1.cs` constants `app_version` / `changelog_id`, the top `UPDATE_LOG.md` entry heading, and `Properties/AssemblyInfo.cs` (`AssemblyVersion`/`AssemblyFileVersion`) must all be bumped together per release. Build time is read from the exe file's write timestamp at runtime (no manual maintenance).

## Build & Run

- Open `steamboxV3.0.sln` in Visual Studio 2022 (Community) and build, or:
  ```
  msbuild steamboxV3.0.sln /p:Configuration=Debug
  ```
- Output: `bin\Debug\steamboxV3.0.exe` + `steamboxV3.0.exe.config` (from `App.config`).
- Requires the device (or a serial/Modbus simulator) on the configured COM port; MQTT broker must be reachable, otherwise only a "MQTT_Server Not Detected" status line is shown.
- `msbuild` is at `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` on the dev machine (see `build_log.txt`).

## Conventions & Gotchas

- **Threading:** All Modbus I/O must hold `modbusLock` (shared with UI-click handlers). Polling runs on background threads; **every UI touch goes through `this.Invoke(...)`**. Never block the UI thread.
- **Indonesian domain terms** are used throughout the code: `pemasakan` = cooking, `durasi` = duration, `resep` = recipe, `selisih` = difference, `aktif` = active, `baca` = read.
- **Unit arrays are size 31** (indexes 0–30); IDs 1–15 are used. `sbmax = 16` bounds the active loop (1..15).
- **`sb_aktif` is detection state; `sb_connected` is per-poll success state.** They must not be conflated.
- Temperatures are stored as integers in tenths of a degree (900 = 90.0 °C) and divided by 10 only at display/publish time.
- JSON is built/parsed **manually with string splitting** — brittle to format changes; improve with `Newtonsoft.Json` if touched.
- Known warnings: unused fields `tim`, `tim1`, `tim2`, `next`; unused variables in `bacaPort`/`button2_Click`; unreachable catch clauses in MQTT handler.
- Hardcoded values to be careful with: MQTT port `1884`, TLS `TLSv1_2`, `ConnectionTimeout=250`, per-read `Sleep(20)`, poll gap `Sleep(50)`.
- `steamboxv3.0_original.txt` holds the previous implementation — when changing behavior, diff against it to preserve intended semantics.
- Do NOT commit MQTT credentials; they are currently plaintext in `App.config`.
