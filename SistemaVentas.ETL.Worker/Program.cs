using SistemaVentas.ETL.Application.Services;
using SistemaVentas.ETL.Domain.Entities;
using SistemaVentas.ETL.Domain.Interfaces;
using SistemaVentas.ETL.Infrastructure.Extractors;
using SistemaVentas.ETL.Infrastructure.Loaders;
using SistemaVentas.ETL.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("AnalisisVentasApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// --- Registro de extractores (uno por fuente) ---
builder.Services.AddScoped<IExtractor<Producto>, CsvExtractor>();
builder.Services.AddScoped<IExtractor<Cliente>, DatabaseExtractor>();
builder.Services.AddScoped<IExtractor<Venta>, AnalisisVentasApiExtractor>();

// --- Registro de escritores de staging (uno por entidad) ---
builder.Services.AddScoped<IStagingWriter<Producto>, JsonStagingWriter<Producto>>();
builder.Services.AddScoped<IStagingWriter<Cliente>, JsonStagingWriter<Cliente>>();
builder.Services.AddScoped<IStagingWriter<Venta>, JsonStagingWriter<Venta>>();

// --- Orquestador y worker ---
builder.Services.AddScoped<ExtractionOrchestrator>();
builder.Services.AddHostedService<EtlWorker>();

var host = builder.Build();
host.Run();
