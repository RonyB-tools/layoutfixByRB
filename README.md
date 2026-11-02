# LayoutFix ByRB

Typed a sentence with the keyboard on the wrong language? Select it and press **Ctrl+Alt+Space**.
`akuo` becomes `שלום`, and `יקךךם` becomes `hello`.

התחלת להקליד והמקלדת הייתה על השפה הלא נכונה? מסמנים ולוחצים **Ctrl+Alt+Space**.

- One small `.exe` (about 13KB). No installation and no dependencies; it uses only what ships with Windows 10/11.
- Nothing selected? It converts the last word before the cursor.
- The direction (Hebrew ↔ English) is detected automatically, and the keyboard language switches to match.
- Tray icon menu: start with Windows, exit.

## Download

[Latest release](../../releases/latest) → `LayoutFix-ByRB.zip`

## Build from source

```powershell
.\build.ps1
```

Compiles `src\*.cs` with the C# compiler built into Windows and writes `dist\`.

## License

MIT. Free to use and share, provided as is, without warranty. See [LICENSE](LICENSE).

Created By RonyB with Claude C.
