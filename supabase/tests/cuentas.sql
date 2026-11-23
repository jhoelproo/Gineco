begin;

insert into gineco_privado.vinculo_administrador(huella, exequatur, vence) values
    (sha256(convert_to('codigo-ficticio-vigente', 'UTF8')), 'med-001', now() + interval '1 hour'),
    (sha256(convert_to('codigo-ficticio-vencido', 'UTF8')), 'med-002', now() - interval '1 hour');

set local role anon;
do $$ begin
    begin
        perform public.gineco_alias_cuenta('med-001', true);
        raise exception 'El cliente público pudo reservar un alias';
    exception when insufficient_privilege then null;
    end;
    begin
        perform public.gineco_consumir_vinculo('med-001', 'codigo-ficticio-vigente');
        raise exception 'El cliente público pudo consumir el vínculo';
    exception when insufficient_privilege then null;
    end;
end $$;

set local role service_role;
do $$ declare correo text;
begin
    if public.gineco_alias_cuenta('med-001', false) is not null then
        raise exception 'La consulta de una cuenta inexistente devolvió un alias';
    end if;
    correo := public.gineco_alias_cuenta('med-001', true);
    if correo is null or correo <> public.gineco_alias_cuenta('med-001', true)
       or correo = public.gineco_alias_cuenta('med-002', true) then
        raise exception 'El alias no es estable y único';
    end if;
    if public.gineco_consumir_vinculo('med-002', 'codigo-ficticio-vigente')
       or public.gineco_consumir_vinculo('med-002', 'codigo-ficticio-vencido') then
        raise exception 'Se aceptó un vínculo ajeno o vencido';
    end if;
    if not public.gineco_consumir_vinculo('med-001', 'codigo-ficticio-vigente')
       or public.gineco_consumir_vinculo('med-001', 'codigo-ficticio-vigente') then
        raise exception 'El vínculo no fue de un solo uso';
    end if;
end $$;

reset role;
insert into auth.users(id, email, raw_app_meta_data)
select '908d8665-2e7d-4a5c-891e-c8571bc75743', correo, '{"gineco":{"exequatur":"med-001"}}'
from gineco_privado.alias_cuenta where exequatur = 'med-001';

set local role service_role;
do $$ begin
    if public.gineco_usuario_de_alias(public.gineco_alias_cuenta('med-001', false)) is distinct from
       '908d8665-2e7d-4a5c-891e-c8571bc75743'::uuid then
        raise exception 'El restablecimiento no identificó a la cuenta de Auth';
    end if;
end $$;

rollback;
