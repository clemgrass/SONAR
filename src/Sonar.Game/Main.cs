using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Sonar.Controller;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;

namespace Sonar.Game;

/// <summary>
/// Nodo raíz: carga la configuración, arma el escenario de apertura y hace de cable entre
/// el Controller y los nodos de presentación. No decide nada de la simulación.
/// </summary>
public partial class Main : Node
{
    private const int Seed = 20260811;

    private readonly SessionMetrics _metrics = new();

    /// <summary>Último tick visto. Solo para el resumen de salida.</summary>
    private int _lastTick;

    private GameController _controller = null!;
    private SonarView _sonar = null!;
    private LineEdit _input = null!;
    private RichTextLabel _log = null!;
    private Label _hud = null!;

    public override void _Ready()
    {
        _sonar = GetNode<SonarView>("%SonarView");
        _input = GetNode<LineEdit>("%CommandInput");
        _log = GetNode<RichTextLabel>("%Log");
        _hud = GetNode<Label>("%Hud");

        var config = LoadConfig();
        _sonar.SetGeometry(config.DeadZoneRadius, config.BandOuterRadii.ToArray());

        _controller = new GameController(
            config, Seed, OpeningScenario.Course, OpeningScenario.SubSpeed);
        _controller.SetContacts(OpeningScenario.Contacts(config));

        _input.TextSubmitted += OnCommandSubmitted;
        _input.TextChanged += OnTextChanged;
        _input.GrabFocus();

        Append(Dim, "SONAR M1 — VALIDATION PROTOTYPE");
        Append(Dim, "TYPE C047 AND PRESS ENTER · RELOAD · SONAR · STATUS · DEF C047");
    }

    /// <summary>
    /// La configuración vive fuera del proyecto de Godot, en config/ de la raíz del repo,
    /// para poder ajustar valores y volver a jugar sin tocar el juego — que es el ciclo
    /// entero de M1. Se lee por ruta absoluta porque res:// no sale del proyecto.
    /// </summary>
    private static SimulationConfig LoadConfig()
    {
        var path = Path.GetFullPath(Path.Combine(
            ProjectSettings.GlobalizePath("res://"), "..", "..", "config", "simulation.working.json"));

        if (!File.Exists(path))
        {
            // Sin config no hay prototipo. Que reviente acá y no con números raros después.
            GD.PushError($"No se encontró la configuración en {path}");
            throw new FileNotFoundException("Falta config/simulation.working.json", path);
        }

        return SimulationConfigLoader.LoadFromJson(File.ReadAllText(path));
    }

    public override void _Process(double delta)
    {
        // Los eventos son cosas que pasaron y se consumen una vez; el snapshot es cómo
        // está el mundo ahora y se puede leer siempre. Ese orden importa.
        foreach (var evt in _controller.Advance(delta)) Report(evt);

        var snap = _controller.Snapshot;
        _lastTick = snap.Tick;
        _sonar.SetSnapshot(snap);

        _hud.Text =
            $"tick {snap.Tick}    ammo {snap.AmmoLoaded}/{snap.AmmoReserve}    " +
            $"defenses {snap.DefensesAvailable}    hits {snap.HitsTaken}/2" +
            (snap.ReloadInProgress ? "    RELOADING" : string.Empty) +
            (snap.SweepRunning ? string.Empty : "    SWEEP STOPPED") +
            (snap.Destroyed ? "    DESTROYED" : string.Empty);
    }

    /// <summary>
    /// El terminal es de 1945: todo va en mayúscula. Se fuerza acá y no con una fuente en
    /// versalitas porque el texto tiene que llegar en mayúscula también al historial.
    /// Godot no emite text_changed al asignar Text por código, así que no hay recursión;
    /// la guarda de igualdad está igual, porque depender de eso es frágil.
    /// </summary>
    private void OnTextChanged(string text)
    {
        _metrics.NoteKeystroke();

        var upper = text.ToUpperInvariant();
        if (upper == text) return;

        var caret = _input.CaretColumn;
        _input.Text = upper;
        _input.CaretColumn = caret;
    }

    private void OnCommandSubmitted(string text)
    {
        _metrics.NoteSubmit();

        var command = text.ToUpperInvariant();
        Append(Typed, $"> {command}");
        _controller.SubmitInput(command);

        _input.Clear();

        // Volver a modo escritura sin tocar nada. Escribir un comando y tener que hacer
        // clic para escribir el siguiente rompe el ritmo entero del terminal.
        _input.GrabFocus();
    }

    /// <summary>
    /// Cuatro registros visuales, el cuarto reservado (spec §4.7). Aquí solo existen los
    /// dos primeros: comando e instrumento. El registro sin firmar no se usa en M1 — que
    /// esté vacío es el punto.
    /// </summary>
    private void Report(GameEvent evt)
    {
        switch (evt)
        {
            case TorpedoFired e:
                _metrics.NoteShot();
                Instrument($"TORPEDO {e.TorpedoId} · {e.BandLetter}{e.BearingDegrees:D3}");
                break;
            case ContactDestroyed e:
                _metrics.NoteKill();
                Instrument($"HIT · CONTACT {e.ContactId} DESTROYED · {e.Cause}");
                break;
            case TorpedoDetonated { HitSomething: false }:
                Instrument("NO CONTACT · DETONATION IN OPEN WATER");
                break;
            case FireRejected e: Instrument($"FIRE REJECTED · {e.Reason}"); break;
            case DefenseDeployed e: Instrument($"DEFENSE {e.DefenseId} DEPLOYED"); break;
            case DefenseRejected e: Instrument($"DEFENSE REJECTED · {e.Reason}"); break;
            case DefenseDetonated e:
                Instrument($"DEFENSE {e.DefenseId} SPENT · {e.TargetsDestroyed} DESTROYED");
                break;
            case ReloadStarted: Instrument("RELOADING"); break;
            case ReloadCompleted e: Instrument($"RELOAD COMPLETE · {e.AmmoLoaded}"); break;
            case SonarToggled e: Instrument(e.Running ? "SWEEP RUNNING" : "SWEEP STOPPED"); break;
            case StatusReported e:
                Instrument($"AMMO {e.AmmoLoaded}/{e.AmmoReserve} · DEFENSES {e.Defenses}");
                break;
            case EnemyTorpedoFired: Instrument("LAUNCH TRANSIENT"); break;
            case EnemyTorpedoDestroyed: Instrument("INBOUND NEUTRALISED"); break;
            case SubHit e: Instrument($"WE ARE HIT · {e.HitsTaken}/2"); break;
            case SubDestroyed: Instrument("HULL BREACHED"); break;

            // NearMiss no se muestra: es base de la pista 2, sin efecto en M1 (spec §4.10).
            // InputNotRecognized ya quedó registrado al escribirlo.
        }
    }

    private const string Dim = "#5d8f74";     // registro de instrumento
    private const string Typed = "#fde047";   // registro de comando

    private void Instrument(string text) => Append(Dim, $"  {text}");

    /// <summary>
    /// El color va como etiqueta aparte del contenido a propósito: el historial se muestra
    /// todo en mayúscula, y pasar la línea entera por ToUpperInvariant convertiría
    /// [color=...] en [COLOR=...], que el parser de BBCode de Godot no reconoce.
    /// </summary>
    private void Append(string color, string content)
    {
        _log.AppendText($"[color={color}]{content.ToUpperInvariant()}[/color]\n");
        _log.ScrollToLine(_log.GetLineCount() - 1);
    }

    // Cerrar la ventana y detener desde el editor no disparan la misma notificación, y una
    // sesión medida que no se guarda es una sesión perdida. WriteReport es idempotente.
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) _metrics.WriteReport();
    }

    public override void _ExitTree()
    {
        _metrics.WriteReport();

        // A la consola además del CSV: durante M1 conviene ver los números al cerrar sin
        // tener que ir a buscar el archivo.
        GD.Print(
            $"[M1] ticks {_lastTick} · commands {_metrics.Commands} · " +
            $"shots {_metrics.ShotsFired} · kills {_metrics.ContactsDestroyed} · " +
            $"hit rate {_metrics.HitRate:F2} · avg compose {_metrics.AverageComposeSeconds:F2}s");
    }
}
