using Itm.Gateway.Api.Middlewares;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 🛡️ ESCUDO DE SEGURIDAD: RATE LIMITING (NIVEL 5)
builder.Services.AddRateLimiter(options =>
{
    // Si alguien abusa, respondemos con un 429 (Too Many Requests)
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Política de "Ventana Fija": 10 peticiones cada 10 segundos por cada IP
    options.AddPolicy("fixed", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 0 // No hacemos fila; se rechaza de inmediato
            }));
});

//1. Agregamos YARP a la caja de herramientas (Dependency Injection)
// Le decimos que lea la configuración de rutas desde el appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Configuramos el Dashboard de salud para monitorear el estado de los servicios backend
builder.Services.AddHealthChecksUI(setupSettings: setup =>
{
    // Aquí matriculamos los pacientes, es decir, los endpoints de salud de cada servicio que queremos monitorear
    setup.AddHealthCheckEndpoint("Inventory API", "http://inventory-api-service:80/health");
    setup.AddHealthCheckEndpoint("Orders API", "http://order-service:8080/health");
    setup.AddHealthCheckEndpoint("Product API", "http://product-api-service:80/health");
    setup.AddHealthCheckEndpoint("Notifications API", "http://notification-service:80/health");

})
    .AddInMemoryStorage(); // Guarda el histórico de salud en memoria


var app = builder.Build();

app.UseRateLimiter(); // 1. Activamos el motor del Rate Limiting

// 2. Activamos el middleware de Correlation ID antes del proxy inverso
app.UseMiddleware<CorrelationIdMiddleware>();

// 3. Activamos el middleware de YARP para que procese las solicitudes entrantes y forzamos el límite de peticiones
app.MapReverseProxy().RequireRateLimiting("fixed");

// Redireccionar al monitor si entra a la ruta raíz (soluciona el 404 local)
app.MapGet("/", () => Results.Redirect("/monitor"));

// Activar el panel gráfico de salud en la ruta /health-ui

app.MapHealthChecksUI(options =>
{
    options.UIPath = "/monitor"; // La URL donde estará disponible el dashboard de salud
});

app.Run();
