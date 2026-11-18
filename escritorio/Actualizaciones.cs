using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Velopack;
using Velopack.Sources;

namespace Gineco;

internal interface IActualizaciones
{
    string Version { get; }
    bool Instalada { get; }
    bool Configurada { get; }
    string? Disponible { get; }
    void GuardarAcceso(string token);
    Task Buscar();
    Task Descargar(Action<int> progreso, CancellationToken cancelacion);
    void AplicarAlCerrar();
}

internal sealed class Actualizaciones(string carpeta, Func<string?, UpdateManager> crearGestor) : IActualizaciones
{
    private const string Repositorio = "https://github.com/jhoelproo/Gineco";
    private readonly CredencialActualizaciones acceso = new(carpeta);
    private (UpdateManager Gestor, UpdateInfo Datos)? disponible;

    internal Actualizaciones(string carpeta) : this(carpeta, CrearGestor) { }

    public string Version => typeof(Actualizaciones).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    public bool Instalada => crearGestor(null).IsInstalled;
    public bool Configurada => acceso.Existe;
    public string? Disponible => disponible?.Datos.TargetFullRelease.Version.ToString();

    public void GuardarAcceso(string token)
    {
        acceso.Guardar(token);
        disponible = null;
    }

    public async Task Buscar()
    {
        disponible = null;
        var gestor = crearGestor(acceso.Leer());
        var datos = await gestor.CheckForUpdatesAsync().WaitAsync(TimeSpan.FromSeconds(20));
        if (datos is not null) disponible = (gestor, datos);
    }

    public Task Descargar(Action<int> progreso, CancellationToken cancelacion)
    {
        var (gestor, datos) = ObtenerActualizacion();
        return gestor.DownloadUpdatesAsync(datos, progreso, cancelacion);
    }

    public void AplicarAlCerrar()
    {
        var (gestor, datos) = ObtenerActualizacion();
        gestor.WaitExitThenApplyUpdates(datos.TargetFullRelease);
    }

    private (UpdateManager Gestor, UpdateInfo Datos) ObtenerActualizacion() =>
        disponible ?? throw new InvalidOperationException("Busca una actualización antes de instalarla.");

    internal static void AbrirPublicaciones() =>
        Process.Start(new ProcessStartInfo($"{Repositorio}/releases") { UseShellExecute = true });

    private static UpdateManager CrearGestor(string? token) =>
        new(new GithubSource(Repositorio, token, prerelease: true));
}

internal sealed class CredencialActualizaciones(string carpeta)
{
    private readonly string ruta = Path.Combine(carpeta, "actualizaciones.dat");
    public bool Existe => File.Exists(ruta);

    internal void Guardar(string token)
    {
        token = token.Trim();
        if (!Regex.IsMatch(token, @"\Agithub_pat_[a-zA-Z0-9_]{20,244}\z"))
            throw new ArgumentException("Introduce un token de acceso detallado de GitHub.");

        var protegido = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
        string temporal = ruta + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporal, protegido);
            File.Move(temporal, ruta, overwrite: true);
        }
        finally { File.Delete(temporal); }
    }

    internal string Leer() => Encoding.UTF8.GetString(
        ProtectedData.Unprotect(File.ReadAllBytes(ruta), null, DataProtectionScope.CurrentUser));
}
