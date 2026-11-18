using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed class SugerenciasMedicacion : FlowLayoutPanel
{
    private readonly Func<List<MedicacionHistorica>> consultar;
    private readonly ComboBox lista = new()
    {
        Name = "sugerenciasMedicacion",
        AccessibleName = "Medicación histórica para revisar",
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 260,
        DropDownWidth = 490
    };
    private readonly TextBox vista = new()
    {
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Width = 260,
        Height = 55,
        AccessibleName = "Texto completo de la sugerencia"
    };
    private readonly Label estado = Estilo.Texto("", 10);
    private readonly Button anadir = Estilo.Boton("Añadir sugerencia");

    internal SugerenciasMedicacion(Func<List<MedicacionHistorica>> consultar, Action<string> anadirTexto)
    {
        this.consultar = consultar;
        AutoSize = true;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        Controls.Add(Estilo.Texto("Sugerencias de historias anteriores", 11, true));
        Controls.Add(estado);
        Controls.Add(lista);
        Controls.Add(vista);
        Controls.Add(anadir);
        lista.SelectedIndexChanged += (_, _) => MostrarSeleccion();
        anadir.Click += (_, _) =>
        {
            if (lista.SelectedItem is MedicacionHistorica item) anadirTexto(item.Texto);
        };
        SizeChanged += (_, _) => vista.Width = lista.Width = Math.Clamp(Width - 16, 160, 460);
    }

    internal void Actualizar()
    {
        try
        {
            var items = consultar();
            lista.DataSource = items;
            estado.Text = items.Count == 0 ? "No hay sugerencias aprobadas para este diagnóstico y perfil." : "";
            estado.Visible = items.Count == 0;
        }
        catch (SqliteException)
        {
            lista.DataSource = null;
            estado.Text = "No se pudieron consultar las sugerencias.";
            estado.Visible = true;
        }
        lista.Visible = lista.Items.Count > 0;
        MostrarSeleccion();
    }

    private void MostrarSeleccion()
    {
        var item = lista.SelectedItem as MedicacionHistorica;
        vista.Text = item?.Texto ?? "";
        vista.Visible = anadir.Visible = item is not null;
    }
}

internal sealed class ConfigurarMedicacion : UserControl
{
    private readonly Archivo archivo;
    private readonly Medico admin;
    private readonly ComboBox perfil = new() { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView tabla = new()
    {
        Name = "medicacionHistorica",
        Dock = DockStyle.Fill,
        BackgroundColor = Estilo.Papel,
        ReadOnly = true,
        AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false
    };
    private readonly Button aprobar = Estilo.Boton("Aprobar", Estilo.Guardar);
    private readonly Button retirar = Estilo.Boton("Retirar");

    internal ConfigurarMedicacion(Archivo archivo, Medico admin)
    {
        this.archivo = archivo;
        this.admin = admin;
        Dock = DockStyle.Fill;
        Padding = new Padding(12);
        tabla.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        AgregarColumna("Diagnóstico", nameof(MedicacionHistorica.Diagnostico), 28);
        AgregarColumna("Texto registrado en medicación", nameof(MedicacionHistorica.Texto), 49);
        AgregarColumna("Historias", nameof(MedicacionHistorica.Historias), 11);
        tabla.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Aprobada", DataPropertyName = nameof(MedicacionHistorica.Aprobada), FillWeight = 12 });
        var barra = Estilo.Barra(DockStyle.Top);
        perfil.AccessibleName = "Perfil de sugerencias";
        barra.Controls.Add(perfil);
        barra.Controls.Add(aprobar);
        barra.Controls.Add(retirar);
        var nota = Estilo.Texto("Revisa el texto completo y aprueba únicamente sugerencias reutilizables sin datos de pacientes.", 10);
        nota.Dock = DockStyle.Bottom;
        Controls.Add(tabla);
        Controls.Add(barra);
        Controls.Add(nota);
        aprobar.Click += (_, _) => Cambiar(true);
        retirar.Click += (_, _) => Cambiar(false);
        tabla.SelectionChanged += (_, _) => ActualizarBotones();
        perfil.SelectedIndexChanged += (_, _) => Cargar();
        perfil.DataSource = archivo.LeerPerfiles();
        ActualizarBotones();
    }

    private void AgregarColumna(string titulo, string propiedad, float peso) =>
        tabla.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = titulo, DataPropertyName = propiedad, FillWeight = peso });

    private void Cargar()
    {
        if (perfil.SelectedItem is PerfilClinico actual)
            tabla.DataSource = archivo.MedicacionParaRevisar(admin, actual.Id);
        ActualizarBotones();
    }

    private void ActualizarBotones()
    {
        var item = tabla.CurrentRow?.DataBoundItem as MedicacionHistorica;
        aprobar.Enabled = item is { Aprobada: false };
        retirar.Enabled = item is { Aprobada: true };
    }

    private void Cambiar(bool aceptar)
    {
        if (perfil.SelectedItem is not PerfilClinico actual || tabla.CurrentRow?.DataBoundItem is not MedicacionHistorica item)
            return;
        try
        {
            archivo.CambiarAprobacionMedicacion(admin, actual.Id, item, aceptar);
            Cargar();
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or SqliteException)
        {
            MessageBox.Show(this, error.Message, "Sugerencias de medicamentos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
