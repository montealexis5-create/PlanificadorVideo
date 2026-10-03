using System.Diagnostics;
using System.Drawing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Channels;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace Planificador;

// Tres tareas en paralelo conectadas por canales (productor-consumidor):
// cámara -> filtro de movimiento -> envío a la API de IA en la nube.
public static class Program
{
    private const string URL_API_POR_DEFECTO = "http://localhost:5080/api/v1/capturas";
    private const string CARPETA_RESPALDO = "capturas";   // Si la API no responde, la imagen se guarda acá
    private const int FPS_CAMARA = 15;
    private const long DEADLINE_MS = 40;
    private const int ESPERA_ENTRE_ENVIOS_MS = 1000;      // Como mucho una imagen por segundo a la nube

    // La API key se puede cambiar con la variable de entorno DETECTOR_API_KEY (tiene que coincidir con la de la API).
    private static readonly string ApiKey = Environment.GetEnvironmentVariable("DETECTOR_API_KEY") ?? "clave-de-desarrollo";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task Main(string[] args)
    {
        string urlApi = args.Length > 0 ? args[0] : URL_API_POR_DEFECTO;
        Console.WriteLine("=== Planificador de imágenes con filtro de movimiento ===");
        Console.WriteLine($"API de detección: {urlApi}");
        Directory.CreateDirectory(CARPETA_RESPALDO);
        Http.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        // Si un canal se llena se descarta lo más viejo: en tiempo real importa el frame actual, no el atrasado.
        var frames = Channel.CreateBounded<Mat>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.DropOldest });
        var envios = Channel.CreateBounded<CapturaPendiente>(new BoundedChannelOptions(20) { FullMode = BoundedChannelFullMode.DropOldest });

        using var cts = new CancellationTokenSource();
        var tareas = new[]
        {
            Task.Run(() => CapturarFrames(frames.Writer, cts.Token)),
            Task.Run(() => FiltrarFrames(frames.Reader, envios.Writer, cts.Token)),
            Task.Run(() => EnviarALaNube(envios.Reader, urlApi, cts.Token))
        };

        Console.WriteLine("Presiona ENTER para detener...");
        Console.ReadLine();
        cts.Cancel();
        await Task.WhenAll(tareas);
        CvInvoke.DestroyAllWindows();
        Console.WriteLine("Programa finalizado.");
    }

    // Productor: lee la cámara a un ritmo fijo.
    private static async Task CapturarFrames(ChannelWriter<Mat> salida, CancellationToken ct)
    {
        using var camara = new VideoCapture(0, VideoCapture.API.DShow);
        if (!camara.IsOpened)
        {
            Escribir("No se pudo abrir la cámara.", ConsoleColor.Red);
            salida.Complete();
            return;
        }

        int intervaloMs = 1000 / FPS_CAMARA;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var reloj = Stopwatch.StartNew();
                var frame = new Mat();
                camara.Read(frame);
                if (frame.IsEmpty)
                    frame.Dispose();
                else
                    await salida.WriteAsync(frame, ct);

                int restante = intervaloMs - (int)reloj.ElapsedMilliseconds;
                if (restante > 0)
                    await Task.Delay(restante, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            salida.Complete();
        }
    }

    // Consumidor: aplica el filtro, muestra el resultado y encola lo que hay que mandar a la nube.
    private static async Task FiltrarFrames(ChannelReader<Mat> entrada, ChannelWriter<CapturaPendiente> envios, CancellationToken ct)
    {
        const string ventana = "Planificador de movimiento";
        CvInvoke.NamedWindow(ventana, WindowFlags.Normal);
        using var filtro = new FiltroMovimiento();
        var relojEnvios = Stopwatch.StartNew();
        int framesDesdeUltimoEnvio = 0;
        try
        {
            await foreach (var frame in entrada.ReadAllAsync(ct))
            {
                using (frame)
                {
                    var reloj = Stopwatch.StartNew();
                    var resultado = filtro.EvaluarFrame(frame);
                    reloj.Stop();
                    framesDesdeUltimoEnvio++;

                    bool seEnvia = resultado.DebeEnviar && relojEnvios.ElapsedMilliseconds >= ESPERA_ENTRE_ENVIOS_MS;
                    if (seEnvia)
                    {
                        // Se manda la imagen limpia, antes de dibujarle los textos.
                        using var jpg = new VectorOfByte();
                        CvInvoke.Imencode(".jpg", frame, jpg);
                        envios.TryWrite(new CapturaPendiente(jpg.ToArray(), $"Movimiento_{DateTime.Now:yyyyMMdd_HHmmssfff}.jpg", framesDesdeUltimoEnvio));
                        framesDesdeUltimoEnvio = 0;
                        relojEnvios.Restart();
                    }

                    MostrarFrame(ventana, frame, resultado, seEnvia, reloj.ElapsedMilliseconds);
                    if (reloj.ElapsedMilliseconds > DEADLINE_MS)
                        Escribir($"[FALLO DEADLINE] {reloj.ElapsedMilliseconds}ms > {DEADLINE_MS}ms", ConsoleColor.Red);
                    else if (seEnvia)
                        Escribir($"[FILTRO] Movimiento ({resultado.Ratio:P2} de cambio) - enviando a la nube", ConsoleColor.Green);
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            envios.Complete();
            CvInvoke.DestroyWindow(ventana);
        }
    }

    private static void MostrarFrame(string ventana, Mat frame, ResultadoPlanificacion resultado, bool seEnvia, long msProceso)
    {
        string estado = seEnvia ? "MOVIMIENTO: enviando a la nube"
            : resultado.DebeEnviar ? "MOVIMIENTO (ya enviado hace menos de 1 s)"
            : resultado.CambioGlobalDetectado ? "DESCARTADO (movimiento de camara)"
            : "SIN MOVIMIENTO";
        var color = resultado.DebeEnviar ? new MCvScalar(0, 255, 0)
            : resultado.CambioGlobalDetectado ? new MCvScalar(0, 165, 255)
            : new MCvScalar(150, 150, 150);
        var blanco = new MCvScalar(255, 255, 255);

        CvInvoke.PutText(frame, estado, new Point(20, 40), FontFace.HersheySimplex, 0.7, color, 2);
        CvInvoke.PutText(frame, $"Proc: {msProceso} ms | Deadline: {DEADLINE_MS} ms", new Point(20, 70), FontFace.HersheySimplex, 0.6, blanco, 2);
        CvInvoke.PutText(frame, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), new Point(20, frame.Height - 20), FontFace.HersheySimplex, 0.6, blanco, 2);
        CvInvoke.Imshow(ventana, frame);
        CvInvoke.WaitKey(1);
    }

    // Manda cada imagen a la API, que confirma con IA si hay una persona.
    private static async Task EnviarALaNube(ChannelReader<CapturaPendiente> entrada, string urlApi, CancellationToken ct)
    {
        try
        {
            await foreach (var captura in entrada.ReadAllAsync(ct))
            {
                try
                {
                    using var formulario = new MultipartFormDataContent();
                    var imagen = new ByteArrayContent(captura.ImagenJpg);
                    imagen.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    formulario.Add(imagen, "Imagen", captura.NombreArchivo);
                    formulario.Add(new StringContent(captura.FramesDesdeUltimoEnvio.ToString()), "FramesDesdeUltimoEnvio");

                    using var respuesta = await Http.PostAsync(urlApi, formulario, ct);
                    if (!respuesta.IsSuccessStatusCode)
                    {
                        Escribir($"[NUBE] La API respondió {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync(ct)}", ConsoleColor.Red);
                        GuardarRespaldo(captura);
                        continue;
                    }

                    var resultado = await respuesta.Content.ReadFromJsonAsync<RespuestaNube>(ct);
                    if (resultado is { HayPersona: true })
                        Escribir($"[NUBE] PERSONA CONFIRMADA ({resultado.CantidadPersonas}) - confianza {resultado.Confianza:P0}", ConsoleColor.Cyan);
                    else
                        Escribir("[NUBE] Sin personas: la IA descartó la imagen", ConsoleColor.DarkYellow);
                }
                catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    Escribir("[NUBE] La API no está disponible, la imagen se guarda en la carpeta de respaldo.", ConsoleColor.Red);
                    GuardarRespaldo(captura);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static void GuardarRespaldo(CapturaPendiente captura)
    {
        string ruta = Path.Combine(CARPETA_RESPALDO, captura.NombreArchivo);
        File.WriteAllBytes(ruta, captura.ImagenJpg);
        Console.WriteLine($"[IMAGEN GUARDADA] {ruta}");
    }

    private static void Escribir(string mensaje, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(mensaje);
        Console.ResetColor();
    }
}
