# WECPBXR.Console

Diagnostic console application for checking MIDI controller input and OSC communication with Behringer XR series before the WPF UI is ready.

## Run

From the repository root:

```powershell
dotnet run --project WECPBXR.Console
```

The app starts without connecting to XR or MIDI devices. It automatically tries to load the default MIDI/OSC map and then prints the command list.

To reload the default MIDI/OSC map manually, run `map load`.

## Mixer Connection

Connect to XR by address:

```text
mixer connect 192.168.1.100
```

Or ask for the address interactively:

```text
mixer connect
```

Then type an address or run discovery:

```text
XR address or scan: scan
```

If mixers are found, select one by number.

## Commands

```text
mixer connect [address]
```

Connects to XR. Without an address, the app asks for one in the console.

```text
mixer disconnect
```

Disconnects the current XR connection.

```text
mixer status
```

Prints current XR connection status.

```text
mixer scan
```

Scans active local IPv4 `/24` subnet(s), sends `/xinfo` to UDP port `10024`, waits for responses, and prints discovered devices.

```text
scan
```

Same as `mixer scan`.

After mixer connection, incoming OSC messages are printed to the console and numeric values are passed into Core mapping. If the OSC address is assigned in the current bank, the matching slot's mixer value is updated.

```text
mute <1-18>
```

Mutes the selected XR input channel.

Example:

```text
mute 1
```

```text
unmute <1-18>
```

Unmutes the selected XR input channel.

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
Mapped MIDI events are converted by Core into OSC-ready mixer commands. If XR is connected, the console sends those commands immediately; if XR is not connected, it only prints the command that would be sent.

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

Prints current Core bank state, including values and MIDI/OSC bindings. Each bank contains:

- 24 knobs
- 9 faders
- 20 assignable buttons, including `bank-prev`, `bank-next`, `solo`, and `send-all`

For every assignable control, the console prints controller value, mixer value, and soft-takeover lock state.

```text
bank layout
```

Prints the current bank in physical controller order:

- Special buttons
- Master fader
- Channel faders
- Bus 1 knobs
- Bus 2 knobs
- Bus 3 knobs
- Mute buttons
- Second button row

```text
bank list
```

Prints all 7 banks. The default map uses these bank names:

- Main mix 1-8
- Main mix 9-16
- Monitors
- FX
- Dynamics/EQ
- Utility/Safety
- Custom 1
- Custom 2

Default MIDI setup uses Control Change on MIDI channel 1:

- knobs 1-24: CC 1-24
- faders 1-9: CC 25-33
- `bank-prev` / `BANK L`: CC 34
- `bank-next` / `BANK R`: CC 35
- `solo`: CC 36
- `send-all`: CC 37
- assignable buttons 1-16: CC 38-53

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
WECPBXR.Console/midi-map.json
```

```text
map load [path]
```

Loads MIDI/OSC map from JSON and applies it to the current Core bank set.

Without a path, loads the current default map path. On startup, the default path is `midi-map.json` from the application output directory. During development, if the file was not copied to output, the app falls back to the project file:

```text
WECPBXR.Console/midi-map.json
```

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
map set osc <slotId> <oscAddress> [level|toggle|pan|action]
```

Assigns an exact OSC address.

Example:

```text
map set osc fader-01 /ch/01/mix/fader level
```

```text
map set command <slotId> <main|mute|pan|bus|aux|fx|bus-on|fx-on> <channel> [index]
```

Assigns a predefined XR command.

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

Prints the predefined XR command catalog.

```text
sim midi <cc|note|noteoff|pitch> <channel> <number> <0-127>
```

Simulates an incoming MIDI event and passes it through Core mapping.

Examples:

```text
sim midi cc 1 26 80
sim midi note 1 34 127
```

If the simulated control is mapped and unlocked, the console prints `OSC ready` with the generated OSC address and value. Simulation never sends to XR.

```text
sim mixer <oscAddress> <0.0-1.0>
```

Simulates an incoming mixer OSC value and passes it through Core mapping.

Example:

```text
sim mixer /ch/01/mix/fader 0.75
```

Useful no-hardware check:

```text
map load
sim mixer /ch/01/mix/fader 0.62
sim midi cc 1 26 80
bank layout
```

Mute buttons use `ToggleByPress` behavior by default for `Toggle` mixer bindings:

- press events toggle the current mixer value;
- release events are ignored;
- XR `/ch/NN/mix/on` is inverted from the word "mute": `1` means channel on, `0` means muted.

No-hardware mute check:

```text
map load
sim mixer /ch/01/mix/on 1
sim midi note 1 34 127
bank layout
```

The simulated button press should produce an OSC-ready command with value `0`.

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

- XR OSC uses UDP port `10024`.
- The app sends `/xremote` when connected and repeats it every 5 seconds.
- MIDI diagnostics use `Melanchall.DryWetMidi`.
- MIDI input events are printed as raw DryWetMIDI event text. Internally the hardware layer also normalizes Control Change, Note On, Note Off, and Pitch Bend events for future mapping logic.
- Core currently uses a provisional default MIDI map: knobs and faders are sequential Control Change values, assignable buttons are sequential Note values. Real Worlde mappings should be adjusted after checking `midi connect <index>` logs.
- Core stores an RGB color per bank. Sending that color to the physical Easycontrol bank buttons is not implemented yet because the controller-specific MIDI/SysEx protocol for button RGB is still unknown.
- `sim` commands do not send anything to XR or a MIDI device. They only exercise Core mapping state inside the console app.
- Core is now responsible for generating outgoing OSC commands from mapped controller changes. The hardware layer only sends the already generated OSC address/value.
- Continuous controls (`level`, `pan`) use soft takeover: Core sends OSC only after both controller and mixer values are known and close enough to unlock the slot.
- Toggle controls use `ToggleByPress`: Core reacts only to press events, reads the current mixer value, inverts it, and generates one OSC command. Release events are ignored.
- XR command catalog currently includes channel main fader, mute, pan, bus/aux send level, FX send level, bus send on/off, and FX send on/off.
- On XR/X-Air, AUX outputs are normally fed by bus sends. The `aux` command is therefore an alias for bus sends 1-6.
- FX send commands currently assume FX sends use send indexes 7-10 (`FX 1` -> `/ch/NN/mix/07/level`). This should be verified on hardware.
- For buttons, the safest first assignments are `mute`, `bus-on`, and `fx-on`. Later candidates: solo, tap tempo, mute groups, or selected-channel actions, but only after confirming their XR OSC addresses and whether the controller buttons behave as momentary or toggle controls.
- Windows Firewall can block incoming UDP responses. If scanning or live updates do not work, check firewall permissions for the console app.
- The current scanner is intentionally simple: it searches only local `/24` subnet(s), not arbitrary routed networks.
