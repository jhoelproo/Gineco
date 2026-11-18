using System.Diagnostics;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace Gineco;

internal interface IActualizaciones
{
    string Version { get; }
    bool Instalada { get; }
    string? Disponible { get; }
    Task Buscar();
    Task Descargar(Action<int> progreso, CancellationToken cancelacion);
    void AplicarAlCerrar();
}

internal sealed class Actualizaciones(Func<UpdateManager> crearGestor) : IActualizaciones
{
    private const string Repositorio = "https://github.com/jhoelproo/Gineco";
    private (UpdateManager Gestor, UpdateInfo Datos)? disponible;

    internal Actualizaciones() : this(CrearGestor) { }

    public string Version => typeof(Actualizaciones).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    public bool Instalada => crearGestor().IsInstalled;
    public string? Disponible => disponible?.Datos.TargetFullRelease.Version.ToString();

    public async Task Buscar()
    {
        disponible = null;
        var gestor = crearGestor();
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

    private static UpdateManager CrearGestor() =>
        new(new GithubSource(Repositorio, accessToken: null, prerelease: true));
}
