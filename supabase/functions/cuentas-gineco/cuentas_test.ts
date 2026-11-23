import { atender } from "./cuentas.ts";

const configuracion = {
  url: "https://gineco.test",
  publica: "publica-qa",
  servicio: "servidor-qa",
};
const alias = "ficticia@cuentas.gineco.invalid";
const medico = {
  exequatur: "Med-001",
  nombre: "Médica ficticia",
  administrador: false,
};

function igual(actual: unknown, esperado: unknown) {
  if (JSON.stringify(actual) !== JSON.stringify(esperado)) {
    throw new Error(
      `Se esperaba ${JSON.stringify(esperado)}, llegó ${
        JSON.stringify(actual)
      }`,
    );
  }
}

function llamar(datos: unknown, pedir: typeof fetch, token?: string) {
  const headers = new Headers();
  if (token) headers.set("Authorization", `Bearer ${token}`);
  return atender(
    new Request("https://gineco.test/funcion", {
      method: "POST",
      body: JSON.stringify(datos),
      headers,
    }),
    configuracion,
    pedir,
  );
}

Deno.test("El acceso compartido usa el alias central y nunca confía en user_metadata", async () => {
  let llamadas = 0;
  const pedir: typeof fetch = (ruta, opciones) => {
    llamadas++;
    if (String(ruta).endsWith("gineco_alias_cuenta")) {
      igual(JSON.parse(String(opciones?.body)), {
        identificador: "med-001",
        reservar: false,
      });
      return Promise.resolve(Response.json(alias));
    }
    igual(new Headers(opciones?.headers).get("apikey"), configuracion.publica);
    igual(JSON.parse(String(opciones?.body)), {
      email: alias,
      password: "ficticia",
    });
    return Promise.resolve(
      Response.json({
        access_token: "sesion-ficticia",
        user: {
          user_metadata: { gineco: { ...medico, administrador: true } },
          app_metadata: { gineco: medico },
        },
      }),
    );
  };
  const respuesta = await llamar({
    accion: "entrar",
    exequatur: " MED-001 ",
    clave: "ficticia",
  }, pedir);
  igual(respuesta.status, 200);
  igual(await respuesta.json(), { medico, token: "sesion-ficticia" });
  igual(llamadas, 2);
});

Deno.test("Solo el administrador validado en Auth registra una cuenta médica", async () => {
  let permitido = false;
  const pedir: typeof fetch = (ruta, opciones) => {
    if (String(ruta).endsWith("/auth/v1/user")) {
      igual(
        new Headers(opciones?.headers).get("Authorization"),
        "Bearer sesion-ficticia",
      );
      return Promise.resolve(
        Response.json({
          app_metadata: { gineco: { ...medico, administrador: permitido } },
        }),
      );
    }
    if (String(ruta).endsWith("gineco_alias_cuenta")) {
      return Promise.resolve(Response.json(alias));
    }
    const cuenta = JSON.parse(String(opciones?.body));
    igual(cuenta.app_metadata.gineco, medico);
    igual(cuenta.email_confirm, true);
    return Promise.resolve(Response.json({ id: "id-ficticio" }));
  };
  const datos = {
    accion: "registrar",
    exequatur: medico.exequatur,
    nombre: medico.nombre,
    clave: "ficticia",
    administrador: true,
  };
  igual((await llamar(datos, pedir)).status, 403);
  igual((await llamar(datos, pedir, "sesion-ficticia")).status, 403);
  permitido = true;
  igual((await llamar(datos, pedir, "sesion-ficticia")).status, 201);
});

Deno.test("El vínculo requiere código válido y una autorización de un solo uso", async () => {
  let usado = false;
  const pedir: typeof fetch = (ruta, opciones) => {
    if (String(ruta).endsWith("gineco_consumir_vinculo")) {
      const aceptado = !usado;
      usado = true;
      return Promise.resolve(Response.json(aceptado));
    }
    if (String(ruta).endsWith("gineco_alias_cuenta")) {
      return Promise.resolve(Response.json(alias));
    }
    const cuenta = JSON.parse(String(opciones?.body));
    igual(cuenta.app_metadata.gineco.administrador, true);
    return Promise.resolve(Response.json({ id: "id-ficticio" }));
  };
  const datos = {
    accion: "vincular",
    exequatur: medico.exequatur,
    nombre: medico.nombre,
    clave: "ficticia",
  };
  igual((await llamar({ ...datos, codigo: "incorrecto" }, pedir)).status, 400);
  igual(
    (await llamar({ ...datos, codigo: "a".repeat(64) }, pedir)).status,
    201,
  );
  igual(
    (await llamar({ ...datos, codigo: "a".repeat(64) }, pedir)).status,
    403,
  );
});

Deno.test("Restablecer pide administrador y consulta la cuenta central antes de actualizar", async () => {
  let tieneAlias = false;
  const pedir: typeof fetch = (ruta, opciones) => {
    if (String(ruta).endsWith("/auth/v1/user")) {
      return Promise.resolve(
        Response.json({
          app_metadata: { gineco: { ...medico, administrador: true } },
        }),
      );
    }
    if (String(ruta).endsWith("gineco_alias_cuenta")) {
      return Promise.resolve(Response.json(tieneAlias ? alias : null));
    }
    if (String(ruta).endsWith("gineco_usuario_de_alias")) {
      return Promise.resolve(Response.json("id-ficticio"));
    }
    igual(opciones?.method, "PUT");
    igual(JSON.parse(String(opciones?.body)), { password: "clave-nueva" });
    return Promise.resolve(Response.json({ id: "id-ficticio" }));
  };
  const datos = {
    accion: "restablecer",
    exequatur: medico.exequatur,
    clave: "clave-nueva",
  };
  igual((await llamar(datos, pedir, "sesion-ficticia")).status, 404);
  tieneAlias = true;
  igual((await llamar(datos, pedir, "sesion-ficticia")).status, 200);
  igual(
    (await llamar({ ...datos, clave: "corta" }, pedir, "sesion-ficticia"))
      .status,
    400,
  );
});

Deno.test("Rechaza métodos, tamaños y solicitudes inválidas antes de llamar a Auth", async () => {
  const prohibido: typeof fetch = () =>
    Promise.reject(new Error("No hubo llamada esperada"));
  igual(
    (await atender(
      new Request("https://gineco.test"),
      configuracion,
      prohibido,
    )).status,
    405,
  );
  const jsonInvalido = new Request("https://gineco.test", {
    method: "POST",
    body: "{",
  });
  igual((await atender(jsonInvalido, configuracion, prohibido)).status, 400);
  igual(
    (await llamar({ accion: "entrar", sobrante: "x".repeat(4096) }, prohibido))
      .status,
    413,
  );
  igual((await llamar(null, prohibido)).status, 400);
  igual(
    (await llamar({
      accion: "entrar",
      exequatur: "a".repeat(41),
      clave: "ficticia",
    }, prohibido)).status,
    401,
  );
  igual(
    (await llamar({ accion: "entrar", exequatur: "M", clave: 0 }, prohibido))
      .status,
    401,
  );
});

Deno.test("Credenciales desconocidas y fallos de Auth devuelven resultados genéricos", async () => {
  for (
    const [estado, esperado] of [[400, 401], [401, 401], [429, 429], [503, 503]]
  ) {
    const pedir: typeof fetch = (ruta, opciones) => {
      if (String(ruta).endsWith("gineco_alias_cuenta")) {
        return Promise.resolve(Response.json(null));
      }
      igual(
        JSON.parse(String(opciones?.body)).email.endsWith(
          "@cuentas.gineco.invalid",
        ),
        true,
      );
      return Promise.resolve(
        new Response("detalle privado", { status: estado }),
      );
    };
    const respuesta = await llamar({
      accion: "entrar",
      exequatur: "M",
      clave: "ficticia",
    }, pedir);
    igual(respuesta.status, esperado);
    igual(await respuesta.json(), {});
  }
  const sinRol: typeof fetch = (ruta) =>
    Promise.resolve(Response.json(
      String(ruta).endsWith("gineco_alias_cuenta")
        ? alias
        : { user: { user_metadata: { gineco: medico } } },
    ));
  igual(
    (await llamar(
      { accion: "entrar", exequatur: "M", clave: "ficticia" },
      sinRol,
    )).status,
    401,
  );
});

Deno.test("Una sesión vencida o un alta duplicada no da acceso nuevo", async () => {
  let autorizar = false;
  const pedir: typeof fetch = (ruta) => {
    if (String(ruta).endsWith("/auth/v1/user")) {
      return Promise.resolve(
        autorizar
          ? Response.json({
            app_metadata: { gineco: { ...medico, administrador: true } },
          })
          : Response.json({}, { status: 401 }),
      );
    }
    if (String(ruta).endsWith("gineco_alias_cuenta")) {
      return Promise.resolve(Response.json(alias));
    }
    return Promise.resolve(Response.json({}, { status: 422 }));
  };
  const datos = { accion: "registrar", ...medico, clave: "ficticia" };
  igual((await llamar(datos, pedir, "token-qa")).status, 403);
  autorizar = true;
  igual(
    (await llamar({ ...datos, nombre: "" }, pedir, "token-qa")).status,
    400,
  );
  igual((await llamar(datos, pedir, "token-qa")).status, 409);
});
