using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed partial class Archivo
{
    private void InicializarRetiros()
    {
        using var orden = Comando("""
            CREATE TABLE IF NOT EXISTS retiro_historia(
                historia INTEGER PRIMARY KEY REFERENCES atencion(id), autor TEXT NOT NULL, fecha TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS retiro_derivacion(
                derivacion TEXT PRIMARY KEY, autor TEXT NOT NULL, fecha TEXT NOT NULL);
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
        using var pendiente = Comando("DELETE FROM cambio_consulta_pendiente WHERE id=$id", tx, ("$id", id.ToString()));
        pendiente.ExecuteNonQuery();
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
