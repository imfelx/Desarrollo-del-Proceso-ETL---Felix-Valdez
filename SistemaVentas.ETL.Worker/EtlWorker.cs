using SistemaVentas.ETL.Application.Services;

namespace SistemaVentas.ETL.Worker;

/// Hosted service que dispara el proceso de extracción.
/// Toda la lógica de orquestación vive en Application; este servicio
/// solo se encarga del ciclo de vida dentro del host de .NET.

public class EtlWorker : BackgroundService
{
  
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EtlWorker> _logger;
    private readonly IConfiguration _configuration;

    public EtlWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EtlWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Intervalo configurable (por defecto, corre una vez y espera 1 hora).
        var intervaloMinutos = _configuration.GetValue("EtlSettings:IntervaloMinutos", 60);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                // Fase E: extrae de las 3 fuentes y escribe a Staging/
                var extractionOrchestrator = scope.ServiceProvider.GetRequiredService<ExtractionOrchestrator>();
                await extractionOrchestrator.EjecutarAsync(stoppingToken);

                // Fase L: lee el staging recién escrito y carga las dimensiones del DW
                var loadOrchestrator = scope.ServiceProvider.GetRequiredService<LoadOrchestrator>();
                await loadOrchestrator.EjecutarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "El proceso ETL finalizó con errores no controlados.");
            }

            _logger.LogInformation("Esperando {Minutos} minutos para la próxima ejecución.", intervaloMinutos);
            await Task.Delay(TimeSpan.FromMinutes(intervaloMinutos), stoppingToken);
        }
    }
}
