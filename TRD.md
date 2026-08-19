# Technical Requirements Document — Smart Parking Navigator

| Field         | Value                               |
|---------------|-------------------------------------|
| Document ID   | TRD-001                             |
| Version       | 1.0.0                               |
| Status        | Draft                               |
| Author        | @copilot                            |
| Created       | 2026-08-19                          |
| Last Updated  | 2026-08-19                          |
| Milestone     | Workshop MVP (step 02)              |
| References    | PRD-001, IDEATION.md                |

---

## 1. Architecture Overview

The solution uses .NET Aspire and consists of four projects:

| Project | Role |
|---------|------|
| **AppHost** (`CarparkAvailability.AppHost`) | Composes and launches all services; injects secrets from user-secrets into each service. |
| **ServiceDefaults** (`CarparkAvailability.ServiceDefaults`) | Shared OpenTelemetry, health checks, and resilience defaults registered by both ApiApp and WebApp. |
| **ApiApp** (`CarparkAvailability.ApiApp`) | ASP.NET Core Minimal API; owns all data-gov.sg communication, CSV parsing, coordinate conversion, and distance calculation. Exposes a JSON API consumed by WebApp. |
| **WebApp** (`CarparkAvailability.WebApp`) | Blazor Server (or WebAssembly) front end; renders the Google Maps view, handles user interaction, and calls ApiApp. |

No new projects are added for the MVP.

---

## 2. Project Responsibilities

### 2.1 AppHost

- Declares `ApiApp` and `WebApp` as resources and wires them with Aspire service discovery.
- Injects `DataGovSg__ApiKey` into `ApiApp` only.
- Injects `GoogleMaps__ApiKey` into `WebApp` only.
- Does **not** contain application logic.

### 2.2 ServiceDefaults

- Configures OpenTelemetry tracing and metrics for all services.
- Registers default health-check endpoints (`/health`, `/alive`).
- Configures standard Polly resilience pipeline for outbound HTTP clients.
- Does **not** contain domain logic.

### 2.3 ApiApp

Owns every server-side concern:

| Responsibility | Detail |
|----------------|--------|
| CSV ingestion | Parse and validate `HDBCarparkInformation.csv` at startup. |
| Coordinate conversion | Convert SVY21 `(x_coord, y_coord)` to WGS84 `(latitude, longitude)`. |
| Live availability | Poll `data.gov.sg` `/v1/transport/carpark-availability`; hold last-known-good in memory. |
| Join | Merge CSV static data with live data on `car_park_no` / `carpark_number`. |
| Distance | Calculate straight-line distance from a supplied WGS84 point to each car park. |
| API endpoints | Serve the merged dataset and individual car-park details to WebApp. |
| Secret boundary | The `DataGovSg__ApiKey` must never appear in any API response or structured log field. |

### 2.4 WebApp

Owns every client-facing concern:

| Responsibility | Detail |
|----------------|--------|
| Map rendering | Embed Google Maps JavaScript API using `GoogleMaps__ApiKey`. |
| Destination search | Use Places Autocomplete (New) scoped to Singapore (`componentRestrictions: { country: 'SG' }`). |
| ApiApp calls | Fetch car-park data from ApiApp via Aspire service discovery; handle errors gracefully. |
| Filters | Apply available-only, vehicle-type, night-parking, and car-park-type filters client-side on the last-received data. |
| UI states | Render loading, empty, stale, unavailable, and error states per PRD-001 section 5. |
| Secret boundary | The `GoogleMaps__ApiKey` must never appear in any server-rendered HTML attribute, JSON response from ApiApp, or server log. |

---

## 3. Data Layer

### 3.1 HDB CSV Ingestion

**File:** `data/HDBCarparkInformation.csv`

**Columns and types:**

| Column | CSV type | Parsed type | Notes |
|--------|----------|-------------|-------|
| `car_park_no` | string | `string` | Join key |
| `address` | string | `string` | |
| `x_coord` | string | `double` | SVY21 easting; convert to WGS84 |
| `y_coord` | string | `double` | SVY21 northing; convert to WGS84 |
| `car_park_type` | string | `string` | `SURFACE CAR PARK`, `MULTI-STOREY CAR PARK`, `BASEMENT CAR PARK`, etc. |
| `type_of_parking_system` | string | `string` | `ELECTRONIC PARKING`, `COUPON PARKING` |
| `short_term_parking` | string | `string` | Display as-is |
| `free_parking` | string | `string` | Display as-is; do **not** parse time ranges |
| `night_parking` | string | `bool` | `YES` → `true`; any other value → `false` |
| `car_park_decks` | string | `int` | Parse safely; 0 is valid |
| `gantry_height` | string | `double` | Metres; 0 = no restriction or not applicable |
| `car_park_basement` | string | `bool` | `Y` → `true`; any other value → `false` |

**Validation:**

- Rows where `car_park_no` is empty or whitespace are discarded and counted in a startup warning log.
- Rows where `x_coord` or `y_coord` cannot be parsed as a finite double are discarded with a warning log.
- Duplicate `car_park_no` values: the first occurrence wins; subsequent duplicates are logged as warnings.
- A structured log entry at startup reports total rows read, rows discarded, and final record count.

### 3.2 SVY21-to-WGS84 Conversion

SVY21 is a Transverse Mercator projection used in Singapore. The conversion to WGS84 latitude/longitude uses the following constants:

| Constant | Value |
|----------|-------|
| Semi-major axis `a` | 6378137.0 m |
| Flattening `f` | 1 / 298.257223563 |
| Origin latitude `φ₀` | 1.366666° N |
| Origin longitude `λ₀` | 103.833333° E |
| False easting `FE` | 28001.642 m |
| False northing `FN` | 38744.572 m |
| Scale factor `k₀` | 1.0 |

The inverse Transverse Mercator algorithm (Bowring or Karney) converts `(E, N)` SVY21 metres to `(latitude, longitude)` in decimal degrees (WGS84).

Implementation requirements:

- The conversion is a pure, stateless function that takes `(double easting, double northing)` and returns `(double latitude, double longitude)`.
- No external NuGet packages are required; implement the standard series expansion (6-term).
- Unit tests must verify at least three known SVY21 → WGS84 pairs from the Singapore Land Authority reference dataset to within ±0.000005°.

### 3.3 Live Availability Polling

**Endpoint:** `GET https://api.data.gov.sg/v1/transport/carpark-availability`

**Authentication:** `Authorization: ****** header.

**Response schema (observed):**

```json
{
  "items": [
    {
      "timestamp": "<ISO 8601 SST>",
      "carpark_data": [
        {
          "carpark_number": "<string>",
          "update_datetime": "<ISO 8601 local, no offset>",
          "carpark_info": [
            {
              "lot_type": "C" | "Y" | "H",
              "total_lots": "<numeric string>",
              "lots_available": "<numeric string>"
            }
          ]
        }
      ]
    }
  ]
}
```

**Parsing rules:**

- `total_lots` and `lots_available` are numeric strings; parse to `int` safely. A parse failure for a single lot entry is logged as a warning; that entry is skipped.
- `update_datetime` has no UTC offset; interpret as SST (UTC+8).
- `lot_type` values other than `C`, `Y`, `H` are preserved as-is to allow forward compatibility.
- Unknown additional JSON fields are ignored (forward-compatible schema).

**Polling behaviour:**

- ApiApp polls the endpoint every **60 seconds** using a background `IHostedService`.
- The last successful response payload is retained in memory as `LastKnownGood` together with a `FetchedAt` timestamp (UTC).
- If a poll fails, the `LastKnownGood` data is retained and a structured error log entry is written (no stack trace in the log field; stack trace goes to the exception log).
- The `DataGovSg__ApiKey` must not appear in any log field or HTTP response body.

### 3.4 Data Join

The join is performed by ApiApp at query time (not at startup):

1. For each car park in the live data, look up the static record by `carpark_number` = `car_park_no`.
2. If a static record is not found, include the car park with static fields as `null` and flag `StaticDataAvailable = false`.
3. If a static record is found but the car park is absent from the live data, include the car park with live fields as `null` and flag `LiveDataAvailable = false`.
4. WGS84 coordinates come exclusively from the CSV (after SVY21 conversion); car parks without CSV coordinates are excluded from distance-based queries.

---

## 4. Distance Calculation

- Input: destination `(lat, lng)` in WGS84 decimal degrees; car-park `(lat, lng)` in WGS84 decimal degrees.
- Algorithm: **Haversine formula** with Earth radius = **6371000 m**.
- Output: distance in metres as a `double`, rounded to the nearest integer for display.
- The 500 m radius filter is applied server-side before returning results to WebApp.
- Distance calculation is a pure, stateless function covered by unit tests.

---

## 5. API Boundaries

### 5.1 Endpoint: `GET /api/carparks`

Returns car parks within a specified radius of a destination.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `lat` | `double` | Yes | Destination latitude (WGS84) |
| `lng` | `double` | Yes | Destination longitude (WGS84) |
| `radius` | `int` | No | Radius in metres; default 500; max 2000 |

**Response (200 OK):**

```json
{
  "fetchedAt": "<ISO 8601 UTC>",
  "items": [
    {
      "carparkNumber": "ACB",
      "address": "BLK 270/271 ALBERT CENTRE BASEMENT CAR PARK",
      "latitude": 1.30283,
      "longitude": 103.84567,
      "distanceMetres": 312,
      "carparkType": "BASEMENT CAR PARK",
      "typeOfParkingSystem": "ELECTRONIC PARKING",
      "shortTermParking": "WHOLE DAY",
      "freeParking": "NO",
      "nightParking": true,
      "carparkDecks": 1,
      "gantryHeight": 1.8,
      "carparkBasement": true,
      "staticDataAvailable": true,
      "liveDataAvailable": true,
      "updateDatetime": "<ISO 8601 SST>",
      "lots": [
        {
          "lotType": "C",
          "totalLots": 105,
          "lotsAvailable": 30
        }
      ]
    }
  ]
}
```

**Error responses:**

| Status | Condition |
|--------|-----------|
| 400 | `lat` or `lng` missing, out of Singapore bounding box (`lat` 1.1–1.5, `lng` 103.6–104.1), or `radius` > 2000. |
| 503 | No live data has ever been successfully fetched (service unavailable). |

### 5.2 Endpoint: `GET /api/carparks/{carparkNumber}`

Returns full details and live availability for a single car park.

**Response (200 OK):** Same shape as an individual item in `GET /api/carparks`.

**Error responses:**

| Status | Condition |
|--------|-----------|
| 404 | Car park number not found in CSV or live data. |

---

## 6. Google Maps Integration

- The Google Maps JavaScript API is loaded client-side in WebApp using the `GoogleMaps__ApiKey` environment variable rendered into the page at server render time (Blazor Server) or via a Razor page / `_Host.cshtml` include.
- **Places Autocomplete (New)** restricts results to Singapore (`componentRestrictions: { country: 'SG' }`).
- Map markers are placed at the WGS84 coordinates returned by ApiApp.
- The `GoogleMaps__ApiKey` value must not appear in any HTTP response that ApiApp sends; it is used exclusively in the WebApp rendering pipeline.
- The key is read from `GoogleMaps__ApiKey` environment variable; hardcoded fallbacks are not permitted.

---

## 7. Security Requirements

| Requirement | Detail |
|-------------|--------|
| Key boundary | `DataGovSg__ApiKey` is injected by AppHost into ApiApp only; `GoogleMaps__ApiKey` is injected into WebApp only. |
| No key logging | Neither key may appear in any log field, response body, or HTTP header sent to a client. |
| No key in source | Neither key is committed to the repository; both are managed via `dotnet user-secrets`. |
| Input validation | `lat`, `lng`, and `radius` query parameters are validated against Singapore bounding box and max-radius constraints; invalid input returns 400. |
| HTTP-only HTTPS | Local development uses HTTPS as configured by Aspire launch profiles. |

---

## 8. Error Handling

| Scenario | ApiApp Behaviour | WebApp Behaviour |
|----------|-----------------|-----------------|
| data.gov.sg poll fails | Log structured error; retain `LastKnownGood`; continue polling. | Show error banner; display last-known-good data with "As of \<time\>" notice. |
| No successful poll yet | Return HTTP 503 from `/api/carparks`. | Show "Service temporarily unavailable" error state. |
| CSV parse error (row) | Log warning per discarded row; continue loading remaining rows. | Not surfaced to user. |
| car park missing from CSV | Include in response with `staticDataAvailable: false`. | Show "Details unavailable" for static fields; show live lots if available. |
| car park missing from live data | Include in response with `liveDataAvailable: false`, `lots: []`. | Show "Availability unavailable" for lot fields. |
| Stale data (`update_datetime` > 5 min) | Include `updateDatetime` in response; no special flag. | Show staleness warning on the affected car park. |
| ApiApp unreachable from WebApp | N/A | Show error banner; do not crash the page. |

---

## 9. Observability

- OpenTelemetry traces and metrics are configured in ServiceDefaults.
- Structured log events (using `ILogger<T>`) are required for:
  - CSV startup summary (total, discarded, final count)
  - Each data.gov.sg poll result (success with record count; failure with error type)
  - Each 400 or 503 response with the rejection reason
- The Aspire dashboard (available at `http://localhost:15888` by default) is the primary observability surface for workshop participants.

---

## 10. Accessibility

- All interactive map controls and filter inputs must have accessible names (`aria-label` or associated `<label>`).
- The result list is a semantic `<ul>` or `<table>` with appropriate ARIA roles.
- Colour alone must not convey state (stale, error, unavailable) — add an icon or text label.
- Keyboard navigation must allow the user to reach every interactive element without a mouse.

---

## 11. Testing Requirements

### 11.1 Unit Tests

Covered in a dedicated test project (e.g., `CarparkAvailability.Tests`):

| Test area | Minimum cases |
|-----------|---------------|
| SVY21-to-WGS84 conversion | ≥3 Singapore Land Authority reference pairs; accuracy ±0.000005° |
| Haversine distance calculation | Known point pairs including 0 m (same point) and cross-equator edge cases |
| CSV parsing | Valid row, row with missing `car_park_no`, row with unparseable coordinates, duplicate `car_park_no` |
| Numeric string parsing (`total_lots`, `lots_available`) | Valid integer string, non-numeric string, empty string |
| `night_parking` parsing | `YES` → `true`, `NO` → `false`, unexpected string → `false` |
| `car_park_basement` parsing | `Y` → `true`, `N` → `false`, unexpected → `false` |
| Data join | Matched pair, CSV-only record, live-only record |
| 500 m radius filter | Car park inside radius returned; car park outside not returned |

### 11.2 Integration / Contract Tests

- A contract test fetches a live response from data.gov.sg and validates it against the observed schema (see section 3.3).
- Required fields `carpark_number`, `update_datetime`, and `carpark_info[].lot_type` must be present.
- Numeric string values must be parseable.
- The test is marked `[Category("Contract")]` or equivalent so it can be skipped in CI without a live API key.

### 11.3 API Tests (ApiApp)

- `GET /api/carparks` with valid `lat`/`lng` returns 200 and at least one item when seeded with known data.
- `GET /api/carparks` with `lat`/`lng` outside Singapore bounding box returns 400.
- `GET /api/carparks` with `radius` > 2000 returns 400.
- `GET /api/carparks/{carparkNumber}` with a known number returns 200.
- `GET /api/carparks/{carparkNumber}` with an unknown number returns 404.
- When no live data is available, `GET /api/carparks` returns 503.

---

## 12. Out of Scope

The following are explicitly excluded from TRD-001:

- Persistent database or caching layer
- User accounts, authentication, favourites, or alerts
- Occupancy forecasting or historical data collection
- Traffic or weather API integration
- Deployment pipeline or cloud infrastructure
- MCP integration
- Agentic AI features (deferred to step 05)
- Parsing the semantic meaning of `free_parking` or `short_term_parking` time strings

---

## Revision History

| Version | Date       | Author   | Changes          |
|---------|------------|----------|------------------|
| 1.0.0   | 2026-08-19 | @copilot | Initial draft    |
