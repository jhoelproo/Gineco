using System.Text.RegularExpressions;

namespace Gineco;

internal sealed record SignoVital(string Clave, string Nombre, string RotuloPdf, string Unidad = "", bool Decimal = false)
{
    internal static readonly SignoVital Presion = new("ta", "Presión arterial (TA)", "TA", "mmHg");
    internal static readonly SignoVital[] Campos =
    [
        Presion, new("temperatura", "Temperatura", "Temp.", "°C", true),
        new("fc", "Frecuencia cardíaca", "FC", "lpm"), new("fr", "Frecuencia respiratoria", "FR", "rpm"),
        new("pc", "PC por 10 minutos", "PC/10 MIN"), new("spo2", "Saturación de oxígeno", "Sat. O2", "%")
    ];
    private static readonly Dictionary<string, SignoVital> PorClave = Campos.ToDictionary(campo => campo.Clave);

    internal static SignoVital? Buscar(string clave) => PorClave.GetValueOrDefault(clave);

    internal string SinUnidad(string valor)
    {
        var limpio = valor.Trim();
        return Unidad.Length > 0 && limpio.EndsWith(Unidad, StringComparison.OrdinalIgnoreCase)
            ? limpio[..^Unidad.Length].TrimEnd() : limpio;
    }

    internal string FormatoPdf(string valor)
    {
        var limpio = SinUnidad(valor);
        return Unidad.Length > 0 && EsNumero(limpio) ? $"{Normalizar(limpio)} {Unidad}" : valor;
    }

    internal bool EsNumero(string valor) => Regex.IsMatch(valor, Patron, RegexOptions.CultureInvariant);

    private string Patron => Clave == Presion.Clave ? @"\A[0-9]+\s*/\s*[0-9]+\z"
        : Decimal ? @"\A[0-9]+(?:[.,][0-9]+)?\z" : @"\A[0-9]+\z";

    internal string Normalizar(string valor) => valor.Replace(" ", "").Replace(',', '.');
}
