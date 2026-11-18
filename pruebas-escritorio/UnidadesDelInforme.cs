using Gineco;
using Xunit;

namespace Gineco.Pruebas;

public sealed class UnidadesDelInforme
{
    [Theory]
    [InlineData("ta", "TA (mmHg)", "110/70", "TA", "110/70 mmHg")]
    [InlineData("ta", "TA (mmHg)", "110/70 mmHg", "TA", "110/70 mmHg")]
    [InlineData("ta", "TA (mmHg)", "", "TA", "")]
    [InlineData("temperatura", "Temperatura (°C)", "36,8", "Temperatura", "36,8 °C")]
    [InlineData("fc", "FC (lpm)", "82", "FC", "82 lpm")]
    [InlineData("fr", "FR (rpm)", "18", "FR", "18 rpm")]
    [InlineData("spo2", "SpO₂ (%)", "98", "SpO₂", "98 %")]
    [InlineData("pc", "PC/10 MIN", "3", "PC/10 MIN", "3")]
    [InlineData("fc", "FC (lpm)", "No medible", "FC", "No medible")]
    public void UnidadesSoloAlExportar(string clave, string etiqueta, string valor, string rotulo, string impreso)
    {
        Assert.Equal((rotulo, impreso), Informe.AplicarUnidad(clave, etiqueta, valor));
    }
}
