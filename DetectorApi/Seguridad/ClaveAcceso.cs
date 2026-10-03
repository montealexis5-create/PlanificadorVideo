using System.Security.Cryptography;
using System.Text;

namespace DetectorApi.Seguridad;

public static class ClaveAcceso
{
    // Compara en tiempo constante: no deja adivinar la clave midiendo cuánto tarda la respuesta.
    public static bool EsValida(string? recibida, string? esperada)
    {
        if (string.IsNullOrEmpty(recibida) || string.IsNullOrEmpty(esperada))
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recibida), Encoding.UTF8.GetBytes(esperada));
    }
}
