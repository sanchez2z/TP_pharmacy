namespace PharmacyReports;

public record SaleRecord(
    string ProductName,
    string Category,
    int Quantity,
    decimal TotalCost);

public static class SalesReportCalculator
{
    public static decimal CalculateRevenue(
        IEnumerable<SaleRecord> sales)
    {
        return sales.Sum(s => s.TotalCost);
    }

    public static int CalculateSoldItems(
        IEnumerable<SaleRecord> sales)
    {
        return sales.Sum(s => s.Quantity);
    }

    public static decimal CalculateAverageCheck(
        IEnumerable<SaleRecord> sales)
    {
        var list = sales.ToList();

        if (list.Count == 0)
            return 0;

        return list.Average(s => s.TotalCost);
    }

    public static SaleRecord? FindBestSellingProduct(
        IEnumerable<SaleRecord> sales)
    {
        return sales
            .GroupBy(s => s.ProductName)
            .Select(group => new SaleRecord(
                group.Key,
                group.First().Category,
                group.Sum(s => s.Quantity),
                group.Sum(s => s.TotalCost)))
            .OrderByDescending(s => s.Quantity)
            .FirstOrDefault();
    }
}