using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gineco;

internal record Medico(string Exequatur, string Nombre, bool Administrador);
internal record Atencion(long Id, Medico Responsable, DateTime Fecha, Dictionary<string, string> Campos,
    bool Finalizada = false, int Version = 0)
{
    public PerfilAplicado? Perfil { get; init; }
    public bool Impresa { get; init; }
    public string Nombre => Valor("nombre");
    public string Valor(string clave) => Campos.GetValueOrDefault(clave, "");
    public override string ToString() => $"{Fecha:dd/MM/yyyy HH:mm} · {Nombre} · {(Finalizada ? "Finalizada" : "Borrador")}";
}

internal static class Clinica
{
    internal const string Titulo = "Historia clínica de emergencia ginecológica";
    internal static string Recurso(string nombre) => Path.Combine(AppContext.BaseDirectory, "Recursos", nombre);
    internal static readonly Dictionary<string, Dictionary<string, string>> Secciones =
        JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(Recurso("campos.json")))!;

    internal static DateTime HoraHospital() => HoraHospital(DateTimeOffset.UtcNow);

    internal static DateTime HoraHospital(DateTimeOffset instante) =>
        TimeZoneInfo.ConvertTime(instante, TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time")).DateTime;

    internal static void Validar(Dictionary<string, string> campos, Atencion? anterior = null, bool finalizada = false)
    {
        if (string.IsNullOrWhiteSpace(campos.GetValueOrDefault("nombre")))
            throw new ArgumentException("Escribe el nombre completo de la paciente.");
        var edad = campos.GetValueOrDefault("edad", "");
        if (edad != anterior?.Valor("edad") && edad.Length > 0 && (!int.TryParse(edad, out int valor) || valor is < 0 or > 130))
            throw new ArgumentException("Introduce una edad entre 0 y 130.");
        NormalizarNumero(campos, anterior, "cedula", "Cédula", 11);
        NormalizarNumero(campos, anterior, "telefono", "Teléfono", 10);
        campos["nss"] = campos.GetValueOrDefault("nss", "").Trim();
        if (finalizada && DetallePendiente(campos) is { } pendiente)
            throw new ArgumentException(pendiente.Mensaje);
    }

    private static void NormalizarNumero(Dictionary<string, string> campos, Atencion? anterior, string clave, string etiqueta, int longitud)
    {
        string valor = campos.GetValueOrDefault(clave, "");
        if (anterior is null || valor != anterior.Valor(clave))
            campos[clave] = NormalizarIdentificador(valor, etiqueta, longitud);
    }

    internal static (string Campo, string Mensaje)? DetallePendiente(IReadOnlyDictionary<string, string> campos)
    {
        if (campos.GetValueOrDefault("alergias") is "Sí" or "Si" && string.IsNullOrWhiteSpace(campos.GetValueOrDefault("detalle_alergias")))
            return ("detalle_alergias", "Especifica las alergias antes de finalizar.");
        if (campos.GetValueOrDefault("previas") is "Sí" or "Si" && string.IsNullOrWhiteSpace(campos.GetValueOrDefault("lugar")))
            return ("lugar", "Completa el detalle de las atenciones previas antes de finalizar.");
        return null;
    }

    internal static string NormalizarIdentificador(string numero, string etiqueta, int longitud)
    {
        string limpio = Regex.Replace(numero, @"[\s().-]", "");
        if (limpio.Length > 0 && (limpio.Length != longitud || limpio.Any(c => !char.IsAsciiDigit(c))))
            throw new ArgumentException($"{etiqueta} debe tener {longitud} dígitos.");
        return limpio;
    }
}
