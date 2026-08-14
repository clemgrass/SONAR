using System.Globalization;
using System.Text;

namespace Sonar.Game.Telemetry;

/// <summary>
/// Construye una línea JSON a mano. Se hace así y no con un serializador por objeto porque
/// los eventos tienen campos distintos entre sí y definir un tipo por cada uno sería más
/// código que valor.
///
/// Todos los números salen en cultura invariante: con la configuración regional de Chile el
/// separador decimal es la coma, que rompería el JSON.
/// </summary>
public sealed class JsonLine
{
    private readonly StringBuilder _sb = new("{");
    private bool _first = true;

    public JsonLine(string type) => Add("t", type);

    public JsonLine Add(string key, string value) => Field(key, $"\"{Escape(value)}\"");
    public JsonLine Add(string key, int value) => Field(key, value.ToString(CultureInfo.InvariantCulture));
    public JsonLine Add(string key, bool value) => Field(key, value ? "true" : "false");

    public JsonLine Add(string key, double value) =>
        Field(key, value.ToString("0.####", CultureInfo.InvariantCulture));

    /// <summary>Inserta JSON ya formado, para anidar la configuración completa.</summary>
    public JsonLine Raw(string key, string json) => Field(key, json);

    private JsonLine Field(string key, string value)
    {
        if (!_first) _sb.Append(',');
        _first = false;
        _sb.Append('"').Append(Escape(key)).Append("\":").Append(value);
        return this;
    }

    /// <summary>
    /// Escapa lo mínimo que puede aparecer en este archivo. El texto crudo que teclea el
    /// jugador entra acá, así que comillas y contrabarras no son hipotéticas.
    /// </summary>
    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    public override string ToString() => _sb.ToString() + "}";
}
