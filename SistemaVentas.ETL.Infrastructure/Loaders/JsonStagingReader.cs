using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Infrastructure.Loaders;


/// Lee el archivo de staging más reciente para un tipo T
/// (el último lote escrito por JsonStagingWriter&lt;T&gt; en Staging/{Tipo}_*.json).

public class JsonStagingReader<T> : IStagingReader<T>
{
    private readonly string _carpetaBase;
    private readonly ILogger<JsonStagingReader<T>> _logger;

    public JsonStagingReader(IConfiguration configuration, ILogger<JsonStagingReader<T>> logger)
    {
        _carpetaBase = configuration["Staging:CarpetaBase"] ?? "Staging";
        _logger = logger;
    }

    public async Task<IReadOnlyList<T>> ReadLatestAsync(CancellationToken cancellationToken = default)
    {
        var nombreTipo = typeof(T).Name;

        if (!Directory.Exists(_carpetaBase))
        {
            _logger.LogWarning("La carpeta de staging '{Carpeta}' no existe todavía.", _carpetaBase);
            return Array.Empty<T>();
        }

        // Los nombres de archivo incluyen el lote como sufijo yyyyMMdd_HHmmss,
        // por lo que el orden alfabético coincide con el orden cronológico.
        var archivo = Directory.GetFiles(_carpetaBase, $"{nombreTipo}_*.json")
            .OrderByDescending(f => f)
            .FirstOrDefault();

        if (archivo is null)
        {
            _logger.LogWarning("No se encontró ningún archivo de staging para {Tipo}. Ejecuta primero la extracción.", nombreTipo);
            return Array.Empty<T>();
        }

        await using var stream = File.OpenRead(archivo);
        var registros = await JsonSerializer.DeserializeAsync<List<T>>(stream, cancellationToken: cancellationToken);

        _logger.LogInformation("Staging leído: {Archivo} ({Cantidad} registros)", archivo, registros?.Count ?? 0);
        return registros ?? new List<T>();
    }
}
