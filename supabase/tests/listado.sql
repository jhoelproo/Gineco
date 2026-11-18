begin;
insert into gineco_privado.estacion values
    (sha256(convert_to('listado-admision','UTF8')), 'admision'),
    (sha256(convert_to('listado-consulta','UTF8')), 'consulta');
set local role anon;
do $$
declare
    primera uuid := gen_random_uuid();
    reingreso uuid := gen_random_uuid();
    cambio uuid := gen_random_uuid();
    datos jsonb := '{"nombre":"Ficticia"}';
    edicion jsonb;
    cursor bigint;
begin
    perform set_config('request.headers', '{"x-gineco-estacion":"listado-admision"}', true);
    perform public.enviar_derivacion(primera,15,localtimestamp,'',datos);
    perform public.enviar_derivacion(reingreso,16,localtimestamp,'',datos);
    edicion := jsonb_build_array(jsonb_build_object('evento',cambio,'id',primera,'nombre','Ficticia corregida','cedula','','estado',2));
    begin
        perform public.leer_cambios_consulta(0);
        raise exception 'Admisión con permiso de lectura';
    exception when insufficient_privilege then null;
    end;
    begin
        perform public.registrar_cambios_consulta(edicion);
        raise exception 'Admisión con permiso de escritura';
    exception when insufficient_privilege then null;
    end;
    perform set_config('request.headers', '{}', true);
    begin
        perform public.leer_cambios_consulta(0);
        raise exception 'Lectura sin credencial';
    exception when insufficient_privilege then null;
    end;
    perform set_config('request.headers', '{"x-gineco-estacion":"listado-consulta"}', true);
    perform public.registrar_cambios_consulta(edicion);
    perform public.registrar_cambios_consulta(edicion);
    if (select count(*) from public.leer_cambios_consulta(0) where id=primera) <> 1 then
        raise exception 'El reintento duplicó el cambio';
    end if;
    select orden into strict cursor from public.leer_cambios_consulta(0)
        where id=primera and visita=15 and nombre='Ficticia corregida' and estado=2;
    begin
        perform public.registrar_cambios_consulta('[{"estado":9}]');
        raise exception 'Se aceptó un cambio incompleto';
    exception when not_null_violation then null;
    end;
    begin
        perform public.registrar_cambios_consulta(jsonb_build_array(
            jsonb_build_object('evento',gen_random_uuid(),'id',primera,'nombre','Ficticia corregida','cedula','','estado',4)));
        raise exception 'El puesto intentó retirar una historia mediante el enlace de consulta';
    exception when check_violation then null;
    end;
    perform public.registrar_cambios_consulta(jsonb_build_array(
        jsonb_build_object('evento',gen_random_uuid(),'id',primera,'nombre','Ficticia corregida','cedula','','estado',3)));
    if (select count(*) from public.leer_cambios_consulta(cursor) where id=primera and estado=3) <> 1
        or exists (select from public.leer_cambios_consulta(cursor) where id=reingreso or estado<>3) then
        raise exception 'El listado no refleja la impresión o se cruzó con un reingreso';
    end if;
end $$;
rollback;
