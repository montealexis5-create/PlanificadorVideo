using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using QRCoder;

namespace DetectorApi.Servicios;

public record EnlaceCamara(string Url, string QrPngBase64);

// Arma el link (y su QR) para usar un celular como cámara. El celular abre camara.html por HTTPS,
// porque los navegadores solo dejan usar la cámara en conexiones seguras.
public class EnlaceCamaraService
{
    private readonly IConfiguration _configuracion;

    public EnlaceCamaraService(IConfiguration configuracion)
    {
        _configuracion = configuracion;
    }

    // Devuelve null si no se pudo averiguar la IP de la compu en la red.
    public EnlaceCamara? Generar()
    {
        string? host = _configuracion["CamaraRemota:Host"] is { Length: > 0 } configurado ? configurado : ObtenerIpDeLaRed();
        if (host == null)
            return null;
        int puerto = _configuracion.GetValue<int>("CamaraRemota:PuertoHttps");

        // La API key va después del "#": el navegador no la manda al servidor ni queda en los logs.
        string url = $"https://{host}:{puerto}/camara.html#clave={Uri.EscapeDataString(_configuracion["Seguridad:ApiKey"]!)}";
        return new EnlaceCamara(url, Convert.ToBase64String(GenerarQr(url)));
    }

    public static byte[] GenerarQr(string texto)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(8);
    }

    // IP de la placa de red que tiene salida a internet (descarta las virtuales, como las de VirtualBox o Docker).
    private static string? ObtenerIpDeLaRed()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(placa => placa.OperationalStatus == OperationalStatus.Up
                && placa.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && placa.GetIPProperties().GatewayAddresses.Any(g => !g.Address.Equals(IPAddress.Any)))
            .SelectMany(placa => placa.GetIPProperties().UnicastAddresses)
            .Select(direccion => direccion.Address)
            .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
            ?.ToString();
    }
}
