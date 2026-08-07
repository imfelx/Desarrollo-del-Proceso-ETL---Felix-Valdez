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

// --- Registro de lectores de staging (uno por entidad, usados en la fase de Carga) ---
builder.Services.AddScoped<IStagingReader<Producto>, JsonStagingReader<Producto>>();
builder.Services.AddScoped<IStagingReader<Cliente>, JsonStagingReader<Cliente>>();
builder.Services.AddScoped<IStagingReader<Venta>, JsonStagingReader<Venta>>();

// --- Registro de loaders hacia DW_SistemaVentas (fase de Carga) ---
builder.Services.AddScoped<IDimensionLoader<Producto>, SqlProductoLoader>();
builder.Services.AddScoped<IDimensionLoader<Cliente>, SqlClienteLoader>();
builder.Services.AddScoped<ITiempoLoader, SqlTiempoLoader>();
builder.Services.AddScoped<IVendedorSeeder, SqlVendedorSeeder>();

// --- Orquestadores y worker ---
builder.Services.AddScoped<ExtractionOrchestrator>();
builder.Services.AddScoped<LoadOrchestrator>();
builder.Services.AddHostedService<EtlWorker>();

var host = builder.Build();
host.Run();
