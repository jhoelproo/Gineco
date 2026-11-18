using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed partial class Editor
{
    private static readonly string[] DatosReutilizables = ["nombre", "cedula", "nss", "telefono", "ars", "direccion", "nacionalidad"];
    private readonly Dictionary<string, string> valoresImportados = [];
    private bool actualizandoAfiliada;

    private void CrearAlergias(FlowLayoutPanel pagina)
    {
        var guardadas = AlergiasRegistradas.Leer(valoresIniciales.GetValueOrDefault("alergias", ""),
            valoresIniciales.GetValueOrDefault("detalle_alergias", ""));
        valoresIniciales["alergias"] = guardadas.Respuesta;
        valoresIniciales["detalle_alergias"] = guardadas.Detalle;
        var alergias = AgregarSeleccion(pagina, "alergias", "Alergias", OpcionesClinicas.Atenciones);
        alergias.FlowDirection = FlowDirection.LeftToRight;
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        pagina.Controls.Add(panel);
        AgregarCampo(panel, "detalle_alergias");
        AdaptarAncho(pagina, campos["detalle_alergias"]);
        panel.Visible = alergias.Valor == "Sí";
        alergias.Editada += (_, _) => panel.Visible = alergias.Valor == "Sí";
    }

    private void CrearAccionesAfiliada(FlowLayoutPanel pagina)
    {
        var barra = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(430, 0) };
        var buscar = Estilo.Boton("Buscar afiliada");
        buscar.Click += (_, _) => ConsultarAfiliada(vistaPrevia: false);
        var anteriores = Estilo.Boton("Historias anteriores");
        anteriores.Click += (_, _) => ConsultarAfiliada(vistaPrevia: true);
        barra.Controls.Add(buscar);
        barra.Controls.Add(anteriores);
        pagina.Controls.Add(barra);

        foreach (var clave in new[] { "cedula", "nss" })
        {
            campos[clave].Validated += (_, _) =>
            {
                if (anterior is null) ConsultarAfiliada(vistaPrevia: false, silenciosa: true);
            };
            campos[clave].TextChanged += (_, _) => LimpiarImportacion(clave);
        }
    }

    private void ConsultarAfiliada(bool vistaPrevia, bool silenciosa = false)
    {
        if (actualizandoAfiliada) return;
        try
        {
            var ids = Identificadores.Leer(campos["cedula"].Text, campos["nss"].Text);
            var resultado = archivo.BuscarAfiliada(usuario, ids);
            if (resultado.Conflicto)
            {
                MostrarAviso("Los identificadores coinciden con datos de distintas pacientes. Verifica la cédula y el NSS.");
                return;
            }
            if (vistaPrevia)
                AbrirHistoriasAnteriores(resultado);
            else if (resultado.Ultima is not null)
                AutocompletarAfiliada(resultado);
            else if (!silenciosa)
                MostrarAviso(ids.Vacio ? "Escribe la cédula o el NSS." : "No se encontraron datos para esos identificadores.");
        }
        catch (ArgumentException error)
        {
            if (!silenciosa) MostrarAviso(error.Message);
        }
        catch (Exception error) when (error is SqliteException or InvalidOperationException)
        {
            MostrarAviso("No se pudo consultar el historial de la afiliada.");
        }
    }

    private void AbrirHistoriasAnteriores(ResultadoAfiliada resultado)
    {
        var historias = resultado.Historias.Where(historia => historia.Id != anterior?.Id).ToArray();
        if (historias.Length == 0)
        {
            MostrarAviso("No hay historias anteriores disponibles para estos identificadores.");
            return;
        }
        using var ventana = new VistaHistorias(historias);
        ventana.ShowDialog(this);
    }

    private void AutocompletarAfiliada(ResultadoAfiliada resultado)
    {
        var ultima = resultado.Ultima!;
        var nombre = campos["nombre"].Text.Trim();
        if (nombre.Length > 0 && !string.Equals(nombre, ultima.Nombre, StringComparison.OrdinalIgnoreCase))
        {
            MostrarAviso("El nombre escrito no coincide con el registro. Verifica los datos de la afiliada.");
            return;
        }
        actualizandoAfiliada = true;
        try
        {
            foreach (string clave in DatosReutilizables)
            {
                var valor = resultado.Historias.Select(historia => historia.Valor(clave)).FirstOrDefault(texto => texto.Length > 0);
                Importar(clave, valor ?? "");
            }
            if (anterior is null && resultado.UltimaAlergia is Atencion previa && selecciones["alergias"].Valor.Length == 0)
            {
                var alergias = AlergiasRegistradas.Leer(previa.Valor("alergias"), previa.Valor("detalle_alergias"));
                selecciones["alergias"].Elegir(alergias.Respuesta);
                valoresImportados["alergias"] = alergias.Respuesta;
                Importar("detalle_alergias", alergias.Detalle);
            }
        }
        finally { actualizandoAfiliada = false; }
    }

    private void Importar(string clave, string valor)
    {
        if (campos[clave].Text.Trim().Length > 0 || valor.Length == 0) return;
        campos[clave].Text = valor;
        valoresImportados[clave] = valor;
    }

    private void LimpiarImportacion(string editado)
    {
        if (actualizandoAfiliada || valoresImportados.Count == 0) return;
        actualizandoAfiliada = true;
        try
        {
            foreach (var (clave, valor) in valoresImportados)
            {
                if (clave == editado) continue;
                if (clave == "alergias")
                {
                    if (selecciones["alergias"].Valor == valor) selecciones["alergias"].Elegir("");
                }
                else if (campos[clave].Text == valor)
                    campos[clave].Clear();
            }
            valoresImportados.Clear();
        }
        finally { actualizandoAfiliada = false; }
    }
}
