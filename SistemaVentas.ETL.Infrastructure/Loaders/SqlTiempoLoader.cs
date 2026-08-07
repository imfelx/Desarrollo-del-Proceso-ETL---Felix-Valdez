using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Entities;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Infrastructure.Loaders;


/// Genera y carga DimTiempo a partir del rango de fechas presente en las
/// ventas extraídas. No depende de una fuente externa: es una dimensión

public class SqlTiempoLoader : ITiempoLoader
{
    private static readonly CultureInfo CulturaEs = new("es-ES");

    private readonly string _connectionString;
    private readonly ILogger<SqlTiempoLoader> _logger;

    public string TablaDestino => "DimTiempo";

    public SqlTiempoLoader(IConfiguration configuration, ILogger<SqlTiempoLoader> logger)
    {
        _connectionString = configuration.GetConnectionString("DW_SistemaVentas")
            ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'DW_SistemaVentas'.");
        _logger = logger;
    }

    private const string InsertSiNoExisteSql = @"
        IF NOT EXISTS (SELECT 1 FROM DimTiempo WHERE FechaKey = @FechaKey)
        INSERT INTO DimTiempo (FechaKey, Fecha, Dia, DiaSemana, Mes, NombreMes, Trimestre, Semestre, Anio)
        VALUES (@FechaKey, @Fecha, @Dia, @DiaSemana, @Mes, @NombreMes, @Trimestre, @Semestre, @Anio);";

    public async Task<int> LoadAsync(IReadOnlyList<Venta> ventas, CancellationToken cancellationToken = default)
    {
        if (ventas.Count == 0)
        {
            _logger.LogWarning("No hay ventas en staging para derivar fechas en {Tabla}.", TablaDestino);
            return 0;
        }

        var fechaInicio = ventas.Min(v => v.FechaVenta.Date);
        var fechaFin = ventas.Max(v => v.FechaVenta.Date);

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            var filas = 0;
            for (var fecha = fechaInicio; fecha <= fechaFin; fecha = fecha.AddDays(1))
            {
                var fila = new
                {
                    FechaKey = int.Parse(fecha.ToString("yyyyMMdd")),
                    Fecha = fecha,
                    Dia = (byte)fecha.Day,
                    DiaSemana = CulturaEs.DateTimeFormat.GetDayName(fecha.DayOfWeek),
                    Mes = (byte)fecha.Month,
                    NombreMes = CulturaEs.DateTimeFormat.GetMonthName(fecha.Month),
                    Trimestre = (byte)(((fecha.Month - 1) / 3) + 1),
                    Semestre = (byte)(fecha.Month <= 6 ? 1 : 2),
                    Anio = (short)fecha.Year
                };

                var command = new CommandDefinition(InsertSiNoExisteSql, fila, transaction, cancellationToken: cancellationToken);
                filas += await connection.ExecuteAsync(command);
            }

            transaction.Commit();
            _logger.LogInformation(
                "Carga completada en {Tabla}: {Filas} fechas nuevas ({Inicio:d} a {Fin:d}).",
                TablaDestino, filas, fechaInicio, fechaFin);
            return filas;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error al cargar fechas en {Tabla}. Se revirtió la transacción.", TablaDestino);
            throw;
        }
    }
}
