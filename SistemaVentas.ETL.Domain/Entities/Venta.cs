namespace SistemaVentas.ETL.Domain.Entities;


/// Representa una línea de venta extraída desde el origen local de ventas.
/// Corresponde al staging previo de FactVentas.

public class Venta
{
    public string NumeroFactura { get; set; } = string.Empty;
    public DateTime FechaVenta { get; set; }
    public string ClienteID { get; set; } = string.Empty;
    public string ProductoID { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public decimal MontoTotal { get; set; }
}
