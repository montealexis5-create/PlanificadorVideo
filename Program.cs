using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
class Program
{
    private const string CARPETA_SALIDA = @"C:\Users\compu\OneDrive\Desktop\alexis monte\camara de seguridad";
    private const int FPS_CAMARA = 15;
    private const long DEADLINE_LOCAL_MS = 40;
    static async Task Main()
    {
        Console.WriteLine("=== Planificador Imágenes Movimiento Mejorado con Timestamp ===");
        if (!Directory.Exists(CARPETA_SALIDA))
            Directory.CreateDirectory(CARPETA_SALIDA);
        var cts = new CancellationTokenSource();
        var canalFrames = Channel.CreateBounded<Mat>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        var planificador = new PlanificadorFiltroDinamico();
        var hiloCaptura = Task.Run(() => HiloProductorCaptura(canalFrames.Writer, cts.Token));
        var hiloPlanificador = Task.Run(() => HiloConsumidorPlanificador(canalFrames.Reader, planificador, cts.Token));
        Console.WriteLine("Presiona ENTER para detener...");
        Console.ReadLine();
        cts.Cancel();
        await Task.WhenAll(hiloCaptura, hiloPlanificador);
        CvInvoke.DestroyAllWindows();
        Console.WriteLine("Programa finalizado.");
    }
    private static async Task HiloProductorCaptura(ChannelWriter<Mat> writer, CancellationToken ct)
    {
        using var captura = new VideoCapture(0, VideoCapture.API.DShow);
        if (!captura.IsOpened)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("No se pudo abrir la cámara.");
            Console.ResetColor();
            writer.Complete();
            return;
        }
        int intervaloMs = (int)(1000.0 / FPS_CAMARA);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                var frame = new Mat();
                captura.Read(frame);
                if (frame.IsEmpty)
                {
                    captura.Set(CapProp.PosFrames, 0);
                    frame.Dispose();
                    continue;
                }
                await writer.WriteAsync(frame.Clone(), ct);
                frame.Dispose();
                int restante = intervaloMs - (int)sw.ElapsedMilliseconds;
                if (restante > 0) await Task.Delay(restante, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            writer.Complete();
        }
    }
    private static async Task HiloConsumidorPlanificador(ChannelReader<Mat> reader, PlanificadorFiltroDinamico planificador, CancellationToken ct)
    {
        const string ventana = "Planificador Movimiento Mejorado";
        CvInvoke.NamedWindow(ventana, WindowFlags.Normal);
        try
        {
            await foreach (var frameActual in reader.ReadAllAsync(ct))
            {
                var sw = Stopwatch.StartNew();
                var resultado = planificador.EvaluarFrame(frameActual);
                sw.Stop();
                var textoEstado = resultado.DebeEnviar ? "¡ENVIAR IMAGEN A NUBE! (Movimiento detectado)" :
                    resultado.CambioGlobalDetectado ? "DESCARTADO (Movimiento de cámara)" :
                    "FILTRADO / DESCARTADO";
                var colorTexto = resultado.DebeEnviar ? new MCvScalar(0, 255, 0) :
                    resultado.CambioGlobalDetectado ? new MCvScalar(0, 165, 255) :
                    new MCvScalar(150, 150, 150);
                // Añadimos timestamp sobre la imagen
                string timestampStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                CvInvoke.PutText(frameActual, textoEstado, new Point(20, 40), FontFace.HersheySimplex, 0.7, colorTexto, 2);
                CvInvoke.PutText(frameActual, timestampStr, new Point(20, frameActual.Height - 20), FontFace.HersheySimplex, 0.6, new MCvScalar(255, 255, 255), 2);
                CvInvoke.PutText(frameActual, $"Proc: {sw.ElapsedMilliseconds} ms | Deadline: {DEADLINE_LOCAL_MS} ms",
                    new Point(20, 70), FontFace.HersheySimplex, 0.6, new MCvScalar(255, 255, 255), 2);
                CvInvoke.Imshow(ventana, frameActual);
                CvInvoke.WaitKey(1);
                if (sw.ElapsedMilliseconds > DEADLINE_LOCAL_MS)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FALLO DEADLINE] {sw.ElapsedMilliseconds}ms > {DEADLINE_LOCAL_MS}ms");
                }
                else
                {
                    Console.ForegroundColor = resultado.DebeEnviar ? ConsoleColor.Green :
                        resultado.CambioGlobalDetectado ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
                    Console.WriteLine($"[t] Proc: {sw.ElapsedMilliseconds}ms | Ratio Cambio: {resultado.Ratio:P2} | {textoEstado}");
                }
                Console.ResetColor();
                if (resultado.DebeEnviar)
                {
                    // Nombre con fecha, hora y milisegundos para evitar colisiones
                    string nombreArchivo = $"Movimiento_{DateTime.Now:yyyyMMdd_HHmmssfff}.jpg";
                    string rutaCompleta = Path.Combine(CARPETA_SALIDA, nombreArchivo);
                    frameActual.Save(rutaCompleta);
                    Console.WriteLine($"[IMAGEN GUARDADA] {rutaCompleta}");
                }
                frameActual.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelado normal
        }
        finally
        {
            CvInvoke.DestroyWindow(ventana);
        }
    }
}
public class ResultadoPlanificacion
{
    public bool DebeEnviar { get; set; }
    public bool CambioGlobalDetectado { get; set; }
    public double Ratio { get; set; }
}
class PlanificadorFiltroDinamico
{
    private Mat _frameAnterior = new();
    private readonly double _umbralSensibilidadPixel = 25.0;
    private readonly double _ratioMinimoPersona = 0.015;
    private readonly double _ratioMaximoMovimientoCamara = 0.45;
    public ResultadoPlanificacion EvaluarFrame(Mat frameActual)
    {
        var res = new ResultadoPlanificacion();
        Mat grisActual = new();
        CvInvoke.CvtColor(frameActual, grisActual, ColorConversion.Bgr2Gray);
        CvInvoke.GaussianBlur(grisActual, grisActual, new Size(5, 5), 0);
        if (_frameAnterior.IsEmpty)
        {
            _frameAnterior = grisActual;
            return res;
        }
        Mat diferencia = new();
        CvInvoke.AbsDiff(_frameAnterior, grisActual, diferencia);
        Mat umbralizada = new();
        CvInvoke.Threshold(diferencia, umbralizada, _umbralSensibilidadPixel, 255, ThresholdType.Binary);
        int pixelesBlancos = CvInvoke.CountNonZero(umbralizada);
        int totalPixeles = umbralizada.Width * umbralizada.Height;
        res.Ratio = (double)pixelesBlancos / totalPixeles;
        // Dividir en bloques para filtro de movimiento global
        int bloquesX = 8, bloquesY = 8;
        int bloqueWidth = umbralizada.Width / bloquesX;
        int bloqueHeight = umbralizada.Height / bloquesY;
        int bloquesConCambio = 0;
        double umbralCambioBloque = 0.01;
        for (int y = 0; y < bloquesY; y++)
        {
            for (int x = 0; x < bloquesX; x++)
            {
                var roi = new Rectangle(x * bloqueWidth, y * bloqueHeight, bloqueWidth, bloqueHeight);
                using var bloque = new Mat(umbralizada, roi);
                int blancos = CvInvoke.CountNonZero(bloque);
                double ratioBloque = (double)blancos / (bloqueWidth * bloqueHeight);
                if (ratioBloque > umbralCambioBloque)
                    bloquesConCambio++;
            }
        }
        double ratioBloquesCambio = (double)bloquesConCambio / (bloquesX * bloquesY);
        if (ratioBloquesCambio > 0.5)
        {
            res.DebeEnviar = false;
            res.CambioGlobalDetectado = true;
        }
        else if (res.Ratio >= _ratioMinimoPersona && res.Ratio <= _ratioMaximoMovimientoCamara)
        {
            res.DebeEnviar = true;
            res.CambioGlobalDetectado = false;
        }
        else
        {
            res.DebeEnviar = false;
            res.CambioGlobalDetectado = false;
        }
        _frameAnterior.Dispose();
        _frameAnterior = grisActual;
        diferencia.Dispose();
        umbralizada.Dispose();
        return res;
    }
}