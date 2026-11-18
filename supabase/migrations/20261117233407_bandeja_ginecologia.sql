create schema gineco_privado;
revoke all on schema gineco_privado from public;
grant usage on schema gineco_privado to anon;

create table gineco_privado.estacion (
    huella bytea primary key check (octet_length(huella) = 32),
    funcion text not null check (funcion in ('admision','consulta'))
);
alter table gineco_privado.estacion enable row level security;
revoke all on gineco_privado.estacion from public, anon, authenticated;

create function gineco_privado.funcion_estacion() returns text
language sql stable security definer set search_path = '' as $$
    select funcion from gineco_privado.estacion
    where huella = sha256(convert_to(
        current_setting('request.headers', true)::jsonb ->> 'x-gineco-estacion', 'UTF8'))
$$;
revoke all on function gineco_privado.funcion_estacion() from public;
grant execute on function gineco_privado.funcion_estacion() to anon;

create table gineco_privado.derivacion (
    id uuid primary key,
    visita bigint not null check (visita > 0),
    llegada timestamp not null,
    prioridad text not null check (length(prioridad) <= 80),
    campos jsonb not null check (jsonb_typeof(campos) = 'object'
        and jsonb_typeof(campos->'nombre') = 'string' and campos ? 'nombre'
        and length(trim(campos->>'nombre')) between 1 and 300
        and octet_length(campos::text) <= 24000
        and campos - array['nombre','cedula','nss','edad','telefono','ars','direccion','nacionalidad','motivo'] = '{}'::jsonb
        and not jsonb_path_exists(campos, '$.* ? (@.type() != "string")')),
    recibida boolean not null default false
);
create index derivaciones_pendientes on gineco_privado.derivacion(llegada,id) where not recibida;
alter table gineco_privado.derivacion enable row level security;
revoke all on gineco_privado.derivacion from public, anon, authenticated;
grant insert(id,visita,llegada,prioridad,campos), select, update(recibida)
    on gineco_privado.derivacion to anon;
create policy enviar on gineco_privado.derivacion for insert to anon
    with check ((select gineco_privado.funcion_estacion()) = 'admision' and not recibida);
create policy consultar on gineco_privado.derivacion for select to anon
    using ((select gineco_privado.funcion_estacion()) = 'consulta');
create policy confirmar on gineco_privado.derivacion for update to anon
    using ((select gineco_privado.funcion_estacion()) = 'consulta')
    with check ((select gineco_privado.funcion_estacion()) = 'consulta' and recibida);

create function public.enviar_derivacion(id uuid, visita bigint, llegada timestamp, prioridad text, campos jsonb)
returns void language sql security invoker set search_path = '' as $$
    insert into gineco_privado.derivacion(id,visita,llegada,prioridad,campos)
    values ($1,$2,$3,$4,$5) on conflict do nothing
$$;
create function public.recibir_derivaciones()
returns table(id uuid,visita bigint,llegada timestamp,prioridad text,campos jsonb)
language plpgsql stable security invoker set search_path = '' as $$
begin
    if gineco_privado.funcion_estacion() is distinct from 'consulta' then
        raise insufficient_privilege;
    end if;
    return query select d.id,d.visita,d.llegada,d.prioridad,d.campos
        from gineco_privado.derivacion d where not d.recibida order by d.llegada,d.id limit 100;
end
$$;
create function public.confirmar_recepcion(ids uuid[]) returns void
language sql security invoker set search_path = '' as $$
    update gineco_privado.derivacion set recibida = true where id = any(ids) and not recibida
$$;
revoke all on function public.enviar_derivacion(uuid,bigint,timestamp,text,jsonb),
    public.recibir_derivaciones(), public.confirmar_recepcion(uuid[]) from public, authenticated;
grant execute on function public.enviar_derivacion(uuid,bigint,timestamp,text,jsonb),
    public.recibir_derivaciones(), public.confirmar_recepcion(uuid[]) to anon;
