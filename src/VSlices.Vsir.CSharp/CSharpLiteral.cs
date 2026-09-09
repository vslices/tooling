using System.Globalization;
using System.Text;

namespace VSlices.Vsir.CSharp;

internal static class CSharpLiteral
{
    public static string String(string value)
    {
        var result = new StringBuilder(value.Length + 2);
        result.Append('"');

        foreach (var character in value)
        {
            switch (character)
            {
                case '\\': result.Append("\\\\"); break;
                case '"': result.Append("\\\""); break;
                case '\r': result.Append("\\r"); break;
                case '\n': result.Append("\\n"); break;
                case '\t': result.Append("\\t"); break;
                case '\0': result.Append("\\0"); break;
                case '\b': result.Append("\\b"); break;
                case '\f': result.Append("\\f"); break;
                case '\v': result.Append("\\v"); break;
                default:
                    if (char.IsControl(character))
                    {
                        result.Append("\\u");
                        result.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        result.Append(character);
                    }
                    break;
            }
        }

        result.Append('"');
        return result.ToString();
    }
}
