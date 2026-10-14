# Order saga sample

One ASP.NET Core process runs an order saga and a static page. Kafka delivers the commands and the facts. Cosmos DB stores the saga document. This is feasible on MassTransit 8.5.11, and it is not the layout MassTransit recommends.

MassTransit expects a broker such as RabbitMQ, Azure Service Bus, or Amazon SQS to own the saga, with Kafka only at the edge, and its transactional outbox in SQL or MongoDB. This sample keeps the saga on the Kafka rider. The in-memory bus exists only because the rider requires a bus. It has no receive endpoints. The state machine does not produce to Kafka.

## What is atomic

The saga document is atomic. The first event inserts it with `CreateItem`. Later transitions replace it with `ReplaceItem` and `If-Match`. The same write stores `pendingFacts`, so the new state and the outbound intent commit together. A stale ETag is HTTP 412.

The Kafka acknowledgement is a separate step. After the document commits, `PendingFactDispatcher` produces each fact and then removes it with a conditional patch. If the process dies between those two steps, the next pass produces the fact again. The page drops a second record with a `MessageId` it already stored.

Run one host. A second dispatcher can produce the same fact before either patch lands. Do not leave `dotnet run` up while the smoke tests run: both use the consumer group `order-saga` and port 5080.

## Run

Start the emulator and Kafka:

```bash
docker compose up -d
```

The emulator must be HTTPS. Wait until `https://localhost:8081/` answers. Data Explorer is `http://localhost:1234`. The account key in `appsettings.json` is the public emulator key. It is not a secret.

```bash
dotnet run --project src/OrderSaga.Host
```

Open `http://localhost:5080`. The page reads saga state from Cosmos. Creating an order and changing its state only produce Kafka records. Sent and received events are rebuilt from Kafka.

## Tests

Unit tests do not need Docker:

```bash
dotnet test tests/OrderSaga.Tests
```

Smoke tests need the compose stack, and they need the host to be stopped:

```bash
dotnet build tests/OrderSaga.SmokeTests
pwsh tests/OrderSaga.SmokeTests/bin/Debug/net8.0/playwright.ps1 install chromium
dotnet test tests/OrderSaga.SmokeTests
```

If Kafka or the emulator is down, the smoke tests skip. `dotnet test` on the solution runs both projects.

## Add an event

Add the contract and a topic name, one entry in the endpoint list in `OrderSagaHost`, one `During` clause that appends a `PendingFact`, and one arm in `OrderFactProducer`. If the page may send it, add it to `OrderFlow`.
