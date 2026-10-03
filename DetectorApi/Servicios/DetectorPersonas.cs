using DetectorApi.Dtos;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DetectorApi.Servicios;

// Detecta personas con el modelo YOLO11n (formato ONNX). El modelo reconoce 80 tipos de objetos;
// acá solo se usa la clase 0, que es "persona".
public class DetectorPersonas : IDetectorPersonas, IDisposable
{
    private const int TAMANIO = 640;   // El modelo recibe imágenes de 640x640
    private const int CLASE_PERSONA = 0;
    private const double UMBRAL_SUPERPOSICION = 0.45;

    private readonly InferenceSession _sesion;
    private readonly float _umbralConfianza;

    public DetectorPersonas(IConfiguration configuracion)
    {
        _sesion = new InferenceSession(Path.Combine(AppContext.BaseDirectory, configuracion["Deteccion:RutaModelo"]!));
        _umbralConfianza = configuracion.GetValue<float>("Deteccion:UmbralConfianza");
    }

    public ResultadoDeteccion Detectar(byte[] imagen)
    {
        var entrada = new DenseTensor<float>(PrepararImagen(imagen), new[] { 1, 3, TAMANIO, TAMANIO });
        using var resultados = _sesion.Run(new[] { NamedOnnxValue.CreateFromTensor("images", entrada) });

        // Salida [1, 84, 8400]: 8400 cajas candidatas. Por cada una, las filas 0 a 3 son la caja
        // (centro x, centro y, ancho, alto, en píxeles de 640) y las filas 4 a 83 la confianza de cada clase.
        Tensor<float> salida = resultados.First().AsTensor<float>();
        var candidatas = new List<CajaDto>();
        float confianzaMaxima = 0;
        for (int i = 0; i < salida.Dimensions[2]; i++)
        {
            float confianza = salida[0, 4 + CLASE_PERSONA, i];
            confianzaMaxima = Math.Max(confianzaMaxima, confianza);
            if (confianza < _umbralConfianza)
                continue;

            // Se pasa a coordenadas de 0 a 1 (esquina superior izquierda) para dibujarla sobre la imagen original.
            float ancho = salida[0, 2, i], alto = salida[0, 3, i];
            candidatas.Add(new CajaDto(
                (salida[0, 0, i] - ancho / 2) / TAMANIO,
                (salida[0, 1, i] - alto / 2) / TAMANIO,
                ancho / TAMANIO,
                alto / TAMANIO,
                confianza));
        }

        return new ResultadoDeteccion(SupresionDeCajas.QuitarSuperpuestas(candidatas, UMBRAL_SUPERPOSICION), confianzaMaxima);
    }

    // Deja la imagen como la espera el modelo: 640x640, RGB, valores de 0 a 1 y ordenada por canal
    // (primero todos los rojos, después los verdes y al final los azules).
    private static float[] PrepararImagen(byte[] bytes)
    {
        using Image<Rgb24> imagen = Image.Load<Rgb24>(bytes);
        imagen.Mutate(x => x.Resize(TAMANIO, TAMANIO));

        const int porCanal = TAMANIO * TAMANIO;
        var datos = new float[3 * porCanal];
        imagen.ProcessPixelRows(filas =>
        {
            for (int y = 0; y < TAMANIO; y++)
            {
                Span<Rgb24> fila = filas.GetRowSpan(y);
                for (int x = 0; x < TAMANIO; x++)
                {
                    int i = y * TAMANIO + x;
                    datos[i] = fila[x].R / 255f;
                    datos[porCanal + i] = fila[x].G / 255f;
                    datos[2 * porCanal + i] = fila[x].B / 255f;
                }
            }
        });
        return datos;
    }

    public void Dispose() => _sesion.Dispose();
}
