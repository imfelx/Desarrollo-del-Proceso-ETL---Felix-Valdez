using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Entities;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Application.Services;

/// Orquesta la ejecución de los extractores de forma asíncrona y en paralelo,
/// y delega la escritura a staging.

public class ExtractionOrchestrator
{
    private readonly IExtractor<Producto> _productoExtractor;
    private readonly IExtractor<Cliente> _clienteExtractor;
    private readonly IExtractor<Venta> _ventaExtractor;

    private readonly IStagingWriter<Producto> _productoStaging;
    private readonly IStagingWriter<Cliente> _clienteStaging;
    private readonly IStagingWriter<Venta> _ventaStaging;

    private readonly ILogger<ExtractionOrchestrator> _logger;

    public ExtractionOrchestrator(
        IExtractor<Producto> productoExtractor,
        IExtractor<Cliente> clienteExtractor,
        IExtractor<Venta> ventaExtractor,
        IStagingWriter<Producto> productoStaging,
        IStagingWriter<Cliente> clienteStaging,
        IStagingWriter<Venta> ventaStaging,
        ILogger<ExtractionOrchestrator> logger)
    {
        _productoExtractor = productoExtractor;
        _clienteExtractor = clienteExtractor;
        _ventaExtractor = ventaExtractor;
        _productoStaging = productoStaging;
        _clienteStaging = clienteStaging;
        _ventaStaging = ventaStaging;
        _logger = logger;
    }

    public async Task EjecutarAsync(CancellationToken cancellationToken = default)
    {
        var loteId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("=== Iniciando proceso de extracción. Lote: {LoteId} ===", loteId);

        try
        {
            var productosTask = ExtraerYRegistrarAsync(_productoExtractor, _productoStaging, loteId, cancellationToken);
            var clientesTask = ExtraerYRegistrarAsync(_clienteExtractor, _clienteStaging, loteId, cancellationToken);
            var ventasTask = ExtraerYRegistrarAsync(_ventaExtractor, _ventaStaging, loteId, cancellationToken);

            await Task.WhenAll(productosTask, clientesTask, ventasTask);

            stopwatch.Stop();
            _logger.LogInformation(
                "=== Extracción completada exitosamente en {ElapsedMs} ms (Lote: {LoteId}) ===",
                stopwatch.ElapsedMilliseconds, loteId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "=== Error durante el proceso de extracción (Lote: {LoteId}) tras {ElapsedMs} ms ===",
                loteId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task ExtraerYRegistrarAsync<T>(
        IExtractor<T> extractor,
        IStagingWriter<T> staging,
        string loteId,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Iniciando extracción desde: {Fuente}", extractor.NombreFuente);

            var datos = await extractor.ExtractAsync(cancellationToken);

            _logger.LogInformation(
                "Extracción de {Fuente} finalizada: {Cantidad} registros en {ElapsedMs} ms",
                extractor.NombreFuente, datos.Count, sw.ElapsedMilliseconds);

            await staging.WriteAsync(datos, loteId, cancellationToken);

            _logger.LogInformation("Registros de {Fuente} guardados en staging (lote {LoteId})",
                extractor.NombreFuente, loteId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al extraer/registrar datos desde {Fuente}", extractor.NombreFuente);
            throw;
        }
    }
}
