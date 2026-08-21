# WECPBXR18.Console

Diagnostic console application for checking MIDI controller input and OSC communication with Behringer XR18 before the WPF UI is ready.

## Run

From the repository root:

```powershell
dotnet run --project WECPBXR18.Console
```

The app starts without connecting to XR18 or MIDI devices and prints the command list.

## Mixer Connection

Connect to XR18 by address:

```text
mixer connect 192.168.1.100
```

Or ask for the address interactively:

```text
mixer connect
```

Then type an address or run discovery:

```text
XR18 address or scan: scan
```

If mixers are found, select one by number.

## Commands

```text
mixer connect [address]
```

Connects to XR18. Without an address, the app asks for one in the console.

```text
mixer disconnect
```

Disconnects the current XR18 connection.

```text
mixer status
```

Prints current XR18 connection status.

```text
mixer scan
```

Scans active local IPv4 `/24` subnet(s), sends `/xinfo` to UDP port `10024`, waits for responses, and prints discovered devices.

```text
scan
```

Same as `mixer scan`.

After mixer connection, incoming OSC messages are printed to the console.

```text
mute <1-18>
```

Mutes the selected XR18 input channel.

Example:

```text
mute 1
```

```text
unmute <1-18>
```

Unmutes the selected XR18 input channel.

Example:

```text
unmute 1
```

```text
midi list
```

Prints available MIDI input devices.

```text
midi connect <index>
```

Connects to a MIDI input device and starts printing incoming MIDI events.

Example:

```text
midi connect 0
```

```text
midi disconnect
```

Disconnects the current MIDI input device.

```text
midi status
```

Prints current MIDI connection status.

```text
midi help
```

Prints MIDI commands.

```text
bank status
```

Prints current Core bank state. Each bank contains:

- 24 knobs
- 9 faders
- 16 assignable buttons
- 2 navigation buttons outside the assignable control array: Bank Previous and Bank Next

For every assignable control, the console prints controller value, mixer value, and soft-takeover lock state.

```text
bank list
```

Prints all 7 banks. Default bank colors follow the rainbow order:

- Red
- Orange
- Yellow
- Green
- Cyan
- Blue
- Violet

```text
bank next
```

Switches to the next bank. This is the software equivalent of `bankR`.

```text
bank prev
```

Switches to the previous bank. This is the software equivalent of `bankL`.

```text
bank select <1-7>
```

Selects a bank by number.

Example:

```text
bank select 3
```

```text
bank rename <name>
```

Renames the current bank. UI labels should use the current bank name and current bank control labels.

Example:

```text
bank rename Vocals
```

```text
bank color <r> <g> <b>
```

Changes the current bank RGB color in Core.

Example:

```text
bank color 255 0 0
```

```text
map save [path]
```

Saves the current MIDI/OSC map to JSON. Default path:

```text
WECPBXR18.Console/midi-map.json
```

```text
map load [path]
```

Loads MIDI/OSC map from JSON and applies it to the current Core bank set.

```text
map list
```

Prints all assignable slots in the current bank with MIDI and mixer bindings.

```text
map show <slotId>
```

Prints one slot binding.

Example:

```text
map show fader-01
```

```text
map set label <slotId> <label>
```

Renames a slot in the current bank.

Example:

```text
map set label fader-01 Vocal 1
```

```text
map set midi <slotId> <cc|note|noteoff|pitch> <channel> <number>
```

Assigns MIDI input to a slot in the current bank.

Example:

```text
map set midi fader-01 cc 1 25
```

```text
map set osc <slotId> <oscAddress> [level|toggle|pan]
```

Assigns an exact OSC address.

Example:

```text
map set osc fader-01 /ch/01/mix/fader level
```

```text
map set command <slotId> <main|mute|pan|bus|aux|fx|bus-on|fx-on> <channel> [index]
```

Assigns a predefined XR18 command.

Examples:

```text
map set command fader-01 main 1
map set command knob-01 bus 1 1
map set command knob-02 aux 1 2
map set command knob-03 fx 1 1
map set command button-01 mute 1
map set command button-02 bus-on 1 1
```

```text
map clear midi <slotId>
map clear osc <slotId>
```

Clears MIDI or mixer binding.

```text
map commands
```

Prints the predefined XR18 command catalog.

```text
help
```

Prints the command list.

```text
exit
```

Stops the diagnostics app.

Aliases:

```text
m      same as mute
u      same as unmute
s      same as scan
h      same as help
q      same as exit
quit   same as exit
```

## Notes

- XR18 OSC uses UDP port `10024`.
- The app sends `/xremote` when connected and repeats it every 5 seconds.
- MIDI diagnostics use `Melanchall.DryWetMidi`.
- MIDI input events are printed as raw DryWetMIDI event text. Internally the hardware layer also normalizes Control Change, Note On, Note Off, and Pitch Bend events for future mapping logic.
- Core currently uses a provisional default MIDI map: knobs and faders are sequential Control Change values, assignable buttons are sequential Note values. Real Worlde mappings should be adjusted after checking `midi connect <index>` logs.
- Core stores an RGB color per bank. Sending that color to the physical Easycontrol bank buttons is not implemented yet because the controller-specific MIDI/SysEx protocol for button RGB is still unknown.
- XR18 command catalog currently includes channel main fader, mute, pan, bus/aux send level, FX send level, bus send on/off, and FX send on/off.
- On XR18/X-Air, AUX outputs are normally fed by bus sends. The `aux` command is therefore an alias for bus sends 1-6.
- FX send commands currently assume FX sends use send indexes 7-10 (`FX 1` -> `/ch/NN/mix/07/level`). This should be verified on hardware.
- For buttons, the safest first assignments are `mute`, `bus-on`, and `fx-on`. Later candidates: solo, tap tempo, mute groups, or selected-channel actions, but only after confirming their XR18 OSC addresses and whether the controller buttons behave as momentary or toggle controls.
- Windows Firewall can block incoming UDP responses. If scanning or live updates do not work, check firewall permissions for the console app.
- The current scanner is intentionally simple: it searches only local `/24` subnet(s), not arbitrary routed networks.
