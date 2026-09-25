using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderProcessing;

// One process owns the file for its lifetime. All writes persist before becoming visible.
public sealed class OrderStore : IDisposable
{
    private readonly object gate = new();
    private readonly string path;
    private readonly FileStream ownership;
    private Dictionary<Guid, Order> orders;
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<OrderStatus>(allowIntegerValues: false) }
    };

    public OrderStore(IConfiguration configuration)
    {
        path = Path.GetFullPath(configuration["Orders:DataPath"] ?? "data/orders.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ownership = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            orders = File.Exists(path)
                ? (JsonSerializer.Deserialize<Order[]>(File.ReadAllText(path), Json)
                    ?? throw new InvalidDataException("Order data is null.")).ToDictionary(o => o.Id)
                : new();
        }
        catch { ownership.Dispose(); throw; }
    }

    public Order Create(CreateOrder request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId) || request.CustomerId.Length > 100)
            throw new OrderException(400, "customerId is required and must be at most 100 characters.");
        if (request.Items is null || request.Items.Length is < 1 or > 100)
            throw new OrderException(400, "An order must contain 1 to 100 items.");
        var items = request.Items.Select(item =>
        {
            if (item is null || string.IsNullOrWhiteSpace(item.ProductId) || item.ProductId.Length > 100)
                throw new OrderException(400, "Each item needs a productId of at most 100 characters.");
            if (item.Quantity is < 1 or > 10000)
                throw new OrderException(400, "quantity must be between 1 and 10000.");
            if (item.UnitPrice <= 0 || item.UnitPrice > 1_000_000 || decimal.Round(item.UnitPrice, 2) != item.UnitPrice)
                throw new OrderException(400, "unitPrice must be positive, at most 1000000, with at most two decimal places.");
            return new OrderItem(item.ProductId.Trim(), item.Quantity, item.UnitPrice);
        }).ToArray();
        if (items.Select(i => i.ProductId).Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new OrderException(400, "Duplicate productId values are not allowed; combine their quantities.");
        var now = DateTimeOffset.UtcNow;
        var order = new Order(Guid.NewGuid(), request.CustomerId.Trim(), items,
            items.Sum(i => i.Quantity * i.UnitPrice), OrderStatus.PENDING, now, now);
        lock (gate)
        {
            var next = new Dictionary<Guid, Order>(orders) { [order.Id] = order };
            Commit(next);
        }
        return Copy(order);
    }

    public Order Get(Guid id)
    {
        lock (gate) return Copy(Find(id));
    }

    public OrderPage List(OrderStatus? status, int offset, int limit)
    {
        lock (gate)
        {
            var matches = orders.Values.Where(o => status is null || o.Status == status)
                .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id).ToArray();
            return new(matches.Skip(offset).Take(limit).Select(Copy).ToArray(), matches.Length, offset, limit);
        }
    }

    public Order Change(Guid id, OrderStatus status)
    {
        lock (gate)
        {
            var current = Find(id);
            // Repeating a completed request is harmless, including cancellation retries.
            if (current.Status == status) return Copy(current);
            var allowed = (current.Status, status) switch
            {
                (OrderStatus.PENDING, OrderStatus.PROCESSING or OrderStatus.CANCELLED) => true,
                (OrderStatus.PROCESSING, OrderStatus.SHIPPED) => true,
                (OrderStatus.SHIPPED, OrderStatus.DELIVERED) => true,
                _ => false
            };
            if (!allowed) throw new OrderException(409, $"Cannot change {current.Status} to {status}.");
            var updated = current with { Status = status, UpdatedAt = DateTimeOffset.UtcNow };
            Commit(new Dictionary<Guid, Order>(orders) { [id] = updated });
            return Copy(updated);
        }
    }

    public int ProcessPending()
    {
        lock (gate)
        {
            var pending = orders.Values.Where(o => o.Status == OrderStatus.PENDING).ToArray();
            if (pending.Length == 0) return 0;
            var now = DateTimeOffset.UtcNow;
            var next = new Dictionary<Guid, Order>(orders);
            foreach (var order in pending)
                next[order.Id] = order with { Status = OrderStatus.PROCESSING, UpdatedAt = now };
            Commit(next);
            return pending.Length;
        }
    }

    private Order Find(Guid id) => orders.TryGetValue(id, out var order)
        ? order : throw new OrderException(404, "Order not found.");
    private static Order Copy(Order order) => order with { Items = order.Items.ToArray() };

    private void Commit(Dictionary<Guid, Order> next)
    {
        var temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, next.Values.ToArray(), Json);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
            orders = next;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Dispose() => ownership.Dispose();
}
