using DetectorApi.Servicios;

namespace PlanificadorVideo.Tests;

public class EnlaceCamaraTests
{
    [Fact]
    public void GenerarQr_DevuelveUnaImagenPng()
    {
        byte[] png = EnlaceCamaraService.GenerarQr("https://192.168.0.10:5443/camara.html#clave=abc");

        // Todo archivo PNG empieza con estos 8 bytes.
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.Take(8).ToArray());
    }
}
