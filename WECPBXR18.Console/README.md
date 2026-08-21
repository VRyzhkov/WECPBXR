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
- Windows Firewall can block incoming UDP responses. If scanning or live updates do not work, check firewall permissions for the console app.
- The current scanner is intentionally simple: it searches only local `/24` subnet(s), not arbitrary routed networks.
