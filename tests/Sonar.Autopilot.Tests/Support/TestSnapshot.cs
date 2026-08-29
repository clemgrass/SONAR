using System;
using System.Collections.Generic;
using Sonar.Controller.Presentation;

namespace Sonar.Autopilot.Tests.Support;

/// <summary>
/// Arma un WorldSnapshot con defaults sanos. Existe porque el record tiene trece campos
/// posicionales y un test que los liste todos esconde lo que de verdad está probando.
/// </summary>
internal static class TestSnapshot
{
    public static WorldSnapshot Build(
        int tick,
        IReadOnlyList<ContactView>? contacts = null,
        IReadOnlyList<TorpedoView>? torpedoes = null,
        IReadOnlyList<EnemyTorpedoView>? enemyTorpedoes = null,
        IReadOnlyList<DefenseView>? defenses = null,
        int ammoLoaded = 3,
        int ammoReserve = 12,
        int defensesAvailable = 1,
        bool reloadInProgress = false,
        int hitsTaken = 0,
        bool destroyed = false) =>
        new(tick, 0.0, true,
            contacts ?? Array.Empty<ContactView>(),
            torpedoes ?? Array.Empty<TorpedoView>(),
            enemyTorpedoes ?? Array.Empty<EnemyTorpedoView>(),
            defenses ?? Array.Empty<DefenseView>(),
            ammoLoaded, ammoReserve, defensesAvailable,
            reloadInProgress, hitsTaken, destroyed);
}
