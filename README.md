# Payment Gateway – take-home submission

An ASP.NET Core 8 API that lets a merchant take a card payment and look it up afterwards. The gateway validates the request, forwards it to the acquiring bank (a Mountebank simulator in this exercise), stores the outcome in memory and returns it.

## Running it

You need the .NET 8 SDK and Docker.

```bash
docker-compose up -d                          # bank simulator on http://localhost:8080
dotnet run --project src/PaymentGateway.Api   # gateway on http://localhost:5067
dotnet test                                   # all 57 tests (needs the simulator running)
dotnet test --filter Category!=Integration    # 53 tests, no Docker required
```

Swagger UI is at `http://localhost:5067/swagger` when running in Development.

## API

### `POST /api/payments`

```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 4,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 1050,
  "cvv": "123"
}
```

| Outcome | HTTP | `status` | Stored? |
|---|---|---|---|
| Bank authorised | 200 | `Authorized` | yes |
| Bank declined | 200 | `Declined` | yes |
| Request failed validation | 400 | `Rejected` + list of errors | no |
| Bank unreachable, timed out or returned 503 | 503 | – (RFC 7807 problem details) | no |

The bank simulator authorises card numbers ending in an odd digit, declines even digits, and returns 503 for numbers ending in `0`.

### `GET /api/payments/{id}`

Returns the stored payment 200 or 404. The response never includes the full card number or CVV – only the last four digits.

## Validation rules

Implemented with FluentValidation in `PostPaymentRequestValidator`, exactly as listed in the brief:

- **Card number** – required, 14–19 characters, digits only. Spaces and dashes are rejected rather than stripped; I'd rather the merchant sends a clean value than guess at formatting. With more time I can add supports for any other possible entry.
- **Expiry month** – required, 1–12.
- **Expiry year** – required. The month/year *combination* must not be in the past. A card is treated as valid up to and including the last day of its expiry month, which is how card schemes work.
- **Currency** – required, exactly 3 characters, one of `GBP`, `USD`, `EUR`. Matching is case-insensitive and the value is normalised to upper case before it reaches the bank or the store.
- **Amount** – required, integer, greater than zero. Minor units as per the brief (`1050` = £10.50). Zero is rejected because a zero-value payment has no meaning here.
- **CVV** – required, 3–4 characters, digits only.

Each field reports only its first failing rule (`CascadeMode.Stop`), so a merchant sees one clear message per field rather than a cascade.

## Design decisions

**Validation happens in the controller, not the service.** A `Rejected` payment is a request that never became a payment. It has no id, is never sent to the bank and is never stored. Keeping that decision at the edge means `PaymentService` only ever deals with well-formed payments.

**`Declined` and `Rejected` are different things.** `Rejected` means the gateway refused the request; `Declined` means the bank looked at a valid request and said no. A merchant reacts differently to each, so a declined payment is a real, retrievable payment with a 200 response.

**A bank outage is neither `Authorized` nor `Declined`.** If the bank returns 503, times out or can't be reached, the merchant hasn't received a decision. Mapping that to `Declined` would tell them the customer's card was refused when nobody looked at it. The gateway returns 503 with a problem-details body, stores nothing, and the merchant can retry.

**Request fields are nullable.** `PostPaymentRequest` uses `int?` and `string?` so that a missing field produces "Amount is required" rather than a misleading "Amount must be greater than 0" from a defaulted zero. The cost is a handful of `!` operators in the service, which are safe because the validator has already run. All "required" checks live in the validator so every validation failure comes back in the same shape.

**Card number and CVV never leave the service.** The response and repository models don't have those properties, so it's enforced by the type system rather than by remembering to omit them. `CardNumberLastFour` is a string so a card ending in `0042` isn't stored as `42`.

**Time is injected.** The validator takes a `TimeProvider` so expiry rules are tested against a fixed date and won't start failing when the calendar moves on.

**The bank client owns HTTP concerns.** `BankClient` translates the bank's HTTP responses into either a `BankResponse` or a typed exception. The rest of the code never sees status codes. A caller-initiated cancellation is deliberately *not* reported as a bank failure.

**Three currencies.** The brief asks for no more than three; I went with `GBP`, `USD`, `EUR`.

## Tests

57 tests across five classes. All but the last run without Docker:

- `PostPaymentRequestValidatorTests` – one theory per field covering required/boundary/format cases, plus the expiry rule evaluated from several different "today" dates.
- `PaymentServiceTests` – bank and repository mocked; checks the mapping to the bank's contract (`MM/YYYY`, upper-case currency, CVV leading zero preserved), the status mapping, and that nothing is stored when the bank fails.
- `BankClientTests` – stubbed `HttpMessageHandler`; checks each bank status code, timeout, and connection failure.
- `PaymentsControllerTests` – full pipeline through `WebApplicationFactory` with only the bank swapped for a mock; one test per HTTP outcome plus a POST → GET round trip.
- `PaymentsIntegrationTests` – the same pipeline with nothing mocked, talking to the real Mountebank simulator from `docker-compose.yml`. Covers authorised, declined, bank 503 and rejected. Tagged `Category=Integration`; these fail with a connection error if the simulator isn't up.

## Assumptions

- Amounts are already in the currency's minor unit; the gateway doesn't convert.
- One bank, one endpoint, configured under `BankSimulator:BaseUrl` in `appsettings.json`.
- No authentication or merchant identity – out of scope for the brief.
- A single process. The in-memory repository is a `List<T>` registered as a singleton; it's not thread-safe and wouldn't survive a restart, but it's a stand-in for a database and the interface is what the rest of the code depends on.

## What I'd do next

- Replace the in-memory store with a real one behind `IPaymentsRepository`.
- Idempotency keys on `POST /api/payments` so a merchant retrying after a 503 doesn't double-charge.
- Retry with backoff on transient bank failures, with a circuit breaker.
- Structured logging with a correlation id, and metrics on bank latency and decline rate.
- Merchant authentication and scoping `GET` to the merchant that created the payment.
