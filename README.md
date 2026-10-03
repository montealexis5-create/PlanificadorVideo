# PlanificadorVideo

Sistema de detección de personas con cámara web, dividido en dos partes:

1. **Planificador:** corre junto a la cámara (una webcam con la app de consola, o **cualquier celular** conectado escaneando un QR) y filtra en tiempo real. Descarta los frames sin movimiento o con movimiento de cámara, y solo manda a la nube los que tienen movimiento real.
2. **DetectorApi:** API en la nube que recibe esas imágenes, confirma con un modelo de IA (YOLO11) si hay una persona, muestra los resultados en vivo en un panel web y le avisa al dueño por **email** y/o **WhatsApp**.

La idea es la misma que usan las cámaras de seguridad: un filtro barato en el dispositivo evita mandar miles de imágenes inútiles, y el modelo de IA, que es más pesado, solo analiza las pocas que pasan el filtro.

```
 Webcam  ──▶ Planificador (C#, OpenCV) ─┐
 Celular ──▶ camara.html (filtro en JS) ┤
                                        ▼
                                 DetectorApi (ASP.NET Core)              Panel web
                                 YOLO11 (ONNX Runtime)  ──SignalR──▶    capturas en vivo
                                 PostgreSQL                              estadísticas
                                     │
                                     └──▶ Aviso al dueño: email (con la foto) y WhatsApp
```

## Planificador

Usa tres tareas en paralelo conectadas por canales (patrón productor-consumidor):

1. **Captura:** lee imágenes de la cámara a 15 cuadros por segundo y las pone en un canal de capacidad 10. Si el canal se llena, descarta la imagen más vieja para no atrasarse.
2. **Filtro:** evalúa cada imagen:
   - La pasa a escala de grises y la suaviza (desenfoque gaussiano) para reducir el ruido.
   - La compara con la anterior y marca los píxeles que cambiaron.
   - Divide la imagen en una grilla de 8x8 bloques. Si cambió más de la mitad, lo toma como movimiento de cámara y la descarta.
   - Si cambió entre el 1,5% y el 45% de los píxeles, lo toma como movimiento real y la manda a la cola de envío (como mucho una por segundo).
3. **Envío:** manda las imágenes a la API. Si la API no responde, las guarda en la carpeta `capturas` para no perderlas.

Cada frame tiene un tiempo máximo de procesamiento de **40 ms**. Si se pasa, la consola avisa con `[FALLO DEADLINE]`. El envío por red va en su propia tarea, así nunca frena el análisis.

## DetectorApi

- Recibe la imagen, la redimensiona a 640x640 y la pasa por **YOLO11n** con ONNX Runtime.
- Se queda con las detecciones de la clase "persona" que superan el 50% de confianza y elimina las cajas repetidas sobre la misma persona (Non-Maximum Suppression).
- Guarda la posición de cada persona para dibujar un recuadro sobre ella en el panel.
- Guarda el resultado en PostgreSQL y la imagen en disco.
- Avisa en vivo al panel web con SignalR.
- Si hay una persona, encola un aviso para el dueño. Un proceso en segundo plano lo manda por email (con la foto adjunta) y/o WhatsApp, respetando un tiempo mínimo entre avisos para no mandar un mensaje por cada foto.

### Endpoints

| Método | Ruta | Qué hace |
|---|---|---|
| POST | `/api/v1/capturas` | Recibe una imagen (multipart) y devuelve si hay personas |
| GET | `/api/v1/capturas?hayPersona=true&limite=50` | Lista las capturas, de la más nueva a la más vieja (`hayPersona` es opcional) |
| GET | `/api/v1/capturas/{id}/imagen` | Devuelve la imagen de una captura |
| GET | `/api/v1/estadisticas` | Porcentaje filtrado por el planificador y por la IA |
| GET | `/api/v1/camaras/enlace` | Link y QR para conectar un celular como cámara (pide la clave de administrador) |
| GET / PUT | `/api/v1/configuracion/avisos` | Ver o cambiar a quién se avisa (pide la clave de administrador) |
| POST | `/api/v1/configuracion/avisos/prueba` | Manda un aviso de prueba (pide la clave de administrador) |

### Panel web

En `http://localhost:5080` muestra:

- El recorrido de las imágenes: frames analizados por la cámara → enviados por el filtro local → personas confirmadas por la IA, con el porcentaje que descarta cada etapa.
- Las capturas en vivo con un recuadro sobre cada persona, filtrables entre todas, personas y descartadas por la IA.
- Un aviso cada vez que la IA confirma una persona.
- El botón **Avisos**, donde el dueño carga su email, su WhatsApp y cada cuántos minutos quiere que le avisen, y puede mandarse un aviso de prueba.

Si no puede cargar SignalR, el panel sigue funcionando consultando la API cada 5 segundos.

## Usar un celular como cámara

1. En el panel, tocá **Conectar cámara**, poné la clave de administrador y generá el QR.
2. Escaneá el QR con el celular, que tiene que estar en la misma red WiFi que la compu.
3. El celular abre `camara.html`, usa su cámara trasera y aplica el mismo filtro de movimiento que el planificador. Solo manda a la IA las imágenes con movimiento real, como mucho una por segundo.

Detalles:

- Los navegadores solo dejan usar la cámara por **HTTPS**, por eso la API también escucha en `https://<IP de la compu>:5443` con el certificado de desarrollo de .NET. La primera vez el celular muestra una advertencia: se elige "Avanzado" y se continúa. Si el certificado no existe, se crea con `dotnet dev-certs https`.
- El QR lleva la API key después del `#` del link, una parte que el navegador nunca manda al servidor.
- Windows puede pedir permiso en el firewall la primera vez que la API escucha en la red.
- La IP se detecta sola. Si detecta mal (por ejemplo, la de una placa virtual), se fija en `CamaraRemota:Host`.

## Avisos por email y WhatsApp

Los dos son gratuitos:

- **Email:** se envía por SMTP. Con Gmail hay que crear una [contraseña de aplicación](https://myaccount.google.com/apppasswords) y cargar la cuenta en la sección `Smtp` (mejor con variables de entorno o *user-secrets* que en el archivo):

  ```bash
  cd DetectorApi
  dotnet user-secrets init
  dotnet user-secrets set "Smtp:Usuario" "micuenta@gmail.com"
  dotnet user-secrets set "Smtp:Clave" "la-contraseña-de-aplicación"
  ```

- **WhatsApp:** se usa [CallMeBot](https://www.callmebot.com/blog/free-api-whatsapp-messages/), un servicio gratuito para mandarse mensajes a uno mismo. El dueño le escribe una vez al bot, recibe su API key y la carga en el panel junto con su número.

## Seguridad

- **API key del planificador:** la API solo acepta imágenes que traigan el header `X-Api-Key`. El planificador la toma de la variable de entorno `DETECTOR_API_KEY`.
- **Clave de administrador:** la configuración de avisos pide el header `X-Clave-Admin`. La API key de WhatsApp nunca se devuelve, solo se informa si está cargada.
- Las claves se comparan en tiempo constante, para que no se puedan adivinar midiendo cuánto tarda la respuesta.
- **Límite de pedidos:** como mucho 5 imágenes por segundo (responde 429 si se pasa).
- **Validación de imágenes:** máximo 5 MB y 4096 px de lado. Se lee el encabezado antes de descomprimir, para evitar imágenes "bomba" que ocupen gigas de memoria.
- Los nombres de archivo los genera la API, así que no se puede escribir fuera de la carpeta de imágenes.
- **Encabezados de seguridad:** Content-Security-Policy (solo scripts propios y el cliente de SignalR), `X-Frame-Options`, `nosniff` y `Permissions-Policy`.
- **Errores:** un manejador centralizado devuelve 400 con un mensaje claro para los errores de validación y 500 sin detalles internos para el resto.

**Limitación del prototipo:** ver el panel y las capturas no pide clave. Por eso la API escucha solo en la red local. Antes de publicarla en internet habría que agregar un inicio de sesión.

Las claves que vienen en `appsettings.json` son **solo para desarrollo**. En producción se reemplazan con variables de entorno, por ejemplo `Seguridad__ApiKey` y `Seguridad__ClaveAdministrador`.

## Estructura

Todo está en una sola solución de Visual Studio (`PlanificadorVideo.sln`):

| Proyecto | Qué es |
|---|---|
| `Planificador` | Aplicación de consola que corre junto a la cámara |
| `DetectorApi` | API con el modelo de IA, el panel web y la página de cámara para celulares |
| `PlanificadorVideo.Tests` | Tests unitarios (xUnit) del filtro de movimiento, la supresión de cajas, las claves y los avisos |

## Tecnologías

- C# / .NET 10
- Planificador: Emgu CV (OpenCV), `System.Threading.Channels`, `HttpClient`
- API: ASP.NET Core Web API, Entity Framework Core, PostgreSQL, SignalR, Swagger
- IA: YOLO11n en formato ONNX, ejecutado con ONNX Runtime; ImageSharp para preparar las imágenes
- Frontend: HTML, CSS y JavaScript, sin frameworks (`getUserMedia` y `canvas` para la cámara del celular)
- QRCoder para generar el QR
- Avisos: SMTP (email) y CallMeBot (WhatsApp), en un `BackgroundService`
- Docker para la base de datos
- xUnit para los tests

## Cómo ejecutarlo

Requisitos: Windows con cámara web, [.NET 10 SDK](https://dotnet.microsoft.com/download) y Docker Desktop.

**1. Levantar la base de datos**

```bash
docker compose up -d --wait
```

**2. Levantar la API** (en una terminal)

```bash
cd DetectorApi
dotnet run
```

El panel queda en http://localhost:5080 y Swagger en http://localhost:5080/swagger (con el botón **Authorize** se cargan las claves para probar los endpoints protegidos).

**3. Levantar el planificador** (en otra terminal)

```bash
cd Planificador
dotnet run
```

Si la API está en otra dirección, pasala como argumento:

```bash
dotnet run -- "http://mi-servidor:5080/api/v1/capturas"
```

Para detener el planificador, presioná **ENTER** en su consola.

Desde Visual Studio también se puede: abrí `PlanificadorVideo.sln`, hacé clic derecho en la solución → **Configurar proyectos de inicio** → **Varios proyectos de inicio**, y marcá `DetectorApi` y `Planificador` en **Iniciar**.

**Tests**

```bash
dotnet test
```

## Modelo de IA

`DetectorApi/Modelos/yolo11n.onnx` es el modelo YOLO11n de [Ultralytics](https://github.com/ultralytics/ultralytics), entrenado con el dataset COCO y distribuido bajo licencia **AGPL-3.0**.

## Autor

Alexis Lionel Monte — [montealexis5-create](https://github.com/montealexis5-create)
