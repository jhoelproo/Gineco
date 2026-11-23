export type Configuracion = { url: string; publica: string; servicio: string };
type Datos = Record<string, unknown>;
type Medico = { exequatur: string; nombre: string; administrador: boolean };
type Registro = { medico: Medico; clave: string };

class ErrorServicio extends Error {}

const responder = (estado: number, datos: Datos = {}) =>
  Response.json(datos, {
    status: estado,
    headers: { "Cache-Control": "no-store" },
  });

function texto(valor: unknown, maximo: number): string {
  return typeof valor === "string" && valor.length <= maximo ? valor : "";
}

function normalizar(exequatur: unknown): string {
  return texto(exequatur, 40).trim().replace(
    /[A-Z]/g,
    (letra) => letra.toLowerCase(),
  );
}

function registro(datos: Datos, administrador: boolean): Registro | null {
  const exequatur = texto(datos.exequatur, 40).trim();
  const nombre = texto(datos.nombre, 300).trim();
  const clave = texto(datos.clave, 256);
  if (!exequatur || !nombre || clave.length < 6) return null;
  return { medico: { exequatur, nombre, administrador }, clave };
}

function datosMedico(usuario: Datos | undefined): Medico | null {
  const app = usuario?.app_metadata as Datos | undefined;
  const medico = app?.gineco as Datos | undefined;
  if (
    !medico || !normalizar(medico.exequatur) || !texto(medico.nombre, 300) ||
    typeof medico.administrador !== "boolean"
  ) return null;
  return medico as Medico;
}

class Supabase {
  constructor(private config: Configuracion, private pedir: typeof fetch) {}

  private solicitar(
    ruta: string,
    clave: string,
    opciones: RequestInit = {},
    token?: string,
  ) {
    const headers = new Headers(opciones.headers);
    headers.set("apikey", clave);
    headers.set("Content-Type", "application/json");
    if (token) headers.set("Authorization", `Bearer ${token}`);
    return this.pedir(new URL(ruta, this.config.url), {
      ...opciones,
      headers,
      redirect: "error",
    });
  }

  private async rpc<T>(nombre: string, datos: Datos): Promise<T> {
    const respuesta = await this.solicitar(
      `/rest/v1/rpc/${nombre}`,
      this.config.servicio,
      { method: "POST", body: JSON.stringify(datos) },
      this.config.servicio,
    );
    if (!respuesta.ok) throw new ErrorServicio();
    return await respuesta.json() as T;
  }

  alias(identificador: string, reservar = false) {
    return this.rpc<string | null>("gineco_alias_cuenta", {
      identificador,
      reservar,
    });
  }

  autorizarVinculo(identificador: string, codigo: string) {
    return this.rpc<boolean>("gineco_consumir_vinculo", {
      identificador,
      codigo,
    });
  }

  async iniciar(correo: string, clave: string) {
    const respuesta = await this.solicitar(
      "/auth/v1/token?grant_type=password",
      this.config.publica,
      {
        method: "POST",
        body: JSON.stringify({ email: correo, password: clave }),
      },
    );
    if (respuesta.status === 429) return responder(429);
    if (respuesta.status === 400 || respuesta.status === 401) {
      return responder(401);
    }
    if (!respuesta.ok) throw new ErrorServicio();
    const sesion = await respuesta.json();
    const medico = datosMedico(sesion.user);
    return medico && typeof sesion.access_token === "string"
      ? responder(200, { medico, token: sesion.access_token })
      : responder(401);
  }

  async esAdministrador(autorizacion: string | null) {
    if (!autorizacion?.startsWith("Bearer ")) return false;
    const respuesta = await this.solicitar(
      "/auth/v1/user",
      this.config.publica,
      {},
      autorizacion.slice(7),
    );
    if (respuesta.status === 401 || respuesta.status === 403) return false;
    if (!respuesta.ok) throw new ErrorServicio();
    return datosMedico(await respuesta.json())?.administrador === true;
  }

  async crear(cuenta: Registro) {
    const email = await this.alias(normalizar(cuenta.medico.exequatur), true);
    const respuesta = await this.solicitar(
      "/auth/v1/admin/users",
      this.config.servicio,
      {
        method: "POST",
        body: JSON.stringify({
          email,
          password: cuenta.clave,
          email_confirm: true,
          app_metadata: { gineco: cuenta.medico },
        }),
      },
      this.config.servicio,
    );
    if (!respuesta.ok && respuesta.status !== 422) throw new ErrorServicio();
    return responder(respuesta.ok ? 201 : 409);
  }

  async restablecer(exequatur: string, clave: string) {
    const alias = await this.alias(exequatur);
    const usuario = alias &&
      await this.rpc<string | null>("gineco_usuario_de_alias", {
        alias_correo: alias,
      });
    if (!usuario) return responder(404);
    const respuesta = await this.solicitar(
      `/auth/v1/admin/users/${encodeURIComponent(usuario)}`,
      this.config.servicio,
      { method: "PUT", body: JSON.stringify({ password: clave }) },
      this.config.servicio,
    );
    if (!respuesta.ok) throw new ErrorServicio();
    return responder(200);
  }
}

async function autenticar(datos: Datos, supabase: Supabase) {
  const id = normalizar(datos.exequatur);
  const clave = texto(datos.clave, 256);
  if (!id || !clave) return responder(401);
  const alias = await supabase.alias(id);
  return supabase.iniciar(
    alias ?? `${crypto.randomUUID()}@cuentas.gineco.invalid`,
    clave,
  );
}

async function vincular(datos: Datos, supabase: Supabase) {
  const cuenta = registro(datos, true);
  const codigo = texto(datos.codigo, 64);
  if (!cuenta || !/^[a-f0-9]{64}$/.test(codigo)) return responder(400);
  if (
    !await supabase.autorizarVinculo(
      normalizar(cuenta.medico.exequatur),
      codigo,
    )
  ) return responder(403);
  return supabase.crear(cuenta);
}

async function gestionar(
  datos: Datos,
  supabase: Supabase,
  autorizacion: string | null,
) {
  if (!await supabase.esAdministrador(autorizacion)) return responder(403);
  if (datos.accion === "registrar") {
    const cuenta = registro(datos, false);
    return cuenta ? supabase.crear(cuenta) : responder(400);
  }
  const id = normalizar(datos.exequatur);
  const clave = texto(datos.clave, 256);
  return id && clave.length >= 6
    ? supabase.restablecer(id, clave)
    : responder(400);
}

export async function atender(
  solicitud: Request,
  config: Configuracion,
  pedir: typeof fetch,
) {
  if (solicitud.method !== "POST") return responder(405);
  const supabase = new Supabase(config, pedir);
  try {
    const cuerpo = await solicitud.text();
    if (cuerpo.length > 4096) return responder(413);
    const datos = JSON.parse(cuerpo) as Datos | null;
    switch (datos?.accion) {
      case "entrar":
        return await autenticar(datos, supabase);
      case "vincular":
        return await vincular(datos, supabase);
      case "registrar":
      case "restablecer":
        return await gestionar(
          datos,
          supabase,
          solicitud.headers.get("Authorization"),
        );
      default:
        return responder(400);
    }
  } catch (error) {
    return responder(error instanceof SyntaxError ? 400 : 503);
  }
}
