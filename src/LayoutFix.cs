// LayoutFix — ממיר טקסט שהוקלד בשפת מקלדת שגויה (עברית <-> אנגלית)
// Ctrl+Alt+Space: יש סימון -> ממיר אותו; אין סימון -> ממיר את המילה האחרונה.
// Created By RonyB with Claude C.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Reflection;

[assembly: AssemblyTitle("LayoutFix ByRB")]
[assembly: AssemblyProduct("LayoutFix ByRB")]
[assembly: AssemblyDescription("Fixes text typed in the wrong keyboard layout (Hebrew/English). Ctrl+Alt+Space.")]
[assembly: AssemblyCompany("RonyB")]
[assembly: AssemblyCopyright("Created By RonyB with Claude C.")]
[assembly: AssemblyVersion("2.2.2.0")]
[assembly: AssemblyFileVersion("2.2.2.0")]

static class Program
{
    public const string QuitEventName = "LayoutFix.Quit.RB";

    [STAThread]
    static void Main()
    {
        bool created;
        var mutex = new Mutex(true, "LayoutFix.SingleInstance.RB", out created);
        if (!created)
        {
            // עותק אחר כבר פועל (למשל גרסה ישנה): מבקשים ממנו להיסגר ונכנסים במקומו
            using (var quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName))
                quit.Set();
            bool acquired = WaitForMutex(mutex, 3000);
            if (!acquired)
            {
                // גרסאות ישנות (2.1 ומטה) לא מכירות את הבקשה — סוגרים אותן ישירות
                var me = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(me.ProcessName))
                    if (p.Id != me.Id) { try { p.Kill(); } catch { } }
                acquired = WaitForMutex(mutex, 3000);
            }
            if (!acquired) return;
        }
        // בכיבוי המחשב נסגרים אחרונים: אם תוכנה אחרת (Word עם מסמך לא שמור וכו')
        // עוצרת את הכיבוי והוא מבוטל, LayoutFix עוד לא נסגר וממשיך לפעול
        try { Native.SetProcessShutdownParameters(0x100, 0); } catch { }
        Application.EnableVisualStyles();
        Application.Run(new TrayApp());
        try { mutex.ReleaseMutex(); } catch { }
        GC.KeepAlive(mutex);
    }

    static bool WaitForMutex(Mutex m, int ms)
    {
        try { return m.WaitOne(ms); }
        catch (AbandonedMutexException) { return true; }
    }
}

class TrayApp : ApplicationContext
{
    const int HOTKEY_ID = 0x4C46;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "LayoutFix";

    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkeyWindow;
    readonly ToolStripMenuItem autoStartItem;
    readonly ToolStripMenuItem pauseItem;
    readonly Icon activeIcon = MakeIcon(Color.FromArgb(47, 91, 211));
    readonly Icon pausedIcon = MakeIcon(Color.FromArgb(150, 150, 150));
    readonly EventWaitHandle quitEvent;
    bool paused;

    public TrayApp()
    {
        hotkeyWindow = new HotkeyWindow(OnHotkey, delegate { ExitThread(); });

        var menu = new ContextMenuStrip();
        menu.RightToLeft = RightToLeft.Yes;
        var title = new ToolStripMenuItem("LayoutFix · Ctrl+Alt+Space");
        title.Enabled = false;
        pauseItem = new ToolStripMenuItem();
        pauseItem.Click += delegate { SetPaused(!paused); };
        autoStartItem = new ToolStripMenuItem("הפעלה אוטומטית עם Windows");
        autoStartItem.Checked = IsAutoStart();
        autoStartItem.Click += delegate { SetAutoStart(!autoStartItem.Checked); autoStartItem.Checked = IsAutoStart(); };
        menu.Items.Add(title);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(pauseItem);
        menu.Items.Add(autoStartItem);

        tray = new NotifyIcon();
        tray.ContextMenuStrip = menu;
        tray.Visible = true;

        // אם ההפעלה האוטומטית מסומנת אבל מצביעה על קובץ אחר (גרסה קודמת), מעדכנים אותה לקובץ הזה
        if (IsAutoStart() && !AutoStartPointsHere()) SetAutoStart(true);

        // עותק חדש שמופעל מבקש מהעותק הזה להיסגר (למשל בעדכון גרסה)
        quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.QuitEventName);
        var listener = new Thread(delegate ()
        {
            quitEvent.WaitOne();
            Native.PostMessage(hotkeyWindow.Handle, HotkeyWindow.WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
        });
        listener.IsBackground = true;
        listener.Start();

        SetPaused(false); // הפעלה רגילה שקטה; הודעה רק אם הקיצור תפוס
    }

    // השהיה: משחררים את הקיצור והסמל הופך לאפור. הפעלה: תופסים אותו שוב.
    void SetPaused(bool pause)
    {
        if (pause)
        {
            Native.UnregisterHotKey(hotkeyWindow.Handle, HOTKEY_ID);
            paused = true;
        }
        else
        {
            bool ok = Native.RegisterHotKey(hotkeyWindow.Handle, HOTKEY_ID,
                Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.Space);
            paused = !ok;
            if (!ok)
                tray.ShowBalloonTip(6000, "LayoutFix",
                    "הקיצור Ctrl+Alt+Space תפוס כרגע על ידי תוכנה אחרת, ולכן LayoutFix מושהה. אפשר לנסות שוב מהתפריט: הפעלה.",
                    ToolTipIcon.Warning);
        }
        tray.Icon = paused ? pausedIcon : activeIcon;
        tray.Text = paused ? "LayoutFix – מושהה" : "LayoutFix – פעיל (Ctrl+Alt+Space)";
        pauseItem.Text = paused ? "הפעלה" : "השהיה";
    }

    protected override void ExitThreadCore()
    {
        Native.UnregisterHotKey(hotkeyWindow.Handle, HOTKEY_ID);
        tray.Visible = false;
        tray.Dispose();
        hotkeyWindow.DestroyHandle();
        base.ExitThreadCore();
    }

    // ---------- the actual work ----------

    bool busy;

    void OnHotkey()
    {
        if (busy) return;
        busy = true;
        try { FixSelection(); }
        catch { }
        finally { busy = false; }
    }

    void FixSelection()
    {
        IntPtr target = Native.GetForegroundWindow();
        WaitForModifiersReleased();

        IDataObject saved = SnapshotClipboard();
        TryClipboard(delegate { Clipboard.Clear(); });

        Native.SendCombo(Native.VK_CONTROL, (byte)'C');
        string text = WaitForClipboardText(400);

        // VS Code ודומיו מעתיקים את כל השורה כשאין סימון — זה לא סימון אמיתי
        if (text != null && text.EndsWith("\n")) text = null;

        bool toHebrew;
        string fixedText;
        if (!string.IsNullOrEmpty(text))
        {
            toHebrew = Converter.ShouldConvertToHebrew(text);
            fixedText = Converter.Convert(text, toHebrew);
        }
        else
        {
            // אין סימון: מסמנים עד תחילת השורה (עובד גם בטקסט מימין לשמאל)
            // ומחליפים את כל הקטע, כשרק המילה האחרונה מומרת
            TryClipboard(delegate { Clipboard.Clear(); });
            Native.SendCombo(Native.VK_SHIFT, Native.VK_HOME);
            Thread.Sleep(40);
            Native.SendCombo(Native.VK_CONTROL, (byte)'C');
            string line = WaitForClipboardText(400);
            if (string.IsNullOrEmpty(line)) { RestoreClipboard(saved); return; }

            string before, word, after;
            Converter.SplitLastWord(line, out before, out word, out after);
            if (!Converter.CanConvert(word))
            {
                Native.Tap(Native.VK_END); // מבטלים את הסימון
                RestoreClipboard(saved);
                return;
            }
            toHebrew = Converter.ShouldConvertToHebrew(word);
            fixedText = before + Converter.Convert(word, toHebrew) + after;
        }
        TryClipboard(delegate { Clipboard.SetText(fixedText); });
        Thread.Sleep(30);
        Native.SendCombo(Native.VK_CONTROL, (byte)'V');

        SwitchLayout(target, toHebrew);

        // מחזירים את הלוח הקודם אחרי שההדבקה הסתיימה
        var t = new System.Windows.Forms.Timer();
        t.Interval = 500;
        t.Tick += delegate { t.Stop(); t.Dispose(); RestoreClipboard(saved); };
        t.Start();
    }

    static void WaitForModifiersReleased()
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 2000 &&
               (Native.IsDown(Native.VK_CONTROL) || Native.IsDown(Native.VK_MENU) ||
                Native.IsDown(Native.VK_SHIFT) || Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN)))
            Thread.Sleep(15);
    }

    static string WaitForClipboardText(int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            Application.DoEvents();
            string s = null;
            TryClipboard(delegate { if (Clipboard.ContainsText()) s = Clipboard.GetText(); });
            if (!string.IsNullOrEmpty(s)) return s;
            Thread.Sleep(20);
        }
        return null;
    }

    // רק פורמטים שנשמרים כעותק מנוהל מלא. פורמטים אחרים (OLE, Embed Source,
    // EnhancedMetafile וכו') הם מצביעים לזיכרון של התוכנה המקורית, והחזרתם
    // ל-OleFlushClipboard גרמה לקריסת Access Violation שאי אפשר לתפוס.
    static readonly string[] SafeFormats =
    {
        DataFormats.UnicodeText, DataFormats.Text, DataFormats.Rtf,
        DataFormats.Html, DataFormats.FileDrop, DataFormats.Bitmap
    };

    static IDataObject SnapshotClipboard()
    {
        var copy = new DataObject();
        bool any = false;
        TryClipboard(delegate
        {
            IDataObject cur = Clipboard.GetDataObject();
            if (cur == null) return;
            foreach (string f in SafeFormats)
            {
                try
                {
                    if (!cur.GetDataPresent(f, false)) continue;
                    object d = cur.GetData(f, false);
                    if (d is string || d is string[] || d is System.Drawing.Bitmap)
                    {
                        if (d is System.Drawing.Bitmap) d = new System.Drawing.Bitmap((System.Drawing.Bitmap)d);
                        copy.SetData(f, false, d);
                        any = true;
                    }
                }
                catch { }
            }
        });
        return any ? copy : null;
    }

    static void RestoreClipboard(IDataObject saved)
    {
        TryClipboard(delegate
        {
            if (saved != null) Clipboard.SetDataObject(saved, true);
            else Clipboard.Clear();
        });
    }

    static void TryClipboard(MethodInvoker action)
    {
        for (int i = 0; i < 6; i++)
        {
            try { action(); return; }
            catch (ExternalException) { Thread.Sleep(30); }
            catch (Exception) { return; }
        }
    }

    static void SwitchLayout(IntPtr window, bool toHebrew)
    {
        int lang = toHebrew ? 0x040D : 0x0409;
        IntPtr hkl = IntPtr.Zero;

        int n = Native.GetKeyboardLayoutList(0, null);
        var list = new IntPtr[n];
        Native.GetKeyboardLayoutList(n, list);
        foreach (IntPtr h in list)
        {
            int primary = (int)((long)h & 0xFFFF) & 0x3FF;
            if (primary == (lang & 0x3FF)) { hkl = h; break; }
        }
        if (hkl == IntPtr.Zero)
            hkl = Native.LoadKeyboardLayout(toHebrew ? "0000040D" : "00000409", 0);
        if (hkl != IntPtr.Zero && window != IntPtr.Zero)
            Native.PostMessage(window, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
    }

    // ---------- autostart ----------

    static bool IsAutoStart()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            return k != null && k.GetValue(RunName) != null;
    }

    static bool AutoStartPointsHere()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
        {
            string v = k == null ? null : k.GetValue(RunName) as string;
            return v != null && string.Equals(v.Trim('"'), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    static void SetAutoStart(bool on)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
            else k.DeleteValue(RunName, false);
        }
    }

    // ---------- tray icon: א/A keycap ----------

    static Icon MakeIcon(Color background)
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var b = new SolidBrush(background))
                g.FillRectangle(b, 1, 1, 30, 30);
            using (var f = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.DrawString("A", f, Brushes.White, 2, 1);
                g.DrawString("א", f, Brushes.White, 15, 13);
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}

class HotkeyWindow : NativeWindow
{
    public const uint WM_APP_QUIT = 0x8001; // WM_APP + 1: בקשה מעותק חדש להיסגר

    readonly Action onHotkey;
    readonly Action onQuit;
    public HotkeyWindow(Action onHotkey, Action onQuit)
    {
        this.onHotkey = onHotkey;
        this.onQuit = onQuit;
        CreateHandle(new CreateParams());
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) onHotkey();                 // WM_HOTKEY
        else if (m.Msg == (int)WM_APP_QUIT) onQuit();
        base.WndProc(ref m);
    }
}

static class Native
{
    [DllImport("kernel32.dll")] public static extern bool SetProcessShutdownParameters(uint level, uint flags);

    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
    public const byte VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_END = 0x23, VK_HOME = 0x24, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    public const uint WM_INPUTLANGCHANGEREQUEST = 0x50;
    const uint KEYUP = 0x2, EXTENDED = 0x1;

    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetKeyboardLayoutList(int n, [Out] IntPtr[] list);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadKeyboardLayout(string klid, uint flags);

    public static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

    static void Key(byte vk, bool up)
    {
        uint flags = (up ? KEYUP : 0) | (vk == VK_HOME || vk == VK_END ? EXTENDED : 0);
        keybd_event(vk, 0, flags, UIntPtr.Zero);
    }

    public static void SendCombo(byte mod, byte key)
    {
        Key(mod, false); Key(key, false); Key(key, true); Key(mod, true);
    }

    public static void Tap(byte key)
    {
        Key(key, false); Key(key, true);
    }
}
