using Itm.Gateway.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);

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

// 2. Activamos el middleware de Correlation ID antes del proxy inverso
app.UseMiddleware<CorrelationIdMiddleware>();

// 3. Activamos el middleware de YARP para que procese las solicitudes entrantes
app.MapReverseProxy();// Activamos el enrrutador de YARP para que procese las solicitudes entrantes y las dirija a los servicios backend según la configuración

// Activar el panel gráfico de salud en la ruta /health-ui

app.MapHealthChecksUI(options =>
{
    options.UIPath = "/monitor"; // La URL donde estará disponible el dashboard de salud
});

app.Run();
