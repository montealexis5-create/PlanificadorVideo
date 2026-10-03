using DetectorApi.Dtos;

namespace DetectorApi.Servicios;

// El modelo suele marcar a la misma persona con varias cajas parecidas. Esta clase se queda con la de
// mayor confianza y descarta las que se le superponen mucho (Non-Maximum Suppression).
// No depende del modelo ni de la base, así que se prueba directo con tests unitarios.
public static class SupresionDeCajas
{
    public static List<CajaDto> QuitarSuperpuestas(IEnumerable<CajaDto> candidatas, double umbralSuperposicion)
    {
        var elegidas = new List<CajaDto>();
        foreach (var caja in candidatas.OrderByDescending(c => c.Confianza))
        {
            if (elegidas.All(elegida => Superposicion(caja, elegida) < umbralSuperposicion))
                elegidas.Add(caja);
        }
        return elegidas;
    }

    // Intersección sobre unión (IoU): 0 si las cajas no se tocan, 1 si son idénticas.
    public static double Superposicion(CajaDto a, CajaDto b)
    {
        double ancho = Math.Min(a.X + a.Ancho, b.X + b.Ancho) - Math.Max(a.X, b.X);
        double alto = Math.Min(a.Y + a.Alto, b.Y + b.Alto) - Math.Max(a.Y, b.Y);
        double interseccion = Math.Max(0, ancho) * Math.Max(0, alto);
        double union = a.Ancho * a.Alto + b.Ancho * b.Alto - interseccion;
        return union <= 0 ? 0 : interseccion / union;
    }
}
