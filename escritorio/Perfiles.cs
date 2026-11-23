using System.Text.RegularExpressions;

namespace Gineco;

internal sealed record PerfilClinico(string Id, string Nombre, Dictionary<string, string> Campos, int Version = 0)
{
    internal static readonly (string Id, string Nombre)[] Tipos =
        [("embarazada", "Paciente embarazada"), ("no_embarazada", "Paciente no embarazada")];

    internal static bool EsValido(string id) => Tipos.Any(tipo => tipo.Id == id);
    public override string ToString() => Nombre;
}

internal sealed record PerfilAplicado(string Id, int Version, Dictionary<string, string> Predeterminados);

internal static class Perfiles
{
    internal static Dictionary<string, string> CamposClinicos => Clinica.Secciones
        .Where(seccion => seccion.Key != "Datos de la paciente").SelectMany(seccion => seccion.Value)
        .ToDictionary(campo => campo.Key, campo => campo.Value);

    internal static bool EsPredeterminadoEditable(string clave) =>
        clave is not ("manejo" or "pruebas" or "hallazgos" or "sonografias") && SignoVital.Buscar(clave) is null;

    internal static bool EsTextoLibre(string clave) =>
        clave is not ("previas" or "estatus" or "origen" or "alergias") && SignoVital.Buscar(clave) is null;

    internal static PerfilAplicado Aplicar(PerfilClinico perfil, Dictionary<string, string> datos, PerfilAplicado? previo)
    {
        var aplicados = new Dictionary<string, string>();
        foreach (string clave in CamposClinicos.Keys.Where(EsPredeterminadoEditable))
        {
            string actual = datos.GetValueOrDefault(clave, "");
            string original = previo?.Predeterminados.GetValueOrDefault(clave, "") ?? "";
            if (!string.IsNullOrWhiteSpace(actual) && Normalizar(clave, actual) != Normalizar(clave, original)) continue;
            string valor = perfil.Campos.GetValueOrDefault(clave, "");
            datos[clave] = valor;
            if (!string.IsNullOrWhiteSpace(valor)) aplicados[clave] = valor;
        }
        return new(perfil.Id, perfil.Version, aplicados);
    }

    internal static List<string> Advertencias(Atencion atencion) => Revisiones(atencion).Select(aviso => aviso.ToString()).ToList();

    internal static List<AvisoRevision> Revisiones(Atencion atencion)
    {
        var avisos = new List<AvisoRevision>();
        if (atencion.Perfil is null)
            avisos.Add(new("Perfil de la paciente pendiente de selección", "", [new("perfilHistoria", "Elegir perfil")]));
        foreach (var (seccion, campos) in Clinica.Secciones)
        {
            var vacios = campos.Where(campo => campo.Key != "lugar" || atencion.Valor("previas") == OpcionesClinicas.No)
                .Where(campo => campo.Key != "detalle_alergias" || atencion.Valor("alergias") == "Sí")
                .Where(campo => string.IsNullOrWhiteSpace(atencion.Valor(campo.Key))).ToArray();
            if (vacios.Length > 0) avisos.Add(new(seccion, "sin completar", vacios));
            var intactos = campos.Where(campo => EsPredeterminado(atencion, campo.Key)).ToArray();
            if (intactos.Length > 0) avisos.Add(new(seccion, "predeterminados sin cambios", intactos));
        }
        return avisos;
    }

    internal static bool EsPredeterminado(Atencion atencion, string clave)
    {
        string original = atencion.Perfil?.Predeterminados.GetValueOrDefault(clave, "") ?? "";
        return !string.IsNullOrWhiteSpace(original) && Normalizar(clave, atencion.Valor(clave)) == Normalizar(clave, original);
    }

    private static string Normalizar(string clave, string texto) => clave == "pruebas"
        ? AnalisisLaboratorio.Leer(texto).Guardar() : texto.Trim().ReplaceLineEndings("\n");
}

internal sealed record Correccion(string Incorrecta, string Correcta);
internal sealed record Propuesta(string Campo, string Texto, long Historias)
{
    public string Etiqueta => Perfiles.CamposClinicos.GetValueOrDefault(Campo, Campo);
}
internal sealed record AvisoRevision(string Seccion, string Estado, KeyValuePair<string, string>[] Campos)
{
    public override string ToString() => Estado.Length == 0 ? Seccion : $"{Seccion} — {Estado}: {string.Join(", ", Campos.Select(c => c.Value))}.";
}

internal sealed record ExportacionPreparada(Atencion Atencion, List<AvisoRevision> Revisiones, List<string> Correcciones)
{
    public List<string> Avisos => Revisiones.Select(aviso => aviso.ToString()).ToList();
}

internal static class Ortografia
{
    private static readonly Regex Palabra = new(@"(?<![\p{L}\p{M}\d])\p{L}[\p{L}\p{M}]*(?![\p{L}\p{M}\d])");
    private static readonly Regex PalabraCompleta = new(@"\A\p{L}[\p{L}\p{M}]*\z");

    internal static void Validar(IReadOnlyList<Correccion> correcciones)
    {
        var fuentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var regla in correcciones)
        {
            if (!PalabraCompleta.IsMatch(regla.Incorrecta) || !PalabraCompleta.IsMatch(regla.Correcta))
                throw new ArgumentException("Cada corrección debe contener una sola palabra, sin números ni puntuación.");
            if (!fuentes.Add(regla.Incorrecta))
                throw new ArgumentException("Hay palabras repetidas en el diccionario de correcciones.");
        }
        if (correcciones.Any(regla => fuentes.Contains(regla.Correcta)))
            throw new ArgumentException("Evita correcciones encadenadas o circulares.");
    }

    internal static ExportacionPreparada Preparar(Atencion atencion, IReadOnlyList<Correccion> correcciones,
        Func<string, string?>? corregir = null)
    {
        var diccionario = correcciones.ToDictionary(regla => regla.Incorrecta, regla => regla.Correcta, StringComparer.OrdinalIgnoreCase);
        var datos = new Dictionary<string, string>(atencion.Campos);
        var cambios = new HashSet<string>();
        var sugerencias = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var clave in Perfiles.CamposClinicos.Keys.Where(Perfiles.EsTextoLibre))
        {
            datos[clave] = Palabra.Replace(atencion.Valor(clave), coincidencia =>
            {
                string palabra = coincidencia.Value;
                if (!diccionario.TryGetValue(palabra, out string? corregida) && corregir is not null && EsNarracion(clave))
                {
                    if (!sugerencias.TryGetValue(palabra, out corregida))
                        sugerencias[palabra] = corregida = corregir(palabra);
                }
                if (corregida is null) return palabra;
                string salida = ConMayusculas(coincidencia.Value, corregida);
                cambios.Add($"{coincidencia.Value} → {salida}");
                return salida;
            });
        }
        return new(atencion with { Campos = datos }, Perfiles.Revisiones(atencion), cambios.Order().ToList());
    }

    private static bool EsNarracion(string clave) => clave is not ("medicamentos" or "manejo" or "diagnosticos" or "detalle_alergias" or "pruebas");

    private static string ConMayusculas(string original, string reemplazo)
    {
        if (original.All(char.IsUpper)) return reemplazo.ToUpperInvariant();
        return char.IsUpper(original[0]) ? char.ToUpperInvariant(reemplazo[0]) + reemplazo[1..] : reemplazo;
    }
}
