using System.Text.Json.Serialization;
using OrderProcessing;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter<OrderStatus>(allowIntegerValues: false)));
builder.Services.AddSingleton<OrderStore>();
builder.Services.AddHostedService<ProcessingWorker>();
var app = builder.Build();

app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (OrderException error)
    {
        await Results.Problem(statusCode: error.StatusCode, title: error.Message).ExecuteAsync(context);
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapPost("/orders", (CreateOrder request, OrderStore store) =>
{
    var order = store.Create(request);
    return Results.Created($"/orders/{order.Id}", order);
});
app.MapGet("/orders/{id:guid}", (Guid id, OrderStore store) => store.Get(id));
app.MapGet("/orders", (string? status, int? offset, int? limit, OrderStore store) =>
{
    OrderStatus? filter = null;
    if (status is not null)
    {
        if (!Enum.GetNames<OrderStatus>().Contains(status))
            throw new OrderException(400, "Unknown status. Use an uppercase status name.");
        filter = Enum.Parse<OrderStatus>(status);
    }
    var skip = offset ?? 0;
    var take = limit ?? 50;
    if (skip < 0 || take is < 1 or > 100)
        throw new OrderException(400, "offset must be nonnegative; limit must be between 1 and 100.");
    return store.List(filter, skip, take);
});
app.MapPatch("/orders/{id:guid}/status", (Guid id, ChangeStatus request, OrderStore store) =>
    store.Change(id, request.Status ?? throw new OrderException(400, "status is required.")));
app.MapPost("/orders/{id:guid}/cancel", (Guid id, OrderStore store) => store.Change(id, OrderStatus.CANCELLED));
app.Run();
