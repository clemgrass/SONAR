using Sonar.Controller;
using Sonar.Core.Events;

namespace Sonar.Game.Telemetry;

/// <summary>
/// Traduce el flujo de eventos a líneas de telemetría. Vive aparte de Main para que agregar
/// una medición no signifique tocar el cableado de la escena.
///
/// No calcula nada sobre el mundo: todo lo que escribe viene ya medido desde Core. La única
/// excepción es el tiempo de composición, que es tiempo de pared y no existe en la
/// simulación.
/// </summary>
public sealed class SessionRecorder
{
    private readonly TelemetryWriter _writer;

    private int _wave;
    private int _waveStartTick;
    private int _waveShots;
    private int _waveKills;
    private int _lastCommandTick = -1;

    public SessionRecorder(TelemetryWriter writer) => _writer = writer;

    public void RunStarted(int attempt, int seed) =>
        _writer.Write(new JsonLine("run").Add("n", attempt).Add("seed", seed));

    public void RunEnded(int attempt, int tick, string cause, int wave,
        int liveEnemies, int ammoLoaded, int ammoReserve, int defenses)
    {
        _writer.Write(new JsonLine("runEnd")
            .Add("n", attempt).Add("tick", tick).Add("cause", cause)
            .Add("wave", wave).Add("liveEnemies", liveEnemies)
            .Add("ammoLoaded", ammoLoaded).Add("ammoReserve", ammoReserve)
            .Add("defenses", defenses));
    }

    /// <summary>
    /// Texto crudo tal como se escribió. Los typos son dato, no ruido.
    ///
    /// `gapTicks` es el silencio desde el comando anterior. Con eso se puede preguntar si el
    /// ritmo funciona — y en particular si teclean algo durante los diez segundos iniciales,
    /// que sería señal de que el silencio se lee como que el juego está roto.
    /// </summary>
    public void Command(int tick, string raw, bool recognised, double composeSeconds)
    {
        _writer.Write(new JsonLine("cmd")
            .Add("tick", tick).Add("raw", raw)
            .Add("ok", recognised).Add("compose", composeSeconds)
            .Add("gapTicks", _lastCommandTick < 0 ? -1 : tick - _lastCommandTick));

        _lastCommandTick = tick;
    }

    public void WaveStarted(int wave, int tick, int spawned)
    {
        FlushWave(tick);
        _wave = wave;
        _waveStartTick = tick;
        _waveShots = 0;
        _waveKills = 0;
        _writer.Write(new JsonLine("wave").Add("n", wave).Add("tick", tick).Add("spawned", spawned));
    }

    /// <summary>Cierra la oleada en curso con su resumen. Idempotente si no hay ninguna.</summary>
    public void FlushWave(int tick)
    {
        if (_wave == 0) return;
        _writer.Write(new JsonLine("waveEnd")
            .Add("n", _wave).Add("ticks", tick - _waveStartTick)
            .Add("shots", _waveShots).Add("kills", _waveKills));
        _wave = 0;
    }

    public void Observe(GameEvent evt)
    {
        switch (evt)
        {
            case ShotAssessed e:
                _waveShots++;
                _writer.Write(new JsonLine("shot")
                    .Add("tick", e.Tick).Add("torpedo", e.TorpedoId)
                    .Add("nearest", e.NearestContactId)
                    .Add("errDeg", e.ErrorDegrees).Add("errRad", e.ErrorRadius)
                    .Add("ageTicks", e.ReadingAgeTicks));
                break;

            case TorpedoFired e:
                // Se registran los dos eventos del mismo disparo. Un `fired` sin su `shot`
                // es un tiro al vacío —no había contra qué medirlo—, y sin esta línea esos
                // disparos desaparecerían de los conteos.
                _writer.Write(new JsonLine("fired")
                    .Add("tick", e.Tick).Add("torpedo", e.TorpedoId)
                    .Add("brg", e.BearingDegrees).Add("band", e.BandLetter.ToString()));
                break;

            case ContactDestroyed e:
                _waveKills++;
                _writer.Write(new JsonLine("kill")
                    .Add("tick", e.Tick).Add("contact", e.ContactId)
                    .Add("cause", e.Cause.ToString()).Add("torpedo", e.TorpedoId)
                    .Add("personality", e.Personality ?? "none"));
                break;

            case TorpedoDetonated e:
                _writer.Write(new JsonLine("shotEnd")
                    .Add("tick", e.Tick).Add("torpedo", e.TorpedoId)
                    .Add("hit", e.HitSomething));
                break;

            case NearMiss e:
                _writer.Write(new JsonLine("nearMiss")
                    .Add("tick", e.Tick).Add("contact", e.ContactId).Add("dist", e.Distance));
                break;

            case FireRejected e:
                _writer.Write(new JsonLine("fireRejected")
                    .Add("tick", e.Tick).Add("reason", e.Reason.ToString()));
                break;

            case ReloadStarted e:
                _writer.Write(new JsonLine("reload").Add("tick", e.Tick));
                break;

            case DefenseDeployed e:
                _writer.Write(new JsonLine("defense").Add("tick", e.Tick).Add("id", e.DefenseId));
                break;

            case DefenseDetonated e:
                _writer.Write(new JsonLine("defenseSpent")
                    .Add("tick", e.Tick).Add("id", e.DefenseId)
                    .Add("destroyed", e.TargetsDestroyed));
                break;

            case StatusReported e:
                // Pedirlo mucho significa que el HUD no comunica.
                _writer.Write(new JsonLine("status").Add("tick", e.Tick));
                break;

            case SonarToggled e:
                _writer.Write(new JsonLine("sweepToggle")
                    .Add("tick", e.Tick).Add("running", e.Running));
                break;

            case SubHit e:
                _writer.Write(new JsonLine("hit").Add("tick", e.Tick).Add("taken", e.HitsTaken));
                break;

            case EnemyTorpedoFired e:
                _writer.Write(new JsonLine("enemyFire")
                    .Add("tick", e.Tick).Add("torpedo", e.TorpedoId)
                    .Add("contact", e.SourceContactId)
                    .Add("personality", e.Personality ?? "none"));
                break;

            // El que dice si la coordinación entre enemigos funciona. Es invisible en
            // pantalla: sin medirlo no hay forma de distinguir "se están cuidando entre
            // ellos" de "dejaron de disparar". Va muestreado cada 60 ticks, así que es un
            // contador RELATIVO — sirve para comparar oleadas y sesiones, no para contar
            // ocasiones.
            case FireHeldForFriendly e:
                _writer.Write(new JsonLine("heldFire")
                    .Add("tick", e.Tick).Add("contact", e.ContactId)
                    .Add("blockedBy", e.BlockedByContactId));
                break;

            case RammingRunDetected e:
                _writer.Write(new JsonLine("ramClosing")
                    .Add("tick", e.Tick).Add("contact", e.ContactId).Add("range", e.Distance));
                break;

            // Cuántos torpedos mueren chocando con otro. Con enemigos que fallan, el agua se
            // llena de torpedos cruzando en diagonal y esto deja de ser una rareza: hay que
            // saber cuántos de los del jugador se pierden así antes de que alguien lo reporte
            // como "me desapareció el tiro".
            case EnemyTorpedoDestroyed e when e.Cause == DestructionCause.TorpedoCollision:
                _writer.Write(new JsonLine("torpedoCollision")
                    .Add("tick", e.Tick).Add("torpedo", e.TorpedoId));
                break;
        }
    }
}
