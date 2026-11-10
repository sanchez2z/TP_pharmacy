namespace TP_pharmacy.Models;

// Exact batches reserved at checkout; cancellation must return those same batches.
public class OrderStock
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int StockId { get; set; }
    public Stock Stock { get; set; } = null!;
    public int Quantity { get; set; }
}
