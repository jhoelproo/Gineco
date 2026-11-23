using System.Net.Http.Json;
using System.Net;
using System.Text.RegularExpressions;

namespace Gineco;

internal sealed class EnlaceAdmision(HttpClient cliente, string? tokenRetiro = null)
{
    private const int LimitePorLote = 100;
    internal static HttpClient? DesdeEstacion() => CrearCliente(LeerConfiguracion("GINECO_PROYECTO"),
        LeerConfiguracion("GINECO_CLAVE_PUBLICA"), LeerConfiguracion("GINECO_TOKEN_CONSULTA"));

    private static string? LeerConfiguracion(string nombre) =>
        Environment.GetEnvironmentVariable(nombre, EnvironmentVariableTarget.User) ??
        Environment.GetEnvironmentVariable(nombre);

    internal static string? TokenRetiro() => LeerConfiguracion("GINECO_TOKEN_CONSULTA_ADMIN");

    internal static HttpClient? CrearCliente(string? proyecto, string? publica, string? estacion)
    {
        if (string.IsNullOrEmpty(proyecto)) return null;
        if (!Regex.IsMatch(proyecto, @"\A[a-z0-9]{20}\z") ||
            publica?.StartsWith("sb_publishable_", StringComparison.Ordinal) != true || string.IsNullOrWhiteSpace(estacion))
            throw new ArgumentException("Falta configurar la conexión de esta PC con Admisión.");
        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri($"https://{proyecto}.supabase.co/rest/v1/rpc/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        http.DefaultRequestHeaders.Add("apikey", publica);
        http.DefaultRequestHeaders.Add("x-gineco-estacion", estacion);
        return http;
    }

    internal async Task<bool> Sincronizar(Archivo archivo, Action<int> notificar, CancellationToken cancelacion)
    {
        using var respuesta = await cliente.PostAsJsonAsync("recibir_derivaciones", new { }, cancelacion);
        respuesta.EnsureSuccessStatusCode();
        var lote = await respuesta.Content.ReadFromJsonAsync<List<Derivacion>>(cancelacion)
            ?? throw new InvalidDataException("Respuesta de Admisión incompleta.");
        if (lote.Count == 0) return true;
        cancelacion.ThrowIfCancellationRequested();
        var nuevas = archivo.Recibir(lote);
        if (nuevas > 0) notificar(nuevas);
        using var acuse = await cliente.PostAsJsonAsync("confirmar_recepcion",
            new { ids = lote.Select(fila => fila.Id).ToArray() }, cancelacion);
        acuse.EnsureSuccessStatusCode();
        return lote.Count < LimitePorLote;
    }

    internal async Task<(bool Activo, bool Cambios, bool AlDia)> SincronizarListado(Archivo archivo, CancellationToken cancelacion)
    {
        var pendientes = archivo.CambiosPendientes();
        if (pendientes.Count > 0)
        {
            using var escritura = await cliente.PostAsJsonAsync("registrar_cambios_consulta", new { cambios = pendientes }, cancelacion);
            if (escritura.StatusCode == HttpStatusCode.NotFound) return (false, false, false);
            escritura.EnsureSuccessStatusCode();
        }
        using var respuesta = await cliente.PostAsJsonAsync("leer_cambios_consulta", new { desde = archivo.UltimoCambioRecibido() }, cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound) return (false, false, false);
        respuesta.EnsureSuccessStatusCode();
        var cambios = await respuesta.Content.ReadFromJsonAsync<List<CambioConsulta>>(cancelacion)
            ?? throw new InvalidDataException("El servidor no devolvió el listado de cambios.");
        cancelacion.ThrowIfCancellationRequested();
        return (true, archivo.IntegrarCambios(cambios, pendientes),
            cambios.Count < LimitePorLote && pendientes.Count < LimitePorLote);
    }

    internal async Task<(bool Cambios, bool AlDia)> SincronizarRetiros(Archivo archivo, CancellationToken cancelacion)
    {
        var pendientes = archivo.RetirosPendientes();
        bool enviados = await EnviarRetiros(pendientes, cancelacion);
        using var respuesta = await cliente.PostAsJsonAsync("leer_retiros_consulta", new { desde = archivo.UltimoRetiroRecibido() }, cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound) return (false, pendientes.Count == 0);
        respuesta.EnsureSuccessStatusCode();
        var lote = await respuesta.Content.ReadFromJsonAsync<List<RetiroCompartido>>(cancelacion)
            ?? throw new InvalidDataException("El servidor no devolvió el listado de retiros.");
        cancelacion.ThrowIfCancellationRequested();
        return (archivo.IntegrarRetiros(lote, enviados ? pendientes : []),
            lote.Count < LimitePorLote && (pendientes.Count == 0 || enviados && pendientes.Count < LimitePorLote));
    }

    private async Task<bool> EnviarRetiros(List<Guid> pendientes, CancellationToken cancelacion)
    {
        if (pendientes.Count == 0 || string.IsNullOrWhiteSpace(tokenRetiro)) return false;
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "registrar_retiros_consulta")
        {
            Content = JsonContent.Create(new { ids = pendientes })
        };
        solicitud.Headers.Add("x-gineco-retiro", tokenRetiro);
        using var respuesta = await cliente.SendAsync(solicitud, cancelacion);
        if (respuesta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) return false;
        respuesta.EnsureSuccessStatusCode();
        return true;
    }
}
