create table gineco_privado.alias_cuenta (
    exequatur text primary key check (length(exequatur) between 1 and 40),
    correo text not null unique default (gen_random_uuid()::text || '@cuentas.gineco.invalid')
);

create table gineco_privado.vinculo_administrador (
    huella bytea primary key check (octet_length(huella) = 32),
    exequatur text not null check (length(exequatur) between 1 and 40),
    vence timestamptz not null,
    utilizado timestamptz
);

alter table gineco_privado.alias_cuenta enable row level security;
alter table gineco_privado.vinculo_administrador enable row level security;
revoke all on gineco_privado.alias_cuenta, gineco_privado.vinculo_administrador from public, anon, authenticated;
grant usage on schema gineco_privado to service_role;
grant select, insert on gineco_privado.alias_cuenta to service_role;
grant select, update(utilizado) on gineco_privado.vinculo_administrador to service_role;

create function public.gineco_alias_cuenta(identificador text, reservar boolean) returns text
language plpgsql security invoker set search_path = '' as $$
begin
    if reservar then
        insert into gineco_privado.alias_cuenta(exequatur) values (identificador) on conflict do nothing;
    end if;
    return (select correo from gineco_privado.alias_cuenta where exequatur = identificador);
end $$;

create function gineco_privado.usuario_de_alias(alias_correo text) returns uuid
language sql stable security definer set search_path = '' as $$
    select id from auth.users where email = alias_correo and raw_app_meta_data ? 'gineco'
$$;
revoke all on function gineco_privado.usuario_de_alias(text) from public, anon, authenticated;
grant execute on function gineco_privado.usuario_de_alias(text) to service_role;

create function public.gineco_usuario_de_alias(alias_correo text) returns uuid
language sql stable security invoker set search_path = '' as $$
    select gineco_privado.usuario_de_alias(alias_correo)
$$;

create function public.gineco_consumir_vinculo(identificador text, codigo text) returns boolean
language sql security invoker set search_path = '' as $$
    with aceptado as (
        update gineco_privado.vinculo_administrador
        set utilizado = statement_timestamp()
        where huella = sha256(convert_to(codigo, 'UTF8')) and exequatur = identificador
          and utilizado is null and vence > statement_timestamp()
        returning 1
    ) select exists (select 1 from aceptado)
$$;

revoke all on function public.gineco_alias_cuenta(text,boolean), public.gineco_usuario_de_alias(text),
    public.gineco_consumir_vinculo(text,text) from public, anon, authenticated;
grant execute on function public.gineco_alias_cuenta(text,boolean), public.gineco_usuario_de_alias(text),
    public.gineco_consumir_vinculo(text,text) to service_role;
