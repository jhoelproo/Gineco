using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Gineco;

internal enum EstadoConsulta { Pendiente, EnAtencion, Finalizada, Impresa }

internal sealed record CambioConsulta(Guid Evento, Guid Id, string Nombre, string Cedula, EstadoConsulta Estado,
    long Orden = 0, long Visita = 0, DateTime Llegada = default);

internal sealed partial class Archivo
{
    private void InicializarSincronizacionListado()
    {
        using var orden = Comando("""
            CREATE TABLE IF NOT EXISTS cambio_consulta_pendiente(
                id TEXT PRIMARY KEY, evento TEXT NOT NULL, nombre TEXT NOT NULL, cedula TEXT NOT NULL, estado INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS cambio_consulta_recibido(
                id TEXT PRIMARY KEY, orden INTEGER NOT NULL, visita INTEGER NOT NULL, llegada TEXT NOT NULL,
                nombre TEXT NOT NULL, cedula TEXT NOT NULL, estado INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS cambio_consulta_por_orden ON cambio_consulta_recibido(orden);
            CREATE TABLE IF NOT EXISTS sincronizacion_consulta(
                id INTEGER PRIMARY KEY CHECK(id=1), instante_utc INTEGER NOT NULL);
            """, null);
        orden.ExecuteNonQuery();
    }

    internal DateTimeOffset? UltimaSincronizacion()
    {
        using var orden = Comando("SELECT instante_utc FROM sincronizacion_consulta WHERE id=1", null);
        return orden.ExecuteScalar() is long instante ? DateTimeOffset.FromUnixTimeMilliseconds(instante) : null;
    }

    internal void RegistrarSincronizacion(DateTimeOffset instante)
    {
        using var orden = Comando("""
            INSERT INTO sincronizacion_consulta(id,instante_utc) VALUES(1,$instante)
            ON CONFLICT(id) DO UPDATE SET instante_utc=excluded.instante_utc
            """, null, ("$instante", instante.ToUnixTimeMilliseconds()));
        orden.ExecuteNonQuery();
    }

    private void EncolarCambio(long historia, SqliteTransaction tx)
    {
        using var orden = Comando("""
            SELECT d.id, a.campos, CASE WHEN d.version_impresa=a.version THEN 3 WHEN a.finalizada=1 THEN 2 ELSE 1 END
            FROM derivacion d JOIN atencion a ON a.id=d.historia WHERE d.historia=$historia
            """, tx, ("$historia", historia));
        using var fila = orden.ExecuteReader();
        if (!fila.Read()) return;
        var campos = JsonSerializer.Deserialize<Dictionary<string, string>>(fila.GetString(1))!;
        var cambio = new CambioConsulta(Guid.NewGuid(), Guid.Parse(fila.GetString(0)),
            campos["nombre"], campos.GetValueOrDefault("cedula", ""), (EstadoConsulta)fila.GetInt32(2));
        fila.Close();
        EncolarCambio(cambio, tx);
    }

    private void EncolarCambio(CambioConsulta cambio, SqliteTransaction tx)
    {
        using var orden = Comando("""
            INSERT INTO cambio_consulta_pendiente(id,evento,nombre,cedula,estado) VALUES($id,$evento,$nombre,$cedula,$estado)
            ON CONFLICT(id) DO UPDATE SET evento=excluded.evento,nombre=excluded.nombre,cedula=excluded.cedula,estado=excluded.estado
            """, tx, ("$id", cambio.Id.ToString()), ("$evento", cambio.Evento.ToString()), ("$nombre", cambio.Nombre),
            ("$cedula", cambio.Cedula), ("$estado", (int)cambio.Estado));
        orden.ExecuteNonQuery();
    }

    internal List<CambioConsulta> CambiosPendientes()
    {
        using var orden = Comando("SELECT evento,id,nombre,cedula,estado FROM cambio_consulta_pendiente ORDER BY rowid LIMIT 100", null);
        using var fila = orden.ExecuteReader();
        var cambios = new List<CambioConsulta>();
        while (fila.Read()) cambios.Add(new(Guid.Parse(fila.GetString(0)), Guid.Parse(fila.GetString(1)),
            fila.GetString(2), fila.GetString(3), (EstadoConsulta)fila.GetInt32(4)));
        return cambios;
    }

    internal long UltimoCambioRecibido()
    {
        using var orden = Comando("SELECT coalesce(max(orden),0) FROM cambio_consulta_recibido", null);
        return (long)orden.ExecuteScalar()!;
    }

    internal bool IntegrarCambios(IEnumerable<CambioConsulta> recibidos, IEnumerable<CambioConsulta> enviados)
    {
        using var tx = conexion.BeginTransaction();
        bool actualizado = false;
        foreach (var fila in recibidos) actualizado |= GuardarCambioRecibido(fila, tx);
        foreach (var fila in enviados)
        {
            using var orden = Comando("""
                DELETE FROM cambio_consulta_pendiente WHERE id=$id AND evento=$evento
                """, tx, ("$id", fila.Id.ToString()), ("$evento", fila.Evento.ToString()));
            orden.ExecuteNonQuery();
        }
        tx.Commit();
        return actualizado;
    }

    private bool GuardarCambioRecibido(CambioConsulta fila, SqliteTransaction tx)
    {
        if (fila.Id == Guid.Empty || fila.Visita < 1 || fila.Orden < 1 || fila.Llegada == default ||
            fila.Estado is < EstadoConsulta.EnAtencion or > EstadoConsulta.Impresa || string.IsNullOrWhiteSpace(fila.Nombre))
            throw new InvalidDataException("El servidor devolvió un cambio incompleto.");
        using var orden = Comando("""
            INSERT INTO cambio_consulta_recibido(id,orden,visita,llegada,nombre,cedula,estado)
                VALUES($id,$orden,$visita,$llegada,$nombre,$cedula,$estado)
            ON CONFLICT(id) DO UPDATE SET orden=excluded.orden,nombre=excluded.nombre,cedula=excluded.cedula,estado=excluded.estado
                WHERE excluded.orden > cambio_consulta_recibido.orden
            """, tx, ("$id", fila.Id.ToString()), ("$orden", fila.Orden), ("$visita", fila.Visita), ("$llegada", fila.Llegada),
            ("$nombre", fila.Nombre), ("$cedula", fila.Cedula), ("$estado", (int)fila.Estado));
        return orden.ExecuteNonQuery() > 0;
    }
}
