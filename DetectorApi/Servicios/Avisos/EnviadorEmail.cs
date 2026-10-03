using System.Net;
using System.Net.Mail;
using DetectorApi.Entidades;

namespace DetectorApi.Servicios.Avisos;

// Manda el aviso por email con la foto adjunta. La cuenta que envía (por ejemplo una de Gmail con
// "contraseña de aplicación") se configura en la sección Smtp, nunca en el código.
public class EnviadorEmail : IEnviadorAviso
{
    private readonly IConfiguration _configuracion;

    public EnviadorEmail(IConfiguration configuracion)
    {
        _configuracion = configuracion;
    }

    public string Nombre => "Email";

    public bool EstaConfigurado(ConfiguracionAvisos configuracion) => !string.IsNullOrWhiteSpace(configuracion.Email);

    public async Task Enviar(ConfiguracionAvisos configuracion, Aviso aviso, CancellationToken ct)
    {
        var smtp = _configuracion.GetSection("Smtp");
        if (string.IsNullOrWhiteSpace(smtp["Usuario"]) || string.IsNullOrWhiteSpace(smtp["Clave"]))
            throw new InvalidOperationException("Falta configurar la cuenta que envía los emails (sección Smtp).");

        using var mensaje = new MailMessage(smtp["Usuario"]!, configuracion.Email!, "Persona detectada", aviso.Texto);
        if (aviso.Imagen.Length > 0)
            mensaje.Attachments.Add(new Attachment(new MemoryStream(aviso.Imagen), "captura.jpg", "image/jpeg"));

        using var cliente = new SmtpClient(smtp["Host"], smtp.GetValue<int>("Puerto"))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(smtp["Usuario"], smtp["Clave"])
        };
        await cliente.SendMailAsync(mensaje, ct);
    }
}
