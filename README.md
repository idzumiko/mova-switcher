# Mova Switcher

A small Windows tray app that fixes text typed in the wrong keyboard layout.
Typed `ghbdsn` when you meant `привіт`? Select it, press the hotkey and it becomes `привіт`.
It works the other way too (`руддщ` → `hello`).

## How it works

1. You press the hotkey (default **Ctrl + Shift + Space**; you can change it in Settings).
2. The app copies the selection (Ctrl+C) and re-types it as if you had pressed the same physical
   keys in another layout. Which layout is set by **Convert** in Settings:
   - **From the current layout to the next one** (default): the text is treated as typed in the
     layout that's active now and converted to the next one in your Windows list. With 3+ layouts,
     pressing again keeps cycling. If the active layout can't type the selected letters at all
     (you already switched by hand), the layout that can type them is used instead.
   - **Detect the language from the text**: works out which of two chosen languages the text
     is in from its characters, whatever layout is active.
3. It pastes the result over the selection (Ctrl+V), then puts back whatever was on your clipboard.
4. Optionally it switches your active keyboard layout to the correct language, so you can keep typing.

If nothing is selected, it selects the word to the left of the cursor and converts that (you can turn this off).

The key maps come from Windows itself (`ToUnicodeEx`), so the app works with any two installed
layouts, not only Ukrainian/English.

## Build and run

Requires the .NET 10 SDK.

```powershell
dotnet run -c Release
```

To get a single standalone `.exe` (no .NET install needed on the target PC):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The result is `publish\MovaSwitcher.exe`. Tick **Start with Windows** in Settings to launch it on login.

## Settings

Right-click the tray icon, then **Settings…** (or double-click the icon):

- **Hotkey**: click the box and press the combination you want.
- **Convert**: current layout → next layout, or detect the language from the text.
- **Language 1 / Language 2**: the pair used by the detect mode.
- **Switch keyboard layout after converting**
- **If nothing is selected, convert the last word**
- **Start with Windows**

Settings are stored in `%APPDATA%\MovaSwitcher\settings.json`.

## Checking the conversion

```powershell
.\bin\Release\net10.0-windows\MovaSwitcher.exe --selftest ghbdsn руддщ > out.txt
```

## Known limitations

- **Admin windows**: Windows blocks a normal app from sending keys to programs that run as
  administrator. To fix text there, run Mova Switcher as administrator too.
- **Apps that copy a whole line when nothing is selected** (e.g. VS Code): with no selection,
  Ctrl+C copies the line, so select the text first in those editors.
- The text the target app copies with Ctrl+C may show up in Windows clipboard history (Win+V).
  The converted text the app pastes is marked to stay out of history.
