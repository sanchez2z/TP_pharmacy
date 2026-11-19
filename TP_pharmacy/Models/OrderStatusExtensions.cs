namespace TP_pharmacy.Models;

public static class OrderStatusExtensions
{
    public static string ToRussian(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Новый",
        OrderStatus.Processing => "В обработке",
        OrderStatus.Ready => "Готов к выдаче",
        OrderStatus.Completed => "Завершён",
        OrderStatus.Cancelled => "Отменён",
        _ => "Неизвестный статус"
    };

    public static bool CanCancel(this OrderStatus status) =>
        status is OrderStatus.Pending or OrderStatus.Processing or OrderStatus.Ready;
}
