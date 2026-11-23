begin;

insert into gineco_privado.estacion(huella, funcion) values
    (sha256(convert_to('qa-retiros-admision', 'UTF8')), 'admision'),
    (sha256(convert_to('qa-retiros-consulta', 'UTF8')), 'consulta');
insert into gineco_privado.estacion_retiro(huella)
    values (sha256(convert_to('qa-retiros-admin', 'UTF8')));

set local role anon;
set local request.headers = '{"x-gineco-estacion":"qa-retiros-admision"}';
select public.enviar_derivacion('eac61472-331e-4c44-b881-53d4df6a1961', 15, timestamp '2026-11-18 12:30', '', '{"nombre":"Paciente ficticia"}');
do $$ begin
    begin
        perform public.leer_retiros_consulta(0);
        raise exception 'Admisión puede leer retiros';
    exception when insufficient_privilege then null;
    end;
end $$;

set local request.headers = '{"x-gineco-estacion":"qa-retiros-consulta"}';
do $$ begin
    begin
        perform public.registrar_retiros_consulta(array['eac61472-331e-4c44-b881-53d4df6a1961'::uuid]);
        raise exception 'Consulta pudo publicar un retiro sin credencial administrativa';
    exception when insufficient_privilege then null;
    end;
end $$;

set local request.headers = '{"x-gineco-estacion":"qa-retiros-consulta","x-gineco-retiro":"qa-retiros-admin"}';
select public.registrar_retiros_consulta(array['eac61472-331e-4c44-b881-53d4df6a1961'::uuid]);
select public.registrar_retiros_consulta(array['eac61472-331e-4c44-b881-53d4df6a1961'::uuid]);
do $$ begin
    begin
        perform public.registrar_retiros_consulta(array_fill('eac61472-331e-4c44-b881-53d4df6a1961'::uuid, array[101]));
        raise exception 'Se aceptó un lote de más de 100 retiros';
    exception when invalid_parameter_value then null;
    end;
end $$;

set local request.headers = '{"x-gineco-estacion":"qa-retiros-consulta"}';
do $$ declare recibido bigint;
begin
    select orden into strict recibido from public.leer_retiros_consulta(0)
        where id = 'eac61472-331e-4c44-b881-53d4df6a1961';
    if exists (select 1 from public.leer_retiros_consulta(recibido)) then
        raise exception 'Se devolvió un retiro cuyo cursor ya se confirmó';
    end if;
end $$;

rollback;
