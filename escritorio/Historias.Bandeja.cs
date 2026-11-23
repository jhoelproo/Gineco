namespace Gineco;

internal sealed partial class Historias
{
    private readonly List<(EstadoConsulta? Estado, string Nombre, RadioButton Boton)> filtros = [];
    private List<FilaHistoria> filasBandeja = [];
    private EstadoConsulta? estadoSeleccionado = EstadoConsulta.Pendiente;
    private readonly Button buscarEnTodas = Estilo.Boton("Buscar en todas");

    private Control CrearBarraSuperior(Control acciones, Button? configuracion)
    {
        var barra = new TableLayoutPanel
        {
            Name = "barraSuperior",
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Estilo.Papel,
            Padding = new Padding(0, 0, 16, 0)
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        acciones.Dock = DockStyle.Fill;
        acciones.Margin = Padding.Empty;
        barra.Controls.Add(acciones, 0, 0);

        var derecha = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(6, 7, 0, 6)
        };
        if (configuracion is not null)
        {
            configuracion.Margin = new Padding(0, 4, 8, 0);
            derecha.Controls.Add(configuracion);
        }
        derecha.Controls.Add(CrearBusqueda());
        barra.Controls.Add(derecha, 1, 0);
        return barra;
    }

    private Control CrearBusqueda()
    {
        var busqueda = new FlowLayoutPanel
        {
            Name = "busquedaPaciente",
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var rotulo = Estilo.Texto("Buscar paciente", 10, true);
        rotulo.Margin = new Padding(0, 0, 0, 3);
        buscar.Name = "buscarPaciente";
        buscar.AccessibleName = "Buscar paciente por nombre o cédula";
        buscar.Width = 245;
        buscar.Margin = Padding.Empty;
        busqueda.Controls.AddRange([rotulo, buscar]);
        return busqueda;
    }

    private Control CrearFiltros()
    {
        var barra = Estilo.Barra(DockStyle.Top);
        barra.AccessibleName = "Filtrar historias por estado";
        barra.Padding = new Padding(16, 3, 16, 6);
        AgregarFiltro(barra, EstadoConsulta.Pendiente, "Pendientes");
        AgregarFiltro(barra, EstadoConsulta.EnAtencion, "En atención");
        AgregarFiltro(barra, EstadoConsulta.Finalizada, "Finalizadas");
        AgregarFiltro(barra, EstadoConsulta.Impresa, "Enviadas a impresión");
        AgregarFiltro(barra, null, "Todas");
        buscarEnTodas.Click += (_, _) => filtros.Single(filtro => filtro.Estado is null).Boton.Checked = true;
        barra.Controls.Add(buscarEnTodas);
        return barra;
    }

    private void AgregarFiltro(Control barra, EstadoConsulta? estado, string nombre)
    {
        var boton = new RadioButton
        {
            Text = nombre,
            Appearance = Appearance.Button,
            AutoSize = true,
            MinimumSize = new Size(0, 38),
            Padding = new Padding(9, 0, 9, 0),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            Checked = estadoSeleccionado == estado
        };
        PintarFiltro(boton);
        boton.CheckedChanged += (_, _) =>
        {
            PintarFiltro(boton);
            if (!boton.Checked) return;
            estadoSeleccionado = estado;
            Filtrar();
        };
        barra.Controls.Add(boton);
        filtros.Add((estado, nombre, boton));
    }

    private static void PintarFiltro(RadioButton boton)
    {
        boton.BackColor = boton.Checked ? Estilo.Principal : Estilo.Papel;
        boton.ForeColor = boton.Checked
            ? (SystemInformation.HighContrast ? SystemColors.HighlightText : Color.White)
            : Estilo.Tinta;
        boton.FlatAppearance.CheckedBackColor = Estilo.Principal;
        boton.FlatAppearance.BorderColor = boton.Checked ? Estilo.Principal : Estilo.Linea;
    }

    private sealed record FilaHistoria(Atencion? Atencion, FilaAdmision? Admision)
    {
        public string Nombre => Atencion?.Nombre ?? Admision!.Nombre;
        public string Cedula => Atencion?.Valor("cedula") ?? Admision!.Cedula;
        public string Fecha => (Admision?.Llegada ?? Atencion!.Fecha).ToString("dd/MM/yyyy HH:mm");
        public string Visita => Admision is null ? "" : $"#{Admision.Visita}";
        internal EstadoConsulta Situacion => Admision?.Situacion ?? EstadoLocal(Atencion!);
        public string Estado => Admision?.Estado ?? (Situacion switch
        {
            EstadoConsulta.Impresa => "Enviada a impresión",
            EstadoConsulta.Finalizada => "Finalizada",
            _ => "Borrador"
        });

        private static EstadoConsulta EstadoLocal(Atencion historia) =>
            historia.Impresa ? EstadoConsulta.Impresa : historia.Finalizada ? EstadoConsulta.Finalizada : EstadoConsulta.EnAtencion;
    }

    private void Recargar()
    {
        atenciones = archivo.Listar(usuario);
        admisiones = archivo.Bandeja();
        var vinculadas = admisiones.Where(a => a.Historia.HasValue).ToDictionary(a => a.Historia!.Value);
        filasBandeja = admisiones.Where(a => !a.Historia.HasValue).Select(a => new FilaHistoria(null, a))
            .Concat(atenciones.Select(a => new FilaHistoria(a, vinculadas.GetValueOrDefault(a.Id)))).ToList();
        ActualizarContadores();
        Filtrar();
    }

    private void ActualizarContadores()
    {
        var cantidades = filasBandeja.CountBy(fila => fila.Situacion).ToDictionary();
        foreach (var (estado, nombre, boton) in filtros)
        {
            int cantidad = estado is { } situacion ? cantidades.GetValueOrDefault(situacion) : filasBandeja.Count;
            boton.Text = $"{nombre} ({cantidad})";
        }
    }

    private void Filtrar()
    {
        var elegida = Seleccion;
        var consulta = buscar.Text.Trim();
        string cedula = consulta.Replace("-", "").Replace(" ", "");
        var visibles = filasBandeja
            .Where(fila => estadoSeleccionado is null || fila.Situacion == estadoSeleccionado)
            .Where(fila => fila.Nombre.Contains(consulta, StringComparison.CurrentCultureIgnoreCase)
                || (cedula.Length > 0 && fila.Cedula.Contains(cedula, StringComparison.Ordinal)))
            .ToList();
        lista.DataSource = visibles;
        RestaurarSeleccion(visibles, elegida);
        bool ampliar = visibles.Count == 0 && consulta.Length > 0 && estadoSeleccionado is not null;
        buscarEnTodas.Visible = ampliar;
        resultados.Text = ampliar
            ? $"Sin coincidencias en {filtros.Single(filtro => filtro.Estado == estadoSeleccionado).Nombre}"
            : TextoDeResultados(visibles.Count, consulta);
        ActualizarAcciones();
    }

    private void RestaurarSeleccion(List<FilaHistoria> visibles, FilaHistoria? elegida)
    {
        if (elegida is null) return;
        int indice = visibles.FindIndex(fila => elegida.Admision is { } d
            ? fila.Admision?.Id == d.Id : fila.Atencion?.Id == elegida.Atencion!.Id);
        if (indice >= 0) lista.CurrentCell = lista.Rows[indice].Cells[0];
    }

    private string TextoDeResultados(int cantidad, string consulta) => cantidad switch
    {
        0 when filasBandeja.Count == 0 => "Todavía no hay historias",
        0 when consulta.Length != 0 => "No se encontraron pacientes",
        0 when estadoSeleccionado == EstadoConsulta.Pendiente => "No hay pacientes pendientes",
        0 => "No hay historias en esta vista",
        1 => "1 historia",
        _ => $"{cantidad} historias"
    };
}
