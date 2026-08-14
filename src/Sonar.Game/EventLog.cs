using System.Collections.Generic;
using Godot;

namespace Sonar.Game;

/// <summary>
/// El historial con ritmo. Las líneas se encolan y salen de a una, en vez de volcarse
/// enteras en el frame en que ocurren.
///
/// Existe porque en el playtest no alcanzaba a leerse: el registro es la superficie donde va
/// a vivir la historia, y volcarlo de golpe lo convierte en salida de depuración.
/// </summary>
public sealed class EventLog
{
    /// <summary>Ritmo normal entre líneas.</summary>
    private const double IntervalSeconds = 0.25;

    /// <summary>
    /// Ritmo de emergencia. Sin él, en la oleada 8 el registro se atrasaría minutos y
    /// dejaría de describir lo que está pasando.
    /// </summary>
    private const double CatchUpSeconds = 0.08;

    private const int BacklogThreshold = 8;

    private readonly RichTextLabel _label;
    private readonly Queue<string> _queue = new();
    private double _sinceLast;

    public EventLog(RichTextLabel label) => _label = label;

    /// <summary>Encola una línea. Se muestra cuando le toque.</summary>
    public void Queue(string color, string content) => _queue.Enqueue(Format(color, content));

    /// <summary>
    /// Salta la cola y escribe de inmediato. Dos usos, por razones distintas:
    ///
    /// El impacto sobre el propio submarino, porque el §4.6 del spec lo declara ley del
    /// mundo — el resto del registro puede llegar tarde; que te peguen, no.
    ///
    /// Y el instructivo, porque es texto de referencia para leer al propio paso, no una
    /// secuencia con ritmo. Encolarlo lo haría desfilar delante de alguien que sólo quiere
    /// leerlo entero.
    /// </summary>
    public void WriteNow(string color, string content) => Emit(Format(color, content));

    public void Advance(double delta)
    {
        if (_queue.Count == 0) return;

        _sinceLast += delta;
        var interval = _queue.Count > BacklogThreshold ? CatchUpSeconds : IntervalSeconds;
        if (_sinceLast < interval) return;

        _sinceLast = 0;
        Emit(_queue.Dequeue());
    }

    /// <summary>
    /// El color va como etiqueta aparte del contenido: el historial se muestra todo en
    /// mayúscula, y pasar la línea entera por ToUpperInvariant convertiría [color=...] en
    /// [COLOR=...], que el parser de BBCode de Godot no reconoce.
    /// </summary>
    private static string Format(string color, string content) =>
        $"[color={color}]{content.ToUpperInvariant()}[/color]";

    private void Emit(string line)
    {
        _label.AppendText(line + "\n");
        _label.ScrollToLine(_label.GetLineCount() - 1);
    }
}
