namespace Gineco;

internal sealed class Configuracion : Form
{
    private readonly Archivo archivo;
    private readonly Medico usuario;
    private readonly ComboBox perfil = new() { Width = 275, DropDownStyle = ComboBoxStyle.DropDownList, Name = "perfilConfiguracion" };
    private readonly DataGridView campos = Tabla();
    private readonly DataGridView correcciones = Tabla();
    private readonly DataGridView propuestas = Tabla();
    private List<Correccion> anteriores = [];
    private PerfilClinico activo = new("", "", []);
    private readonly Label estado = Estilo.Texto("", 10);
    private bool cargando;
    private bool modificado;
    private bool diccionarioModificado;

    internal Configuracion(Archivo archivo, Medico usuario)
    {
        archivo.ExigirAdministrador(usuario);
        this.archivo = archivo;
        this.usuario = usuario;
        Estilo.Ventana(this);
        Text = "Configuración interna";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(950, 640);
        MinimumSize = new Size(740, 480);
        var pestanas = new TabControl { Dock = DockStyle.Fill };
        pestanas.TabPages.Add(PaginaPerfiles());
        pestanas.TabPages.Add(PaginaCorrecciones());
        pestanas.TabPages.Add(new TabPage("Sugerencias de medicamentos") { Controls = { new ConfigurarMedicacion(archivo, usuario) } });
        Controls.Add(pestanas);
        estado.Dock = DockStyle.Bottom;
        estado.Padding = new Padding(12, 4, 12, 4);
        Controls.Add(estado);
        perfil.SelectedIndexChanged += (_, _) => CargarPerfil();
        perfil.DataSource = archivo.LeerPerfiles();
        FormClosing += ConfirmarSalida;
    }

    private static DataGridView Tabla() => new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Estilo.Papel,
        RowHeadersVisible = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(4, 5, 4, 5), WrapMode = DataGridViewTriState.True },
        RowTemplate = { Height = 34 }
    };

    private TabPage PaginaPerfiles()
    {
        var pagina = new TabPage("Perfiles y aprendizaje") { Padding = new Padding(12) };
        var barra = Estilo.Barra(DockStyle.Top);
        barra.Controls.Add(perfil);
        var guardar = Estilo.Boton("Guardar predeterminados", Estilo.Guardar);
        guardar.Click += (_, _) => Ejecutar(GuardarPerfil);
        barra.Controls.Add(guardar);
        campos.Name = "predeterminados";
        campos.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sección / campo", ReadOnly = true, FillWeight = 45 });
        campos.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Texto predeterminado editable", FillWeight = 55 });
        campos.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        campos.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        campos.CellValueChanged += (_, _) => { if (!cargando) modificado = true; };

        var aprendizaje = new Panel { Dock = DockStyle.Bottom, Height = 215 };
        propuestas.ReadOnly = true;
        propuestas.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        propuestas.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        propuestas.Name = "propuestas";
        propuestas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Campo", DataPropertyName = nameof(Propuesta.Etiqueta), FillWeight = 20 });
        propuestas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Texto escrito por médicos", DataPropertyName = nameof(Propuesta.Texto), FillWeight = 65 });
        propuestas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Historias", DataPropertyName = nameof(Propuesta.Historias), FillWeight = 15 });
        var acciones = Estilo.Barra(DockStyle.Top);
        var aceptar = Estilo.Boton("Usar propuesta como predeterminado");
        aceptar.Click += (_, _) => UsarPropuesta();
        acciones.Controls.Add(aceptar);
        aprendizaje.Controls.Add(propuestas);
        aprendizaje.Controls.Add(acciones);
        pagina.Controls.Add(campos);
        pagina.Controls.Add(aprendizaje);
        pagina.Controls.Add(barra);
        return pagina;
    }

    private TabPage PaginaCorrecciones()
    {
        var pagina = new TabPage("Corrección ortográfica") { Padding = new Padding(12) };
        anteriores = archivo.ConsultarCorrecciones(usuario);
        correcciones.Name = "diccionario";
        correcciones.AllowUserToAddRows = true;
        correcciones.AllowUserToDeleteRows = true;
        correcciones.Columns.Add("incorrecta", "Palabra mal escrita");
        correcciones.Columns.Add("correcta", "Reemplazar por");
        foreach (var regla in anteriores) correcciones.Rows.Add(regla.Incorrecta, regla.Correcta);
        correcciones.CellValueChanged += (_, _) => diccionarioModificado = true;
        correcciones.UserDeletedRow += (_, _) => diccionarioModificado = true;
        var nota = Estilo.Texto("Autoriza palabras completas. Se corregirán al exportar el PDF; revisa términos médicos y medicamentos.");
        nota.Dock = DockStyle.Top;
        var barra = Estilo.Barra(DockStyle.Bottom);
        var guardar = Estilo.Boton("Guardar diccionario", Estilo.Guardar);
        guardar.Click += (_, _) => Ejecutar(GuardarDiccionario);
        var eliminar = Estilo.Boton("Quitar regla");
        eliminar.Click += (_, _) =>
        {
            if (correcciones.CurrentRow is { IsNewRow: false } fila)
            {
                correcciones.Rows.Remove(fila);
                diccionarioModificado = true;
            }
        };
        barra.Controls.Add(guardar);
        barra.Controls.Add(eliminar);
        pagina.Controls.Add(correcciones);
        pagina.Controls.Add(barra);
        pagina.Controls.Add(nota);
        return pagina;
    }

    private void CargarPerfil()
    {
        if (perfil.SelectedItem is not PerfilClinico elegido || elegido.Id == activo.Id) return;
        if (modificado && MessageBox.Show(this, "Hay cambios sin guardar. ¿Deseas descartarlos al cambiar de perfil?",
            Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
        {
            perfil.SelectedIndex = ((List<PerfilClinico>)perfil.DataSource!).FindIndex(opcion => opcion.Id == activo.Id);
            return;
        }
        cargando = true;
        activo = archivo.LeerPerfiles().Single(opcion => opcion.Id == elegido.Id);
        campos.Rows.Clear();
        foreach (var (seccion, definicion) in Clinica.Secciones.Where(seccion => seccion.Key != "Datos de la paciente"))
        {
            foreach (var (clave, etiqueta) in definicion.Where(campo => Perfiles.EsPredeterminadoEditable(campo.Key)))
            {
                int indice = campos.Rows.Add($"{seccion} / {etiqueta}", activo.Campos.GetValueOrDefault(clave, ""));
                campos.Rows[indice].Tag = clave;
            }
        }
        propuestas.DataSource = archivo.LeerPropuestas(usuario, activo.Id);
        modificado = false;
        cargando = false;
    }

    private void UsarPropuesta()
    {
        if (propuestas.CurrentRow?.DataBoundItem is not Propuesta propuesta) return;
        var fila = campos.Rows.Cast<DataGridViewRow>().Single(fila => fila.Tag as string == propuesta.Campo);
        fila.Cells[1].Value = propuesta.Texto;
        campos.CurrentCell = fila.Cells[1];
    }

    private void GuardarPerfil()
    {
        campos.EndEdit();
        var valores = campos.Rows.Cast<DataGridViewRow>()
            .ToDictionary(fila => Convert.ToString(fila.Tag) ?? throw new InvalidOperationException("Campo sin identificador."), fila => Convert.ToString(fila.Cells[1].Value)?.Trim() ?? "");
        var actualizado = activo with { Campos = valores };
        archivo.GuardarPerfil(usuario, actualizado);
        activo = actualizado with { Version = activo.Version + 1 };
        modificado = false;
        estado.Text = "Predeterminados guardados. Se aplicarán cuando el médico elija este perfil.";
    }

    private void GuardarDiccionario()
    {
        correcciones.EndEdit();
        var reglas = correcciones.Rows.Cast<DataGridViewRow>().Where(fila => !fila.IsNewRow)
            .Select(fila => new Correccion(Convert.ToString(fila.Cells[0].Value)?.Trim() ?? "",
                Convert.ToString(fila.Cells[1].Value)?.Trim() ?? "")).ToList();
        archivo.GuardarCorrecciones(usuario, reglas, anteriores);
        anteriores = archivo.ConsultarCorrecciones(usuario);
        diccionarioModificado = false;
        estado.Text = "Diccionario guardado.";
    }

    private void Ejecutar(Action accion)
    {
        try { accion(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ConfirmarSalida(object? remitente, FormClosingEventArgs evento)
    {
        campos.EndEdit();
        correcciones.EndEdit();
        if ((modificado || diccionarioModificado) && MessageBox.Show(this, "Hay cambios sin guardar. ¿Cerrar y descartar los cambios?",
            Text, MessageBoxButtons.YesNo) != DialogResult.Yes) evento.Cancel = true;
    }
}

internal sealed class RevisionExportacion : Form
{
    internal RevisionExportacion(ExportacionPreparada preparada, bool imprimir = false)
    {
        Estilo.Ventana(this);
        Text = imprimir ? "Confirmar impresión" : "Revisar antes de exportar";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 480);
        MinimumSize = new Size(560, 350);
        var detalle = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Estilo.Papel,
            AccessibleName = "Avisos y correcciones del PDF",
            Text = string.Join(Environment.NewLine + Environment.NewLine, preparada.Avisos)
        };
        if (preparada.Correcciones.Count > 0)
            detalle.AppendText(Environment.NewLine + Environment.NewLine + "Correcciones autorizadas en el PDF:" +
                Environment.NewLine + string.Join(Environment.NewLine, preparada.Correcciones));
        var barra = Estilo.Barra(DockStyle.Bottom);
        var revisar = Estilo.Boton("Revisar historia");
        revisar.DialogResult = DialogResult.Retry;
        if (imprimir && detalle.TextLength == 0) detalle.Text = "La historia está lista para enviar a la impresora predeterminada.";
        var continuar = Estilo.Boton(imprimir ? "Confirmar e imprimir" : "Continuar y exportar", Estilo.Principal);
        continuar.DialogResult = DialogResult.OK;
        barra.Controls.Add(revisar);
        barra.Controls.Add(continuar);
        Controls.Add(detalle);
        Controls.Add(barra);
        CancelButton = revisar;
        Shown += (_, _) =>
        {
            detalle.Select(0, 0);
            revisar.Focus();
        };
    }
}
