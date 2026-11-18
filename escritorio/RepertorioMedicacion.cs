using System.Text;
using System.Text.RegularExpressions;

namespace Gineco;

internal sealed record MedicacionHistorica(string Diagnostico, string Texto, int Historias, bool Aprobada)
{
    public override string ToString() => Texto;
}

internal static partial class RepertorioMedicacion
{
    internal static string Clave(string texto) =>
        Espacios().Replace(texto.Trim().Normalize(NormalizationForm.FormC), " ").ToUpperInvariant();

    internal static string Anadir(string anterior, string sugerencia) =>
        string.IsNullOrWhiteSpace(anterior) ? sugerencia : anterior.TrimEnd() + Environment.NewLine + sugerencia;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}
