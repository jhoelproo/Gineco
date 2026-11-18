create table gineco_privado.evento_consulta (
    orden bigint generated always as identity primary key,
    evento uuid not null unique,
    id uuid not null references gineco_privado.derivacion(id),
    nombre text not null check (length(nombre) between 1 and 300),
    cedula text not null check (length(cedula) <= 80),
    estado smallint not null check (estado between 1 and 3)
);
create index eventos_consulta_por_derivacion on gineco_privado.evento_consulta(id, orden desc);
alter table gineco_privado.evento_consulta enable row level security;
revoke all on gineco_privado.evento_consulta from public, anon, authenticated;
grant select, insert(evento, id, nombre, cedula, estado) on gineco_privado.evento_consulta to anon;
create policy consulta_lee_eventos on gineco_privado.evento_consulta for select to anon
    using ((select gineco_privado.funcion_estacion()) = 'consulta');
create policy consulta_publica_eventos on gineco_privado.evento_consulta for insert to anon
    with check ((select gineco_privado.funcion_estacion()) = 'consulta');

create function public.registrar_cambios_consulta(cambios jsonb)
returns void language plpgsql security invoker set search_path = '' as $$
declare fila record;
begin
    if gineco_privado.funcion_estacion() is distinct from 'consulta' then
        raise insufficient_privilege;
    end if;
    if jsonb_typeof(cambios) is distinct from 'array' or jsonb_array_length(cambios) > 100 then
        raise invalid_parameter_value;
    end if;
    -- Serializa la asignación del cursor con la confirmación de la transacción.
    perform pg_advisory_xact_lock(287647157102501);
    for fila in select * from jsonb_to_recordset(cambios)
        as dato(evento uuid, id uuid, nombre text, cedula text, estado smallint)
    loop
        insert into gineco_privado.evento_consulta(evento,id,nombre,cedula,estado)
        values (fila.evento,fila.id,fila.nombre,fila.cedula,fila.estado)
        on conflict (evento) do nothing;
    end loop;
end $$;

create function public.leer_cambios_consulta(desde bigint)
returns table(orden bigint, evento uuid, id uuid, visita bigint, llegada timestamp, nombre text, cedula text, estado smallint)
language plpgsql stable security invoker set search_path = '' as $$
begin
    if gineco_privado.funcion_estacion() is distinct from 'consulta' then
        raise insufficient_privilege;
    end if;
    return query select e.orden,e.evento,e.id,d.visita,d.llegada,e.nombre,e.cedula,e.estado
        from gineco_privado.evento_consulta e join gineco_privado.derivacion d on d.id=e.id
        where e.orden > desde order by e.orden limit 100;
end $$;

revoke all on function public.registrar_cambios_consulta(jsonb), public.leer_cambios_consulta(bigint)
    from public, authenticated;
grant execute on function public.registrar_cambios_consulta(jsonb), public.leer_cambios_consulta(bigint) to anon;
