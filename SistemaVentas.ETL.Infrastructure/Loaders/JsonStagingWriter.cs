using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Infrastructure.Loaders;

/// <summary>
/// Implementación genérica de staging que serializa los registros extraídos
/// a archivos JSON temporales, organizados por lote.
/// </summary>
public class JsonStagingWriter<T> : IStagingWriter<T>
{
    private readonly string _carpetaBase;
    private readonly ILogger<JsonStagingWriter<T>> _logger;

    public JsonStagingWriter(IConfiguration configuration, ILogger<JsonStagingWriter<T>> logger)
    {
        _carpetaBase = configuration["Staging:CarpetaBase"] ?? "Staging";
        _logger = logger;
    }

    public async Task WriteAsync(IReadOnlyList<T> registros, string nombreLote, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_carpetaBase);

        var nombreTipo = typeof(T).Name;
        var rutaArchivo = Path.Combine(_carpetaBase, $"{nombreTipo}_{nombreLote}.json");

        var opciones = new JsonSerializerOptions { WriteIndented = true };

        await using var stream = File.Create(rutaArchivo);
        await JsonSerializer.SerializeAsync(stream, registros, opciones, cancellationToken);

        _logger.LogInformation("Staging escrito: {Archivo} ({Cantidad} registros)", rutaArchivo, registros.Count);
    }
}
