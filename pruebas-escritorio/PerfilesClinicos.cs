using Gineco;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gineco.Pruebas;

public sealed class PerfilesClinicos
{
    [Fact]
    public void AplicacionProtegeEdicionesYActualizaSoloPredeterminados()
    {
        var datos = new Dictionary<string, string> { ["nombre"] = "Paciente", ["motivo"] = "Escrito por médico", ["abdomen"] = "Base antigua" };
        var previo = new PerfilAplicado("embarazada", 1, new() { ["abdomen"] = "Base antigua" });
        var perfil = new PerfilClinico("no_embarazada", "Paciente no embarazada", new() { ["motivo"] = "Plantilla", ["abdomen"] = "Base nueva", ["pruebas"] = "Hemograma" }, 2);
        var aplicado = Perfiles.Aplicar(perfil, datos, previo);
        Assert.Equal("Paciente", datos["nombre"]);
        Assert.Equal("Escrito por médico", datos["motivo"]);
        Assert.Equal("Base nueva", datos["abdomen"]);
        Assert.False(datos.ContainsKey("pruebas"));
        Assert.False(aplicado.Predeterminados.ContainsKey("motivo"));
        Assert.Equal(2, aplicado.Version);
        var historia = Historia(datos) with { Perfil = aplicado };
        var avisos = Perfiles.Advertencias(historia);
        Assert.Contains(avisos, aviso => aviso.Contains("predeterminados sin cambios: Abdomen"));
        Assert.Contains(avisos, aviso => aviso.Contains("sin completar:"));
        datos["abdomen"] = "Editado";
        Assert.DoesNotContain(Perfiles.Advertencias(historia), aviso => aviso.Contains("predeterminados sin cambios: Abdomen"));
    }

    [Fact]
    public void HistoriaCompletaSinPlantillaNoProduceAvisos()
    {
        var datos = Clinica.Secciones.Values.SelectMany(seccion => seccion).ToDictionary(campo => campo.Key, _ => "Registrado");
        Assert.Empty(Perfiles.Advertencias(Historia(datos) with { Perfil = new("embarazada", 0, []) }));
    }

    [Fact]
    public void PermisosConcurrenciaAprendizajeYCorreccionesPersisten()
    {
        string ruta = Path.GetTempFileName();
        try
        {
            using (var archivo = new Archivo(ruta))
            {
                archivo.Alta(null, "a", "Administrador", "abcdef");
                var admin = Assert.IsType<Medico>(archivo.Entrar("a", "Administrador", "abcdef"));
                archivo.Alta(admin, "m", "Médica", "abcdef");
                var medica = Assert.IsType<Medico>(archivo.Entrar("m", "Médica", "abcdef"));
                var perfiles = archivo.LeerPerfiles();
                Assert.Equal(2, perfiles.Count);
                Assert.All(perfiles, perfil => Assert.Empty(perfil.Campos));
                var perfil = perfiles[0] with { Campos = new() { ["motivo"] = "Base original", ["abdomen"] = "Base abdomen" } };
                Assert.Throws<InvalidOperationException>(() => archivo.GuardarPerfil(medica with { Administrador = true }, perfil));
                Assert.Throws<InvalidOperationException>(() => archivo.LeerPropuestas(medica, perfil.Id));
                Assert.Throws<InvalidOperationException>(() => archivo.ConsultarCorrecciones(medica));
                Assert.Throws<InvalidOperationException>(() => archivo.GuardarCorrecciones(medica, [], []));
                Assert.Throws<ArgumentException>(() => archivo.GuardarPerfil(admin, perfil with { Campos = new() { ["nombre"] = "No" } }));
                archivo.GuardarPerfil(admin, perfil);
                Assert.Throws<InvalidOperationException>(() => archivo.GuardarPerfil(admin, perfil));
                var datos = new Dictionary<string, string> { ["nombre"] = "Paciente ficticia" };
                var aplicado = Perfiles.Aplicar(archivo.LeerPerfiles()[0], datos, null);
                datos["motivo"] = "Dolro ficticio";
                archivo.Guardar(medica, Historia(datos) with { Perfil = aplicado }, null, "");
                var historia = Assert.Single(archivo.Listar(medica));
                Assert.Equal(perfil.Id, historia.Perfil!.Id);
                Assert.Equal("Base original", historia.Perfil.Predeterminados["motivo"]);
                Assert.Empty(archivo.LeerPropuestas(admin, perfil.Id));
                archivo.Guardar(medica, historia with { Finalizada = true }, historia, "");
                var propuesta = Assert.Single(archivo.LeerPropuestas(admin, perfil.Id));
                Assert.Equal("motivo", propuesta.Campo);
                Assert.Equal(1, propuesta.Historias);
                Assert.Equal("Dolro ficticio", propuesta.Texto);
                archivo.GuardarCorrecciones(admin, [new("dolro", "dolor")], []);
                Assert.Throws<InvalidOperationException>(() => archivo.GuardarCorrecciones(admin, [], []));
                var preparada = archivo.PrepararExportacion(historia);
                Assert.Equal("Dolor ficticio", preparada.Atencion.Valor("motivo"));
                Assert.Equal("Dolro ficticio", historia.Valor("motivo"));
                Assert.Single(preparada.Correcciones);
                Assert.NotEmpty(preparada.Avisos);
                using var verificacion = new SqliteConnection($"Data Source={ruta};Pooling=False");
                verificacion.Open();
                using var consulta = verificacion.CreateCommand();
                consulta.CommandText = "SELECT COUNT(*) FROM configuracion_revision";
                Assert.Equal(2L, consulta.ExecuteScalar());
            }
            using var reabierto = new Archivo(ruta);
            var medico = Assert.IsType<Medico>(reabierto.Entrar("m", "Médica", "abcdef"));
            var almacenada = Assert.Single(reabierto.Listar(medico));
            Assert.NotNull(almacenada.Perfil);
            Assert.Equal("Dolor ficticio", reabierto.PrepararExportacion(almacenada).Atencion.Valor("motivo"));
        }
        finally { File.Delete(ruta); }
    }

    [Theory]
    [InlineData("dolro", "dolor", "DOLRO, Dolro; dolro 25dolro.", "DOLOR, Dolor; dolor 25dolro.")]
    [InlineData("infeccion", "infección", "infeccion", "infección")]
    public void CorreccionRespetaPuntuacionIdentidadYNumeros(string error, string correcta, string entrada, string esperado)
    {
        var historia = Historia(new() { ["nombre"] = entrada, ["motivo"] = entrada, ["ta"] = entrada });
        var preparada = Ortografia.Preparar(historia, [new(error, correcta)]);
        Assert.Equal(esperado, preparada.Atencion.Valor("motivo"));
        Assert.Equal(entrada, preparada.Atencion.Nombre);
        Assert.Equal(entrada, preparada.Atencion.Valor("ta"));
        Assert.Equal(entrada, historia.Valor("motivo"));
    }

    [Theory]
    [InlineData("", "dolor")]
    [InlineData("12", "dolor")]
    [InlineData("dolro", "dos palabras")]
    public void RechazaReglasInvalidas(string origen, string destino) =>
        Assert.Throws<ArgumentException>(() => Ortografia.Validar([new(origen, destino)]));

    [Fact]
    public void RechazaDuplicadosYCadenas()
    {
        Assert.Throws<ArgumentException>(() => Ortografia.Validar([new("dolro", "dolor"), new("DOLRO", "dolor")]));
        Assert.Throws<ArgumentException>(() => Ortografia.Validar([new("uno", "dos"), new("dos", "tres")]));
        Ortografia.Validar([]);
    }

    [Fact]
    public void AprendizajeActualizaLaPropuestaSinDuplicarlaYRespetaCambiosDePlantilla()
    {
        using var archivo = new Archivo(":memory:");
        archivo.Alta(null, "m", "Médica", "abcdef");
        var admin = Assert.IsType<Medico>(archivo.Entrar("m", "Médica", "abcdef"));
        var perfil = archivo.LeerPerfiles()[0] with { Campos = new() { ["motivo"] = "Base anterior" } };
        archivo.GuardarPerfil(admin, perfil);
        var datos = new Dictionary<string, string> { ["nombre"] = "Paciente ficticia" };
        var aplicado = Perfiles.Aplicar(perfil, datos, null);
        archivo.Guardar(admin, Historia(datos) with { Perfil = aplicado }, null, "");
        Assert.Empty(archivo.LeerPropuestas(admin, perfil.Id));
        var historia = Assert.Single(archivo.Listar(admin));
        archivo.GuardarPerfil(admin, perfil with { Version = 1, Campos = new() { ["motivo"] = "Base nueva" } });
        Assert.True(Perfiles.EsPredeterminado(historia, "motivo"));
        archivo.Guardar(admin, historia with { Finalizada = true, Campos = new(historia.Campos) { ["motivo"] = "Edición uno" } }, historia, "");
        historia = Assert.Single(archivo.Listar(admin));
        Assert.Equal("Edición uno", Assert.Single(archivo.LeerPropuestas(admin, perfil.Id)).Texto);
        archivo.Guardar(admin, historia with { Campos = new(historia.Campos) { ["motivo"] = "Edición dos" } }, historia, "Corrección de prueba");
        Assert.Equal("Edición dos", Assert.Single(archivo.LeerPropuestas(admin, perfil.Id)).Texto);
        Assert.Throws<InvalidOperationException>(() => archivo.Guardar(admin, historia, historia, "Corrección de prueba"));
        Assert.Equal("Edición dos", Assert.Single(archivo.LeerPropuestas(admin, perfil.Id)).Texto);
        historia = Assert.Single(archivo.Listar(admin));
        archivo.Guardar(admin, historia with { Campos = new(historia.Campos) { ["motivo"] = "" } }, historia, "Corrección de prueba");
        Assert.Empty(archivo.LeerPropuestas(admin, perfil.Id));
    }

    [Fact]
    public void ErrorDePerfilRevierteLaAtencionEntera()
    {
        using var archivo = new Archivo(":memory:");
        var historia = Historia(new() { ["nombre"] = "Paciente ficticia" }) with { Perfil = new("inexistente", 0, []) };
        Assert.Throws<ArgumentException>(() => archivo.Guardar(historia.Responsable, historia, null, ""));
        Assert.Empty(archivo.Listar(historia.Responsable));
    }

    [Fact]
    public void AperturaDeBaseAnteriorConservaDatosYEsRepetible()
    {
        string ruta = Path.GetTempFileName();
        try
        {
            using (var inicial = new Archivo(ruta))
                inicial.Guardar(new("m", "Médica", false), Historia(new() { ["nombre"] = "Registro anterior" }), null, "");
            using (var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False"))
            {
                conexion.Open();
                using var comando = conexion.CreateCommand();
                comando.CommandText = "DROP TABLE propuesta; DROP TABLE perfil_atencion; DROP TABLE perfil; DROP TABLE correccion; DROP TABLE configuracion_revision;";
                comando.ExecuteNonQuery();
            }
            for (int apertura = 0; apertura < 2; apertura++)
            {
                using var archivo = new Archivo(ruta);
                var historia = Assert.Single(archivo.Listar(new("m", "Médica", false)));
                Assert.Equal("Registro anterior", historia.Nombre);
                Assert.Null(historia.Perfil);
                Assert.Equal(2, archivo.LeerPerfiles().Count);
                Assert.Contains(archivo.PrepararExportacion(historia).Avisos, aviso => aviso.Contains("pendiente de selección"));
            }
        }
        finally { File.Delete(ruta); }
    }

    [Fact]
    public void DiccionarioPuedeVaciarseYRechazaConfiguracionDesconocida()
    {
        using var archivo = new Archivo(":memory:");
        archivo.Alta(null, "m", "Médica", "abcdef");
        var admin = Assert.IsType<Medico>(archivo.Entrar("m", "Médica", "abcdef"));
        Assert.Throws<ArgumentException>(() => archivo.GuardarPerfil(admin, new("otro", "Otro", [])));
        archivo.GuardarCorrecciones(admin, [new("dolro", "dolor")], []);
        archivo.GuardarCorrecciones(admin, [], archivo.ConsultarCorrecciones(admin));
        Assert.Empty(archivo.ConsultarCorrecciones(admin));
        Assert.Empty(archivo.PrepararExportacion(Historia(new() { ["motivo"] = "dolro" })).Correcciones);
    }

    private static Atencion Historia(Dictionary<string, string> datos) =>
        new(0, new("m", "Médica", false), new DateTime(2026, 11, 15), datos);
}
