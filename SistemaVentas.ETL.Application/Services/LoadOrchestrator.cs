using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Entities;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Application.Services;

/// Orquesta la fase de Carga (L de ETL): lee el último lote de staging de
/// cada entidad y lo carga hacia las dimensiones de DW_SistemaVentas.
/// Se ejecuta después de <see cref="ExtractionOrchestrator"/>.

public class LoadOrchestrator
{
    private readonly IStagingReader<Producto> _productoReader;
    private readonly IStagingReader<Cliente> _clienteReader;
    private readonly IStagingReader<Venta> _ventaReader;

    private readonly IDimensionLoader<Producto> _productoLoader;
    private readonly IDimensionLoader<Cliente> _clienteLoader;
    private readonly ITiempoLoader _tiempoLoader;
    private readonly IVendedorSeeder _vendedorSeeder;

    private readonly ILogger<LoadOrchestrator> _logger;

    public LoadOrchestrator(
        IStagingReader<Producto> productoReader,
        IStagingReader<Cliente> clienteReader,
        IStagingReader<Venta> ventaReader,
        IDimensionLoader<Producto> productoLoader,
        IDimensionLoader<Cliente> clienteLoader,
        ITiempoLoader tiempoLoader,
        IVendedorSeeder vendedorSeeder,
        ILogger<LoadOrchestrator> logger)
    {
        _productoReader = productoReader;
        _clienteReader = clienteReader;
        _ventaReader = ventaReader;
        _productoLoader = productoLoader;
        _clienteLoader = clienteLoader;
        _tiempoLoader = tiempoLoader;
        _vendedorSeeder = vendedorSeeder;
        _logger = logger;
    }

    public async Task EjecutarAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("=== Iniciando carga de dimensiones hacia DW_SistemaVentas ===");

        try
        {
            // DimProducto <- staging de productos (CSV)
            var productos = await _productoReader.ReadLatestAsync(cancellationToken);
            var filasProducto = await _productoLoader.LoadAsync(productos, cancellationToken);
            _logger.LogInformation("DimProducto: {Filas} filas insertadas/actualizadas.", filasProducto);

            // DimCliente <- staging de clientes (BD relacional)
            var clientes = await _clienteReader.ReadLatestAsync(cancellationToken);
            var filasCliente = await _clienteLoader.LoadAsync(clientes, cancellationToken);
            _logger.LogInformation("DimCliente: {Filas} filas insertadas/actualizadas.", filasCliente);

            // DimTiempo <- calculada a partir de las fechas del staging de ventas (API)
            var ventas = await _ventaReader.ReadLatestAsync(cancellationToken);
            var filasTiempo = await _tiempoLoader.LoadAsync(ventas, cancellationToken);
            _logger.LogInformation("DimTiempo: {Filas} fechas nuevas generadas.", filasTiempo);

            // DimVendedor <- miembro por defecto (sin fuente real de vendedores aún)
            var filasVendedor = await _vendedorSeeder.EnsureDefaultAsync(cancellationToken);
            _logger.LogInformation("DimVendedor: miembro por defecto verificado ({Filas} filas nuevas).", filasVendedor);

            stopwatch.Stop();
            _logger.LogInformation("=== Carga de dimensiones completada en {ElapsedMs} ms ===", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "=== Error durante la carga de dimensiones tras {ElapsedMs} ms ===", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
