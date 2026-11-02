// LayoutFix — מיפוי מקשים בין פריסה אנגלית לפריסה עברית סטנדרטית
using System.Text;

static class Converter
{
    // אותו מקש פיזי, באותו מיקום בשתי המחרוזות
    const string En = "qwertyuiop" + "asdfghjkl;'" + "zxcvbnm,./" + "`";
    const string He = "/'קראטוןםפ" + "שדגכעיחלךף," + "זסבהנמצתץ." + ";";

    public static bool ShouldConvertToHebrew(string text)
    {
        int heb = 0, lat = 0;
        foreach (char c in text)
        {
            if (c >= 'א' && c <= 'ת') heb++;
            else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) lat++;
        }
        return heb < lat;
    }

    public static string Convert(string text, bool toHebrew)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            int i;
            if (toHebrew)
            {
                i = En.IndexOf(char.ToLowerInvariant(c));
                sb.Append(i >= 0 ? He[i] : c);
            }
            else
            {
                i = He.IndexOf(c);
                sb.Append(i >= 0 ? En[i] : c);
            }
        }
        return sb.ToString();
    }
}
