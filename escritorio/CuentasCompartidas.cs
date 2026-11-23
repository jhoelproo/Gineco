using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Gineco;

internal sealed record UsuarioCompartido(string Exequatur, string Nombre, bool Administrador);
internal sealed record InicioCompartido(UsuarioCompartido Medico, string Token);
internal sealed record SesionCompartida(Medico Medico, string Token);

internal sealed class CuentasCompartidas(HttpClient cliente) : IDisposable
{
    private const string Proyecto = "agnhgwtmczrtuqlsuefx";
    private const string ClavePublica = "sb_publishable_SLBaZpWFq-7RnF3CNYsi7w_dNgcf-pO";

    internal static CuentasCompartidas DelHospital()
    {
        var cliente = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri($"https://{Proyecto}.supabase.co/functions/v1/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        cliente.DefaultRequestHeaders.Add("apikey", ClavePublica);
        return new(cliente);
    }

    internal async Task<SesionCompartida?> Entrar(string exequatur, string clave, CancellationToken cancelacion)
    {
        using var respuesta = await Enviar(new { accion = "entrar", exequatur, clave }, null, cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized) return null;
        Verificar(respuesta.StatusCode);
        var inicio = await respuesta.Content.ReadFromJsonAsync<InicioCompartido>(cancelacion);
        if (inicio?.Medico is not { } usuario || string.IsNullOrWhiteSpace(inicio.Token) ||
            !string.Equals(usuario.Exequatur, exequatur, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(usuario.Nombre))
            throw new InvalidDataException("El servicio de cuentas devolvió un acceso incompleto.");
        return new(new(usuario.Exequatur, usuario.Nombre, usuario.Administrador), inicio.Token);
    }

    internal Task Vincular(Medico admin, string clave, string codigo, CancellationToken cancelacion) =>
        Ejecutar(new { accion = "vincular", exequatur = admin.Exequatur, nombre = admin.Nombre, clave, codigo }, null, cancelacion);

    internal Task Registrar(string exequatur, string nombre, string clave, SesionCompartida admin, CancellationToken cancelacion) =>
        Ejecutar(new { accion = "registrar", exequatur, nombre, clave }, admin.Token, cancelacion);

    internal Task Restablecer(string exequatur, string clave, SesionCompartida admin, CancellationToken cancelacion) =>
        Ejecutar(new { accion = "restablecer", exequatur, clave }, admin.Token, cancelacion);

    private async Task Ejecutar(object datos, string? token, CancellationToken cancelacion)
    {
        using var respuesta = await Enviar(datos, token, cancelacion);
        Verificar(respuesta.StatusCode);
    }

    private async Task<HttpResponseMessage> Enviar(object datos, string? token, CancellationToken cancelacion)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "cuentas-gineco") { Content = JsonContent.Create(datos) };
        if (token is not null) solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await cliente.SendAsync(solicitud, cancelacion);
    }

    private static void Verificar(HttpStatusCode estado)
    {
        if (estado is HttpStatusCode.OK or HttpStatusCode.Created) return;
        throw new InvalidOperationException(estado switch
        {
            HttpStatusCode.BadRequest => "Verifica los datos y la contraseña.",
            HttpStatusCode.Forbidden => "Verifica la autorización del administrador.",
            HttpStatusCode.NotFound => "La cuenta o el servicio compartido no está disponible.",
            HttpStatusCode.Conflict => "El exequátur ya está registrado o la contraseña no cumple los requisitos del servicio.",
            HttpStatusCode.TooManyRequests => "Hubo varios intentos. Espera un momento y vuelve a probar.",
            _ => "El servicio de cuentas no está disponible. Vuelve a intentarlo."
        });
    }

    public void Dispose() => cliente.Dispose();
}
