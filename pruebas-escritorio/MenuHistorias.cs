using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gineco.Pruebas;

public sealed partial class Ventanas
{
    private const uint ContextMenuMessage = 0x007B;
    private const uint CommandMessage = 0x0111;
    private const uint LeftButtonDown = 0x0201;
    private const uint LeftButtonUp = 0x0202;
    private const uint RightButtonDown = 0x0204;
    private const uint RightButtonUp = 0x0205;
    private const int LeftButtonKey = 1;
    private const int RightButtonKey = 2;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ElMenuDeHistoriaSeleccionaLaFilaYRespetaElEstadoEnAmbosRoles(bool administrador)
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            var medico = new Medico("01", "Médico ficticio", administrador);
            GuardarHistoriaParaMenu(archivo, medico, "Paciente borrador", 14, false);
            GuardarHistoriaParaMenu(archivo, medico, "Paciente finalizada", 13, true);
            archivo.Recibir([Ficticia(1)]);
            using var ventana = new Historias(archivo, medico);
            ventana.Show();
            FiltroDeBandeja(ventana, "Todas").Checked = true;
            var tabla = Assert.Single(Descendientes(ventana).OfType<DataGridView>());
            int borrador = BuscarFila(tabla, "Paciente borrador");
            int finalizada = BuscarFila(tabla, "Paciente finalizada");
            int pendiente = BuscarFila(tabla, "Paciente 1");
            tabla.CurrentCell = tabla.Rows[borrador].Cells[0];

            var menu = AbrirMenuDeHistoria(tabla, finalizada);
            Assert.Equal(finalizada, tabla.CurrentCell?.RowIndex);
            Assert.True(Opcion(menu, "Abrir historia").Enabled);
            Assert.True(Opcion(menu, "Imprimir").Enabled);
            Assert.True(Opcion(menu, "Exportar PDF").Enabled);
            Assert.True(Opcion(menu, "Eliminar del listado").Enabled);
            menu.Close();

            menu = AbrirMenuDeHistoria(tabla, borrador);
            Assert.False(Opcion(menu, "Imprimir").Enabled);
            Assert.True(Opcion(menu, "Exportar PDF").Enabled);
            menu.Close();

            menu = AbrirMenuDeHistoria(tabla, pendiente);
            Assert.True(Opcion(menu, "Abrir historia").Enabled);
            Assert.False(Opcion(menu, "Imprimir").Enabled);
            Assert.False(Opcion(menu, "Exportar PDF").Enabled);
            Assert.True(Opcion(menu, "Eliminar del listado").Enabled);

            var buscar = Assert.Single(Descendientes(ventana).OfType<TextBox>());
            buscar.Text = "Sin resultados";
            Assert.False(menu.Visible);
            Assert.Empty(tabla.Rows.Cast<DataGridViewRow>());
            buscar.Clear();
            ClicDerecho(tabla, new Point(10, 5), menu);
            Assert.False(menu.Visible);
            Assert.Null(tabla.Columns[0].HeaderCell.ContextMenuStrip);
            var celda = tabla.GetCellDisplayRectangle(0, finalizada, false);
            nint punto = Coordenadas(new Point(15, celda.Top + celda.Height / 2));
            SendMessage(tabla.Handle, LeftButtonDown, LeftButtonKey, punto);
            SendMessage(tabla.Handle, LeftButtonUp, 0, punto);
            Assert.False(menu.Visible);
        });
    }

    [Fact]
    public async Task EliminarDesdeElMenuPideConfirmacionYRetiraSoloLaFilaElegida()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "A", "Administradora ficticia", "abcdef");
            var admin = Assert.IsType<Medico>(archivo.Entrar("A", "Administradora ficticia", "abcdef"));
            archivo.Alta(admin, "M", "Médica ficticia", "ficticia");
            var medica = Assert.IsType<Medico>(archivo.Entrar("M", "Médica ficticia", "ficticia"));
            GuardarHistoriaParaMenu(archivo, medica, "Paciente que se conserva", 14, false);
            GuardarHistoriaParaMenu(archivo, medica, "Paciente que se retira", 13, false);
            using var ventana = new Historias(archivo, medica);
            ventana.Show();
            FiltroDeBandeja(ventana, "Todas").Checked = true;
            var tabla = Assert.Single(Descendientes(ventana).OfType<DataGridView>());
            int otraFila = BuscarFila(tabla, "Paciente que se conserva");
            tabla.CurrentCell = tabla.Rows[otraFila].Cells[0];
            int objetivo = BuscarFila(tabla, "Paciente que se retira");

            ResponderRetiro(AbrirMenuDeHistoria(tabla, objetivo), confirmar: false);
            Assert.Equal(2, archivo.Listar(medica).Count);
            ResponderRetiro(AbrirMenuDeHistoria(tabla, objetivo), confirmar: true, permiso => permiso.Close());
            Assert.Equal(2, archivo.Listar(medica).Count);
            ResponderRetiro(AbrirMenuDeHistoria(tabla, objetivo), confirmar: true, permiso =>
            {
                var local = Assert.IsType<AutorizarRetiroLocal>(permiso);
                var clave = Assert.Single(Descendientes(local).OfType<TextBox>());
                Assert.True(clave.UseSystemPasswordChar);
                var autorizar = Descendientes(local).OfType<Button>().Single(boton => boton.Text == "Autorizar retiro");
                clave.Text = "incorrecta";
                autorizar.PerformClick();
                Assert.Contains(Descendientes(local).OfType<Label>(), rotulo => rotulo.Text.Contains("Verifica la cuenta"));
                Assert.Empty(clave.Text);
                Assert.Equal(2, archivo.Listar(medica).Count);
                clave.Text = "abcdef";
                autorizar.PerformClick();
            });
            Assert.Equal("Paciente que se conserva", Assert.Single(archivo.Listar(medica)).Nombre);
            Assert.Equal("Paciente que se conserva", Assert.Single(tabla.Rows.Cast<DataGridViewRow>()).Cells[0].Value);
        });
    }

    [Fact]
    public async Task LaAutorizacionCompartidaPermiteUnRetiroSinCambiarDeSesion()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            using var transporte = new CuentasEntrePuestos.Servicio { Administrador = false };
            using var cuentas = CuentasEntrePuestos.Crear(transporte);
            var medica = new Medico("M", "Médica ficticia", false);
            archivo.EstablecerSesionCompartida(medica);
            var visita = Ficticia(1);
            archivo.Recibir([visita]);
            var permisoMedico = new SesionCompartida(medica, "token-de-prueba");
            var permisoAdmin = new SesionCompartida(new("QA", "Administradora ficticia", true), "token-de-prueba");
            Assert.Throws<InvalidOperationException>(() => archivo.Retirar(medica, null, visita.Id, permisoMedico));
            Assert.Throws<InvalidOperationException>(() => archivo.Retirar(medica with { }, null, visita.Id, permisoAdmin));
            using var ventana = new Historias(archivo, medica, cuentasCompartidas: cuentas);
            ventana.Show();
            FiltroDeBandeja(ventana, "Todas").Checked = true;
            var tabla = Assert.Single(Descendientes(ventana).OfType<DataGridView>());

            ResponderRetiro(AbrirMenuDeHistoria(tabla, 0), confirmar: true, permiso => permiso.Close());
            Assert.Single(archivo.Bandeja());
            ResponderRetiro(AbrirMenuDeHistoria(tabla, 0), confirmar: true, permiso =>
            {
                var compartida = Assert.IsType<AccesoCompartido>(permiso);
                EscribirCuentaCompartida(compartida, "Autorizar", "ficticia");
                EsperarHasta(() => Descendientes(compartida).OfType<Label>().Any(l => l.Text.StartsWith("Verifica el exequátur")));
                Assert.Single(archivo.Bandeja());
                transporte.Administrador = true;
                EscribirCuentaCompartida(compartida, "Autorizar", "ficticia");
            });
            Assert.Empty(archivo.Bandeja());
            Assert.Equal(visita.Id, Assert.Single(archivo.RetirosPendientes()));
            Assert.Equal(new[] { "entrar", "entrar" }, transporte.Acciones);
            Assert.Throws<InvalidOperationException>(() => archivo.ExigirAdministrador(medica));
        });
    }

    [Fact]
    public async Task UnaFallaAlVerificarLaCuentaLocalNoAutorizaElRetiro()
    {
        await EnVentana(() =>
        {
            string ruta = Path.Combine(Path.GetTempPath(), $"gineco-retiro-{Guid.NewGuid():N}.sqlite3");
            try
            {
                using var archivo = new Archivo(ruta);
                archivo.Alta(null, "A", "Administradora ficticia", "abcdef");
                using var permiso = new AutorizarRetiroLocal(archivo);
                permiso.Show();
                using var conexionDePrueba = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = ruta, Pooling = false }.ToString());
                conexionDePrueba.Open();
                using var quitarTabla = conexionDePrueba.CreateCommand();
                quitarTabla.CommandText = "DROP TABLE medico";
                quitarTabla.ExecuteNonQuery();
                var clave = Assert.Single(Descendientes(permiso).OfType<TextBox>());
                clave.Text = "abcdef";

                Descendientes(permiso).OfType<Button>().Single(boton => boton.Text == "Autorizar retiro").PerformClick();

                Assert.Null(permiso.Administrador);
                Assert.Empty(clave.Text);
                Assert.Contains(Descendientes(permiso).OfType<Label>(), rotulo => rotulo.Text.StartsWith("No se pudo verificar"));
            }
            finally { File.Delete(ruta); }
        });
    }

    private static void GuardarHistoriaParaMenu(Archivo archivo, Medico medico, string nombre, int hora, bool finalizada)
    {
        var historia = new Atencion(0, medico, new DateTime(2026, 11, 24, hora, 0, 0), new() { ["nombre"] = nombre }, finalizada);
        archivo.Guardar(medico, historia, null, "");
    }

    private static int BuscarFila(DataGridView tabla, string nombre) =>
        tabla.Rows.Cast<DataGridViewRow>().Single(fila => Equals(fila.Cells[0].Value, nombre)).Index;

    private static ToolStripMenuItem Opcion(ContextMenuStrip menu, string texto) =>
        menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == texto);

    private static ContextMenuStrip AbrirMenuDeHistoria(DataGridView tabla, int fila)
    {
        var menu = Assert.IsType<ContextMenuStrip>(tabla.Rows[fila].Cells[0].ContextMenuStrip);
        var celda = tabla.GetCellDisplayRectangle(0, fila, false);
        ClicDerecho(tabla, new Point(celda.Left + 15, celda.Top + celda.Height / 2), menu);
        Assert.True(menu.Visible);
        return menu;
    }

    private static void ClicDerecho(DataGridView tabla, Point punto, ContextMenuStrip menu)
    {
        SendMessage(tabla.Handle, RightButtonDown, RightButtonKey, Coordenadas(punto));
        SendMessage(tabla.Handle, RightButtonUp, 0, Coordenadas(punto));
        if (!menu.Visible)
            SendMessage(tabla.Handle, ContextMenuMessage, tabla.Handle, Coordenadas(tabla.PointToScreen(punto)));
    }

    private static nint Coordenadas(Point punto) => (punto.X & 0xFFFF) | (punto.Y << 16);

    private static void ResponderRetiro(ContextMenuStrip menu, bool confirmar, Action<Form>? autorizar = null)
    {
        const int idSi = 6;
        const int idNo = 7;
        bool respondido = false;
        bool solicitado = false;
        using var respuesta = new System.Windows.Forms.Timer { Interval = 25 };
        respuesta.Tick += (_, _) =>
        {
            if (respondido)
            {
                if (Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Visible && f is AutorizarRetiroLocal or AccesoCompartido) is not { } permiso)
                    return;
                respuesta.Stop();
                solicitado = true;
                autorizar!(permiso);
                return;
            }
            nint dialogo = GetActiveWindow();
            if (GetDlgItem(dialogo, idSi) == 0 || GetDlgItem(dialogo, idNo) == 0) return;
            if (!confirmar) respuesta.Stop();
            respondido = true;
            SendMessage(dialogo, CommandMessage, confirmar ? idSi : idNo, 0);
        };
        var eliminar = Opcion(menu, "Eliminar del listado");
        respuesta.Start();
        eliminar.PerformClick();
        Assert.True(respondido);
        Assert.Equal(autorizar is not null, solicitado);
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint ventana, uint mensaje, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern nint GetDlgItem(nint dialogo, int identificador);
}
