# AGENTS.md

## Project overview (starter state)

- This repository is a prepared **.NET 10** workshop starter for Smart Parking Navigator.
- Solution: `CarparkAvailability.slnx`
- Projects under `src/`:
  - `CarparkAvailability.ApiApp`: ASP.NET Core API starter (`/api` endpoint, OpenAPI in Development).
  - `CarparkAvailability.WebApp`: Blazor Web App (Interactive Server Components).
  - `CarparkAvailability.AppHost`: .NET Aspire AppHost that wires WebApp + ApiApp and passes secrets via environment variables.
  - `CarparkAvailability.ServiceDefaults`: shared defaults for service discovery, resilience, OpenTelemetry, and health endpoints.
- Prepared assets:
  - `data/`: HDB CSV and car-park-availability API contract/sample files.
  - `docs/`: setup, API-key guides, and workshop step-by-step instructions.

## Technology stack

- .NET SDK: `10.0.100` (`global.json`)
- Target framework: `net10.0` (`Directory.Build.props`)
- Aspire AppHost SDK: `13.4.6` (`src/CarparkAvailability.AppHost/CarparkAvailability.AppHost.csproj`)
- ASP.NET Core + Blazor + OpenAPI packages are managed centrally in `Directory.Packages.props`.

## Verified commands

Run from repository root:

```bash
dotnet restore CarparkAvailability.slnx
dotnet build CarparkAvailability.slnx --no-restore --configuration Release
dotnet build CarparkAvailability.slnx
aspire run
```

Validation/tests (when test projects exist):

```bash
dotnet test CarparkAvailability.slnx --no-build
```

Linting: there is currently no dedicated lint command configured in this starter.

## Security and architecture guardrails

- **Never commit credentials** (Google Maps key, data.gov.sg key, tokens, secrets).
- Keep API keys in user secrets as documented in `docs/google-maps-api-key.md` and `docs/data-gov-sg-api-key.md`.
- **Do not call `api.data.gov.sg` directly from the browser/WebApp.** data.gov.sg access must stay server-side in ApiApp.
- Keep Aspire project boundaries intact: WebApp -> ApiApp for parking data, and AppHost for composition/configuration.

## Testing and documentation expectations

- Do not invent behavior beyond the current starter and issue scope.
- For implementation work, ensure code, tests, and docs align with the repository’s eventual `PRD.md` and `TRD.md`.
- Update relevant docs in `docs/` whenever setup, behavior, or boundaries change.

## Commit and pull request guidance

- Keep commits focused and coherent.
- Use Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`).
- In PRs, link the issue, summarize validated commands run, and call out any doc updates.
