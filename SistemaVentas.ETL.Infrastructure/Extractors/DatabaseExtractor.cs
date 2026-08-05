using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Entities;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Infrastructure.Extractors;


/// Extrae clientes desde la base relacional de Análisis de ventas.

public class DatabaseExtractor : IExtractor<Cliente>
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseExtractor> _logger;

    public string NombreFuente => "BD - Clientes";

    private const string Query = @"
        SELECT
            CAST(cu.customer_id AS VARCHAR(20)) AS ClienteID,
            CONCAT(cu.firstname, ' ', cu.lastname) AS NombreCliente,
            NULL AS Segmento,
            COALESCE(co.name, 'Desconocido') AS Pais,
            NULL AS Region,
            ci.name AS Ciudad
        FROM dbo.customers cu
        LEFT JOIN dbo.cities ci ON ci.city_id = cu.city
        LEFT JOIN dbo.countries co ON co.country_id = cu.country";

    public DatabaseExtractor(IConfiguration configuration, ILogger<DatabaseExtractor> logger)
    {
        _connectionString = configuration.GetConnectionString("OrigenAnalisisVentas")
            ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'OrigenAnalisisVentas'.");
        _logger = logger;
    }

    public async Task<IReadOnlyList<Cliente>> ExtractAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var command = new CommandDefinition(Query, cancellationToken: cancellationToken);
            var resultado = await connection.QueryAsync<Cliente>(command);
            return resultado.AsList();
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error de conexión/consulta contra la base de datos de clientes.");
            throw;
        }
    }
}
