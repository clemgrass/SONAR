using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Sonar.Controller;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Events;
using Sonar.Core.Spatial;
using Sonar.Autopilot;
using Sonar.Game.Telemetry;

namespace Sonar.Game;

/// <summary>
/// Nodo raíz: carga la configuración, arma el escenario de apertura y hace de cable entre
/// el Controller y los nodos de presentación. No decide nada de la simulación.
/// </summary>
public partial class Main : Node
{
    private const int Seed = 20260811;

    private const double SubSpeed = 0.012;
    private static readonly Bearing Course = new(0);

    private readonly SessionMetrics _metrics = new();

    /// <summary>Último tick visto. Solo para el resumen de salida.</summary>
    private int _lastTick;

    private double _tickDuration;
    private int _magazineSize;

    private GameController _controller = null!;
    private SonarView _sonar = null!;
    private LineEdit _input = null!;
    private RichTextLabel _log = null!;
    private Label _hud = null!;
    private EventLog _eventLog = null!;
    private bool _announcedStart;

    private TelemetryWriter _telemetry = null!;
    private SessionRecorder _recorder = null!;
    private int _recordedAttempt;
    private bool _runEndRecorded;
    private string _lastDeathCause = "Unknown";

    // --- Modo demo: `godot-mono --path src/Sonar.Game -- --demo` ---
    private AutopilotRun? _demo;
    private double _demoSpeed = 1.0;
    private double _demoAccumulator;
    private bool _demoReported;

    /// <summary>
    /// Tope de ticks por frame en modo demo. Diez segundos de juego: suficiente para
    /// cualquier velocidad de reproducción razonable, y bajo para que un frame trabado no
    /// congele la ventana mientras la simulación se pone al día.
    /// </summary>
    private const int MaxDemoTicksPerFrame = 600;

    public override void _Ready()
    {
        _sonar = GetNode<SonarView>("%SonarView");
        _input = GetNode<LineEdit>("%CommandInput");
        _log = GetNode<RichTextLabel>("%Log");
        _hud = GetNode<Label>("%Hud");
        _eventLog = new EventLog(_log);

        var config = LoadConfig();
        _tickDuration = config.TickDurationSeconds;
        _magazineSize = config.MagazineSize;
        _sonar.SetGeometry(config.DeadZoneRadius, config.BandOuterRadii.ToArray(),
            config.SweepPeriodSeconds / config.TickDurationSeconds);

        // Las banderas se leen acá y no más abajo porque la semilla tiene que estar decidida
        // antes de construir el controller.
        var args = OS.GetCmdlineUserArgs();
        var seed = (int)ArgValue(args, "--seed=", Seed);

        // El director de oleadas vive dentro del GameController: acá sólo se le da rumbo y
        // avance, y él coloca los contactos cuando toca.
        _controller = new GameController(config, seed, Course, SubSpeed);

        _telemetry = new TelemetryWriter(config, DisplayServer.WindowGetSize());
        _recorder = new SessionRecorder(_telemetry);
        _recorder.RunStarted(_controller.Attempt, _controller.Seed);
        _recordedAttempt = _controller.Attempt;

        // El modo demo y el autotest del terminal son excluyentes: el primero teclea por el
        // piloto y el segundo inyecta teclas para comprobar que el LineEdit las recibe.
        if (System.Array.IndexOf(args, "--demo") >= 0)
        {
            var pace = ArgValue(args, "--pace=", 1.5);
            _demoSpeed = ArgValue(args, "--speed=", 1.0);
            _demo = new AutopilotRun(config, _controller, pace);
            _demo.CommandEmitted += OnAutopilotCommand;
            _recorder.Pilot(pace);
            _eventLog.WriteNow(Dim,
                $"AUTOPILOT · SEED {seed} · PACE {pace:F1}S · SPEED X{_demoSpeed:F0}");
        }

        _input.TextSubmitted += OnCommandSubmitted;
        _input.TextChanged += OnTextChanged;

        // Godot 4.4 separó el modo edición del foco. Al enviar con Enter, el LineEdit
        // conserva el foco pero SALE del modo edición y deja de aceptar teclas hasta que
        // lo reactives — de ahí el clic obligatorio para escribir el segundo comando.
        // HasFocus() seguía devolviendo true, que es justo lo que lo hacía difícil de ver.
        _input.KeepEditingOnTextSubmit = true;

        _input.GrabFocus();
        _input.Edit();

        _selfTest = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--m1-selftest") >= 0;

        // Sin ritmo y de golpe: es texto de referencia, y encolarlo lo haría desfilar
        // durante diez segundos delante de alguien que sólo quiere leerlo entero.
        foreach (var line in Briefing.Lines) _eventLog.WriteNow(Dim, line);

        // El aviso de privacidad va sin ritmo y de golpe, igual que el instructivo. El plan
        // original lo encolaba con Queue, pero eso lo haría desfilar diez segundos después,
        // mezclado con el resto del historial, en vez de estar a la vista desde el primer
        // frame junto a lo que explica qué hace el prototipo.
        //
        // Corta a propósito y sin la ruta: el panel envuelve pasados ~46 caracteres, y una
        // línea envuelta acá empuja el título del instructivo fuera de la pantalla. Dónde
        // queda el archivo está en docs/development/m1-findings.md.
        _eventLog.WriteNow(Dim, "RECORDS ANONYMOUS PLAY DATA · NO PERSONAL INFO");
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

    // --- Verificación del terminal: `godot-mono --path src/Sonar.Game -- --m1-selftest` ---
    //
    // Teclea dos comandos seguidos sin ningún clic entremedio y comprueba que ambos llegan.
    // Existe porque este camino ya se rompió una vez de una forma que engañaba: al enviar
    // con Enter el LineEdit conservaba el foco pero salía del modo edición, así que
    // HasFocus() decía true mientras las teclas se perdían. No hay forma de teclear desde
    // fuera del juego en este entorno, y ninguna prueba de xUnit alcanza a la escena.

    private bool _selfTest;
    private int _frame;
    private bool _sawStatus;
    private int _announcedWave;

    /// <summary>
    /// Guion tecleado, una tecla por paso. `\n` es Enter. Va en minúscula a propósito: así
    /// el arnés comprueba de paso la conversión a mayúsculas.
    /// </summary>
    private const string SelfTestScript = "start\nc047\nstatus\nretry\n";

    private const int SettleFrames = 30;
    private const int FramesPerKey = 3;

    private int _scriptIndex;

    private static void TypeKey(Key keycode, char unicode = '\0')
    {
        var down = new InputEventKey { Keycode = keycode, Pressed = true };
        if (unicode != '\0') down.Unicode = unicode;
        Input.ParseInputEvent(down);

        var up = (InputEventKey)down.Duplicate();
        up.Pressed = false;
        Input.ParseInputEvent(up);
    }

    private void RunSelfTest()
    {
        // El contador no avanza hasta que el terminal esté de verdad en modo edición.
        // Durante los primeros frames Godot todavía está levantando la ventana y pierde
        // eventos inyectados: contar desde ahí hacía fallar el arnés dos de cada tres
        // veces, y un guardián intermitente es peor que ninguno porque se termina ignorando.
        if (!_input.IsEditing()) return;

        _frame++;
        if (_frame < SettleFrames || _frame % FramesPerKey != 0) return;

        // Una tecla por paso, no la palabra entera de golpe. Inyectar varias en el mismo
        // frame perdía caracteres de forma intermitente. Es un artefacto de la inyección y
        // no algo que un humano pueda provocar tecleando — nadie escribe cuatro caracteres
        // en dieciséis milisegundos — pero bastaba para volver inútil al guardián.
        if (_scriptIndex < SelfTestScript.Length)
        {
            var c = SelfTestScript[_scriptIndex++];
            if (c == '\n') TypeKey(Key.Enter);
            else TypeKey((Key)char.ToUpperInvariant(c), c);
            return;
        }

        // Margen para que el último comando alcance a aplicarse en la simulación.
        if (_frame > SettleFrames + (SelfTestScript.Length + 20) * FramesPerKey) ReportSelfTest();
    }

    private void ReportSelfTest()
    {
        // Cuatro comandos: START, el disparo, STATUS y el RETRY que debe ser ignorado.
        // Que Started siga en true y Attempt en 1 es lo que fija que RETRY con el submarino
        // vivo no reinicie nada ni devuelva al instructivo.
        var expected = _controller.Snapshot.AmmoLoaded == 2;   // el disparo consumió uno
        var ok = _metrics.Commands == 4 && _metrics.ShotsFired == 1 && _sawStatus
                 && _controller.Started && _controller.Attempt == 1 && expected;

        GD.Print($"[SELFTEST] {(ok ? "PASA" : "FALLA")} · commands={_metrics.Commands}/4 " +
                 $"shots={_metrics.ShotsFired}/1 status={_sawStatus} " +
                 $"started={_controller.Started} attempt={_controller.Attempt}/1 " +
                 $"ammo={_controller.Snapshot.AmmoLoaded}/2");

        if (!ok) GD.PushError("El terminal perdió el segundo comando: se puede escribir uno solo sin clic.");

        GetTree().Quit(ok ? 0 : 1);
    }

    public override void _Process(double delta)
    {
        if (_selfTest) RunSelfTest();

        // RETRY reinicia la corrida dentro del controller sin emitir un evento propio: se
        // detecta comparando el número de intento contra el último que se registró.
        if (_controller.Attempt != _recordedAttempt)
        {
            _recordedAttempt = _controller.Attempt;
            _recorder.RunStarted(_controller.Attempt, _controller.Seed);
            _announcedWave = 0;
            _runEndRecorded = false;
            _lastDeathCause = "Unknown";
            _eventLog.Queue(Dim, $"— ATTEMPT {_controller.Attempt} —");
        }

        // Los eventos son cosas que pasaron y se consumen una vez; el snapshot es cómo
        // está el mundo ahora y se puede leer siempre. Ese orden importa.
        if (_demo is not null) AdvanceDemo(delta);
        else foreach (var evt in _controller.Advance(delta)) Report(evt);
        _eventLog.Advance(delta);

        // El terminal siempre tiene que estar listo para escribir, sin tener que tocarlo.
        // Las dos condiciones son distintas y hacen falta las dos: el foco se pierde al
        // hacer clic en otra parte, y el modo edición se pierde al enviar con Enter.
        //
        // Cuesta poder seleccionar texto del registro con el mouse. Es un intercambio
        // aceptado: no hay nada más con qué interactuar en esta escena, y un terminal que
        // deja de aceptar teclas es peor que un registro que no se puede copiar.
        if (!_input.HasFocus()) _input.GrabFocus();
        if (!_input.IsEditing()) _input.Edit();

        var snap = _controller.Snapshot;
        _lastTick = snap.Tick;
        _sonar.SetSnapshot(snap);

        // El director de oleadas no emite eventos —es guion, no simulación—, así que la
        // presentación detecta el cambio comparando.
        if (_controller.Wave != _announcedWave)
        {
            _announcedWave = _controller.Wave;
            _metrics.NoteWave(_announcedWave);
            Instrument($"— WAVE {_announcedWave} —");
            _recorder.WaveStarted(_announcedWave, snap.Tick, _controller.LiveEnemyCount);
        }

        _hud.Text =
            $"{MissionClock(snap.Tick)}    wave {_controller.Wave}    " +
            $"{Tubes(snap.AmmoLoaded)}    reserve {snap.AmmoReserve}    " +
            $"defenses {snap.DefensesAvailable}    hits {snap.HitsTaken}/2" +
            (snap.ReloadInProgress ? "    RELOADING" : string.Empty) +
            (snap.SweepRunning ? string.Empty : "    SWEEP STOPPED") +
            (snap.Destroyed ? "    DESTROYED — TYPE RETRY" : string.Empty);

        if (snap.Destroyed && !_runEndRecorded)
        {
            _runEndRecorded = true;
            _recorder.FlushWave(snap.Tick);
            _recorder.RunEnded(_controller.Attempt, snap.Tick, _lastDeathCause,
                _controller.Wave, _controller.LiveEnemyCount,
                snap.AmmoLoaded, snap.AmmoReserve, snap.DefensesAvailable);
        }
    }

    /// <summary>
    /// El mismo número que el tick, contado como tiempo en el puesto. Cambiar la unidad
    /// cambia lo que el número significa para quien lo lee: `tick 748` es salida de
    /// depuración, `00:12` es una guardia.
    /// </summary>
    private string MissionClock(int tick)
    {
        var seconds = tick * _tickDuration;
        return $"{(int)(seconds / 60):D2}:{(int)(seconds % 60):D2}";
    }

    /// <summary>
    /// `ammo 3/12` se leía como "te quedan 3 de 12". No es eso: son tres en los tubos y doce
    /// en la santabárbara, dos números sin relación separados por una barra que inventaba una.
    /// Los puntos dicen cuántos tubos hay listos sin obligar a leer un número.
    /// </summary>
    private string Tubes(int loaded)
    {
        var full = new string('*', loaded);
        var empty = new string('.', System.Math.Max(0, _magazineSize - loaded));
        return $"tubes {full}{empty}";
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
        _eventLog.Queue(Typed, $"> {command}");
        var recognised = _controller.SubmitInput(command);
        _recorder.Command(_controller.Snapshot.Tick, command, recognised,
            _metrics.LastComposeSeconds);

        if (_controller.Started && !_announcedStart)
        {
            _announcedStart = true;
            _eventLog.Queue(Dim, "  BEAM RUNNING · GOOD HUNTING");
        }

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
        _recorder.Observe(evt);

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
            case SweepCompleted e:
                Instrument($"SWEEP {e.Revolution} · {e.ReadingCount} CONTACTS");
                break;

            case StatusReported e:
                _sawStatus = true;
                Instrument($"AMMO {e.AmmoLoaded}/{e.AmmoReserve} · DEFENSES {e.Defenses}");
                break;
            case EnemyTorpedoFired: Instrument("LAUNCH TRANSIENT"); break;
            case EnemyTorpedoDestroyed: Instrument("INBOUND NEUTRALISED"); break;
            // Salta la cola como los impactos: un aviso de embestida que llega paceado,
            // detrás de ocho líneas de registro, llega tarde para lo único que sirve.
            case RammingRunDetected e:
                _eventLog.WriteNow(Dim, $"  !! FAST SCREWS CLOSING · RANGE {e.Distance:F2}");
                break;
            case SubHit e: _eventLog.WriteNow(Dim, $"  WE ARE HIT · {e.HitsTaken}/2"); break;
            case SubDestroyed e:
                _lastDeathCause = e.Cause.ToString();
                _eventLog.WriteNow(Dim, e.Cause == SubDeathCause.Collision
                    ? "  COLLISION · HULL BREACHED"
                    : "  HULL BREACHED");
                break;

            // NearMiss no se muestra: es base de la pista 2, sin efecto en M1 (spec §4.10).
            // InputNotRecognized ya quedó registrado al escribirlo.
        }
    }

    private const string Dim = "#5d8f74";     // registro de instrumento
    private const string Typed = "#fde047";   // registro de comando

    private void Instrument(string text) => _eventLog.Queue(Dim, $"  {text}");

    /// <summary>
    /// Valor numérico de una bandera `--clave=valor`, o el default. En cultura invariante:
    /// con la configuración regional de Chile el separador decimal es la coma y `--pace=1.5`
    /// se leería como 15.
    /// </summary>
    private static double ArgValue(string[] args, string prefix, double fallback)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith(prefix)) continue;
            if (double.TryParse(arg[prefix.Length..],
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                return value;
        }
        return fallback;
    }

    /// <summary>
    /// Avanza la corrida pilotada tick a tick, no con el delta del frame. El piloto decide por
    /// número de tick, así que un frame perdido hace la simulación más lenta pero nunca
    /// distinta: lo que se mira acá es exactamente la corrida que afirma TheWallTests.
    /// </summary>
    private void AdvanceDemo(double delta)
    {
        if (_demo!.Finished)
        {
            ReportDemoOnce();
            return;
        }

        _demoAccumulator += delta * _demoSpeed;

        var executed = 0;
        while (_demoAccumulator >= _tickDuration && executed < MaxDemoTicksPerFrame)
        {
            _demoAccumulator -= _tickDuration;
            executed++;

            _demo.StepOneTick();

            // Los eventos del tick que acaba de correr. StepOneTick llama a Advance por
            // dentro, así que hay que leerlos de LastEvents en vez de recibirlos.
            foreach (var evt in _controller.LastEvents) Report(evt);

            if (_demo.Finished) break;
        }

        if (_demoAccumulator >= _tickDuration) _demoAccumulator = 0;
    }

    /// <summary>
    /// El comando del bot entra por el mismo camino que uno tecleado: el registro y la
    /// telemetría no distinguen quién lo escribió, y eso es lo que hace que las corridas
    /// pilotadas entren al mismo corpus que las de los testers.
    ///
    /// NO se llama a _metrics.NoteSubmit(): SessionMetrics mide tiempo de composición humano,
    /// y un comando de cero segundos envenenaría el promedio de la pregunta 2 de M1. Los
    /// disparos y las muertes sí se cuentan, porque esos sí son reales — pasan por Report.
    /// </summary>
    private void OnAutopilotCommand(int tick, string text)
    {
        _eventLog.Queue(Typed, $"> {text}");
        _recorder.Command(tick, text, true, 0.0);
    }

    /// <summary>El reporte va a consola una sola vez, con el scope congelado en la muerte.</summary>
    private void ReportDemoOnce()
    {
        if (_demoReported) return;
        _demoReported = true;
        GD.Print(_demo!.BuildReport().Render());

        // Sin ventana no hay nadie mirando el scope congelado y el proceso quedaría corriendo
        // para siempre, que es lo que hace imposible verificar la demo desde un script. Con
        // ventana se deja abierto a propósito: el instante de la muerte es justamente lo que
        // hay que poder mirar con calma.
        if (DisplayServer.GetName() == "headless") GetTree().Quit(0);
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
        _telemetry.Dispose();

        // A la consola además del CSV: durante M1 conviene ver los números al cerrar sin
        // tener que ir a buscar el archivo.
        GD.Print(
            $"[M1] ticks {_lastTick} · commands {_metrics.Commands} · " +
            $"shots {_metrics.ShotsFired} · kills {_metrics.ContactsDestroyed} · " +
            $"hit rate {_metrics.HitRate:F2} · avg compose {_metrics.AverageComposeSeconds:F2}s");
    }
}
