using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace Planificador;

// Compara cada frame con el anterior y decide si vale la pena mandarlo a la nube.
public class FiltroMovimiento : IDisposable
{
    private const double UMBRAL_SENSIBILIDAD_PIXEL = 25.0;
    private const double RATIO_MINIMO_PERSONA = 0.015;
    private const double RATIO_MAXIMO_MOVIMIENTO = 0.45;
    private const double UMBRAL_CAMBIO_BLOQUE = 0.01;
    private const double RATIO_BLOQUES_MOVIMIENTO_CAMARA = 0.5;
    private const int BLOQUES = 8;   // La imagen se divide en una grilla de 8x8

    private Mat _frameAnterior = new();

    public ResultadoPlanificacion EvaluarFrame(Mat frameActual)
    {
        var grisActual = new Mat();
        CvInvoke.CvtColor(frameActual, grisActual, ColorConversion.Bgr2Gray);
        CvInvoke.GaussianBlur(grisActual, grisActual, new Size(5, 5), 0);
        if (_frameAnterior.IsEmpty)
        {
            _frameAnterior = grisActual;
            return new ResultadoPlanificacion(false, false, 0);
        }

        // Píxeles que cambiaron respecto del frame anterior, en blanco sobre negro.
        using var diferencia = new Mat();
        using var cambios = new Mat();
        CvInvoke.AbsDiff(_frameAnterior, grisActual, diferencia);
        CvInvoke.Threshold(diferencia, cambios, UMBRAL_SENSIBILIDAD_PIXEL, 255, ThresholdType.Binary);

        double ratio = (double)CvInvoke.CountNonZero(cambios) / (cambios.Width * cambios.Height);
        double ratioBloques = CalcularRatioBloquesConCambio(cambios);

        _frameAnterior.Dispose();
        _frameAnterior = grisActual;
        return Clasificar(ratio, ratioBloques);
    }

    // Decisión pura, sin OpenCV, para poder probarla con tests unitarios.
    // - Si cambió más de la mitad de los bloques, se movió la cámara o cambió la luz: se descarta.
    // - Si cambió entre el 1,5% y el 45% de los píxeles, es movimiento real: se envía.
    public static ResultadoPlanificacion Clasificar(double ratio, double ratioBloquesConCambio)
    {
        if (ratioBloquesConCambio > RATIO_BLOQUES_MOVIMIENTO_CAMARA)
            return new ResultadoPlanificacion(false, true, ratio);

        bool debeEnviar = ratio >= RATIO_MINIMO_PERSONA && ratio <= RATIO_MAXIMO_MOVIMIENTO;
        return new ResultadoPlanificacion(debeEnviar, false, ratio);
    }

    private static double CalcularRatioBloquesConCambio(Mat cambios)
    {
        int anchoBloque = cambios.Width / BLOQUES;
        int altoBloque = cambios.Height / BLOQUES;
        int bloquesConCambio = 0;
        for (int y = 0; y < BLOQUES; y++)
        {
            for (int x = 0; x < BLOQUES; x++)
            {
                using var bloque = new Mat(cambios, new Rectangle(x * anchoBloque, y * altoBloque, anchoBloque, altoBloque));
                double ratioBloque = (double)CvInvoke.CountNonZero(bloque) / (anchoBloque * altoBloque);
                if (ratioBloque > UMBRAL_CAMBIO_BLOQUE)
                    bloquesConCambio++;
            }
        }
        return (double)bloquesConCambio / (BLOQUES * BLOQUES);
    }

    public void Dispose() => _frameAnterior.Dispose();
}
