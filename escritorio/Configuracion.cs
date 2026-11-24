namespace Gineco;

internal sealed class Configuracion : Form
{
    private readonly Archivo archivo;
    private readonly Medico usuario;
    private readonly ComboBox perfil = new() { Width = 275, DropDownStyle = ComboBoxStyle.DropDownList, Name = "perfilConfiguracion" };
    private readonly DataGridView campos = Estilo.Tabla("Campos predeterminados");
    private readonly DataGridView propuestas = Estilo.Tabla("Propuestas de los médicos");
    private readonly Label sinPropuestas = Estilo.MensajeVacio("Todavía no hay propuestas para este perfil.");
    private readonly Button aceptar = Estilo.Boton("Usar propuesta como predeterminado");
    private PerfilClinico activo = new("", "", []);
    private readonly Label estado = Estilo.Texto("", 10);
    private bool cargando;
    private bool modificado;

    internal Configuracion(Archivo archivo, Medico usuario)
    {
        archivo.ExigirAdministrador(usuario);
        this.archivo = archivo;
        this.usuario = usuario;
        Estilo.Ventana(this);
        Text = "Configuración interna";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1000, 700);
        MinimumSize = new Size(780, 546);
        var pestanas = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(14, 7),
            DrawMode = TabDrawMode.OwnerDrawFixed,
            AccessibleName = "Secciones de configuración"
        };
        pestanas.DrawItem += DibujarPestana;
        pestanas.TabPages.Add(PaginaPerfiles());
        pestanas.TabPages.Add(PaginaCorrecciones());
        pestanas.TabPages.Add(new TabPage("Sugerencias de medicamentos")
        {
            BackColor = Estilo.Fondo,
            Controls = { new ConfigurarMedicacion(archivo, usuario) }
        });
        var marco = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 12) };
        marco.Controls.Add(pestanas);
        Controls.Add(marco);
        estado.Dock = DockStyle.Bottom;
        estado.Padding = new Padding(22, 0, 22, 7);
        estado.ForeColor = Estilo.Guardar;
        estado.Visible = false;
        Controls.Add(estado);
        Controls.Add(Estilo.Encabezado(Text, "Administra los perfiles clínicos y las sugerencias que verán los médicos."));
        perfil.SelectedIndexChanged += (_, _) => CargarPerfil();
        perfil.DataSource = archivo.LeerPerfiles();
        FormClosing += ConfirmarSalida;
    }

    private static void DibujarPestana(object? remitente, DrawItemEventArgs evento)
    {
        if (remitente is not TabControl pestanas || evento.Index < 0) return;
        bool activa = evento.Index == pestanas.SelectedIndex;
        using var fondo = new SolidBrush(activa ? Estilo.Principal : Estilo.Fondo);
        evento.Graphics.FillRectangle(fondo, evento.Bounds);
        TextRenderer.DrawText(evento.Graphics, pestanas.TabPages[evento.Index].Text, pestanas.Font, evento.Bounds,
            activa ? Estilo.EnPrincipal : Estilo.Tinta, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        evento.DrawFocusRectangle();
    }

    private TabPage PaginaPerfiles()
    {
        var pagina = new TabPage("Perfiles y aprendizaje") { Padding = new Padding(10), BackColor = Estilo.Fondo };
        Estilo.PresentarTabla(campos);
        Estilo.PresentarTabla(propuestas);
        var barra = Estilo.Barra(DockStyle.Top);
        perfil.AccessibleName = "Perfil para editar";
        perfil.Width = 220;
        perfil.Margin = new Padding(0, 10, 12, 0);
        barra.Controls.Add(perfil);
        var guardar = Estilo.Boton("Guardar predeterminados", Estilo.Guardar);
        guardar.Click += (_, _) => Ejecutar(GuardarPerfil);
        barra.Controls.Add(guardar);
        var editar = Estilo.Boton("Editar texto seleccionado");
        editar.Click += (_, _) =>
        {
            if (campos.CurrentRow is { } fila) EditarTexto(fila);
        };
        barra.Controls.Add(editar);
        campos.Name = "predeterminados";
        campos.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Sección / campo", ReadOnly = true, FillWeight = 45 });
        campos.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Texto predeterminado editable", FillWeight = 55 });
        campos.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        campos.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        campos.CellValueChanged += (_, _) => { if (!cargando) modificado = true; };

        var aprendizaje = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        propuestas.ReadOnly = true;
        propuestas.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        propuestas.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        propuestas.Name = "propuestas";
        propuestas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Campo", DataPropertyName = nameof(Propuesta.Etiqueta), FillWeight = 20 });
        propuestas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Texto escrito por médicos", DataPropertyName = nameof(Propuesta.Texto), FillWeight = 65 });
        propuestas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Historias", DataPropertyName = nameof(Propuesta.Historias), FillWeight = 15 });
        var acciones = Estilo.Barra(DockStyle.Top);
        var titulo = Estilo.Texto("Propuestas de los médicos", 12, true);
        titulo.Margin = new Padding(0, 12, 16, 0);
        acciones.Controls.Add(titulo);
        aceptar.Enabled = false;
        aceptar.Click += (_, _) => UsarPropuesta();
        propuestas.SelectionChanged += (_, _) => aceptar.Enabled = propuestas.CurrentRow?.DataBoundItem is Propuesta;
        acciones.Controls.Add(aceptar);
        aprendizaje.Controls.Add(propuestas);
        aprendizaje.Controls.Add(sinPropuestas);
        aprendizaje.Controls.Add(acciones);
        var tablas = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        tablas.RowStyles.Add(new RowStyle(SizeType.Percent, 61));
        tablas.RowStyles.Add(new RowStyle(SizeType.Percent, 39));
        campos.Margin = Padding.Empty;
        tablas.Controls.Add(campos, 0, 0);
        tablas.Controls.Add(aprendizaje, 0, 1);
        pagina.Controls.Add(tablas);
        pagina.Controls.Add(barra);
        return pagina;
    }

    private TabPage PaginaCorrecciones()
    {
        var pagina = new TabPage("Corrección ortográfica") { Padding = new Padding(10), BackColor = Estilo.Fondo };
        var contenido = Estilo.Columna();
        var titulo = Estilo.Texto("Corrección automática activa", 17, true);
        titulo.ForeColor = Estilo.Guardar;
        contenido.Controls.Add(titulo);
        var nota = Estilo.Texto("Antes de imprimir o exportar, la revisión muestra las palabras que se corregirán en el documento.");
        nota.MaximumSize = new Size(560, 0);
        contenido.Controls.Add(nota);
        pagina.Controls.Add(contenido);
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
        var aprendidas = archivo.LeerPropuestas(usuario, activo.Id);
        propuestas.DataSource = aprendidas;
        propuestas.Visible = aprendidas.Count > 0;
        sinPropuestas.Visible = aprendidas.Count == 0;
        aceptar.Enabled = propuestas.CurrentRow?.DataBoundItem is Propuesta;
        estado.Visible = false;
        modificado = false;
        cargando = false;
    }

    private void UsarPropuesta()
    {
        if (propuestas.CurrentRow?.DataBoundItem is not Propuesta propuesta) return;
        var fila = campos.Rows.Cast<DataGridViewRow>().Single(fila => fila.Tag as string == propuesta.Campo);
        EditarTexto(fila, propuesta.Texto);
    }

    private void EditarTexto(DataGridViewRow fila, string? propuesta = null)
    {
        campos.EndEdit();
        using var editor = new EditarPredeterminado(Convert.ToString(fila.Cells[0].Value) ?? "",
            Convert.ToString(fila.Cells[1].Value) ?? "", propuesta);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        fila.Cells[1].Value = editor.Valor;
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
        estado.Visible = true;
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
        if (modificado && MessageBox.Show(this, "Hay cambios sin guardar. ¿Cerrar y descartar los cambios?",
            Text, MessageBoxButtons.YesNo) != DialogResult.Yes) evento.Cancel = true;
    }
}

internal sealed class EditarPredeterminado : Form
{
    private readonly TextBox texto;
    internal string Valor => texto.Text.Trim();

    internal EditarPredeterminado(string campo, string actual, string? propuesta)
    {
        Estilo.Ventana(this);
        Text = campo;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(propuesta is null ? 580 : 840, 400);
        MinimumSize = new Size(480, 320);
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = propuesta is null ? 1 : 2, RowCount = 2, Padding = new Padding(16) };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        texto = AgregarTexto(panel, 0, "Texto predeterminado", actual, false);
        if (propuesta is not null) AgregarTexto(panel, 1, "Propuesta de los médicos", propuesta, true);
        var pie = Estilo.Barra(DockStyle.Bottom);
        if (propuesta is not null)
        {
            var copiar = Estilo.Boton("Copiar propuesta al editor");
            copiar.Click += (_, _) => { texto.Text = propuesta; texto.Focus(); };
            pie.Controls.Add(copiar);
        }
        var aceptar = Estilo.Boton("Aceptar texto", Estilo.Guardar);
        var cancelar = Estilo.Boton("Cancelar");
        aceptar.DialogResult = DialogResult.OK;
        cancelar.DialogResult = DialogResult.Cancel;
        pie.Controls.AddRange([aceptar, cancelar]);
        Controls.Add(panel);
        Controls.Add(pie);
        Controls.Add(Estilo.Encabezado(propuesta is null ? "Editar predeterminado" : "Revisar propuesta"));
        CancelButton = cancelar;
    }

    private static TextBox AgregarTexto(TableLayoutPanel panel, int columna, string titulo, string valor, bool lectura)
    {
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / panel.ColumnCount));
        panel.Controls.Add(Estilo.Texto(titulo, 12, true), columna, 0);
        var campo = new TextBox
        {
            AccessibleName = titulo,
            Multiline = true,
            Dock = DockStyle.Fill,
            ReadOnly = lectura,
            ScrollBars = ScrollBars.Vertical,
            Text = valor
        };
        panel.Controls.Add(campo, columna, 1);
        return campo;
    }
}

internal sealed class RevisionExportacion : Form
{
    private readonly TreeView detalle = new()
    {
        Dock = DockStyle.Fill,
        AccessibleName = "Avisos y correcciones del PDF",
        FullRowSelect = true,
        HideSelection = false,
        ShowLines = false,
        ItemHeight = 24
    };
    internal string? CampoParaRevisar { get; private set; }

    internal RevisionExportacion(ExportacionPreparada preparada, bool imprimir = false)
    {
        Estilo.Ventana(this);
        Text = imprimir ? "Confirmar impresión" : "Revisar antes de exportar";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 480);
        MinimumSize = new Size(560, 350);
        CargarAvisos(preparada);
        if (detalle.Nodes.Count == 1) detalle.Nodes[0].Expand();
        var barra = Estilo.Barra(DockStyle.Bottom);
        var revisar = Estilo.Boton("Revisar historia");
        revisar.Click += (_, _) => RevisarSeleccion();
        detalle.AfterSelect += (_, evento) => revisar.Text = evento.Node?.Tag is string ? "Ir al campo" : "Revisar historia";
        var continuar = Estilo.Boton(imprimir ? "Confirmar e imprimir" : "Continuar y exportar", Estilo.Principal);
        continuar.DialogResult = DialogResult.OK;
        barra.Controls.Add(revisar);
        barra.Controls.Add(continuar);
        var marco = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 0) };
        marco.Controls.Add(detalle);
        Controls.Add(marco);
        Controls.Add(barra);
        Controls.Add(Estilo.Encabezado(Text, "Selecciona un aviso para ir al campo correspondiente."));
        CancelButton = revisar;
        Shown += (_, _) => revisar.Focus();
    }

    private void CargarAvisos(ExportacionPreparada preparada)
    {
        foreach (var aviso in preparada.Revisiones) AgregarAviso(aviso);
        if (preparada.Correcciones.Count > 0)
        {
            var correcciones = detalle.Nodes.Add("Palabras corregidas en el PDF");
            foreach (string palabra in preparada.Correcciones) correcciones.Nodes.Add(palabra);
        }
        if (detalle.Nodes.Count == 0) detalle.Nodes.Add("La historia está lista para enviar a la impresora.");
    }

    private void AgregarAviso(AvisoRevision aviso)
    {
        var titulo = detalle.Nodes.Add(aviso.Estado.Length == 0 ? aviso.Seccion : $"{aviso.Seccion} · {aviso.Estado}");
        foreach (var campo in aviso.Campos) titulo.Nodes.Add(new TreeNode(campo.Value) { Tag = campo.Key });
    }

    private void RevisarSeleccion()
    {
        CampoParaRevisar = detalle.SelectedNode?.Tag as string;
        DialogResult = DialogResult.Retry;
    }
}
