namespace Sonar.Core.Sensing;

public static class SweepCrossing
{
    /// <summary>
    /// ¿El haz cruzó la marcación dada al pasar de previous a current? Maneja la vuelta
    /// completa: cuando current es menor que previous, el haz cruzó el punto cero.
    /// Los tres valores en grados, [0, 360).
    ///
    /// El intervalo es abierto por la izquierda y cerrado por la derecha a propósito. Con
    /// ambos extremos cerrados, una marcación que cae justo en el borde de dos pasos
    /// consecutivos del haz se registraría dos veces en la misma vuelta.
    /// </summary>
    public static bool Crossed(double previous, double current, double target) =>
        current >= previous
            ? target > previous && target <= current
            : target > previous || target <= current;
}
