using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Sonar.Game;

/// <summary>
/// Mide la pregunta 2 de M1 — tiempo medio por comando — y la tasa de acierto que sirve
/// de proxy para la pregunta 1. Escribe a user:// al cerrar.
///
/// No mide nada de presentación a propósito: si M1 dice que extrapolar no es divertido,
/// tiene que poder decirlo con la UI fea que tenga en ese momento.
/// </summary>
public sealed class SessionMetrics
{
    private readonly List<double> _composeTimes = new();
    private ulong _composeStartedMsec;
    private bool _composing;
    private bool _written;

    public int ShotsFired { get; private set; }
    public int ContactsDestroyed { get; private set; }
    public int Commands => _composeTimes.Count;

    public double AverageComposeSeconds =>
        _composeTimes.Count == 0 ? 0 : _composeTimes.Average();

    public double HitRate => ShotsFired == 0 ? 0 : (double)ContactsDestroyed / ShotsFired;

    /// <summary>Primera tecla tras el último Enter: arranca el cronómetro de composición.</summary>
    public void NoteKeystroke()
    {
        if (_composing) return;
        _composing = true;
        _composeStartedMsec = Time.GetTicksMsec();
    }

    public void NoteSubmit()
    {
        if (!_composing) return;
        _composeTimes.Add((Time.GetTicksMsec() - _composeStartedMsec) / 1000.0);
        _composing = false;
    }

    public void NoteShot() => ShotsFired++;
    public void NoteKill() => ContactsDestroyed++;

    /// <summary>
    /// Idempotente a propósito: conviene llamarla tanto desde NotificationWMCloseRequest
    /// como desde _ExitTree, porque cerrar la ventana y detener desde el editor (F8) no
    /// disparan la misma notificación, y una sesión medida que no se guarda es una sesión
    /// perdida.
    /// </summary>
    public void WriteReport()
    {
        if (_written) return;
        _written = true;

        using var file = FileAccess.Open("user://m1-session.csv", FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushError("No se pudo escribir user://m1-session.csv");
            return;
        }

        file.StoreLine("metric,value");
        file.StoreLine($"commands,{_composeTimes.Count}");
        file.StoreLine($"avg_compose_seconds,{Format(AverageComposeSeconds)}");
        file.StoreLine($"shots_fired,{ShotsFired}");
        file.StoreLine($"contacts_destroyed,{ContactsDestroyed}");
        file.StoreLine($"hit_rate,{Format(HitRate)}");
    }

    // Cultura invariante: con la configuración regional de Chile el separador decimal es
    // la coma, que en un CSV parte la columna en dos.
    private static string Format(double value) =>
        value.ToString("F3", CultureInfo.InvariantCulture);
}
