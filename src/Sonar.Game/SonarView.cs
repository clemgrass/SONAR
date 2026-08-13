using Godot;
using Sonar.Controller.Presentation;

namespace Sonar.Game;

/// <summary>
/// Dibuja el snapshot. No sabe nada de reglas: no decide si un disparo acierta, no mide
/// tiempo, no genera azar. Solo pinta lo que el Controller le entrega.
///
/// Deliberadamente feo: M1 valida si extrapolar lecturas es divertido, no si se ve bien.
/// Pulir esto es señal de haberse salido del milestone.
/// </summary>
public partial class SonarView : Node2D
{
    private static readonly Color Grid = new(0.29f, 0.87f, 0.50f, 0.28f);
    private static readonly Color GridStrong = new(0.35f, 0.95f, 0.58f, 0.80f);
    private static readonly Color Graticule = new(0.29f, 0.87f, 0.50f, 0.14f);
    private static readonly Color DeadZone = new(1.0f, 0.42f, 0.42f, 0.35f);
    private static readonly Color Label = new(0.78f, 1.0f, 0.88f);
    private static readonly Color MinorLabel = new(0.60f, 0.85f, 0.72f);
    private static readonly Color ContactColor = new(0.36f, 1.0f, 0.61f);
    private static readonly Color OwnTorpedo = new(1.0f, 0.82f, 0.36f);
    private static readonly Color EnemyTorpedo = new(1.0f, 0.36f, 0.36f);
    private static readonly Color DefenseColor = new(0.56f, 0.83f, 1.0f);
    private static readonly Color SubColor = new(0.95f, 1.0f, 0.97f);

    /// <summary>
    /// Radio del scope en píxeles. Con el centro en (400, 380) del viewport de 1280x800,
    /// el anillo de rótulos llega hasta y≈690 y el campo de comandos arranca en 736:
    /// crecer este número los hace chocar.
    /// </summary>
    private const float Radius = 290f;

    /// <summary>Marcaciones múltiplo de esto llevan rótulo grande y marca larga.</summary>
    private const int MajorEvery = 30;

    /// <summary>Paso de los rótulos de marcación. Cada uno es un número en el anillo.</summary>
    private const int LabelEvery = 5;

    /// <summary>Paso de las radiales punteadas que ayudan a estimar la marcación a ojo.</summary>
    private const int GraticuleEvery = 10;

    private const int MajorLabelSize = 18;
    private const int MinorLabelSize = 10;
    private const int BandLabelSize = 14;

    private WorldSnapshot? _snapshot;

    /// <summary>Radios de banda normalizados y radio de zona muerta. Los fija Main desde la config.</summary>
    private double[] _bandOuterRadii = { 0.28, 0.46, 0.64, 0.82, 1.00 };
    private double _deadZoneRadius = 0.10;

    /// <summary>
    /// Ticks de una vuelta completa del haz. Sale de la configuración y no de una constante
    /// porque es la escala con la que se atenúan las lecturas: fijarla a mano haría que
    /// cambiar el período del barrido —la pregunta 4 de M1— desincronizara el desvanecido.
    /// </summary>
    private float _sweepTicks = 300f;

    private Font _font = null!;

    public override void _Ready() => _font = ThemeDB.FallbackFont;

    public void SetGeometry(double deadZoneRadius, double[] bandOuterRadii, double sweepTicks)
    {
        _deadZoneRadius = deadZoneRadius;
        _bandOuterRadii = bandOuterRadii;
        _sweepTicks = (float)sweepTicks;
    }

    public void SetSnapshot(WorldSnapshot snapshot)
    {
        _snapshot = snapshot;
        QueueRedraw();
    }

    /// <summary>
    /// Espacio de simulación a píxeles. El submarino está en el origen y la marcación 0 es
    /// el norte, o sea (0,-1) — que coincide con el eje Y hacia abajo de Godot.
    /// </summary>
    private static Vector2 ToScreen(Sonar.Core.Spatial.Vec2 v) =>
        new((float)v.X * Radius, (float)v.Y * Radius);

    private static Vector2 BearingToDirection(float degrees)
    {
        var rad = Mathf.DegToRad(degrees);
        return new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
    }

    public override void _Draw()
    {
        if (_snapshot is not { } snap) return;

        DrawGrid();
        DrawSweep(snap);
        DrawContacts(snap);
        DrawOwnTorpedoes(snap);
        DrawEnemyTorpedoes(snap);

        foreach (var defense in snap.Defenses)
            DrawArc(ToScreen(defense.Position), 11f, 0, Mathf.Tau, 24, DefenseColor, 2.5f);

        DrawCircle(Vector2.Zero, 6f, SubColor);
    }

    /// <summary>Dibuja un texto centrado en el punto dado, no anclado a su esquina.</summary>
    private void DrawCenteredString(Vector2 at, string text, int size, Color color)
    {
        var width = _font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        DrawString(_font, at + new Vector2(-width / 2f, _font.GetAscent(size) / 2f),
            text, HorizontalAlignment.Left, -1, size, color);
    }

    private void DrawGrid()
    {
        var deadZone = (float)_deadZoneRadius * Radius;

        // Zona muerta: dentro de este radio ningún torpedo propio está armado. Se dibuja
        // porque explica visualmente por qué un disparo a quemarropa no hace nada.
        DrawArc(Vector2.Zero, deadZone, 0, Mathf.Tau, 48, DeadZone, 2f);

        // Radiales punteadas muy finas. No son decoración: sin ellas hay que estimar la
        // marcación de un punto a ojo entre rótulos separados 30 grados, y el juego pide
        // escribir la coordenada exacta.
        //
        // Arrancan en el borde de la primera banda y no en la zona muerta: hacia el centro
        // convergen tanto que dejan de servir para leer una marcación y sólo ensucian.
        var graticuleFrom = (float)_bandOuterRadii[0] * Radius;
        for (var d = 0; d < 360; d += GraticuleEvery)
        {
            var dir = BearingToDirection(d);
            DrawDashedLine(dir * graticuleFrom, dir * Radius, Graticule, 1f, 5f);
        }

        for (var i = 0; i < _bandOuterRadii.Length; i++)
        {
            var outer = (float)_bandOuterRadii[i] * Radius;
            var last = i == _bandOuterRadii.Length - 1;
            DrawArc(Vector2.Zero, outer, 0, Mathf.Tau, 96, last ? GridStrong : Grid, last ? 2.5f : 1.5f);

            // Letra de la banda, sobre el eje este, en el centro de la banda.
            var inner = i == 0 ? deadZone : (float)_bandOuterRadii[i - 1] * Radius;
            DrawCenteredString(new Vector2((inner + outer) / 2f, 0f),
                ((char)('A' + i)).ToString(), BandLabelSize, new Color(Label, 0.50f));
        }

        // Rótulos de marcación cada LabelEvery grados. Los múltiplos de MajorEvery van más
        // grandes y con marca más larga: la jerarquía de tamaño es lo que deja leer una
        // marcación de un vistazo sin contar rayitas.
        for (var d = 0; d < 360; d += LabelEvery)
        {
            var dir = BearingToDirection(d);
            var major = d % MajorEvery == 0;

            DrawLine(dir * (Radius - (major ? 15 : 7)), dir * Radius,
                major ? GridStrong : Grid, major ? 2f : 1f);

            DrawCenteredString(dir * (Radius + (major ? 22 : 13)), d.ToString("D3"),
                major ? MajorLabelSize : MinorLabelSize,
                major ? Label : MinorLabel);
        }
    }

    private void DrawSweep(WorldSnapshot snap)
    {
        var dir = BearingToDirection((float)snap.SweepDegrees);
        DrawLine(Vector2.Zero, dir * Radius, snap.SweepRunning ? GridStrong : Grid, 2.5f);
    }

    private void DrawContacts(WorldSnapshot snap)
    {
        // Posición de la LECTURA, no la real. Atenuada por edad: cuanto más vieja la
        // lectura, menos confiable la extrapolación. Es el juego entero.
        //
        // Sin rótulo de coordenada a propósito. Ponerle "C047" al punto le entrega hecha
        // justamente la lectura que M1 está midiendo si es satisfactorio hacer: el jugador
        // saca la marcación del anillo y la banda de los anillos, y la escribe él.
        foreach (var contact in snap.Contacts)
        {
            var freshness = Mathf.Clamp(1f - contact.AgeTicks / _sweepTicks, 0.22f, 1f);
            DrawCircle(ToScreen(contact.Position), 7f, new Color(ContactColor, freshness));
        }
    }

    private void DrawOwnTorpedoes(WorldSnapshot snap)
    {
        foreach (var torpedo in snap.Torpedoes)
        {
            var position = ToScreen(torpedo.Position);
            DrawLine(Vector2.Zero, position, new Color(OwnTorpedo, 0.5f), 2f);
            DrawCircle(position, 4.5f, OwnTorpedo);

            // Punto de detonación por fin de corrida: hace legible la decisión de banda.
            var mark = BearingToDirection(torpedo.BearingDegrees) * (float)torpedo.RunToRadius * Radius;
            DrawArc(mark, 14f, 0, Mathf.Tau, 24, new Color(OwnTorpedo, 0.40f), 1.5f);
        }
    }

    private void DrawEnemyTorpedoes(WorldSnapshot snap)
    {
        foreach (var torpedo in snap.EnemyTorpedoes)
        {
            var p = ToScreen(torpedo.Position);
            DrawLine(p + new Vector2(-7, -7), p + new Vector2(7, 7), EnemyTorpedo, 3f);
            DrawLine(p + new Vector2(-7, 7), p + new Vector2(7, -7), EnemyTorpedo, 3f);
        }
    }
}
