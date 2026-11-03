using System;
using System.IO;

static class Test
{
    static int fails;

    static void Log(bool ok, string msg)
    {
        File.AppendAllText(Environment.GetEnvironmentVariable("LF_OUT"), (ok ? "OK   " : "FAIL ") + msg + "\n");
        if (!ok) fails++;
    }

    // המרה של טקסט מסומן
    static void Check(string input, string expected)
    {
        string got = Converter.Convert(input, Converter.ShouldConvertToHebrew(input));
        Log(got == expected, "[selection] " + input + " -> " + got + (got == expected ? "" : "  (expected " + expected + ")"));
    }

    // המרת המילה האחרונה, כמו שהאפליקציה מרכיבה את ההדבקה
    static void CheckLastWord(string line, string expected)
    {
        string b, w, a;
        Converter.SplitLastWord(line, out b, out w, out a);
        string got = Converter.CanConvert(w)
            ? b + Converter.Convert(w, Converter.ShouldConvertToHebrew(w)) + a
            : "(no change)";
        Log(got == expected, "[last word] '" + line + "' -> '" + got + "'" + (got == expected ? "" : "  (expected '" + expected + "')"));
    }

    static void CheckSplit(string line, string eb, string ew, string ea)
    {
        string b, w, a;
        Converter.SplitLastWord(line, out b, out w, out a);
        bool ok = b == eb && w == ew && a == ea;
        Log(ok, "[split] '" + line + "' -> '" + b + "' | '" + w + "' | '" + a + "'");
    }

    static int Main()
    {
        Check("akuo", "שלום");
        Check("AKUO", "שלום");
        Check("ehv nv, 123", "קיה מהת 123");
        Check("יקךךם", "hello");
        Check("שלום", "akuo");
        Check("t,h", "אתי");

        CheckSplit("hello akuo", "hello ", "akuo", "");
        CheckSplit("akuo", "", "akuo", "");
        CheckSplit("שלום t,h  ", "שלום ", "t,h", "  ");
        CheckSplit("", "", "", "");
        CheckSplit("   ", "", "", "   ");
        CheckSplit("a\tb", "a\t", "b", "");

        CheckLastWord("shalom akuo", "shalom שלום");
        CheckLastWord("שלום t,h", "שלום אתי");
        CheckLastWord("שלום t,h ", "שלום אתי ");
        CheckLastWord("I said יקךךם", "I said hello");
        CheckLastWord("abc 123", "(no change)");
        CheckLastWord("", "(no change)");
        CheckLastWord("   ", "(no change)");
        CheckLastWord("akuo", "שלום");
        return fails;
    }
}
