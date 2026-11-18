using Gineco;
using Xunit;

namespace Gineco.Pruebas;

public sealed class HistoriasDeAfiliadas
{
    [Fact]
    public void EncuentraPorIdentificadoresNormalizadosYEnlazaHistoriasParciales()
    {
        var anterior = Historia(1, "", "001234567", "Si", "Látex");
        var reciente = Historia(2, "00123456789", "001234567");
        var resultado = Afiliadas.Buscar([anterior, reciente], Identificadores.Leer("001-2345678-9", ""));
        Assert.Equal(new long[] { 2, 1 }, resultado.Historias.Select(historia => historia.Id));
        Assert.Equal(reciente, resultado.Ultima);
        Assert.Equal(anterior, resultado.UltimaAlergia);
        Assert.Equal(reciente, Afiliadas.Buscar([anterior, reciente], Identificadores.Leer("", "001-234-567")).Ultima);
        Assert.Empty(Afiliadas.Buscar([reciente], new("", "")).Historias);
        Assert.Empty(Afiliadas.Buscar([reciente], new("", "111111111")).Historias);
        Assert.Throws<ArgumentException>(() => Identificadores.Leer("001", ""));
        Assert.Throws<ArgumentException>(() => Identificadores.Leer("", "00123456x"));
    }

    [Fact]
    public void ConflictosNoDevuelvenHistoriasNiAlergias()
    {
        var titular = Historia(1, "00123456789", "001234567");
        var otra = Historia(2, "00012345678", "098765432");
        var conflicto = Afiliadas.Buscar([titular, otra], new("00123456789", "098765432"));
        Assert.True(conflicto.Conflicto);
        Assert.Empty(conflicto.Historias);
        Assert.Null(conflicto.UltimaAlergia);
        var nssDuplicado = Historia(3, "00012345678", "001234567");
        Assert.True(Afiliadas.Buscar([titular, nssDuplicado], new("00123456789", "")).Conflicto);
    }

    [Fact]
    public void LaUltimaRespuestaExplicitaTienePrioridadYRespetaPermisos()
    {
        using var archivo = new Archivo(":memory:");
        archivo.Alta(null, "a", "Administrador", "abcdef");
        var admin = Assert.IsType<Medico>(archivo.Entrar("a", "Administrador", "abcdef"));
        archivo.Alta(admin, "m", "Médica", "abcdef");
        var medico = Assert.IsType<Medico>(archivo.Entrar("m", "Médica", "abcdef"));
        archivo.Guardar(admin, Historia(1, "00123456789", "001234567", "Sí", "Látex"), null, "");
        archivo.Guardar(admin, Historia(2, "00123456789", "001234567", "No", "No reutilizar"), null, "");
        archivo.Guardar(admin, Historia(3, "00123456789", "001234567"), null, "");
        var ids = new Identificadores("00123456789", "001234567");
        Assert.Empty(archivo.BuscarAfiliada(medico, ids).Historias);
        Assert.Throws<InvalidOperationException>(() => archivo.BuscarAfiliada(medico with { Administrador = true }, ids));
        var anterior = Assert.IsType<Atencion>(archivo.BuscarAfiliada(admin, ids).UltimaAlergia);
        Assert.Equal(new AlergiasRegistradas("No", ""), AlergiasRegistradas.Leer(anterior.Valor("alergias"), anterior.Valor("detalle_alergias")));
    }

    [Theory]
    [InlineData("Sí", "Látex", "Sí", "Látex")]
    [InlineData("No", "No reutilizar", "No", "")]
    [InlineData("Ninguna", "", "No", "")]
    [InlineData("", "", "", "")]
    [InlineData("", "Látex", "Sí", "Látex")]
    [InlineData("Látex", "", "Sí", "Látex")]
    public void InterpretaAlergiasDeHistoriasAnteriores(string respuesta, string detalle, string esperado, string texto) =>
        Assert.Equal(new AlergiasRegistradas(esperado, texto), AlergiasRegistradas.Leer(respuesta, detalle));

    [Fact]
    public void LosPerfilesNoRellenanSignosNiPruebasAunqueSeanLegados()
    {
        var datos = new Dictionary<string, string> { ["pruebas"] = "Prueba escrita por el médico" };
        var aplicado = Perfiles.Aplicar(new("embarazada", "Embarazada", new()
        {
            ["ta"] = "120/80",
            ["pruebas"] = "Hemograma",
            ["hallazgos"] = "Anterior",
            ["sonografias"] = "Anterior",
            ["motivo"] = "Base"
        }), datos, null);
        Assert.Equal("Prueba escrita por el médico", datos["pruebas"]);
        Assert.All(new[] { "ta", "hallazgos", "sonografias" }, clave => Assert.False(datos.ContainsKey(clave)));
        Assert.Equal("motivo", Assert.Single(aplicado.Predeterminados).Key);
    }

    internal static Atencion Historia(int dia, string cedula, string nss, string alergias = "", string detalle = "") =>
        new(dia, new("a", "Médico", false), new DateTime(2026, 11, dia),
            new()
            {
                ["nombre"] = "Paciente ficticia",
                ["cedula"] = cedula,
                ["nss"] = nss,
                ["edad"] = "30",
                ["telefono"] = "8095551234",
                ["alergias"] = alergias,
                ["detalle_alergias"] = detalle
            });
}
