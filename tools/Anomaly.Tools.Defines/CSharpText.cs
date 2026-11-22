using System.Globalization;
using System.Text;

namespace Anomaly.Tools.Defines;

/// <summary>C# literal and comment text, written the same way on every machine.</summary>
public static class CSharpText
{
    /// <summary>A regular C# string literal; characters outside printable ASCII are written as <c>\uXXXX</c>.</summary>
    public static string Literal(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                default:
                    if (c < 0x20 || c > 0x7E)
                        sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>The shortest C# <c>float</c> literal that round-trips to <paramref name="v"/>.</summary>
    public static string FloatLiteral(float v)
    {
        if (float.IsPositiveInfinity(v))
            return "float.PositiveInfinity";
        if (float.IsNegativeInfinity(v))
            return "float.NegativeInfinity";
        if (float.IsNaN(v))
            return "float.NaN";
        return v.ToString("R", CultureInfo.InvariantCulture) + "f";
    }

    /// <summary>Text for a <c>//</c> comment: one line.</summary>
    public static string CommentLine(string text) => text.Replace("\r", "").Replace("\n", "\\n");

    /// <summary>Text for an XML doc comment: escaped, one entry per line.</summary>
    public static IEnumerable<string> XmlDocLines(string text) =>
        text.Split('\n').Select(l => l.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"));
}
