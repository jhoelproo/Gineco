import { atender, type Configuracion } from "./cuentas.ts";

function variable(nombre: string): string {
  const valor = Deno.env.get(nombre);
  if (!valor) throw new Error(`Falta ${nombre}`);
  return valor;
}

const configuracion: Configuracion = {
  url: variable("SUPABASE_URL"),
  publica: variable("SUPABASE_ANON_KEY"),
  servicio: variable("SUPABASE_SERVICE_ROLE_KEY"),
};
Deno.serve((solicitud) => atender(solicitud, configuracion, fetch));
