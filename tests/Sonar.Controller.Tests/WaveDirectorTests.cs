using System;
using System.Collections.Generic;
using System.Linq;
using Sonar.Core;
using Sonar.Core.Entities;
using Sonar.Core.Spatial;
using Xunit;

namespace Sonar.Controller.Tests;

public class WaveDirectorTests
{
    private static SimulationConfig Config() => new()
    {
        TickDurationSeconds = 1.0 / 60.0,
        MaxCatchUpTicks = 5,
        DeadZoneRadius = 0.10,
        BandOuterRadii = new[] { 0.28, 0.46, 0.64, 0.82, 1.00 },
        SweepPeriodSeconds = 7.0,
        TorpedoSpeed = 0.043,
        HitRadius = 0.040,
        BlastRadius = 0.100,
        NearMissRadius = 0.160,
        EnemyTorpedoSpeed = 0.03,
        EnemyFireIntervalTicks = 1500,
        EnemyFireStaggerTicks = 300,
        SubHitRadius = 0.045,
        PassiveHearingRadius = 0.46,
        FriendlyLaneRadius = 0.040,
        DefenseRadius = 0.070,
        DefenseCapacity = 1,
        MagazineSize = 3,
        AmmoReserve = 12,
        ReloadTicks = 420,
        EnemyReloadTicks = 840,
    };

    private static WaveDirector New(int seed = 1) =>
        new(Config(), new SeededRandomSource(seed), new Bearing(0), subSpeed: 0.012);

    /// Corre el director hasta `ticks`, fingiendo que los contactos se destruyen al instante.
    private static List<(int Tick, IReadOnlyList<Contact> Spawned)> RunClearingInstantly(
        WaveDirector director, int ticks)
    {
        var log = new List<(int, IReadOnlyList<Contact>)>();
        for (var tick = 0; tick < ticks; tick++)
        {
            var spawned = director.Advance(tick, liveEnemies: 0);
            if (spawned.Count > 0) log.Add((tick, spawned));
        }
        return log;
    }

    [Fact]
    public void NothingHappensDuringTheOpeningQuiet()
    {
        var director = New();
        for (var tick = 0; tick < WaveDirector.OpeningQuietTicks; tick++)
            Assert.Empty(director.Advance(tick, liveEnemies: 0));

        // Justo en este tick termina el silencio y la oleada 1 se genera: el llamado
        // devuelve un contacto, no vacío. (El plan original pedía Assert.Empty acá, pero
        // eso contradice la aserción de abajo: Wave sólo llega a 1 porque este mismo
        // llamado lo generó. Ver reporte de la tarea 6.)
        Assert.Single(director.Advance(WaveDirector.OpeningQuietTicks, liveEnemies: 0));
        Assert.Equal(1, director.Wave);
    }

    [Fact]
    public void WavesDouble()
    {
        var log = RunClearingInstantly(New(), 20000);
        Assert.Equal(new[] { 1, 2, 4, 8 }, log.Take(4).Select(e => e.Spawned.Count).ToArray());
    }

    [Fact]
    public void ANewWaveWaitsForTheBoardToClear()
    {
        var director = New();
        for (var tick = 0; tick <= WaveDirector.OpeningQuietTicks; tick++)
            director.Advance(tick, liveEnemies: 0);
        Assert.Equal(1, director.Wave);

        // Con un enemigo vivo no entra nada, por muchos ticks que pasen.
        for (var tick = 0; tick < 5000; tick++)
            Assert.Empty(director.Advance(WaveDirector.OpeningQuietTicks + tick, liveEnemies: 1));

        Assert.Equal(1, director.Wave);
    }

    [Fact]
    public void EverythingEntersFromTheEdge()
    {
        var log = RunClearingInstantly(New(), 20000);
        var all = log.SelectMany(e => e.Spawned).ToArray();

        Assert.NotEmpty(all);
        Assert.All(all, c => Assert.Equal(WaveDirector.SpawnRadius, c.Position.Length, precision: 6));
    }

    [Fact]
    public void ContactsInAWaveAreAngularlySeparated()
    {
        // Sin separación mínima, dos entradas cercanas se leen como una sola marca y la
        // oleada mide menos de lo que dice medir.
        var log = RunClearingInstantly(New(), 20000);

        foreach (var (_, spawned) in log.Where(e => e.Spawned.Count > 1))
        {
            var bearings = spawned.Select(c => Bearing.DegreesOf(c.Position)).ToArray();
            var floor = 360.0 / bearings.Length / 2.0;

            for (var i = 0; i < bearings.Length; i++)
            for (var j = i + 1; j < bearings.Length; j++)
                Assert.True(Bearing.Separation(bearings[i], bearings[j]) >= floor - 1e-6,
                    $"Marcaciones {bearings[i]:F1} y {bearings[j]:F1} más juntas que {floor:F1}°.");
        }
    }

    [Fact]
    public void ContactsCloseOnTheSub()
    {
        var log = RunClearingInstantly(New(), 20000);

        foreach (var contact in log.SelectMany(e => e.Spawned))
        {
            // Velocidad propia más deriva del mundo: el neto tiene que apuntar al origen.
            var net = contact.Velocity + new Vec2(0.0, 0.012);
            var towardOrigin = (Vec2.Zero - contact.Position).Normalized();
            var dot = net.X * towardOrigin.X + net.Y * towardOrigin.Y;

            Assert.True(dot > 0, $"El contacto {contact.Id.Value} no se acerca: dot={dot:F4}.");
        }
    }

    [Fact]
    public void LaterWavesCloseFaster()
    {
        var log = RunClearingInstantly(New(), 20000);

        double NetSpeed(Contact c) => (c.Velocity + new Vec2(0.0, 0.012)).Length;

        var first = NetSpeed(log[0].Spawned[0]);
        var third = NetSpeed(log[2].Spawned[0]);

        Assert.True(third > first * 1.5,
            $"La tercera oleada no cierra más rápido: {first:F4} -> {third:F4}.");
    }

    [Fact]
    public void NoHullEverOutrunsHalfATorpedo()
    {
        // El contraste de velocidades es lo que hace que el torpedo se lea como rápido.
        // La escalada por oleada tiene techo justamente aquí: sin él, alrededor de la
        // oleada 5 los cascos alcanzaban a sus propias municiones.
        var config = Config();
        var ceiling = config.EnemyTorpedoSpeed / 2.0;
        var log = RunClearingInstantly(New(), 20000);

        foreach (var (tick, spawned) in log)
        foreach (var contact in spawned)
        {
            var net = (contact.Velocity + new Vec2(0.0, 0.012)).Length;
            Assert.True(net <= ceiling + 1e-9,
                $"El contacto {contact.Id.Value} del tick {tick} cierra a {net:F4}, sobre el techo {ceiling:F4}.");
        }

        // Control positivo: alguna oleada tiene que llegar realmente al techo. Sin esto el
        // test pasaría igual si los contactos se hubieran quedado quietos del todo.
        var fastest = log.SelectMany(e => e.Spawned)
            .Max(c => (c.Velocity + new Vec2(0.0, 0.012)).Length);
        Assert.Equal(ceiling, fastest, precision: 6);
    }

    [Fact]
    public void ContactIdsAreUniqueAcrossWaves()
    {
        var all = RunClearingInstantly(New(), 20000).SelectMany(e => e.Spawned).ToArray();
        var ids = all.Select(c => c.Id.Value).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void SameSeed_SameSpawns()
    {
        var a = RunClearingInstantly(New(seed: 77), 20000).SelectMany(e => e.Spawned).ToArray();
        var b = RunClearingInstantly(New(seed: 77), 20000).SelectMany(e => e.Spawned).ToArray();

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeed_DifferentSpawns()
    {
        // El primer test del proyecto que puede afirmar esto: hasta ahora nada consumía
        // IRandomSource y forzarlo habría sido inventar aleatoriedad para satisfacer un test.
        var a = RunClearingInstantly(New(seed: 1), 20000).SelectMany(e => e.Spawned).ToArray();
        var b = RunClearingInstantly(New(seed: 2), 20000).SelectMany(e => e.Spawned).ToArray();

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void NoWaveIsEverBornAlreadyArmed()
    {
        // El defecto original: NextFireTick era un tick absoluto, así que de la oleada 4 en
        // adelante el plazo ya había vencido y los contactos disparaban al aparecer.
        var log = RunClearingInstantly(New(), 20000);

        foreach (var (tick, spawned) in log)
        foreach (var contact in spawned)
            Assert.True(contact.NextFireTick > tick,
                $"El contacto {contact.Id.Value} de la oleada que entra en {tick} " +
                $"nace armado (NextFireTick={contact.NextFireTick}).");
    }

    [Fact]
    public void ContactsInAWaveDoNotAllFireOnTheSameTick()
    {
        var log = RunClearingInstantly(New(), 20000);

        foreach (var (tick, spawned) in log)
        {
            if (spawned.Count < 2) continue;
            var distinct = spawned.Select(c => c.NextFireTick).Distinct().Count();
            Assert.True(distinct == spawned.Count,
                $"La oleada que entra en {tick} tiene {spawned.Count} contactos pero sólo " +
                $"{distinct} tiempos de disparo distintos.");
        }
    }

    [Fact]
    public void StaggerSpanIsBoundedNotLinear()
    {
        // El escalonamiento garantiza separación en el tick de entrada; no está pensado
        // para marcar el ritmo. Las oleadas 1 a 4 (1, 2, 4 y 8 contactos) quedan
        // idénticas al valor sin techo: el reparto completo de la oleada 4 es
        // 7 x 300 = 2100, exactamente el techo. De la oleada 5 en adelante (16
        // contactos) el reparto sin techo excedería el techo y por eso se recorta.
        var log = RunClearingInstantly(New(), 20000);

        var wave4 = log[3].Spawned; // oleada 4: 8 contactos
        Assert.Equal(8, wave4.Count);
        var span4 = wave4.Max(c => c.NextFireTick) - wave4.Min(c => c.NextFireTick);
        Assert.Equal(2100, span4);

        var wave5 = log[4].Spawned; // oleada 5: 16 contactos
        Assert.Equal(16, wave5.Count);
        var span5 = wave5.Max(c => c.NextFireTick) - wave5.Min(c => c.NextFireTick);
        Assert.True(span5 <= 2100,
            $"El reparto de la oleada 5 ({span5} ticks) supera el techo de 2100.");

        // Control positivo: sin techo, el reparto de la oleada 5 sería 15 x 300 = 4500.
        // Que el resultado real sea menor confirma que el techo de verdad actuó acá y no
        // que 16 contactos coincidieron por casualidad debajo del límite.
        Assert.True(span5 < 15 * 300,
            "El reparto de la oleada 5 coincide con el valor sin techo; el techo no actuó.");
    }

    [Fact]
    public void WithinAWaveTheLowestIdIsTheFirstArmed()
    {
        // Los ids, el sector de marcación y el escalonamiento salen todos del mismo índice
        // del bucle, así que sólo son coherentes entre sí mientras eso siga siendo cierto.
        // Y la telemetría anota ids: quien lea veinte sesiones va a suponer que el contacto
        // 100 entró y se armó antes que el 101. Si eso deja de valer, el análisis queda mal
        // sin que nada lo delate.
        var log = RunClearingInstantly(New(), 20000);

        foreach (var (tick, spawned) in log)
        {
            var byId = spawned.OrderBy(c => c.Id.Value).ToArray();
            for (var i = 1; i < byId.Length; i++)
                Assert.True(byId[i].NextFireTick > byId[i - 1].NextFireTick,
                    $"En la oleada que entra en {tick}, el contacto {byId[i].Id.Value} " +
                    $"se arma antes que el {byId[i - 1].Id.Value}.");
        }
    }

    [Fact]
    public void EverySpawnedContactHasAPersonality()
    {
        var all = RunClearingInstantly(New(), 20000).SelectMany(e => e.Spawned);
        Assert.All(all, c => Assert.NotNull(c.Personality));
    }

    [Fact]
    public void TheFirstWaveIsASingleStalker()
    {
        // La oleada 1 enseña el ciclo acercarse-disparar-abrirse. Si fuera otra cosa, el
        // jugador aprende otra lección.
        var log = RunClearingInstantly(New(), 20000);
        var first = Assert.Single(log[0].Spawned);
        Assert.Equal("STALKER", first.Personality!.Name);
    }

    [Fact]
    public void TheHunterDoesNotAppearBeforeWaveFive()
    {
        var log = RunClearingInstantly(New(), 20000);

        for (var wave = 0; wave < Math.Min(4, log.Count); wave++)
            Assert.DoesNotContain(log[wave].Spawned, c => c.Personality!.Name == "HUNTER");

        // Control positivo: si nunca apareciera, el test de arriba sería vacuo.
        var everything = log.SelectMany(e => e.Spawned);
        Assert.Contains(everything, c => c.Personality!.Name == "HUNTER");
    }

    [Fact]
    public void EveryContactStartsWithAFreshFixSoItDoesNotFireBlindOnArrival()
    {
        // Sin esto, un contacto recién aparecido tendría LastFixTick = 0 y una antigüedad
        // enorme desde el primer tick: nacería apuntando a un punto absurdo.
        var log = RunClearingInstantly(New(), 20000);
        foreach (var (tick, spawned) in log)
        foreach (var contact in spawned)
            Assert.Equal(tick, contact.LastFixTick);
    }
}
