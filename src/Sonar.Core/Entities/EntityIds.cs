namespace Sonar.Core.Entities;

/// <summary>
/// Ids monótonos asignados por WorldState. Nunca se reutilizan dentro de una corrida:
/// son la clave de orden determinista en la resolución de colisiones simultáneas (TDD §3.3).
/// </summary>
public readonly record struct ContactId(int Value);

public readonly record struct TorpedoId(int Value);

public readonly record struct DefenseId(int Value);
