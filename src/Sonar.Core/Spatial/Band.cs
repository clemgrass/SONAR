namespace Sonar.Core.Spatial;

/// <summary>
/// Índice de banda de distancia, base 0. La letra se deriva del índice.
/// El rango de letras depende del número de bandas configurado: nunca asumir cinco.
/// </summary>
public readonly record struct Band(int Index)
{
    public char Letter => (char)('A' + Index);

    public static bool TryParseLetter(char letter, int bandCount, out Band band)
    {
        var upper = char.ToUpperInvariant(letter);
        var index = upper - 'A';
        if (index < 0 || index >= bandCount)
        {
            band = default;
            return false;
        }
        band = new Band(index);
        return true;
    }

    public override string ToString() => Letter.ToString();
}
