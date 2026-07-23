# DigiSign

DigiSign is a Windows Forms desktop app (.NET Framework 4.7.2) that digitally signs PDF invoices with a certificate (typically a USB hardware token / smart card), prints invoices and shipping labels, and can run as a background HTTP listener that an ERP system calls to fetch/sign/print documents and print raw ZPL labels. It's a single-EXE tool distributed to customer machines, not a web app or service with a database.

## Build & run

```
dotnet build DigiSign.csproj
```

Output goes to `..\..\Build\Digisign\` relative to the repo (e.g. `C:\Users\<user>\Build\Digisign`) — this is where `DigiSign.exe`, `appsettings.json`, `IP.xml`, `license.txt`, and `logs/` land during manual testing. There are no automated tests; verification is done by running the built EXE and inspecting `logs/application_log.txt` / `plf.txt`, or by exercising the HTTP listener routes below with `curl`/Postman.

`MockDocumentServer/` is a separate console project standing in for the real invoice provider API during local dev (`dotnet run` from that folder, listens on port 9091).

## Run modes

| Mode | Trigger | License required | Purpose |
|---|---|---|---|
| Admin | `/admin` | `admin.license` | Turn a customer's `license.key` into a `license.txt` |
| Settings | `/settings` | none | Edit `IP.xml` + `appsettings.json` |
| Listener | `/listen`, or no args when `EnableListenerMode` is `true` | none to start; checked per-request | Runs the HTTP listener (this document) + a tray icon |
| Batch signing | no args when `EnableListenerMode` is `false` (default), or any other arg | `license.txt` | Signs the PDFs listed in `IP.xml` via `BatchSigner` |

## Configuration

- **`IP.xml`** — legacy, positional config written by the ERP (Tally-style): input files, output folder, certificate CommonName, PIN, signature placement, sign-on-page mode.
- **`appsettings.json`** — listener-mode settings: `Port` (default `5000`), `InvoiceApiBaseUrl`, `InvoiceApiKey`, `PrinterName`, `EnableOcspCheck`/`OcspTimeoutSeconds`, `EnableListenerMode`, `UpdateCheckUrl` (blank = update checking disabled), etc. Auto-created/migrated on first run if missing.

Both are edited together via `DigiSign.exe /settings`.

## HTTP listener API

Base URL: `http://localhost:{port}/` (`Port` in `appsettings.json`, default `5000`). All responses are JSON. CORS is open (`Access-Control-Allow-Origin: *` unless an `Origin` header is echoed back) and `OPTIONS` preflight is handled generically for every route.

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/invoice` | Fetch an invoice PDF, no signing, no printing |
| `POST` | `/invoice-print` | Fetch + print, no signing |
| `POST` | `/invoice-sign` | Fetch + sign, no printing |
| `POST` | `/invoice-sign-print` | Fetch + sign + print |
| `POST` | `/label-print` | Print a raw ZPL label command directly to a printer |
| `GET` | `/label-printers` | List installed printer names |
| `GET` | `/heartbeat` | Confirm the listener process is alive |

### `POST /invoice`, `/invoice-print`, `/invoice-sign`, `/invoice-sign-print`

Batches one or more tokens for the same client; each token becomes its own tracked job (visible in the tray's Jobs window, with Resume/Cancel). The route name alone decides whether signing/printing happen — there's no separate document-type field.

**Request body:**
```json
{
  "ClientId": "acme-corp",
  "Tokens": [
    { "TokenId": "abc123", "InvoiceNo": "INV-0001" },
    { "TokenId": "def456", "InvoiceNo": "INV-0002" }
  ]
}
```
- `ClientId` — required, non-blank.
- `Tokens` — required, at least one entry; every `TokenId` must be non-blank. `InvoiceNo` is used for the filename and logging.

**Response — `202 Accepted`** (the batch is accepted and queued; processing happens asynchronously, one token at a time):
```json
{
  "success": true,
  "clientId": "acme-corp",
  "route": "invoice-sign-print",
  "accepted": 2,
  "jobs": [
    { "tokenId": "abc123", "invoiceNo": "INV-0001", "jobId": "..." },
    { "tokenId": "def456", "invoiceNo": "INV-0002", "jobId": "..." }
  ]
}
```

**Error responses:** `400` (missing/invalid `ClientId`, `Tokens`, or a blank `TokenId`, or invalid JSON), `404` (unknown route), `405` (non-POST).

Behind the scenes, each token flows through fetch → (sign) → (print) → invoice-signed callback, sequentially per batch (hardware token signing is never hit concurrently). Final outcome, per token, is only observable via the tray's Jobs window or the logs — the `202` response does not report success/failure of the actual work.

### `POST /label-print`

Prints a raw ZPL (Zebra Programming Language) command string directly to a printer via the Windows spooler's RAW datatype — no document fetch, no signing, no token/ClientId. Unlike the invoice routes, this is **synchronous**: the response reflects the actual print outcome.

**Request body:**
```json
{
  "Printer": "ZDesigner ZT230",
  "Zpl": "^XA\n^PW812\n^LL1218\n^FO50,50^GB712,1118,4^FS\n^FO80,90^A0N,50,50^FDShip To:^FS\n^XZ"
}
```
- `Zpl` — required; must contain a `^XA ... ^XZ` command block. Darkness (`^MD`) and copy count (`^PQ`) must already be embedded by the caller — DigiSign passes the string through unmodified.
- `Printer` — optional; if blank, the system default printer is used.

**Response — `200 OK`** (printed successfully):
```json
{ "success": true, "jobId": "..." }
```

**Response — `400 Bad Request`** (malformed body — no job is created for these):
```json
{ "success": false, "route": "label-print", "error": "Zpl must contain a ^XA ... ^XZ command block." }
```

**Response — `500 Internal Server Error`** (print failure — a job is still recorded, visible in the Jobs window):
```json
{ "success": false, "jobId": "...", "error": "Printer 'ZDesigner ZT230' is offline or unreachable: printer is set to work offline." }
```
Other causes of a `500`: the named printer isn't installed, or the printer didn't respond within the internal 15s write timeout. A pre-flight WMI connectivity check catches most offline/unreachable printers before spooling — this depends on the printer driver reporting bidirectional status, so it's best-effort, not a guarantee.

### `GET /label-printers`

Lists installed printer names, so a caller can discover a valid value for `/label-print`'s `Printer` field.

**Response — `200 OK`:**
```json
{ "success": true, "printers": ["Microsoft Print to PDF", "ZDesigner ZT230"] }
```

### `GET /heartbeat`

Confirms the listener process itself is up and responding — no license check, no printer check, no other dependency. Intended for simple liveness monitoring.

**Response — `200 OK`:**
```json
{ "success": true, "status": "ok" }
```

## Logging

- `logs/application_log.txt` — structured, leveled log (rotates at 1MB).
- `plf.txt` (app root) — overwritten (not appended) single-line status after each batch-signing run; read by the invoking ERP process.
- `logs/jobs/*.json` — one file per tracked job (listener requests, label prints, and batch-mode runs), readable via the tray's "View Job Status..." window.

## Licensing

Two independent tiers: a **user license** (`license.txt`, device-locked, required to sign PDFs) and an **admin license** (`admin.license`, gates `/admin` mode). The listener always starts regardless of user-license validity — licensing is enforced per request, not at startup.

A brand-new install with no `license.txt` gets a 30-day evaluation period instead of being blocked outright — signing works normally during this window, and after it ends the existing "valid license required" behavior applies. Remaining trial days are shown in `/settings`' General tab and in the tray icon's context-menu header.

## Update checking

There's no automatic/background update check. `DigiSign.exe /admin` has a "Check for Updates" button next to the `UpdateCheckUrl` field (manifest format: `{"version", "downloadUrl", "sha256", "notes"}`) — click it to check on demand. This is admin-only (not shown in `/settings`); deciding when to check for and ship an update is Ten Info Tech's call, not a customer-facing setting. If a newer version is found, "Update Now" downloads and verifies the package, then applies it via a helper script that always preserves `license.txt`, `license.key`, `admin.license`, `IP.xml`, `appsettings.json`, `plf.txt`, and `trial.lic` — an update never resets a license or signing/print configuration.
