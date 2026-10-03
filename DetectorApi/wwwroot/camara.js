// Usa la cámara del celular como el "planificador": compara cada frame con el anterior y solo manda
// a la API los que tienen movimiento real. Es el mismo filtro que Planificador/FiltroMovimiento.cs.
const FILTRO = {
    ancho: 160,                     // Se analiza una versión chica de la imagen: es más rápido y reduce el ruido
    alto: 120,
    sensibilidadPixel: 25,          // Diferencia de brillo (0-255) para considerar que un píxel cambió
    ratioMinimoPersona: 0.015,      // Entre 1,5% ...
    ratioMaximoMovimiento: 0.45,    // ... y 45% de píxeles cambiados = movimiento real
    bloques: 8,                     // Grilla de 8x8
    umbralCambioBloque: 0.01,
    ratioBloquesMovimientoCamara: 0.5
};
const FPS = 8;
const ESPERA_ENTRE_ENVIOS_MS = 1000;
const ANCHO_ENVIO = 640;

const $ = id => document.getElementById(id);
const video = $("video");
const apiKey = new URLSearchParams(location.hash.slice(1)).get("clave");   // Viene en el link del QR

const lienzoAnalisis = Object.assign(document.createElement("canvas"), { width: FILTRO.ancho, height: FILTRO.alto });
const contextoAnalisis = lienzoAnalisis.getContext("2d", { willReadFrequently: true });
let grisAnterior = null;
let frames = 0;
let enviadas = 0;
let framesDesdeUltimoEnvio = 0;
let ultimoEnvio = 0;
let enviando = false;

// ===== Filtro de movimiento (funciones puras) =====

function aGrises(pixeles) {
    const gris = new Uint8Array(pixeles.length / 4);
    for (let i = 0; i < gris.length; i++) {
        gris[i] = 0.299 * pixeles[i * 4] + 0.587 * pixeles[i * 4 + 1] + 0.114 * pixeles[i * 4 + 2];
    }
    return gris;
}

function clasificar(anterior, actual) {
    const { ancho, alto, bloques } = FILTRO;
    const anchoBloque = Math.floor(ancho / bloques);
    const altoBloque = Math.floor(alto / bloques);
    const cambiosPorBloque = new Array(bloques * bloques).fill(0);
    let cambiados = 0;

    for (let y = 0; y < alto; y++) {
        for (let x = 0; x < ancho; x++) {
            const i = y * ancho + x;
            if (Math.abs(actual[i] - anterior[i]) > FILTRO.sensibilidadPixel) {
                cambiados++;
                const bloque = Math.min(Math.floor(y / altoBloque), bloques - 1) * bloques + Math.min(Math.floor(x / anchoBloque), bloques - 1);
                cambiosPorBloque[bloque]++;
            }
        }
    }

    const ratio = cambiados / (ancho * alto);
    const bloquesConCambio = cambiosPorBloque.filter(c => c / (anchoBloque * altoBloque) > FILTRO.umbralCambioBloque).length;
    if (bloquesConCambio / (bloques * bloques) > FILTRO.ratioBloquesMovimientoCamara) {
        return { debeEnviar: false, movimientoCamara: true, ratio };
    }
    return { debeEnviar: ratio >= FILTRO.ratioMinimoPersona && ratio <= FILTRO.ratioMaximoMovimiento, movimientoCamara: false, ratio };
}

// ===== Cámara y envío =====

function mostrarEstado(texto, clase) {
    $("estadoCamara").className = `estado ${clase}`;
    $("estadoCamara").querySelector(".estado-texto").textContent = texto;
}

function mostrarResultado(texto, esPersona) {
    $("ultimoResultado").textContent = texto;
    $("ultimoResultado").className = `camara-resultado${esPersona ? " persona" : ""}`;
    $("ultimoResultado").hidden = false;
}

async function enviar() {
    enviando = true;
    const lienzo = document.createElement("canvas");
    lienzo.width = ANCHO_ENVIO;
    lienzo.height = Math.round(ANCHO_ENVIO * video.videoHeight / video.videoWidth);
    lienzo.getContext("2d").drawImage(video, 0, 0, lienzo.width, lienzo.height);
    const imagen = await new Promise(resolver => lienzo.toBlob(resolver, "image/jpeg", 0.85));

    const formulario = new FormData();
    formulario.append("Imagen", imagen, "camara.jpg");
    formulario.append("FramesDesdeUltimoEnvio", framesDesdeUltimoEnvio);
    framesDesdeUltimoEnvio = 0;
    try {
        const respuesta = await fetch("/api/v1/capturas", { method: "POST", headers: { "X-Api-Key": apiKey }, body: formulario });
        if (!respuesta.ok) throw new Error(await respuesta.text());
        const captura = await respuesta.json();
        enviadas++;
        mostrarResultado(captura.hayPersona
            ? `Persona confirmada por la IA (${captura.cantidadPersonas})`
            : "La IA no encontró personas: falsa alarma", captura.hayPersona);
    } catch (error) {
        mostrarResultado(`No se pudo enviar: ${error.message}`, false);
    } finally {
        enviando = false;
    }
}

function analizarFrame() {
    if (video.readyState < 2) return;
    contextoAnalisis.drawImage(video, 0, 0, FILTRO.ancho, FILTRO.alto);
    const gris = aGrises(contextoAnalisis.getImageData(0, 0, FILTRO.ancho, FILTRO.alto).data);
    frames++;
    framesDesdeUltimoEnvio++;

    if (grisAnterior) {
        const resultado = clasificar(grisAnterior, gris);
        if (resultado.movimientoCamara) {
            mostrarEstado("Movimiento de cámara: descartado", "sin-conexion");
        } else if (resultado.debeEnviar) {
            mostrarEstado("Movimiento: enviando a la IA", "en-vivo");
            if (!enviando && Date.now() - ultimoEnvio >= ESPERA_ENTRE_ENVIOS_MS) {
                ultimoEnvio = Date.now();
                enviar();
            }
        } else {
            mostrarEstado("Vigilando", "en-vivo");
        }
    }
    grisAnterior = gris;
    $("contador").textContent = `${frames} frames · ${enviadas} enviadas`;
}

async function iniciar() {
    if (!apiKey) {
        $("errorCamara").textContent = "Falta la clave. Abrí esta página escaneando el QR del panel.";
        return;
    }
    if (!window.isSecureContext || !navigator.mediaDevices) {
        $("errorCamara").textContent = "El navegador solo deja usar la cámara por HTTPS. Abrí el link del QR (empieza con https).";
        return;
    }
    try {
        video.srcObject = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" }, audio: false });
        $("inicio").hidden = true;
        setInterval(analizarFrame, 1000 / FPS);
    } catch (error) {
        $("errorCamara").textContent = `No se pudo abrir la cámara: ${error.message}`;
    }
}

$("botonIniciar").addEventListener("click", iniciar);
