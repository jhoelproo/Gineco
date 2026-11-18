document.querySelectorAll("form[data-cambios]").forEach((formulario) => {
    const advertir = (evento) => {
        evento.preventDefault();
        evento.returnValue = true;
    };
    formulario.addEventListener("input", () => window.addEventListener("beforeunload", advertir));
    formulario.addEventListener("submit", () => window.removeEventListener("beforeunload", advertir));
});

document.querySelectorAll(".secciones a").forEach((enlace) => {
    enlace.addEventListener("click", () => {
        const seccion = document.getElementById(enlace.hash.slice(1));
        seccion.open = true;
    });
});
