namespace OrderProcessing;

public enum OrderStatus { PENDING, PROCESSING, SHIPPED, DELIVERED, CANCELLED }
public sealed record CreateItem(string? ProductId, int Quantity, decimal UnitPrice);
public sealed record CreateOrder(string? CustomerId, CreateItem?[]? Items);
public sealed record ChangeStatus(OrderStatus? Status);
public sealed record OrderItem(string ProductId, int Quantity, decimal UnitPrice);
public sealed record Order(Guid Id, string CustomerId, OrderItem[] Items, decimal Total,
    OrderStatus Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record OrderPage(Order[] Orders, int Total, int Offset, int Limit);
public sealed class OrderException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
