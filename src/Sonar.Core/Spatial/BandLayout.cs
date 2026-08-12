using System;
using System.Collections.Generic;
using System.Linq;

namespace Sonar.Core.Spatial;

/// <summary>
/// Geometría de las bandas de distancia. La banda i cubre [inner, outer), donde inner es
/// el radio exterior de la banda anterior, o el de la zona muerta para la primera.
/// Ver TDD §1.
/// </summary>
public sealed class BandLayout
{
    private readonly double[] _outerRadii;

    public double DeadZoneRadius { get; }
    public int Count => _outerRadii.Length;

    public BandLayout(double deadZoneRadius, IReadOnlyList<double> outerRadii)
    {
        if (outerRadii.Count == 0)
            throw new ArgumentException("Se requiere al menos una banda.", nameof(outerRadii));
        if (deadZoneRadius < 0)
            throw new ArgumentException("El radio de zona muerta no puede ser negativo.", nameof(deadZoneRadius));
        if (outerRadii[0] <= deadZoneRadius)
            throw new ArgumentException(
                $"El primer radio exterior ({outerRadii[0]}) debe ser mayor al radio de zona muerta ({deadZoneRadius}).",
                nameof(outerRadii));

        for (var i = 1; i < outerRadii.Count; i++)
        {
            if (outerRadii[i] <= outerRadii[i - 1])
                throw new ArgumentException(
                    $"Los radios exteriores deben ser estrictamente crecientes; índice {i} ({outerRadii[i]}) no supera al anterior ({outerRadii[i - 1]}).",
                    nameof(outerRadii));
        }

        DeadZoneRadius = deadZoneRadius;
        _outerRadii = outerRadii.ToArray();
    }

    public double InnerRadius(Band band) =>
        band.Index == 0 ? DeadZoneRadius : _outerRadii[band.Index - 1];

    public double OuterRadius(Band band) => _outerRadii[band.Index];

    public double CenterRadius(Band band) => (InnerRadius(band) + OuterRadius(band)) / 2.0;

    /// <summary>
    /// Banda que contiene el radio dado. Un radio que cae justo en una frontera pertenece
    /// a la banda exterior. Devuelve false dentro de la zona muerta o fuera del scope.
    /// </summary>
    public bool TryBandAt(double radius, out Band band)
    {
        if (radius < DeadZoneRadius || radius > _outerRadii[^1])
        {
            band = default;
            return false;
        }

        for (var i = 0; i < _outerRadii.Length; i++)
        {
            if (radius < _outerRadii[i])
            {
                band = new Band(i);
                return true;
            }
        }

        band = new Band(_outerRadii.Length - 1);
        return true;
    }
}
