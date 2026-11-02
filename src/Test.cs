using System;
using System.IO;

static class Test
{
    static int Check(string input, string expected)
    {
        bool toHe = Converter.ShouldConvertToHebrew(input);
        string got = Converter.Convert(input, toHe);
        bool ok = got == expected;
        File.AppendAllText(Environment.GetEnvironmentVariable("LF_OUT"),
            (ok ? "OK   " : "FAIL ") + input + " -> " + got + (ok ? "" : "  (expected " + expected + ")") + "\n");
        return ok ? 0 : 1;
    }

    static int Main()
    {
        int f = 0;
        f += Check("akuo", "שלום");
        f += Check("AKUO", "שלום");
        f += Check("ehv nv, 123", "קיה מהת 123");
        f += Check("יקךךם", "hello");
        f += Check("שלום", "akuo");
        f += Check("t,h", "אתי");
        return f;
    }
}
