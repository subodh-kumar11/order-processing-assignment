# Order Processing API

A take-home backend implemented in C# / ASP.NET Core (.NET 10). It creates multi-item orders, retrieves and lists them, enforces status transitions, cancels pending orders, and moves pending orders to processing every five minutes.

## Windows quick start and walkthrough

```powershell
.\scripts\Run.ps1
```

In a second terminal, run `.\scripts\Demo.ps1` to demonstrate the API, or `.\scripts\Test.ps1` to build and run the automated tests. API health: http://localhost:5080/health. The backend has no graphical frontend.

- [Job-description skills mapping](docs/JD_MAPPING.md)
- [Architecture and interview walkthrough](docs/ARCHITECTURE.md)
- [GitHub, cloning and publishing guide](docs/GITHUB.md)
- [AI assistance disclosure](AI_USAGE.md)
- [Verification record](VERIFICATION.md)

## Run

Prerequisite: .NET 10 SDK. No third-party runtime packages or database installation are required.

```sh
dotnet run --urls http://localhost:5080
```

The server writes to `data/orders.json`. Restarting preserves orders. Configuration uses normal ASP.NET Core configuration sources, including environment variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `Orders__DataPath` | `data/orders.json` | Persistent data file |
| `Orders__ProcessingIntervalSeconds` | `300` | Background job interval; shorten only for testing |
| `ASPNETCORE_URLS` | Framework default | HTTP listening address |

The first processing tick happens five minutes after startup, then every five minutes. Each tick processes **all orders currently PENDING**, including recently created orders. It does not wait for each individual order to become five minutes old. Downtime is not replayed; pending orders are handled on the next tick after restart.

## API

All request and response bodies use JSON. IDs are UUIDs. Dates use UTC ISO 8601. Monetary values use decimal arithmetic, a single assumed currency, and at most two decimal places. `CANCELLED` is the additional terminal state required for cancellation.

| Method | Path | Result |
| --- | --- | --- |
| POST | `/orders` | Create an order; 201 and Location header |
| GET | `/orders/{id}` | Retrieve an order; 200 or 404 |
| GET | `/orders?status=PENDING&offset=0&limit=50` | List orders with optional status and pagination |
| PATCH | `/orders/{id}/status` | Change status; 200, 400, 404 or 409 |
| POST | `/orders/{id}/cancel` | Cancel a pending order; 200, 404 or 409 |
| GET | `/health` | Liveness check |

Example creation (PowerShell users can use `curl.exe` or `Invoke-RestMethod`):

```sh
curl -i http://localhost:5080/orders \
  -H 'Content-Type: application/json' \
  -d '{"customerId":"customer-1","items":[{"productId":"book","quantity":2,"unitPrice":12.50},{"productId":"pen","quantity":3,"unitPrice":0.10}]}'
```

Example response (generated identifiers and timestamps vary):

```json
{
  "id": "9d4c8f01-d714-4be1-843b-06f496567b2e",
  "customerId": "customer-1",
  "items": [
    { "productId": "book", "quantity": 2, "unitPrice": 12.50 },
    { "productId": "pen", "quantity": 3, "unitPrice": 0.10 }
  ],
  "total": 25.30,
  "status": "PENDING",
  "createdAt": "2026-09-25T18:30:00+00:00",
  "updatedAt": "2026-09-25T18:30:00+00:00"
}
```

Use the returned ID:

```sh
curl http://localhost:5080/orders/ORDER_ID
curl 'http://localhost:5080/orders?status=PENDING&limit=10'
curl -X PATCH http://localhost:5080/orders/ORDER_ID/status \
  -H 'Content-Type: application/json' -d '{"status":"PROCESSING"}'
curl -X POST http://localhost:5080/orders/ANOTHER_PENDING_ORDER_ID/cancel
```

List responses are `{ "orders": [...], "total": 12, "offset": 0, "limit": 50 }`. `total` counts matching orders before pagination. Results sort newest first, then by ID for ties. `limit` is 1–100; `offset` is nonnegative. Status filters require uppercase names. Domain errors use Problem Details (`application/problem+json`); malformed JSON and model binding errors are framework-generated 400 responses.

## Business rules

```text
PENDING -> PROCESSING -> SHIPPED -> DELIVERED
   |
   +----> CANCELLED
```

- New orders always start PENDING; the caller cannot choose their ID, total or initial status.
- Only the transitions above are allowed. Attempts to skip or reverse states return 409.
- A retry requesting the existing state returns 200 without changing timestamps. A repeated cancellation of an already cancelled order is also a successful no-op.
- `customerId` and each `productId` must be nonblank and at most 100 characters. IDs are trimmed; product IDs are case-sensitive.
- Orders contain 1–100 items. Duplicate product IDs are rejected; combine their quantities.
- Quantity must be an integer from 1–10000. Price must be positive, no greater than 1,000,000, with at most two decimal places.
- Total is computed on the server using `decimal`, avoiding binary floating-point rounding errors.

## Design and tradeoffs

`Program.cs` defines HTTP endpoints, `OrderStore.cs` owns business rules and persistence, and `ProcessingWorker.cs` runs the cancellable timer. Records describe requests and responses.

A single lock serializes creation, status changes, cancellation, and scheduled processing. Each mutation builds a new snapshot, writes and flushes a temporary file in the same directory, then replaces the data file before publishing the new state in memory. Failure leaves the prior in-memory snapshot intact. A process-level exclusive lock file prevents two server instances from sharing this storage. Invalid persisted JSON fails startup rather than silently discarding orders. The worker logs storage errors and retries on the next tick.

This intentionally small, single-instance solution rewrites the complete dataset on each mutation and keeps it in memory. File replacement and power-loss durability depend on the filesystem; this is not a substitute for a transactional database. A larger deployment should use SQL transactions and conditional status updates (`WHERE status = 'PENDING'`), migrations, and an appropriately coordinated scheduler. Offset pagination can shift during concurrent inserts.

Authentication, customer ownership checks, payment, inventory, tax, shipping integrations, and a product catalog are outside the supplied requirements. This assignment accepts customer IDs and item prices from the caller; a production service would authenticate callers and fetch authoritative prices. POST creation is not idempotent; a production checkout should add an idempotency key. The health endpoint reports process liveness, not storage writability.

## Verification

Prerequisites: .NET 10 SDK and Node.js 24 (test runner only).

```sh
dotnet build -c Release
node --test tests/api.test.mjs
```

The tests start a real HTTP server on a temporary port with isolated storage. They cover multi-item creation, exact totals, retrieval, validation, missing orders, transitions, cancellation retries, filtering, pagination, concurrent conflicting changes, concurrent creates, restart persistence, and the actual scheduled worker with its interval shortened to one second. No production data is touched. The checked-in GitHub Actions workflow runs these commands on pushes and pull requests.

## Container

```sh
docker build -t order-processing .
docker volume create order-data
docker run --rm -p 5080:8080 -v order-data:/data order-processing
```

The container runs as the built-in non-root `app` user. A bind-mounted directory must grant that user write access. Docker instructions are supplied for convenience; see `VERIFICATION.md` for what was actually run.

## Submission

Repository: https://github.com/subodh-kumar11/order-processing-assignment

Review AI_USAGE.md and add your own explanation of what you reviewed and learned. The repository is private; grant your interviewer access before submitting the URL. See [the GitHub guide](docs/GITHUB.md) for cloning, running and publishing further changes.
