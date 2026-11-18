namespace Gineco;

internal sealed record AnalisisLaboratorio(IReadOnlyList<string> Habituales, string Otros)
{
    internal static IReadOnlyList<string> Opciones { get; } = Array.AsReadOnly(new[]
    {
        "Hemograma", "Examen de orina", "Prueba de embarazo", "Glicemia", "Coprológico"
    });

    internal static AnalisisLaboratorio Leer(string guardado)
    {
        var habituales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var otros = new List<string>();
        foreach (string linea in guardado.ReplaceLineEndings("\n").Split('\n'))
        {
            string nombre = linea.Trim();
            if (Opciones.Contains(nombre, StringComparer.OrdinalIgnoreCase))
                habituales.Add(nombre);
            else
                otros.Add(linea);
        }
        return new(Opciones.Where(habituales.Contains).ToArray(), string.Join("\n", otros).Trim());
    }

    internal string Guardar() => string.Join("\n", Habituales.Append(Otros.Trim().ReplaceLineEndings("\n")).Where(valor => valor.Length != 0));
}
