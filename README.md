# Chaos Interactions

sense it's just a silly lil thing; I used a bunch'a copilot assistance to speed it's creation up: I claim ownership of nothing its just a toy I made for homies

Chaos Interactions is a Windows desktop utility that inverts WASD input at the user level.

When enabled, it swaps:

- W with S
- A with D

The app uses a global low-level keyboard hook plus synthetic input injection, so it does not require a kernel driver.

## Features

- Tray-friendly UI
- Separate toggles for WASD inversion, system audio mute, and blur filter overlay
- `Ctrl+Shift+I` hotkey for inversion, `Ctrl+Shift+M` for mute, and `Ctrl+Shift+B` for blur
- Blur strength slider for live adjustment of the overlay intensity
- Checkbox to scramble WASD outputs instead of using a straight inversion
- User-mode input remapping for W, A, S, and D

## Requirements

- Windows 10 or newer
- .NET 8 SDK

## Build

```powershell
dotnet build
```

## Run

```powershell
dotnet run
```

## Notes

This approach works by intercepting the keyboard hook and re-emitting the opposite key. Some games that use specialized input paths may still behave differently, but no kernel-level driver is required for the app to operate.

## Mix It Up Integration

Chaos Interactions exposes a local HTTP command API on `http://127.0.0.1:28914/` so Mix It Up can trigger actions without interacting with the UI.

In the commandImports folder are a couple of the command lists you can directly import into mixitup to add to your redeems for functionality if youre lazy:

Blinded is for your blurring redeem
Deaf is for your audio removing redeem
Vertigo is for your WASD altering redeem 

All commands are set to be de-activated after 69sec (heh nice)

Available endpoints:

- `GET /status`
- `GET` or `POST /toggle/invert`
- `GET` or `POST /toggle/mute`
- `GET` or `POST /toggle/blur`
- `GET` or `POST /mode/scramble?enabled=true|false`
- `GET` or `POST /blur/strength?value=0-100`

Mix It Up can call these as a web request or browser/open-URL style action pointing at the localhost endpoint.

Example commands:

- `http://127.0.0.1:28914/toggle/invert`
- `http://127.0.0.1:28914/toggle/mute`
- `http://127.0.0.1:28914/toggle/blur`
- `http://127.0.0.1:28914/mode/scramble?enabled=true`