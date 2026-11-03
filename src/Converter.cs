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

    // מפצל טקסט ל: מה שלפני המילה האחרונה / המילה האחרונה / רווחים בסוף.
    // "מילה" = רצף תווים שאינם רווח, כולל , . ; ' (שהם אותיות בפריסה העברית)
    public static void SplitLastWord(string text, out string before, out string word, out string after)
    {
        int end = text.Length;
        while (end > 0 && char.IsWhiteSpace(text[end - 1])) end--;
        int start = end;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        before = text.Substring(0, start);
        word = text.Substring(start, end - start);
        after = text.Substring(end);
    }

    // האם יש במילה לפחות תו אחד שיש לו מקבילה בפריסה השנייה
    public static bool CanConvert(string word)
    {
        foreach (char c in word)
            if (En.IndexOf(char.ToLowerInvariant(c)) >= 0 || He.IndexOf(c) >= 0) return true;
        return false;
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
