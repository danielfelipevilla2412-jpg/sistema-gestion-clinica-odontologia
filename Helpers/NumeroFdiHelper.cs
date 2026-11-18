namespace SmileTrack_MVC.Helpers;

public static class NumeroFdiHelper
{
    public static bool EsValido(string? numeroFdi)
    {
        if (numeroFdi is not { Length: 2 } || numeroFdi.Any(c => c is < '0' or > '9'))
            return false;

        int cuadrante = numeroFdi[0] - '0';
        int posicion = numeroFdi[1] - '0';
        return (cuadrante is >= 1 and <= 4 && posicion is >= 1 and <= 8)
            || (cuadrante is >= 5 and <= 8 && posicion is >= 1 and <= 5);
    }

    public static bool EsPiezaPermanente(string? numeroFdi)
    {
        if (numeroFdi is not { Length: 2 } || numeroFdi.Any(c => c is < '0' or > '9'))
            return false;

        int cuadrante = numeroFdi[0] - '0';
        int posicion = numeroFdi[1] - '0';
        return cuadrante is >= 1 and <= 4 && posicion is >= 1 and <= 8;
    }
}
