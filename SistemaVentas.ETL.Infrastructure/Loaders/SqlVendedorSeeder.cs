using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Infrastructure.Loaders;

public class SqlVendedorSeeder : IVendedorSeeder
{
    private readonly string _connectionString;
    private readonly ILogger<SqlVendedorSeeder> _logger;

    public string TablaDestino => "DimVendedor";

    public SqlVendedorSeeder(IConfiguration configuration, ILogger<SqlVendedorSeeder> logger)
    {
        _connectionString = configuration.GetConnectionString("DW_SistemaVentas")
            ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'DW_SistemaVentas'.");
        _logger = logger;
    }

    private const string InsertSiNoExisteSql = @"
        IF NOT EXISTS (SELECT 1 FROM DimVendedor WHERE VendedorID = 'VEND-000')
        INSERT INTO DimVendedor (VendedorID, NombreVendedor, Region)
        VALUES ('VEND-000', 'Sin asignar', NULL);";

    public async Task<int> EnsureDefaultAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_connectionString);
        var command = new CommandDefinition(InsertSiNoExisteSql, cancellationToken: cancellationToken);
        var filas = await connection.ExecuteAsync(command);

        _logger.LogInformation("Verificado en {Tabla}: miembro por defecto 'VEND-000 - Sin asignar' presente.", TablaDestino);
        return filas;
    }
}
