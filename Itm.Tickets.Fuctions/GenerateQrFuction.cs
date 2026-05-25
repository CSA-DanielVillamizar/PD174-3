using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
namespace Itm.Tickets.Fuctions_
{
    public class GenerateQrFuction
    {
        private readonly ILogger<GenerateQrFuction> _logger;

        public GenerateQrFuction(ILogger<GenerateQrFuction> logger)
        {
            _logger = logger;
        }

        [Function("GenerateQrFuction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Procesando solicitud para generar QR...");

            //1. Leer el cuerpo d ela petición (Vine del microservicio de compras)
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<OrderDto>(requestBody);

            if (data == null || string.IsNullOrEmpty(data.OrderId))
            {
                return new BadRequestObjectResult("Datos de orden inválidos.");
            }

            // 2. Simulación lógoca pesada ( Generación de imágenes QR)
            _logger.LogInformation($"Generando QR para la orden: {data.OrderId}...");

            // Aquí llamariamos a una libreria como SkiaSharp o QRCoder para generar la imagen del QR basado en el OrderId o cualquier otra información relevante.

            await Task.Delay(500); // Simulación de proceso pesado

            // 3. Respuesta al cliente
            return new OkObjectResult(new { Message = "QR generado exitosamente para la orden",
                StorageUrl = $"https://storageaccount.blob.core.windows.net/qrcodes/{data.OrderId}.png",
                Timestamp = DateTime.UtcNow

            });

        }
    }
    public record OrderDto(string OrderId, string UserEmail);
}
