using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Entities;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Infrastructure.Extractors;

/// Extrae productos desde el archivo CSV de origen.

public class CsvExtractor : IExtractor<Producto>
{
    private readonly string _rutaArchivo;
    private readonly ILogger<CsvExtractor> _logger;

    public string NombreFuente => "CSV - Productos";

    public CsvExtractor(IConfiguration configuration, ILogger<CsvExtractor> logger)
    {
        _rutaArchivo = configuration["FuentesDatos:CsvProductos:Ruta"]
            ?? throw new InvalidOperationException("No se configuró la ruta del CSV de productos.");
        _logger = logger;
    }

    public async Task<IReadOnlyList<Producto>> ExtractAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_rutaArchivo))
        {
            _logger.LogWarning("El archivo CSV no existe en la ruta: {Ruta}", _rutaArchivo);
            return Array.Empty<Producto>();
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(_rutaArchivo);
        using var csv = new CsvReader(reader, config);

        var registros = new List<Producto>();

        await foreach (var registro in csv.GetRecordsAsync<ProductoCsvRow>(cancellationToken))
        {
            registros.Add(new Producto
            {
                ProductoID = registro.ProductID,
                NombreProducto = registro.ProductName,
                Categoria = registro.Category,
                Subcategoria = null,
                PrecioUnitario = registro.Price,
                Estado = "Activo"
            });
        }

        return registros;
    }

    private sealed class ProductoCsvRow
    {
        public string ProductID { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
