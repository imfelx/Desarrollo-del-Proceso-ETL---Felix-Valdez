using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Entities;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Infrastructure.Loaders;

/// Carga (upsert) los clientes extraídos hacia DimCliente en DW_SistemaVentas.
/// Usa MERGE sobre la llave de negocio ClienteID (dimensión tipo 1).

public class SqlClienteLoader : IDimensionLoader<Cliente>
{
    private readonly string _connectionString;
    private readonly ILogger<SqlClienteLoader> _logger;

    public string TablaDestino => "DimCliente";

    public SqlClienteLoader(IConfiguration configuration, ILogger<SqlClienteLoader> logger)
    {
        _connectionString = configuration.GetConnectionString("DW_SistemaVentas")
            ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'DW_SistemaVentas'.");
        _logger = logger;
    }

    private const string MergeSql = @"
        MERGE DimCliente AS destino
        USING (SELECT @ClienteID AS ClienteID, @NombreCliente AS NombreCliente,
                      @Segmento AS Segmento, @Pais AS Pais,
                      @Region AS Region, @Ciudad AS Ciudad) AS origen
        ON destino.ClienteID = origen.ClienteID
        WHEN MATCHED THEN
            UPDATE SET NombreCliente = origen.NombreCliente,
                       Segmento      = origen.Segmento,
                       Pais          = origen.Pais,
                       Region        = origen.Region,
                       Ciudad        = origen.Ciudad
        WHEN NOT MATCHED THEN
            INSERT (ClienteID, NombreCliente, Segmento, Pais, Region, Ciudad)
            VALUES (origen.ClienteID, origen.NombreCliente, origen.Segmento,
                    origen.Pais, origen.Region, origen.Ciudad);";

    public async Task<int> LoadAsync(IReadOnlyList<Cliente> registros, CancellationToken cancellationToken = default)
    {
        if (registros.Count == 0)
        {
            _logger.LogWarning("No hay clientes en staging para cargar en {Tabla}.", TablaDestino);
            return 0;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            var filas = 0;
            foreach (var cliente in registros)
            {
                var command = new CommandDefinition(MergeSql, cliente, transaction, cancellationToken: cancellationToken);
                filas += await connection.ExecuteAsync(command);
            }

            transaction.Commit();
            _logger.LogInformation("Carga completada en {Tabla}: {Cantidad} clientes procesados.", TablaDestino, registros.Count);
            return filas;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error al cargar clientes en {Tabla}. Se revirtió la transacción.", TablaDestino);
            throw;
        }
    }
}
