using System.Globalization;
using System.Text;
using WeCantSpell.Hunspell;

namespace Gineco;

internal static class OrtografiaAutomatica
{
    private static readonly Lazy<WordList> Espanol = new(() => WordList.CreateFromFiles(
        Path.Combine(AppContext.BaseDirectory, "Recursos", "Ortografia", "es_DO.dic")));

    internal static string? Sugerir(string palabra)
    {
        if (palabra.Length is < 4 or > 45 || palabra.Length < 7 && palabra.All(char.IsUpper)) return null;
        string original = palabra.ToLowerInvariant().Normalize();
        var lexico = Espanol.Value;
        if (lexico.Check(original)) return null;
        return Elegir(original, lexico.Suggest(original));
    }

    internal static string? Elegir(string original, IEnumerable<string> sugerencias)
    {
        var candidatas = sugerencias.Where(p => p.All(char.IsLetter) && p == p.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal).ToArray();
        var sinTildes = SinTildes(original);
        var acentos = candidatas.Where(p => SinTildes(p) == sinTildes).Take(2).ToArray();
        if (acentos.Length > 0) return acentos.Length == 1 ? acentos[0] : null;
        if (original.Length < 6) return null;
        var cercanas = candidatas.Where(p => UnCambio(original, p)).Take(2).ToArray();
        return cercanas.Length == 1 ? cercanas[0] : null;
    }

    private static string SinTildes(string palabra) => string.Concat(palabra.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));

    private static bool UnCambio(string original, string palabra)
    {
        if (Math.Abs(original.Length - palabra.Length) > 1) return false;
        if (original.Length > palabra.Length) return UnCambio(palabra, original);
        int indice = PrimerCambio(original, palabra);
        if (palabra.Length > original.Length) return original.AsSpan(indice).SequenceEqual(palabra.AsSpan(indice + 1));
        if (indice == original.Length) return false;
        return original.AsSpan(indice + 1).SequenceEqual(palabra.AsSpan(indice + 1)) || Intercambio(original, palabra, indice);
    }

    private static int PrimerCambio(string original, string palabra)
    {
        int indice = 0;
        while (indice < original.Length && original[indice] == palabra[indice]) indice++;
        return indice;
    }

    private static bool Intercambio(string original, string palabra, int indice) =>
        indice + 1 < original.Length && original[indice] == palabra[indice + 1] &&
            original[indice + 1] == palabra[indice] && original.AsSpan(indice + 2).SequenceEqual(palabra.AsSpan(indice + 2));
}
