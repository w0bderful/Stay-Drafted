# Stay Drafted

RimWorld 1.7 · Version **3.1.0** · Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).

Use the normal **Draft** command and your player-controlled pawns stay drafted. No second toggle is required. Manual undrafting and forced undrafting remain available; only vanilla's idle automatic undraft decision is blocked.

## Optional button

The extra button is **hidden by default**. In **Options → Mod settings → Stay Drafted**, you can enable an **Allow auto-undraft** button. It uses a cancel icon rather than the Draft icon and defaults to **off** for each pawn. Turning it on delegates idle undrafting to vanilla for that pawn; it does not immediately undraft them. Hiding the button does not erase saved per-pawn opt-outs.

There is no separate hotkey that must be pressed to keep a pawn drafted.

## Long-draft warning

A persistent alert appears when a humanlike player pawn has remained continuously drafted for **24 in-game hours**. The threshold is configurable from 1 to 168 hours.

- The timer follows game ticks, not real time: 24 hours is 60,000 ticks.
- Manual or forced undrafting clears the timer and alert immediately.
- Drafting again starts a fresh timer.
- Draft start times and per-pawn opt-outs are saved.
- Drafted pawns in an older save without a recorded start time begin their warning timer when first observed; earlier time cannot be reconstructed.
- The warning targets spawned humanlike player pawns. Colony mechs are excluded from this warning.
- The alert can take up to 600 ticks (14.4 in-game minutes) to appear after reaching the threshold. It does not send repeated letters.

## Installation and compatibility

Place this repository's contents in a mod folder under `RimWorld/Mods`, enable it after Harmony, and restart the game. The prebuilt DLL is included at `1.7/Assemblies/HoldDraft.dll`.

The display name has changed from **Hold Draft** to **Stay Drafted**. The package ID `w0bderful.holddraft`, assembly name, and save namespaces are retained so existing load orders continue to find the mod. Install only one copy. The former `GM_holdDraft` preference is superseded by the new default behavior; the new optional auto-undraft opt-out defaults to off.

## Performance and validation

Draft, spawn, faction-change, and load events maintain a per-game set of drafted player pawns. After one startup/load scan, the monitor checks **only that set once every 600 ticks**. Alert report queries reuse the cached culprit list. It does not scan every pawn each tick or each time the alert UI requests a report.

External checks run against the installed RimWorld runtime and actual Harmony targets. They cover default behavior, opt-outs, the exact 24-hour boundary, reset on undrafting, redrafting, registration limits, and steady-state allocations. Synthetic performance measurements are recorded in `Tests/performance-result.json`.

On the development machine, a scan of 10 tracked pawn fixtures averaged approximately **0.00043 ms**; a stress scan of 10,001 tracked fixtures averaged approximately **0.21 ms**. The tested idle tick, cached-report, and warmed scan loops allocated **0 additional managed bytes**. Initial registration, save/load, and tooltip text can allocate. These microbenchmarks are not an FPS/TPS benchmark of a running colony.

An isolated actual-game run also passed **14 checks** with no game error entries: native drafting, the optional icon and opt-out, alert activation, actual save/load, immediate manual clearing, redrafting, and the live tick monitor. The 24-hour boundary was tested by advancing the game clock rather than waiting through a full day of simulation. This run used Core, Harmony, Stay Drafted, and a local test harness; it does not validate every external mod combination.

## Building and external tests

Requires the .NET 10 SDK. The test runner also requires `uv` and the installed game's runtime.

```powershell
./Source/build.ps1 -GameDir 'D:/SteamLibrary/steamapps/common/RimWorld' -WorkshopDir 'D:/SteamLibrary/steamapps/workshop/content/294100'
./Tests/test.ps1 -GameDir 'D:/SteamLibrary/steamapps/common/RimWorld' -WorkshopDir 'D:/SteamLibrary/steamapps/workshop/content/294100'
```

Game and Harmony binaries are referenced from those installations and are not distributed here.
