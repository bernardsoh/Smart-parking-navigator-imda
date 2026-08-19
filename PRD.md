# Product Requirements Document — Smart Parking Navigator

| Field         | Value                               |
|---------------|-------------------------------------|
| Document ID   | PRD-001                             |
| Version       | 1.0.0                               |
| Status        | Draft                               |
| Author        | @copilot                            |
| Created       | 2026-08-19                          |
| Last Updated  | 2026-08-19                          |
| Milestone     | Workshop MVP (step 02)              |

---

## 1. Overview

Smart Parking Navigator helps drivers in Singapore find an available HDB car park near a searched destination. The app surfaces live lot availability, basic car-park details, and data-freshness indicators so that drivers can make an informed parking decision in seconds.

This document defines the MVP scope for a single workshop session. Advanced features — favorites, alerts, forecasting, accounts, traffic, weather, deployment, databases, and agentic AI — are explicitly deferred.

---

## 2. Target Users

| Persona | Description |
|---------|-------------|
| **Daily Commuter** | Drives to an HDB estate or town centre regularly; wants the quickest path to an open lot. |
| **Occasional Visitor** | Unfamiliar with the area; needs to discover car parks near a destination they searched by address. |
| **Shared-Car Driver** | Time-sensitive; needs to know whether a lot is still available before committing to a route. |

All users are assumed to be in Singapore, using a modern mobile or desktop browser with an active data connection.

---

## 3. Singapore Context

- All HDB car-park coordinates are published in the **SVY21** projected coordinate system and must be converted to **WGS84** (latitude/longitude) for map display.
- Live availability comes from the **data.gov.sg Carpark Availability API** (`/v1/transport/carpark-availability`), which is updated approximately every minute.
- Static car-park metadata (address, type, parking system, height limit, night parking, free-parking schedule, short-term parking hours) comes from the **HDB Carpark Information CSV**.
- Lot types in the live data: `C` = Car, `Y` = Motorcycle, `H` = Heavy Vehicle.
- Free-parking schedules are published as human-readable strings (e.g., `SUN & PH FR 7AM-10.30PM`) whose exact parsing rules are not documented; the MVP displays the raw string without programmatic time-range interpretation.
- Times from the API are in **Singapore Standard Time (SST, UTC+8)**.

---

## 4. User Journeys

### 4.1 Search for a Destination

1. User opens the app; a Google Maps view centred on Singapore is shown.
2. User types a destination in the search box (Places Autocomplete, Singapore scope).
3. Map pans and zooms to the destination.
4. Car parks within **500 metres** of the destination are retrieved and shown as markers.
5. A result list alongside the map shows each car park with its distance, available lots, and occupancy rate.

### 4.2 Apply Filters

1. User opens the filter panel.
2. User toggles one or more of: Available Lots Only, Vehicle Type (Car / Motorcycle / Heavy Vehicle), Night Parking, Car Park Type (Surface / Multi-storey / Basement).
3. Map markers and result list update immediately to reflect the active filters.

### 4.3 View Car Park Details

1. User clicks a marker or a result-list item.
2. A detail panel opens showing:
   - Address
   - Car park type and parking system
   - Short-term parking hours
   - Free-parking schedule (raw string)
   - Night parking (Yes / No)
   - Number of decks, gantry height
   - Whether underground
   - Available and total lots per lot type
   - Occupancy rate per lot type
   - Data update time (SST) and freshness indicator

### 4.4 Handle Stale or Unavailable Data

1. If the API response is older than **5 minutes**, a "Data may be outdated" warning is shown on each affected car park.
2. If the live API call fails, the last successfully retrieved data is displayed with an "As of \<time\>" notice and an error banner.
3. If no car parks are found within the search radius, a "No car parks found nearby" empty state is shown.

---

## 5. MVP Feature Scope

### P0 — Must Have (Workshop MVP)

| ID | Feature | Notes |
|----|---------|-------|
| F-01 | Destination search | Google Places Autocomplete, Singapore only |
| F-02 | HDB car parks within 500 m | Radius measured from the searched destination |
| F-03 | Available and total lots by lot type | C, Y, H where data exists |
| F-04 | Occupancy rate per lot type | `(total − available) / total × 100 %` |
| F-05 | Distance from destination | Straight-line, in metres |
| F-06 | Data update time and freshness state | SST; stale threshold = 5 minutes |
| F-07 | Available-only filter | Hide car parks with 0 available lots of any type |
| F-08 | Vehicle-type filter | Show only car parks with lots of the selected type |
| F-09 | Night-parking filter | Show only car parks where `night_parking = YES` |
| F-10 | Car-park-type filter | Surface / Multi-storey / Basement |
| F-11 | Car park detail panel | All static metadata + live availability |
| F-12 | Loading state | Spinner while data is being fetched |
| F-13 | Empty state | "No car parks found" message |
| F-14 | Stale state | Warning on car parks whose `update_datetime` is >5 min old |
| F-15 | Error state | Banner when live API call fails; display last-known-good data |
| F-16 | Unavailable state | Car parks present in CSV but absent from live API shown as "Availability unavailable" |

### Explicitly Deferred (Out of Scope for MVP)

- Favorites and saved searches
- Availability alerts / push notifications
- Occupancy forecasting and historical data
- User accounts and profiles
- Traffic and weather integration
- Deployment pipeline and cloud infrastructure
- Persistent database
- Model Context Protocol (MCP) integration
- Agentic AI features (introduced in step 05)
- Programmatic parsing of free-parking and short-term-parking time strings
- Vehicle-height-based filtering

---

## 6. Acceptance Criteria

Each criterion must be demonstrable in the workshop environment (`dotnet run` with Aspire AppHost).

| AC-ID | Criterion |
|-------|-----------|
| AC-01 | Searching "Bishan MRT" surfaces at least one HDB car park marker within 500 m on the map. |
| AC-02 | The result list shows distance (m), available lots, and occupancy (%) for each car park. |
| AC-03 | Enabling "Available Lots Only" removes all car parks with 0 available lots from the map and list. |
| AC-04 | Selecting "Motorcycle" in vehicle-type filter shows only car parks with `lot_type = Y` data. |
| AC-05 | Enabling "Night Parking" filter shows only car parks where `night_parking = YES`. |
| AC-06 | Selecting "Basement" type shows only car parks of type `BASEMENT CAR PARK`. |
| AC-07 | Clicking a car park marker opens the detail panel with address, static metadata, and live lot counts. |
| AC-08 | A car park whose `update_datetime` is more than 5 minutes ago displays a visible staleness warning. |
| AC-09 | When the live API is unreachable, the last-known-good data is shown with an "As of \<time\>" notice and an error banner; no unhandled exception is shown. |
| AC-10 | Searching an address with no HDB car parks within 500 m shows the "No car parks found nearby" empty state. |
| AC-11 | A car park present in the CSV but absent from the live API response shows "Availability unavailable" rather than a missing-data error. |
| AC-12 | The Google Maps API key is never included in any ApiApp response or server log. |
| AC-13 | The data.gov.sg API key is never rendered in any WebApp page source or browser network response. |

---

## 7. Non-Functional Requirements

| Category | Requirement |
|----------|-------------|
| **Performance** | Initial car-park list renders within 3 seconds on a local development machine with a live internet connection. |
| **Accessibility** | All interactive elements are keyboard-navigable and have visible focus indicators. |
| **Security** | Google Maps API key is supplied only to WebApp; data.gov.sg API key is supplied only to ApiApp. Neither key appears in logs or responses. |
| **Freshness** | The UI polls ApiApp for updated availability every 60 seconds while the page is in focus. |
| **Error visibility** | All error and stale states are surfaced to the user; no silent failures. |

---

## 8. Assumptions and Constraints

1. The Aspire scaffold (AppHost, ApiApp, WebApp, ServiceDefaults) is the fixed architecture; no new projects are added for the MVP.
2. Only HDB car parks are in scope; private and URA car parks are excluded.
3. The `car_park_no` (CSV) and `carpark_number` (API) fields are the join key; unmatched records are handled gracefully.
4. SVY21-to-WGS84 conversion is performed server-side in ApiApp.
5. No persistent data store is required for the MVP.
6. The app targets Singapore only; no internationalisation or multi-region support is required.

---

## Revision History

| Version | Date       | Author   | Changes          |
|---------|------------|----------|------------------|
| 1.0.0   | 2026-08-19 | @copilot | Initial draft    |
