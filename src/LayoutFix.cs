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
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

static class Program
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (var mutex = new Mutex(true, "LayoutFix.SingleInstance.RB", out created))
        {
            if (!created)
            {
                MessageBox.Show("LayoutFix כבר פועל (הסמל נמצא ליד השעון).", "LayoutFix",
                    MessageBoxButtons.OK, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                return;
            }
            Application.EnableVisualStyles();
            Application.Run(new TrayApp());
        }
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

    public TrayApp()
    {
        hotkeyWindow = new HotkeyWindow(OnHotkey);
        bool ok = Native.RegisterHotKey(hotkeyWindow.Handle, HOTKEY_ID,
            Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.Space);

        var menu = new ContextMenuStrip();
        menu.RightToLeft = RightToLeft.Yes;
        var title = new ToolStripMenuItem("LayoutFix · Ctrl+Alt+Space");
        title.Enabled = false;
        autoStartItem = new ToolStripMenuItem("הפעלה אוטומטית עם Windows");
        autoStartItem.Checked = IsAutoStart();
        autoStartItem.Click += delegate { SetAutoStart(!autoStartItem.Checked); autoStartItem.Checked = IsAutoStart(); };
        var exitItem = new ToolStripMenuItem("יציאה");
        exitItem.Click += delegate { ExitThread(); };
        menu.Items.Add(title);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(autoStartItem);
        menu.Items.Add(exitItem);

        tray = new NotifyIcon();
        tray.Icon = MakeIcon();
        tray.Text = "LayoutFix – Ctrl+Alt+Space";
        tray.ContextMenuStrip = menu;
        tray.Visible = true;

        if (ok)
            tray.ShowBalloonTip(3000, "LayoutFix פועל",
                "מסמנים טקסט ולוחצים Ctrl+Alt+Space כדי להמיר עברית ↔ אנגלית.", ToolTipIcon.Info);
        else
            tray.ShowBalloonTip(6000, "LayoutFix",
                "הקיצור Ctrl+Alt+Space תפוס על ידי תוכנה אחרת. סגרי אותה והפעילי שוב.", ToolTipIcon.Warning);
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
        if (string.IsNullOrEmpty(text))
        {
            // אין סימון — מסמנים את המילה האחרונה
            Native.SendCombo2(Native.VK_CONTROL, Native.VK_SHIFT, Native.VK_LEFT);
            Thread.Sleep(40);
            Native.SendCombo(Native.VK_CONTROL, (byte)'C');
            text = WaitForClipboardText(400);
        }
        if (string.IsNullOrEmpty(text)) { RestoreClipboard(saved); return; }

        bool toHebrew = Converter.ShouldConvertToHebrew(text);
        string fixedText = Converter.Convert(text, toHebrew);
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

    static IDataObject SnapshotClipboard()
    {
        var copy = new DataObject();
        bool any = false;
        TryClipboard(delegate
        {
            IDataObject cur = Clipboard.GetDataObject();
            if (cur == null) return;
            foreach (string f in cur.GetFormats(false))
            {
                try
                {
                    object d = cur.GetData(f, false);
                    if (d != null) { copy.SetData(f, false, d); any = true; }
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

    static void SetAutoStart(bool on)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
            else k.DeleteValue(RunName, false);
        }
    }

    // ---------- tray icon: א/A keycap ----------

    static Icon MakeIcon()
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var b = new SolidBrush(Color.FromArgb(47, 91, 211)))
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
    readonly Action onHotkey;
    public HotkeyWindow(Action onHotkey)
    {
        this.onHotkey = onHotkey;
        CreateHandle(new CreateParams());
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) onHotkey(); // WM_HOTKEY
        base.WndProc(ref m);
    }
}

static class Native
{
    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
    public const byte VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LEFT = 0x25, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
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
        uint flags = (up ? KEYUP : 0) | (vk == VK_LEFT ? EXTENDED : 0);
        keybd_event(vk, 0, flags, UIntPtr.Zero);
    }

    public static void SendCombo(byte mod, byte key)
    {
        Key(mod, false); Key(key, false); Key(key, true); Key(mod, true);
    }

    public static void SendCombo2(byte mod1, byte mod2, byte key)
    {
        Key(mod1, false); Key(mod2, false); Key(key, false); Key(key, true); Key(mod2, true); Key(mod1, true);
    }
}
