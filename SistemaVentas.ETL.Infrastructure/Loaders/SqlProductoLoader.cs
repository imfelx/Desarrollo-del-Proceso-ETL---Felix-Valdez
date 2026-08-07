using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Entities;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Infrastructure.Loaders;


/// Carga (upsert) los productos extraídos hacia DimProducto en DW_SistemaVentas.
/// Usa MERGE sobre la llave de negocio ProductoID para evitar duplicados 

public class SqlProductoLoader : IDimensionLoader<Producto>
{
    private readonly string _connectionString;
    private readonly ILogger<SqlProductoLoader> _logger;

    public string TablaDestino => "DimProducto";

    public SqlProductoLoader(IConfiguration configuration, ILogger<SqlProductoLoader> logger)
    {
        _connectionString = configuration.GetConnectionString("DW_SistemaVentas")
            ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'DW_SistemaVentas'.");
        _logger = logger;
    }

    private const string MergeSql = @"
        MERGE DimProducto AS destino
        USING (SELECT @ProductoID AS ProductoID, @NombreProducto AS NombreProducto,
                      @Categoria AS Categoria, @Subcategoria AS Subcategoria,
                      @PrecioUnitario AS PrecioUnitario, @Estado AS Estado) AS origen
        ON destino.ProductoID = origen.ProductoID
        WHEN MATCHED THEN
            UPDATE SET NombreProducto = origen.NombreProducto,
                       Categoria      = origen.Categoria,
                       Subcategoria   = origen.Subcategoria,
                       PrecioUnitario = origen.PrecioUnitario,
                       Estado         = origen.Estado
        WHEN NOT MATCHED THEN
            INSERT (ProductoID, NombreProducto, Categoria, Subcategoria, PrecioUnitario, Estado)
            VALUES (origen.ProductoID, origen.NombreProducto, origen.Categoria,
                    origen.Subcategoria, origen.PrecioUnitario, origen.Estado);";

    public async Task<int> LoadAsync(IReadOnlyList<Producto> registros, CancellationToken cancellationToken = default)
    {
        if (registros.Count == 0)
        {
            _logger.LogWarning("No hay productos en staging para cargar en {Tabla}.", TablaDestino);
            return 0;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            var filas = 0;
            foreach (var producto in registros)
            {
                var command = new CommandDefinition(MergeSql, producto, transaction, cancellationToken: cancellationToken);
                filas += await connection.ExecuteAsync(command);
            }

            transaction.Commit();
            _logger.LogInformation("Carga completada en {Tabla}: {Cantidad} productos procesados.", TablaDestino, registros.Count);
            return filas;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error al cargar productos en {Tabla}. Se revirtió la transacción.", TablaDestino);
            throw;
        }
    }
}
