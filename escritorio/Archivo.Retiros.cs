using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed record RetiroCompartido(Guid Id, long Orden);

internal sealed partial class Archivo
{
    private void InicializarRetiros()
    {
        using var orden = Comando("""
            CREATE TABLE IF NOT EXISTS retiro_historia(
                historia INTEGER PRIMARY KEY REFERENCES atencion(id), autor TEXT NOT NULL, fecha TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS retiro_derivacion(
                derivacion TEXT PRIMARY KEY, autor TEXT NOT NULL, fecha TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS retiro_pendiente(derivacion TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS retiro_recibido(id INTEGER PRIMARY KEY CHECK(id=1), orden INTEGER NOT NULL);
            """, null);
        orden.ExecuteNonQuery();
    }

    internal void Retirar(Medico administrador, long? historia, Guid? derivacion)
    {
        using var tx = conexion.BeginTransaction();
        ExigirAdministrador(administrador, tx);
        if (derivacion is { } id) RetirarDerivacion(administrador, id, tx);
        else if (historia is { } numero) RetirarHistoria(administrador, numero, tx);
        else throw new ArgumentException("Selecciona la atención que quieres retirar.");
        tx.Commit();
    }

    private void RetirarDerivacion(Medico administrador, Guid id, SqliteTransaction tx)
    {
        using var orden = Comando("""
            INSERT OR IGNORE INTO retiro_derivacion(derivacion,autor,fecha)
            SELECT $id,$autor,$fecha
            WHERE EXISTS (SELECT 1 FROM derivacion WHERE id=$id UNION ALL SELECT 1 FROM cambio_consulta_recibido WHERE id=$id)
            """, tx, ("$id", id.ToString()), ("$autor", administrador.Exequatur), ("$fecha", DateTime.UtcNow));
        if (orden.ExecuteNonQuery() != 1) throw new InvalidOperationException("La admisión ya no está en el listado.");
        using var pendiente = Comando("""
            DELETE FROM cambio_consulta_pendiente WHERE id=$id;
            INSERT INTO retiro_pendiente(derivacion) VALUES($id);
            """, tx, ("$id", id.ToString()));
        pendiente.ExecuteNonQuery();
    }

    internal List<Guid> RetirosPendientes()
    {
        using var orden = Comando("SELECT derivacion FROM retiro_pendiente ORDER BY rowid LIMIT 100", null);
        using var filas = orden.ExecuteReader();
        var ids = new List<Guid>();
        while (filas.Read()) ids.Add(Guid.Parse(filas.GetString(0)));
        return ids;
    }

    internal long UltimoRetiroRecibido()
    {
        using var orden = Comando("SELECT coalesce((SELECT orden FROM retiro_recibido WHERE id=1),0)", null);
        return (long)orden.ExecuteScalar()!;
    }

    internal bool IntegrarRetiros(IEnumerable<RetiroCompartido> lote, IEnumerable<Guid> publicados)
    {
        using var tx = conexion.BeginTransaction();
        bool cambios = false;
        foreach (var retiro in lote) cambios |= RegistrarRetiroRecibido(retiro, tx);
        foreach (var id in publicados) QuitarRetiroPendiente(id, tx);
        tx.Commit();
        return cambios;
    }

    private bool RegistrarRetiroRecibido(RetiroCompartido retiro, SqliteTransaction tx)
    {
        if (retiro.Id == Guid.Empty || retiro.Orden < 1) throw new InvalidDataException("Retiro recibido incompleto.");
        using var guardar = Comando("""
            INSERT OR IGNORE INTO retiro_derivacion(derivacion,autor,fecha) VALUES($id,'Puesto vinculado',$fecha);
            """, tx, ("$id", retiro.Id.ToString()), ("$fecha", DateTime.UtcNow));
        bool cambio = guardar.ExecuteNonQuery() == 1;
        using var ordenar = Comando("""
            INSERT INTO retiro_recibido(id,orden) VALUES(1,$orden)
            ON CONFLICT(id) DO UPDATE SET orden=MAX(orden,excluded.orden);
            DELETE FROM cambio_consulta_pendiente WHERE id=$id;
            """, tx, ("$orden", retiro.Orden), ("$id", retiro.Id.ToString()));
        ordenar.ExecuteNonQuery();
        QuitarRetiroPendiente(retiro.Id, tx);
        return cambio;
    }

    private void QuitarRetiroPendiente(Guid id, SqliteTransaction tx)
    {
        using var quitar = Comando("DELETE FROM retiro_pendiente WHERE derivacion=$id", tx, ("$id", id.ToString()));
        quitar.ExecuteNonQuery();
    }

    private void RetirarHistoria(Medico administrador, long id, SqliteTransaction tx)
    {
        using var orden = Comando("""
            INSERT OR IGNORE INTO retiro_historia(historia,autor,fecha)
            SELECT id,$autor,$fecha FROM atencion WHERE id=$id
              AND NOT EXISTS (SELECT 1 FROM derivacion WHERE historia=$id)
            """, tx, ("$id", id), ("$autor", administrador.Exequatur), ("$fecha", DateTime.UtcNow));
        if (orden.ExecuteNonQuery() != 1) throw new InvalidOperationException("La historia ya no está disponible en el listado.");
    }
}
