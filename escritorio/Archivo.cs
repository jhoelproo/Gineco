using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed partial class Archivo : IDisposable
{
    internal const int MinimoClave = 6;
    private const int Iteraciones = 600_000;
    private readonly SqliteConnection conexion;

    static Archivo() => SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());

    internal Archivo(string ruta)
    {
        conexion = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = ruta, Pooling = false, ForeignKeys = true }.ToString());
        conexion.Open();
        using var comando = Comando("""
            CREATE TABLE IF NOT EXISTS medico(
                exequatur TEXT PRIMARY KEY COLLATE NOCASE, nombre TEXT NOT NULL, admin INTEGER NOT NULL,
                activo INTEGER NOT NULL, sal BLOB NOT NULL, hash BLOB NOT NULL);
            CREATE TABLE IF NOT EXISTS atencion(
                id INTEGER PRIMARY KEY, responsable TEXT NOT NULL, nombre_medico TEXT NOT NULL,
                fecha TEXT NOT NULL, campos TEXT NOT NULL, finalizada INTEGER NOT NULL, version INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS revision(
                id INTEGER PRIMARY KEY, atencion INTEGER NOT NULL REFERENCES atencion(id),
                autor TEXT NOT NULL, fecha TEXT NOT NULL, motivo TEXT NOT NULL, anterior TEXT);
            """, null);
        comando.ExecuteNonQuery();
        InicializarPerfiles();
        InicializarDerivaciones();
    }

    private SqliteCommand Comando(string sql, SqliteTransaction? transaccion, params (string, object?)[] parametros)
    {
        var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
            comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        return comando;
    }

    internal bool SinCuentas()
    {
        using var comando = Comando("SELECT NOT EXISTS(SELECT 1 FROM medico)", null);
        return (long)comando.ExecuteScalar()! == 1;
    }

    internal void Alta(Medico? autor, string exequatur, string nombre, string clave)
    {
        ValidarCuenta(exequatur, nombre, clave);
        var sal = RandomNumberGenerator.GetBytes(16);
        using var transaccion = conexion.BeginTransaction();
        using var comando = Comando("""
            INSERT INTO medico(exequatur,nombre,admin,activo,sal,hash)
            SELECT $id,$nombre,$inicial,1,$sal,$hash
            WHERE ($inicial=1 AND NOT EXISTS(SELECT 1 FROM medico))
               OR EXISTS(SELECT 1 FROM medico WHERE exequatur=$autor AND admin=1 AND activo=1)
            """, transaccion, ("$id", exequatur), ("$nombre", nombre), ("$inicial", autor is null),
            ("$autor", autor?.Exequatur), ("$sal", sal), ("$hash", Derivar(clave, sal)));
        if (comando.ExecuteNonQuery() != 1) throw new InvalidOperationException("No tienes permiso para crear esta cuenta.");
        transaccion.Commit();
    }

    private static void ValidarCuenta(string exequatur, string nombre, string clave)
    {
        if (exequatur.Length is < 1 or > 40 || nombre.Length is < 1 or > 300 || clave.Length < MinimoClave)
            throw new ArgumentException($"Introduce el nombre, un exequátur de hasta 40 caracteres y una contraseña de al menos {MinimoClave} caracteres.");
    }

    private static byte[] Derivar(string clave, byte[] sal) =>
        Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, 32);

    internal Medico? Entrar(string exequatur, string nombre, string clave) =>
        Autenticar(exequatur, nombre, clave, null);

    private Medico? Autenticar(string exequatur, string nombre, string clave, SqliteTransaction? transaccion)
    {
        using var comando = Comando("SELECT exequatur,nombre,admin,sal,hash FROM medico WHERE exequatur=$id AND activo=1",
            transaccion, ("$id", exequatur));
        using var lector = comando.ExecuteReader();
        if (!lector.Read()) return null;
        var valido = CryptographicOperations.FixedTimeEquals((byte[])lector[4], Derivar(clave, (byte[])lector[3]));
        return valido && string.Equals(nombre, lector.GetString(1), StringComparison.OrdinalIgnoreCase)
            ? new(lector.GetString(0), lector.GetString(1), lector.GetBoolean(2)) : null;
    }

    internal List<Medico> UsuariosActivos()
    {
        using var comando = Comando("SELECT exequatur,nombre,admin FROM medico WHERE activo=1 ORDER BY nombre,exequatur", null);
        using var lector = comando.ExecuteReader();
        var usuarios = new List<Medico>();
        while (lector.Read())
            usuarios.Add(new(lector.GetString(0), lector.GetString(1), lector.GetBoolean(2)));
        return usuarios;
    }

    internal bool RestablecerClave(string exequatur, Medico administrador, string claveAdministrador, string nuevaClave)
    {
        if (nuevaClave.Length < MinimoClave)
            throw new ArgumentException($"La contraseña debe tener al menos {MinimoClave} caracteres.");
        using var transaccion = conexion.BeginTransaction();
        if (Autenticar(administrador.Exequatur, administrador.Nombre, claveAdministrador, transaccion)?.Administrador != true)
            return false;
        byte[] sal = RandomNumberGenerator.GetBytes(16);
        using var comando = Comando(
            "UPDATE medico SET sal=$sal,hash=$hash WHERE exequatur=$id AND activo=1",
            transaccion, ("$sal", sal), ("$hash", Derivar(nuevaClave, sal)), ("$id", exequatur));
        bool actualizada = comando.ExecuteNonQuery() == 1;
        transaccion.Commit();
        return actualizada;
    }

    internal List<Atencion> Listar(Medico usuario)
    {
        var perfiles = LeerPerfilesDeHistorias();
        using var comando = Comando("""
            SELECT id,responsable,nombre_medico,fecha,campos,finalizada,version FROM atencion
            WHERE responsable=$autor OR $admin=1 OR id IN (SELECT historia FROM derivacion)
            ORDER BY fecha DESC,id DESC
            """, null, ("$autor", usuario.Exequatur), ("$admin", usuario.Administrador));
        using var lector = comando.ExecuteReader();
        var historias = new List<Atencion>();
        while (lector.Read())
            historias.Add(new(lector.GetInt64(0), new(lector.GetString(1), lector.GetString(2), false),
                lector.GetDateTime(3), JsonSerializer.Deserialize<Dictionary<string, string>>(lector.GetString(4))!,
                lector.GetBoolean(5), lector.GetInt32(6))
            { Perfil = perfiles.GetValueOrDefault(lector.GetInt64(0)) });
        return historias;
    }

    internal ResultadoAfiliada BuscarAfiliada(Medico usuario, Identificadores ids)
    {
        if (ids.Vacio) return new([]);
        if (usuario.Administrador) ExigirAdministrador(usuario);
        return Afiliadas.Buscar(Listar(usuario), ids);
    }

    internal long Guardar(Medico autor, Atencion actual, Atencion? anterior, string motivo)
    {
        actual = actual with { Perfil = actual.Perfil ?? anterior?.Perfil };
        Clinica.Validar(actual.Campos);
        if (anterior?.Finalizada == true && (!actual.Finalizada || string.IsNullOrWhiteSpace(motivo)))
            throw new ArgumentException("Escribe el motivo de la corrección de la historia finalizada.");
        using var transaccion = conexion.BeginTransaction();
        var id = anterior is null ? Insertar(autor, actual, transaccion) : Actualizar(autor, actual, anterior, transaccion);
        GuardarPerfilDeHistoria(id, actual, transaccion);
        using var revision = Comando("""
            INSERT INTO revision(atencion,autor,fecha,motivo,anterior) VALUES($id,$autor,$fecha,$motivo,$anterior)
            """, transaccion, ("$id", id), ("$autor", autor.Exequatur), ("$fecha", DateTime.UtcNow),
            ("$motivo", motivo), ("$anterior", anterior is null ? null : JsonSerializer.Serialize(anterior)));
        revision.ExecuteNonQuery();
        transaccion.Commit();
        return id;
    }

    private long Insertar(Medico autor, Atencion actual, SqliteTransaction transaccion)
    {
        using var comando = Comando("""
            INSERT INTO atencion(responsable,nombre_medico,fecha,campos,finalizada,version)
            VALUES($autor,$nombre,$fecha,$campos,$finalizada,0);
            SELECT last_insert_rowid();
            """, transaccion, ("$autor", autor.Exequatur), ("$nombre", autor.Nombre), ("$fecha", actual.Fecha),
            ("$campos", JsonSerializer.Serialize(actual.Campos)), ("$finalizada", actual.Finalizada));
        return (long)comando.ExecuteScalar()!;
    }

    private long Actualizar(Medico autor, Atencion actual, Atencion anterior, SqliteTransaction transaccion)
    {
        using var comando = Comando("""
            UPDATE atencion SET fecha=$fecha,campos=$campos,finalizada=$finalizada,version=version+1,
                responsable=CASE WHEN finalizada=0 AND id IN (SELECT historia FROM derivacion) THEN $autor ELSE responsable END,
                nombre_medico=CASE WHEN finalizada=0 AND id IN (SELECT historia FROM derivacion) THEN $nombre ELSE nombre_medico END
            WHERE id=$id AND version=$version AND (responsable=$autor OR $admin=1 OR id IN (SELECT historia FROM derivacion))
            """, transaccion, ("$id", anterior.Id), ("$version", anterior.Version), ("$autor", autor.Exequatur),
            ("$admin", autor.Administrador), ("$nombre", autor.Nombre), ("$fecha", actual.Fecha), ("$campos", JsonSerializer.Serialize(actual.Campos)),
            ("$finalizada", actual.Finalizada));
        if (comando.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("La historia cambió o no tienes acceso. Vuelve al historial.");
        return anterior.Id;
    }

    public void Dispose() => conexion.Dispose();
}
