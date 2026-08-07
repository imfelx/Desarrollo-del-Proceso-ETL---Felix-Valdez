using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaVentas.ETL.Domain.Entities;
using SistemaVentas.ETL.Domain.Interfaces;

namespace SistemaVentas.ETL.Infrastructure.Extractors;


/// Extrae registros de Análisis de ventas desde una API pública de datos.


public class AnalisisVentasApiExtractor : IExtractor<Venta>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnalisisVentasApiExtractor> _logger;

    public string NombreFuente => "API REST - JSONPlaceholder";

    public AnalisisVentasApiExtractor(
        IHttpClientFactory httpClientFactory,
        ILogger<AnalisisVentasApiExtractor> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AnalisisVentasApi");
        _logger = logger;
    }

    public async Task<IReadOnlyList<Venta>> ExtractAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("https://jsonplaceholder.typicode.com/posts?_limit=10", cancellationToken);
            response.EnsureSuccessStatusCode();

            var posts = await response.Content.ReadFromJsonAsync<List<PostDto>>(cancellationToken: cancellationToken);

            if (posts is null || posts.Count == 0)
            {
                _logger.LogWarning("La API REST no devolvió registros.");
                return Array.Empty<Venta>();
            }

            var ventas = posts.Select((post, index) => new Venta
            {
                NumeroFactura = $"FAC-{post.Id:D4}",
                FechaVenta = DateTime.Now.AddDays(-index),
                ClienteID = post.UserId.ToString(),
                ProductoID = post.Id.ToString(),
                Cantidad = 1,
                PrecioUnitario = 100.00m + (post.Id * 5.50m),
                Descuento = 0m,
                MontoTotal = 100.00m + (post.Id * 5.50m)
            }).ToList();

            _logger.LogInformation("Se extrajeron {Cantidad} registros desde JSONPlaceholder.", ventas.Count);
            return ventas;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Tiempo de espera agotado al consumir la API REST. Se devolverá una colección vacía.");
            return Array.Empty<Venta>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No fue posible consumir la API REST. Se devolverá una colección vacía.");
            return Array.Empty<Venta>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al extraer datos desde la API REST.");
            return Array.Empty<Venta>();
        }
    }

    private sealed class PostDto
    {
        public int UserId { get; set; }
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}
