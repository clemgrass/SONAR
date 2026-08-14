using System;
using System.Text.Json;
using Godot;
using Sonar.Core;

namespace Sonar.Game.Telemetry;

/// <summary>
/// Registro de eventos en JSONL, una línea por evento, escrito incrementalmente.
///
/// Incremental y no al cerrar: un cierre forzado perdería la sesión entera, y con oleadas y
/// RETRY las sesiones se alargan.
///
/// Guarda eventos y no promedios: de los eventos se derivan los agregados, y al revés no.
/// Con veinte testers, las preguntas que todavía no se le ocurren a nadie se contestan
/// después sobre los mismos datos.
///
/// **Nada personal.** Id anónimo por sesión, sin nombre de usuario, sin rutas, sin nombre de
/// máquina.
/// </summary>
public sealed class TelemetryWriter : IDisposable
{
    /// <summary>Sube cuando cambie la forma del archivo, para poder mezclar sesiones viejas.</summary>
    private const int Schema = 1;

    public const string Path = "user://m1-session.jsonl";

    private readonly FileAccess? _file;

    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..8];

    public TelemetryWriter(SimulationConfig config, Vector2I viewport)
    {
        _file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        if (_file is null)
        {
            GD.PushError($"No se pudo abrir {Path} para telemetría");
            return;
        }

        // La configuración completa va en la cabecera: sin ella, dos testers con tuning
        // distinto producen datos que no se pueden juntar.
        Write(new JsonLine("session")
            .Add("schema", Schema)
            .Add("id", SessionId)
            .Add("startedUtc", DateTime.UtcNow.ToString("s"))
            .Add("viewportW", viewport.X)
            .Add("viewportH", viewport.Y)
            .Raw("config", JsonSerializer.Serialize(config)));
    }

    public void Write(JsonLine line)
    {
        if (_file is null) return;
        _file.StoreLine(line.ToString());
        _file.Flush();
    }

    public void Dispose() => _file?.Close();
}
