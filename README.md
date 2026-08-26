# WECPBXR

> [!WARNING]
> WECPBXR is beta software and is still being tested with real MIDI controllers and Behringer XR mixers.

WECPBXR is a cross-platform desktop application for controlling Behringer XR series digital mixers over Ethernet with a Miwayer Worlde Easycontrol Plus MIDI controller. It translates incoming MIDI messages into XR-compatible OSC commands, keeps the software control state synchronized with mixer values, and provides an editable MIDI-to-mixer map for practical live sound workflows.

The project targets Behringer XR-series mixers. UI labels and diagnostics use the general XR name because these mappings target the full XR family.

## Main Features

- Avalonia desktop interface styled after the Easycontrol Plus surface.
- OSC communication with Behringer XR mixers over UDP port `10024`.
- MIDI input support through `Melanchall.DryWetMidi`.
- Automatic or manual connection to a selected MIDI input device.
- Manual mixer connection by IP address, with optional pull of current mixer values.
- Editable mapping between physical controls and mixer functions.
- Seven software banks with independent names, colors, labels, and assignments.
- Support for 24 knobs, 9 faders, and 20 assignable buttons, including `BANK L`, `BANK R`, `SOLO`, and `SEND ALL`.
- Built-in command catalog for channel level, mute, pan, bus sends, AUX aliases, FX sends, and send on/off commands.
- Soft takeover for continuous controls, so a fader or knob starts sending only after it catches the current mixer value.
- Toggle-by-press behavior for button mappings such as channel mute.
- Live visual feedback for controller values, mixer values, selected bank, MIDI status, mixer status, and recent log messages.
- Assignment mode for changing mixer bindings directly in the UI.
- MIDI learn mode for assigning the next physical controller event to the selected software control.
- Map validation for missing OSC bindings, unknown OSC bindings, and duplicate MIDI assignments.
- Diagnostic console application for scanning mixers, testing MIDI input, editing maps, and simulating MIDI or mixer events without the desktop UI.

## Solution Structure

- `WECPBXR.UI` - Avalonia desktop application and user settings.
- `WECPBXR.Console` - diagnostic console utility for hardware checks and map editing.
- `WECPBXR.Core` - bank model, mapping engine, MIDI/OSC map configuration, mixer command catalog, and soft-takeover logic.
- `WECPBXR.Hardware` - MIDI input handling, OSC mixer client, and local network scanner.

## Requirements

- Windows or Linux.
- If you use a Mac, buy yourself professional equipment.
- .NET 8 SDK for building from source.
- A Behringer XR-series mixer reachable on the local network.
- A Miwayer Worlde Easycontrol Plus MIDI controller, or another MIDI controller with a compatible/custom map.
- Network access to the mixer on UDP port `10024`.

Windows Firewall can block incoming UDP responses from the mixer. On Linux, local firewall rules or missing MIDI permissions can cause similar symptoms. If discovery, MIDI input, or live updates do not work, check OS permissions and firewall settings.

On Linux, MIDI input uses ALSA sequencer devices. Install the ALSA runtime/tools and make sure your user can access audio/MIDI devices:

```bash
sudo apt install alsa-utils libasound2
sudo usermod -aG audio "$USER"
```

Log out and back in after changing groups. To verify that the controller is visible before starting WECPBXR:

```bash
aconnect -l
aseqdump -l
aseqdump -p <client>:<port>
```

## Build

From the repository root:

```powershell
dotnet build WECPBXR.slnx
```

Run the Avalonia desktop application:

```powershell
dotnet run --project WECPBXR.UI
```

Publish platform-specific desktop builds:

```powershell
dotnet publish WECPBXR.UI -c Release -r win-x64 --self-contained true
dotnet publish WECPBXR.UI -c Release -r linux-x64 --self-contained true
```

Run the diagnostic console:

```powershell
dotnet run --project WECPBXR.Console
```

## Linux Application Menu Shortcut

After publishing a Linux build, copy or unpack the published files into a stable directory, for example `/home/user/opt/WECPBXR`. Then create a desktop entry:

```bash
mkdir -p ~/.local/share/applications
nano ~/.local/share/applications/wecpbxr.desktop
```

Use this content, adjusting `Exec`, `Path`, and `Icon` to your install location and username:

```ini
[Desktop Entry]
Type=Application
Name=WECPBXR
Comment=Control Behringer XR mixers from a MIDI controller
Exec=/home/user/opt/WECPBXR/WECPBXR.UI
Path=/home/user/opt/WECPBXR
Icon=/home/user/opt/WECPBXR/midi-connector.png
Terminal=false
Categories=AudioVideo;Audio;Midi;
StartupNotify=true
```

Make the shortcut executable and refresh the desktop menu database:

```bash
chmod +x ~/.local/share/applications/wecpbxr.desktop
update-desktop-database ~/.local/share/applications 2>/dev/null || true
```

If the app was published as a framework-dependent build, use `Exec=dotnet /home/user/opt/WECPBXR/WECPBXR.UI.dll` instead.

## Basic Workflow

1. Connect the computer, MIDI controller, and mixer to the required USB/network setup.
2. Start `WECPBXR.UI`.
3. Enter the mixer IP address, for example `192.168.1.100`.
4. Click `X+` to connect to the mixer.
5. Select a MIDI input device and click `M+`.
6. Click `Pull` to request current values for assigned OSC addresses.
7. Move a fader, knob, or button on the MIDI controller.
8. If a continuous control is locked by soft takeover, move it until it reaches the current mixer value; after that, WECPBXR starts sending OSC changes.

## Mapping

The default map is stored in `WECPBXR.Console/midi-map.json` and is copied into the UI output as `midi-map.json`. The map contains bank definitions, control labels, MIDI bindings, and mixer OSC bindings.

In the desktop UI, use `Assign` to enter assignment mode. Select a control, choose a command, set channel and index values, then click `Set`. Use `Learn` to bind the selected software control to the next incoming MIDI event. Use `Save` to write the updated map.

The `slot: none` label means that no assignable control is currently selected. Click any visual control while assignment mode is enabled to select its slot. `BANK L` and `BANK R` stop switching banks while assignment mode is enabled, so they can be selected and assigned safely.

## Default Banks

The bundled map contains six ready-to-use live-sound banks and two spare banks:

- `Main mix 1-8` - Main LR, channel 1-8 faders, channel pan, channel mute, channel solo, clear solo, and main mute.
- `Main mix 9-16` - Main LR, channel 9-16 faders, channel pan, channel mute, channel solo, clear solo, and main mute.
- `Monitors` - Main LR, bus 1-6 masters, bus mutes, and channel 1-8 sends to bus 1-3.
- `FX` - FX return levels, FX send masters, channel 1-8 sends to FX 1-3, FX return mutes, tap tempo, and FX mute group.
- `Dynamics/EQ` - Channel 1-8 compressor thresholds, EQ gain controls, HPF toggles, compressor toggles, clear solo, and mute group 1. HPF means high-pass filter: it removes low-frequency rumble below the selected cutoff.
- `Utility/Safety` - Mute groups, clear solo, tap tempo, snapshot previous/next, snapshot 1-4 load buttons, main mute, bus mutes, and FX mute group.
- `Custom 1` and `Custom 2` - spare banks with MIDI bindings already assigned but no mixer OSC functions.

Snapshot and tap-tempo OSC commands are included as practical utility actions, but they should be verified on your XR mixer before using them in a live show.

## MIDI Controller Setup

Configure the Miwayer Worlde Easycontrol Plus controls as MIDI Control Change messages on MIDI channel `1`.

| Control | MIDI channel | CC number |
| --- | ---: | ---: |
| Knob 1-24 | 1 | 1-24 |
| Fader 1-9 | 1 | 25-33 |
| BANK L | 1 | 34 |
| BANK R | 1 | 35 |
| SOLO | 1 | 36 |
| SEND ALL | 1 | 37 |
| Button 1-16 | 1 | 38-53 |

Button-style controls should send `0` for off/release and `127` for on/press when the controller editor allows it. `BANK L` and `BANK R` are learned and stored as MIDI bindings like other controls, but WECPBXR treats their assigned MIDI messages as bank navigation commands.

Supported command keys include:

- `main` - input channel main LR fader.
- `mute` - input channel on/off state, presented as mute control.
- `pan` - input channel pan.
- `bus` - input channel send level to bus 1-6.
- `aux` - alias for bus 1-6, commonly used for AUX output workflows.
- `fx` - input channel send level to FX 1-4.
- `bus-on` - input channel send on/off to bus 1-6.
- `fx-on` - input channel send on/off to FX 1-4.

The diagnostic console exposes additional map commands such as `map list`, `map show`, `map set`, `map clear`, `map save`, and `map commands`.

## Current Limitations

- The project is in beta and should be tested carefully before use in production live sound scenarios.
- Physical RGB feedback for Easycontrol Plus bank buttons is not implemented yet because the controller-specific protocol is not confirmed.
- FX send OSC indexes are currently based on the expected XR/X-Air send layout and should be verified on real hardware.
- The scanner searches local IPv4 `/24` subnet ranges only.
- `Rug.Osc` currently restores as a .NET Framework package and produces NuGet compatibility warnings during build, although the solution builds successfully.

## Feedback and Contributions

Bug reports and feature ideas are welcome in [GitHub Issues](https://github.com/VRyzhkov/WECPBXR/issues). When reporting a hardware problem, include the mixer model, controller model, operating system, connection method, and steps to reproduce the issue.

Pull requests are welcome. Please test changes locally, especially when they affect MIDI input, OSC output, mapping behavior, or live mixer state.

## Support

If this project saved your time or helped your setup, you can support the author here:

| Регион / Region | Способ оплаты / Payment Method | Ссылка / Link |
| :--- | :--- | :--- |
| **🇷🇺 Для пользователей из РФ** | Карта любого банка РФ / СБП | [![Donate](https://shields.io)](https://cloudtips.ru) |
| **🌐 International / Crypto** | **USDT / ETH / BNB** *(Network: BSC BEP-20 or Ethereum)* | `0x90E87117C67344a7daeD7cb8d1d03267de03FF08` |

---

# WECPBXR

> [!WARNING]
> WECPBXR - бета-версия программы. Она ещё проходит проверку с реальными MIDI-контроллерами и микшерами Behringer XR.

WECPBXR - это кроссплатформенное desktop-приложение для управления цифровыми микшерами Behringer серии XR по Ethernet с помощью MIDI-контроллера Miwayer Worlde Easycontrol Plus. Программа преобразует входящие MIDI-сообщения в OSC-команды, понятные микшеру XR, синхронизирует состояние программных контролов со значениями микшера и позволяет редактировать карту соответствий между MIDI-контроллером и функциями микшера.

Проект рассчитан на микшеры Behringer серии XR. В интерфейсе и диагностике используется общее название XR, потому что назначения рассчитаны на всё семейство XR.

## Основной функционал

- Avalonia-интерфейс, визуально повторяющий рабочую поверхность Easycontrol Plus.
- Обмен OSC-сообщениями с микшерами Behringer XR по UDP-порту `10024`.
- Работа с MIDI-входом через `Melanchall.DryWetMidi`.
- Ручное или автоматическое подключение к выбранному MIDI-устройству.
- Подключение к микшеру по IP-адресу и запрос текущих значений микшера.
- Редактируемая карта соответствий между физическими контролами и функциями микшера.
- Семь программных банков с отдельными названиями, цветами, подписями и назначениями.
- Поддержка 24 энкодеров, 9 фейдеров и 20 назначаемых кнопок, включая `BANK L`, `BANK R`, `SOLO` и `SEND ALL`.
- Встроенный каталог команд для уровня канала, mute, panorama, посылов на bus, AUX-алиасов, FX-посылов и включения/выключения посылов.
- Soft takeover для плавных контролов: фейдер или энкодер начинает отправлять изменения только после того, как догонит текущее значение микшера.
- Поведение toggle-by-press для кнопок, например для mute.
- Живая индикация значений контроллера, значений микшера, текущего банка, MIDI-статуса, статуса микшера и последних сообщений журнала.
- Режим назначения функций прямо в интерфейсе.
- MIDI learn: назначение следующего физического MIDI-события на выбранный программный контрол.
- Проверка карты на отсутствующие OSC-назначения, неизвестные OSC-адреса и дублирующиеся MIDI-назначения.
- Диагностическая консоль для поиска микшеров, проверки MIDI-входа, редактирования карты и симуляции MIDI/OSC-событий без desktop-интерфейса.

## Структура решения

- `WECPBXR.UI` - Avalonia-приложение и пользовательские настройки.
- `WECPBXR.Console` - диагностическая консоль для проверки оборудования и редактирования карты.
- `WECPBXR.Core` - модель банков, движок маппинга, конфигурация MIDI/OSC-карты, каталог команд микшера и логика soft takeover.
- `WECPBXR.Hardware` - работа с MIDI-входом, OSC-клиент микшера и сканер локальной сети.

## Требования

- Windows или Linux.
- Если вы используете Mac - купите себе профессиональное оборудование.
- .NET 8 SDK для сборки из исходников.
- Микшер Behringer серии XR, доступный в локальной сети.
- MIDI-контроллер Miwayer Worlde Easycontrol Plus или другой MIDI-контроллер с совместимой/настроенной картой.
- Сетевой доступ к микшеру по UDP-порту `10024`.

Windows Firewall может блокировать входящие UDP-ответы от микшера. В Linux похожие симптомы могут вызывать правила локального firewall или права доступа к MIDI-устройствам. Если поиск устройств, MIDI-вход или живое обновление значений не работают, проверьте разрешения ОС и настройки firewall.

В Linux MIDI-вход использует ALSA sequencer devices. Установите ALSA runtime/tools и проверьте, что пользователь имеет доступ к audio/MIDI-устройствам:

```bash
sudo apt install alsa-utils libasound2
sudo usermod -aG audio "$USER"
```

После изменения групп выйдите из сессии и зайдите снова. Перед запуском WECPBXR можно проверить, что контроллер виден системе:

```bash
aconnect -l
aseqdump -l
aseqdump -p <client>:<port>
```

## Сборка и запуск

Из корня репозитория:

```powershell
dotnet build WECPBXR.slnx
```

Запуск Avalonia-приложения:

```powershell
dotnet run --project WECPBXR.UI
```

Публикация desktop-сборок под конкретные платформы:

```powershell
dotnet publish WECPBXR.UI -c Release -r win-x64 --self-contained true
dotnet publish WECPBXR.UI -c Release -r linux-x64 --self-contained true
```

Запуск диагностической консоли:

```powershell
dotnet run --project WECPBXR.Console
```

## Ярлык в меню Linux

После публикации Linux-сборки скопируйте или распакуйте файлы программы в постоянную папку, например `/home/user/opt/WECPBXR`. Затем создайте desktop entry:

```bash
mkdir -p ~/.local/share/applications
nano ~/.local/share/applications/wecpbxr.desktop
```

Вставьте содержимое ниже, поправив `Exec`, `Path` и `Icon` под ваш путь установки и имя пользователя:

```ini
[Desktop Entry]
Type=Application
Name=WECPBXR
Comment=Control Behringer XR mixers from a MIDI controller
Exec=/home/user/opt/WECPBXR/WECPBXR.UI
Path=/home/user/opt/WECPBXR
Icon=/home/user/opt/WECPBXR/midi-connector.png
Terminal=false
Categories=AudioVideo;Audio;Midi;
StartupNotify=true
```

Сделайте ярлык исполняемым и обновите базу меню:

```bash
chmod +x ~/.local/share/applications/wecpbxr.desktop
update-desktop-database ~/.local/share/applications 2>/dev/null || true
```

Если программа опубликована как framework-dependent сборка, используйте `Exec=dotnet /home/user/opt/WECPBXR/WECPBXR.UI.dll`.

## Базовый сценарий работы

1. Подключите компьютер, MIDI-контроллер и микшер к нужной USB/сетевой схеме.
2. Запустите `WECPBXR.UI`.
3. Укажите IP-адрес микшера, например `192.168.1.100`.
4. Нажмите `X+`, чтобы подключиться к микшеру.
5. Выберите MIDI-вход и нажмите `M+`.
6. Нажмите `Pull`, чтобы запросить текущие значения назначенных OSC-адресов.
7. Двигайте фейдер, энкодер или кнопку на MIDI-контроллере.
8. Если плавный контрол заблокирован soft takeover, двигайте его до текущего значения микшера; после совпадения WECPBXR начнёт отправлять OSC-изменения.

## Карта назначений

Карта по умолчанию хранится в `WECPBXR.Console/midi-map.json` и копируется в выходную папку UI как `midi-map.json`. В карте описаны банки, подписи контролов, MIDI-привязки и OSC-привязки микшера.

В desktop-интерфейсе нажмите `Assign`, чтобы перейти в режим назначения. Выберите контрол, укажите команду, канал и индекс, затем нажмите `Set`. Кнопка `Learn` назначает выбранному программному контролу следующее входящее MIDI-событие. Кнопка `Save` сохраняет обновлённую карту.

Надпись `slot: none` означает, что сейчас не выбран ни один назначаемый контрол. Слот задаётся кликом по любому визуальному контролу при включённом режиме назначения. В этом режиме `BANK L` и `BANK R` не переключают банки, поэтому их можно безопасно выбрать и назначить.

## Банки по умолчанию

В комплектной карте есть шесть готовых банков для живой работы и два запасных банка:

- `Main mix 1-8` - Main LR, фейдеры каналов 1-8, панорама каналов, mute, solo, clear solo и main mute.
- `Main mix 9-16` - Main LR, фейдеры каналов 9-16, панорама каналов, mute, solo, clear solo и main mute.
- `Monitors` - Main LR, мастера bus 1-6, mute для bus и посылы каналов 1-8 на bus 1-3.
- `FX` - уровни FX return, мастера FX send, посылы каналов 1-8 на FX 1-3, mute FX return, tap tempo и FX mute group.
- `Dynamics/EQ` - compressor threshold каналов 1-8, усиление полос EQ, HPF on/off, compressor on/off, clear solo и mute group 1. HPF - это high-pass filter, то есть фильтр верхних частот: он срезает низкочастотный гул ниже выбранной частоты.
- `Utility/Safety` - mute groups, clear solo, tap tempo, snapshot previous/next, загрузка snapshots 1-4, main mute, bus mute и FX mute group.
- `Custom 1` и `Custom 2` - запасные банки с готовыми MIDI-привязками, но без назначенных OSC-функций микшера.

OSC-команды для snapshots и tap tempo добавлены как практичные служебные действия, но их нужно проверить на вашем XR-микшере до использования на живом выступлении.

## Настройка MIDI-контроллера

Настройте Miwayer Worlde Easycontrol Plus так, чтобы все органы управления отправляли MIDI Control Change на MIDI-канале `1`.

| Контрол | MIDI-канал | CC number |
| --- | ---: | ---: |
| Knob 1-24 | 1 | 1-24 |
| Fader 1-9 | 1 | 25-33 |
| BANK L | 1 | 34 |
| BANK R | 1 | 35 |
| SOLO | 1 | 36 |
| SEND ALL | 1 | 37 |
| Button 1-16 | 1 | 38-53 |

Для кнопок желательно настроить значение `0` на отпускание/off и `127` на нажатие/on, если редактор контроллера это позволяет. `BANK L` и `BANK R` обучаются и сохраняются как обычные MIDI-привязки, но WECPBXR использует назначенные на них MIDI-сообщения как команды переключения банков.

Поддерживаемые ключи команд:

- `main` - основной LR-фейдер входного канала.
- `mute` - состояние включения канала, представленное как mute.
- `pan` - панорама входного канала.
- `bus` - уровень посыла входного канала на bus 1-6.
- `aux` - алиас для bus 1-6, удобный для сценариев с AUX-выходами.
- `fx` - уровень посыла входного канала на FX 1-4.
- `bus-on` - включение/выключение посыла входного канала на bus 1-6.
- `fx-on` - включение/выключение посыла входного канала на FX 1-4.

Диагностическая консоль дополнительно поддерживает команды `map list`, `map show`, `map set`, `map clear`, `map save` и `map commands`.

## Текущие ограничения

- Проект находится в бета-стадии, поэтому перед использованием на реальном мероприятии его нужно внимательно проверить на вашем оборудовании.
- Физическая RGB-индикация кнопок банков Easycontrol Plus пока не реализована, потому что протокол контроллера ещё не подтверждён.
- OSC-индексы FX-посылов сейчас основаны на ожидаемой структуре XR/X-Air и требуют проверки на реальном микшере.
- Сканер ищет устройства только в локальных IPv4-подсетях `/24`.
- `Rug.Osc` восстанавливается как .NET Framework-пакет и даёт NuGet-предупреждения совместимости при сборке, хотя решение собирается успешно.

## Обратная связь и участие

Баги и идеи по развитию можно оставлять в [GitHub Issues](https://github.com/VRyzhkov/WECPBXR/issues). Если сообщаете о проблеме с оборудованием, укажите модель микшера, модель контроллера, операционную систему, способ подключения и шаги для воспроизведения.

Pull Request'ы приветствуются. Пожалуйста, проверяйте изменения локально, особенно если они затрагивают MIDI-вход, OSC-выход, логику маппинга или живое состояние микшера.

## Поддержать проект

Если проект сэкономил вам время или помог в настройке, вы можете поддержать автора:

| Регион / Region | Способ оплаты / Payment Method | Ссылка / Link |
| :--- | :--- | :--- |
| **🇷🇺 Для пользователей из РФ** | Карта любого банка РФ / СБП | [![Donate](https://shields.io)](https://cloudtips.ru) |
| **🌐 International / Crypto** | **USDT / ETH / BNB** *(Network: BSC BEP-20 or Ethereum)* | `0x90E87117C67344a7daeD7cb8d1d03267de03FF08` |

Спасибо за поддержку.
