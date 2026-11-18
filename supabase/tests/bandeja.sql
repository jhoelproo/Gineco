-- Ejecutar en una base de pruebas con la migración aplicada.
begin;
insert into gineco_privado.estacion values
    (sha256(convert_to('prueba-emisor','UTF8')), 'admision'),
    (sha256(convert_to('prueba-consulta','UTF8')), 'consulta');
set local role anon;
do $$
declare
    clave uuid := gen_random_uuid();
    datos jsonb := '{"nombre":"Paciente ficticia","cedula":"","nss":""}';
    cantidad int;
begin
    perform set_config('request.headers','{}',true);
    begin
        perform public.recibir_derivaciones();
        raise exception 'Lectura sin credencial';
    exception when insufficient_privilege then null;
    end;
    begin
        perform public.enviar_derivacion(clave,1,localtimestamp,'',datos);
        raise exception 'Escritura sin credencial';
    exception when insufficient_privilege then null;
    end;
    perform set_config('request.headers','{"x-gineco-estacion":"prueba-emisor"}',true);
    perform public.enviar_derivacion(clave,1,localtimestamp,'',datos);
    perform public.enviar_derivacion(clave,1,localtimestamp,'',datos);
    begin
        perform public.enviar_derivacion(gen_random_uuid(),0,localtimestamp,'',datos);
        raise exception 'Visita inválida aceptada';
    exception when check_violation then null;
    end;
    begin
        perform public.enviar_derivacion(gen_random_uuid(),1,localtimestamp,'','{}');
        raise exception 'Falta nombre';
    exception when check_violation then null;
    end;
    begin
        perform public.recibir_derivaciones();
        raise exception 'Emisor con acceso de lectura';
    exception when insufficient_privilege then null;
    end;
    perform public.confirmar_recepcion(array[clave]);
    perform set_config('request.headers','{"x-gineco-estacion":"prueba-consulta"}',true);
    select count(*) into cantidad from public.recibir_derivaciones() where id=clave;
    if cantidad <> 1 then raise exception 'Entrega omitida o duplicada'; end if;
    begin
        perform public.enviar_derivacion(gen_random_uuid(),2,localtimestamp,'',datos);
        raise exception 'Consulta con acceso de escritura a la admisión';
    exception when insufficient_privilege then null;
    end;
    perform public.confirmar_recepcion(array[clave]);
    perform public.confirmar_recepcion(array[clave]);
    if exists(select from public.recibir_derivaciones() where id=clave) then
        raise exception 'La admisión confirmada sigue pendiente';
    end if;
end
$$;
rollback;
