create table gineco_privado.estacion_retiro (
    huella bytea primary key check (octet_length(huella) = 32)
);
alter table gineco_privado.estacion_retiro enable row level security;
revoke all on gineco_privado.estacion_retiro from public, anon, authenticated;

create function gineco_privado.puede_retirar() returns boolean
language sql stable security definer set search_path = '' as $$
    select gineco_privado.funcion_estacion() = 'consulta' and exists (
        select 1 from gineco_privado.estacion_retiro
        where huella = sha256(convert_to(
            current_setting('request.headers', true)::jsonb ->> 'x-gineco-retiro', 'UTF8'))
    )
$$;
revoke all on function gineco_privado.puede_retirar() from public, authenticated;
grant execute on function gineco_privado.puede_retirar() to anon;

create table gineco_privado.retiro_consulta (
    orden bigint generated always as identity primary key,
    id uuid not null unique references gineco_privado.derivacion(id)
);
alter table gineco_privado.retiro_consulta enable row level security;
revoke all on gineco_privado.retiro_consulta from public, anon, authenticated;
grant select, insert(id) on gineco_privado.retiro_consulta to anon;
create policy consulta_lee_retiros on gineco_privado.retiro_consulta for select to anon
    using ((select gineco_privado.funcion_estacion()) = 'consulta');
create policy admin_registra_retiros on gineco_privado.retiro_consulta for insert to anon
    with check ((select gineco_privado.puede_retirar()));

create function public.registrar_retiros_consulta(ids uuid[]) returns void
language plpgsql security invoker set search_path = '' as $$
begin
    if gineco_privado.puede_retirar() is distinct from true then
        raise insufficient_privilege;
    end if;
    if ids is null or cardinality(ids) > 100 or array_position(ids, null) is not null then
        raise invalid_parameter_value;
    end if;
    perform pg_advisory_xact_lock(287647157102502);
    insert into gineco_privado.retiro_consulta(id) select unnest(ids) on conflict (id) do nothing;
end $$;

create function public.leer_retiros_consulta(desde bigint) returns table(id uuid, orden bigint)
language plpgsql stable security invoker set search_path = '' as $$
begin
    if gineco_privado.funcion_estacion() is distinct from 'consulta' then
        raise insufficient_privilege;
    end if;
    return query select r.id, r.orden from gineco_privado.retiro_consulta r
        where r.orden > desde order by r.orden limit 100;
end $$;

revoke all on function public.registrar_retiros_consulta(uuid[]), public.leer_retiros_consulta(bigint)
    from public, authenticated;
grant execute on function public.registrar_retiros_consulta(uuid[]), public.leer_retiros_consulta(bigint) to anon;
