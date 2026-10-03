// Panel web: muestra las capturas con las personas marcadas y se actualiza en vivo con SignalR.
// Si SignalR no está disponible (por ejemplo, sin acceso al CDN), consulta la API cada pocos segundos.
const API = "/api/v1";
const LIMITE = 48;
const SEGUNDOS_CONSULTA = 5;

const $ = id => document.getElementById(id);
const lista = $("listaCapturas");
const mensaje = $("mensaje");

const porcentaje = (valor, decimales = 0) =>
    `${(valor * 100).toLocaleString("es-AR", { maximumFractionDigits: decimales })}%`;
const numero = valor => valor.toLocaleString("es-AR");
const fechaHora = iso => new Date(iso).toLocaleString("es-AR");
const filtroActual = () => document.querySelector("input[name=filtro]:checked").value;   // "", "true" o "false"

// fetch que lanza un error con el mensaje de la API si la respuesta no es exitosa.
async function pedir(url, opciones = {}) {
    const respuesta = await fetch(url, opciones);
    const texto = await respuesta.text();
    if (!respuesta.ok) {
        throw new Error(texto || `Error ${respuesta.status}`);
    }
    return texto ? JSON.parse(texto) : null;
}

// ===== Capturas y estadísticas =====

async function cargarCapturas() {
    try {
        const filtro = filtroActual() === "" ? "" : `&hayPersona=${filtroActual()}`;
        const capturas = await pedir(`${API}/capturas?limite=${LIMITE}${filtro}`);
        lista.replaceChildren(...capturas.map(c => crearTarjeta(c, false)));
        mostrarMensaje(capturas.length === 0
            ? "Todavía no hay capturas. Levantá el planificador y movete frente a la cámara."
            : "");
    } catch {
        mostrarMensaje("No se pudo conectar con la API. Revisá que esté levantada.");
    }
}

async function cargarEstadisticas() {
    try {
        const e = await pedir(`${API}/estadisticas`);
        $("statFrames").textContent = numero(e.framesAnalizados);
        $("statRecibidas").textContent = numero(e.imagenesRecibidas);
        $("statFiltrado").textContent = porcentaje(e.porcentajeFiltradoLocal, 1);
        $("barraFiltrado").style.width = porcentaje(e.porcentajeFiltradoLocal);
        $("statPersonas").textContent = numero(e.personasConfirmadas);
        $("statInferencia").textContent = `${Math.round(e.promedioMsInferencia)} ms`;
        $("statDescartadoIa").textContent = porcentaje(e.porcentajeDescartadoIa);
        $("barraIa").style.width = porcentaje(e.porcentajeDescartadoIa);
    } catch {
        // El mensaje de error ya lo muestra cargarCapturas.
    }
}

function mostrarMensaje(texto) {
    mensaje.textContent = texto;
    mensaje.hidden = texto === "";
}

// Imagen con un recuadro sobre cada persona. Las cajas vienen de 0 a 1, así que se ubican en porcentaje.
function crearImagenConCajas(captura) {
    const contenedor = document.createElement("div");
    contenedor.className = "imagen-con-cajas";
    const imagen = document.createElement("img");
    imagen.src = captura.urlImagen;
    imagen.alt = captura.hayPersona ? `Captura con ${captura.cantidadPersonas} persona(s)` : "Captura sin personas";
    imagen.loading = "lazy";
    contenedor.appendChild(imagen);

    for (const caja of captura.cajas) {
        const recuadro = document.createElement("div");
        recuadro.className = caja.y < 0.06 ? "caja arriba" : "caja";
        recuadro.style.left = `${caja.x * 100}%`;
        recuadro.style.top = `${caja.y * 100}%`;
        recuadro.style.width = `${caja.ancho * 100}%`;
        recuadro.style.height = `${caja.alto * 100}%`;
        const texto = document.createElement("span");
        texto.textContent = `persona ${porcentaje(caja.confianza)}`;
        recuadro.appendChild(texto);
        contenedor.appendChild(recuadro);
    }
    return contenedor;
}

function crearTarjeta(captura, esNueva) {
    // Es un <button> para que se pueda abrir también con el teclado.
    const tarjeta = document.createElement("button");
    tarjeta.type = "button";
    tarjeta.className = `captura${captura.hayPersona ? " con-persona" : ""}${esNueva ? " nueva" : ""}`;
    tarjeta.addEventListener("click", () => ampliar(captura));

    const etiqueta = document.createElement("span");
    etiqueta.className = `etiqueta${captura.hayPersona ? " persona" : ""}`;
    etiqueta.textContent = captura.hayPersona ? `${captura.cantidadPersonas} persona(s)` : "Falsa alarma";

    const hora = document.createElement("span");
    hora.textContent = fechaHora(captura.fechaHora);

    const pie = document.createElement("div");
    pie.className = "captura-pie";
    pie.append(etiqueta, hora);

    tarjeta.append(crearImagenConCajas(captura), pie);
    return tarjeta;
}

function ampliar(captura) {
    $("contenidoDialogo").replaceChildren(crearImagenConCajas(captura));
    $("detalleDialogo").textContent =
        `${fechaHora(captura.fechaHora)} · ${captura.hayPersona ? `${captura.cantidadPersonas} persona(s)` : "sin personas"}` +
        ` · confianza ${porcentaje(captura.confianza)} · IA ${captura.msInferencia} ms`;
    $("dialogo").showModal();
}

// Cierra un diálogo al hacer clic en el fondo oscuro.
for (const dialogo of document.querySelectorAll("dialog")) {
    dialogo.addEventListener("click", evento => {
        if (evento.target === dialogo) dialogo.close();
    });
}

let temporizadorAviso;
function mostrarAviso(texto) {
    const aviso = $("aviso");
    aviso.textContent = texto;
    aviso.hidden = false;
    clearTimeout(temporizadorAviso);
    temporizadorAviso = setTimeout(() => (aviso.hidden = true), 5000);
}

function recibirCaptura(captura) {
    const coincide = filtroActual() === "" || String(captura.hayPersona) === filtroActual();
    if (coincide) {
        lista.prepend(crearTarjeta(captura, true));
        lista.children[LIMITE]?.remove();
        mostrarMensaje("");
    }
    if (captura.hayPersona) {
        mostrarAviso(`Persona detectada a las ${new Date(captura.fechaHora).toLocaleTimeString("es-AR")}`);
    }
    cargarEstadisticas();
}

// ===== Conexión en vivo =====

function mostrarEstado(texto, clase) {
    $("estadoConexion").className = `estado ${clase}`;
    $("estadoConexion").querySelector(".estado-texto").textContent = texto;
}

function consultarPeriodicamente() {
    mostrarEstado(`Actualiza cada ${SEGUNDOS_CONSULTA} s`, "sin-conexion");
    setInterval(() => {
        cargarCapturas();
        cargarEstadisticas();
    }, SEGUNDOS_CONSULTA * 1000);
}

async function conectarEnVivo() {
    if (typeof signalR === "undefined") {
        consultarPeriodicamente();
        return;
    }
    const conexion = new signalR.HubConnectionBuilder().withUrl("/hubs/capturas").withAutomaticReconnect().build();
    conexion.on("NuevaCaptura", recibirCaptura);
    conexion.onreconnecting(() => mostrarEstado("Reconectando...", "sin-conexion"));
    conexion.onreconnected(() => {
        mostrarEstado("En vivo", "en-vivo");
        cargarCapturas();
        cargarEstadisticas();
    });
    conexion.onclose(() => mostrarEstado("Desconectado", "sin-conexion"));
    try {
        await conexion.start();
        mostrarEstado("En vivo", "en-vivo");
    } catch {
        consultarPeriodicamente();
    }
}

// ===== Configuración de avisos =====

const cabecerasAdmin = () => ({ "Content-Type": "application/json", "X-Clave-Admin": $("claveAdmin").value });

function mostrarResultado(texto, esError) {
    $("resultadoAvisos").textContent = texto;
    $("resultadoAvisos").className = `resultado ${esError ? "error" : "ok"}`;
}

function habilitarFormulario(habilitado) {
    $("camposAvisos").disabled = !habilitado;
    $("botonGuardarAvisos").disabled = !habilitado;
    $("botonProbarAvisos").disabled = !habilitado;
}

async function desbloquearAvisos() {
    try {
        const c = await pedir(`${API}/configuracion/avisos`, { headers: cabecerasAdmin() });
        $("avisosActivos").checked = c.avisosActivos;
        $("avisoEmail").value = c.email ?? "";
        $("avisoTelefono").value = c.whatsappTelefono ?? "";
        $("avisoApiKey").value = "";
        $("avisoApiKey").placeholder = c.whatsappApiKeyCargada ? "Ya está cargada (dejala vacía para no cambiarla)" : "";
        $("avisoMinutos").value = c.minutosEntreAvisos;
        habilitarFormulario(true);
        mostrarResultado("", false);
    } catch (error) {
        habilitarFormulario(false);
        mostrarResultado(error.message, true);
    }
}

async function guardarAvisos() {
    const datos = {
        avisosActivos: $("avisosActivos").checked,
        email: $("avisoEmail").value,
        whatsappTelefono: $("avisoTelefono").value,
        whatsappApiKey: $("avisoApiKey").value,
        minutosEntreAvisos: Number($("avisoMinutos").value)
    };
    try {
        await pedir(`${API}/configuracion/avisos`, { method: "PUT", headers: cabecerasAdmin(), body: JSON.stringify(datos) });
        mostrarResultado("Configuración guardada.", false);
    } catch (error) {
        mostrarResultado(error.message, true);
    }
}

async function probarAvisos() {
    mostrarResultado("Enviando...", false);
    try {
        const resultados = await pedir(`${API}/configuracion/avisos/prueba`, { method: "POST", headers: cabecerasAdmin() });
        mostrarResultado(resultados.join("\n"), resultados.some(r => r.includes("error")));
    } catch (error) {
        mostrarResultado(error.message, true);
    }
}

$("botonAvisos").addEventListener("click", () => $("dialogoAvisos").showModal());
$("botonCargarAvisos").addEventListener("click", desbloquearAvisos);
$("claveAdmin").addEventListener("keydown", evento => {
    if (evento.key === "Enter") {
        evento.preventDefault();
        desbloquearAvisos();
    }
});
$("botonGuardarAvisos").addEventListener("click", guardarAvisos);
$("botonProbarAvisos").addEventListener("click", probarAvisos);

// ===== Conectar cámara (QR) =====

async function generarQr() {
    $("resultadoCamara").textContent = "";
    try {
        const enlace = await pedir(`${API}/camaras/enlace`, { headers: { "X-Clave-Admin": $("claveCamara").value } });
        $("imagenQr").src = `data:image/png;base64,${enlace.qrPngBase64}`;
        $("urlQr").textContent = enlace.url.split("#")[0];   // Se muestra sin la clave
        $("qr").hidden = false;
    } catch (error) {
        $("qr").hidden = true;
        $("resultadoCamara").textContent = error.message;
    }
}

$("botonCamara").addEventListener("click", () => $("dialogoCamara").showModal());
$("botonGenerarQr").addEventListener("click", generarQr);
$("claveCamara").addEventListener("keydown", evento => {
    if (evento.key === "Enter") {
        evento.preventDefault();
        generarQr();
    }
});

// ===== Inicio =====

document.querySelectorAll("input[name=filtro]").forEach(opcion => opcion.addEventListener("change", cargarCapturas));
Promise.all([cargarCapturas(), cargarEstadisticas()]).then(conectarEnVivo);
