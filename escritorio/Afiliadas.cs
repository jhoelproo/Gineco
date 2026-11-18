namespace Gineco;

internal sealed record Identificadores(string Cedula, string Nss)
{
    internal bool Vacio => Cedula.Length == 0 && Nss.Length == 0;

    internal static Identificadores Leer(string cedula, string nss) =>
        new(Clinica.NormalizarIdentificador(cedula, "Cédula", 11), ClaveNss(nss));

    internal static Identificadores De(Atencion atencion) => new(atencion.Valor("cedula"), ClaveNss(atencion.Valor("nss")));

    private static string ClaveNss(string nss)
    {
        string limpio = nss.Trim().ToUpperInvariant();
        if (limpio is "N/S" or "S/N" or "N/A" or "SIN NSS") return "";
        return string.Concat(limpio.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '.' or '(' or ')')));
    }

    internal bool Coincide(Identificadores otro) =>
        (Cedula.Length > 0 && Cedula == otro.Cedula) || (Nss.Length > 0 && Nss == otro.Nss);
}

internal sealed record ResultadoAfiliada(List<Atencion> Historias, bool Conflicto = false)
{
    internal Atencion? Ultima => Historias.FirstOrDefault();

    internal Atencion? UltimaAlergia => Historias.FirstOrDefault(historia =>
        AlergiasRegistradas.EsRespuestaExplicita(historia.Valor("alergias")));
}

internal static class Afiliadas
{
    internal static ResultadoAfiliada Buscar(IEnumerable<Atencion> historias, Identificadores entrada)
    {
        if (entrada.Vacio) return new([]);
        var disponibles = historias.ToList();
        var coincidencias = disponibles.Where(historia => entrada.Coincide(Identificadores.De(historia))).ToList();
        if (coincidencias.Count == 0) return new([]);
        if (EsIncompatible(coincidencias, entrada)) return new([], true);

        var vinculados = new Identificadores(Completar(entrada.Cedula, coincidencias.Select(h => h.Valor("cedula"))),
            Completar(entrada.Nss, coincidencias.Select(h => Identificadores.De(h).Nss)));
        var anteriores = disponibles.Where(historia => vinculados.Coincide(Identificadores.De(historia)))
            .OrderByDescending(historia => historia.Fecha).ThenByDescending(historia => historia.Id).ToList();
        return EsIncompatible(anteriores, vinculados) ? new([], true) : new(anteriores);
    }

    private static bool EsIncompatible(List<Atencion> historias, Identificadores ids) =>
        HayConflicto(historias.Select(historia => historia.Valor("cedula")).Append(ids.Cedula)) ||
        HayConflicto(historias.Select(historia => Identificadores.De(historia).Nss).Append(ids.Nss));

    private static string Completar(string actual, IEnumerable<string> numeros) =>
        actual.Length > 0 ? actual : numeros.FirstOrDefault(valor => valor.Length > 0) ?? "";

    private static bool HayConflicto(IEnumerable<string> numeros) =>
        numeros.Where(numero => numero.Length > 0).Distinct().Skip(1).Any();
}

internal sealed record AlergiasRegistradas(string Respuesta, string Detalle)
{
    private static readonly string[] Negativas = ["No", "Ninguna", "No refiere", "Niega alergias", "Sin alergias conocidas"];

    internal static bool EsRespuestaExplicita(string respuesta) =>
        EsAfirmativa(respuesta.Trim()) || Negativas.Contains(respuesta.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool EsAfirmativa(string respuesta) =>
        string.Equals(respuesta, "Sí", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(respuesta, "Si", StringComparison.OrdinalIgnoreCase);

    internal static AlergiasRegistradas Leer(string respuesta, string detalle)
    {
        respuesta = respuesta.Trim();
        if (Negativas.Contains(respuesta, StringComparer.OrdinalIgnoreCase)) return new("No", "");
        if (EsAfirmativa(respuesta)) return new("Sí", detalle);
        if (respuesta.Length == 0) return detalle.Trim().Length == 0 ? new("", "") : new("Sí", detalle);
        return new("Sí", string.IsNullOrWhiteSpace(detalle) ? respuesta : respuesta + Environment.NewLine + detalle);
    }
}
