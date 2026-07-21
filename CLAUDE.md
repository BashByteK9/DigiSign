# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

DigiSign is a Windows Forms desktop app (.NET Framework 4.7.2, `net472`) that digitally signs PDF invoices with a certificate (typically a USB hardware token / smart card), and can run as a background HTTP listener that an ERP system calls to fetch, sign, and print invoice/label documents. It's a single-EXE tool distributed to customer machines (Ten Info Tech), not a web app or service with a database.

## Build / run

- Build: `dotnet build DigiSign.csproj` (this is the VS Code default build task in [.vscode/tasks.json](.vscode/tasks.json)).
- Output goes to `..\..\Build\Digisign\` relative to the repo (i.e. `C:\Users\<user>\Build\Digisign`), configured via `OutputPath` in [DigiSign.csproj](DigiSign.csproj) — this is where you'll find the runnable `DigiSign.exe` plus `appsettings.json`, `IP.xml`, `license.txt`, logs, etc. during manual testing.
- There are no automated tests in this repo. Verification is done by running the built `DigiSign.exe` with different CLI args and inspecting `logs/application_log.txt` / `plf.txt`.
- `MockDocumentServer/` is a separate console project (its own `.csproj`, excluded from the main build via `<Compile Remove>` in DigiSign.csproj) that stands in for the real invoice provider API (`reports.myerpwise.net`) during local dev — run it standalone (`dotnet run` from that folder) on port 9091, expects header `X-Api-Key: local-dev-key`, exposes `POST /invoice/` (returns raw PDF bytes for a `{ClientId, TokenId}` body) and `POST /invoice-signed/` (logs the signed-PDF callback and returns `200`).
- `AdminKeyTester.cs` and `AdminLicenseValidator.cs` are also excluded from the main build (see `<Compile Remove>` in the csproj) — they're standalone one-off console tools for generating/checking `admin.license` files, mirrored by the PowerShell/batch scripts under `tools/admin-license/`.

## Entry point and run modes

Everything starts in [Program.cs](Program.cs) `Main()`, which branches on `args[0]` into four user-facing mutually exclusive modes (plus one internal/hidden one):

| Mode | Trigger | License required | Purpose |
|---|---|---|---|
| Admin | `/admin` | `admin.license` (admin license) | Opens `LicenseGenerationForm` so an operator can turn a customer's `license.key` into a `license.txt` |
| Settings | `/settings` | none | Opens `LicenseGenerationForm(settingsOnly: true)` to edit `IP.xml` + `appsettings.json` |
| Listener | `/listen`, or no args when `appsettings.json` → `EnableListenerMode` is `true` | none to start; checked per-request | Runs `HttpListenerService` + a tray icon (background/headless most of the time) |
| Batch signing | no args when `EnableListenerMode` is `false` (the default), or any other arg | `license.txt` (user license) | Reads `IP.xml`, signs the listed PDFs via `BatchSigner`, optionally opens the output folder |

`/verbose` (or `VerboseMode` in config) only applies to batch-signing mode and pops up `VerboseProgressForm` with live step-by-step progress, auto-closing when done.

Batch-signing mode itself still exits the instant signing finishes (this is how the ERP invokes it — once per document), but a tray icon must always be present regardless of mode. To reconcile these, batch mode self-spawns a hidden `DigiSign.exe /traycompanion` process (idle tray icon only, no HTTP listener) whenever nothing already owns the tray-presence slot. `TraySingleton.cs` guards this with a system-wide named mutex (`Global\DigiSign_TrayPresence`) so exactly one tray icon exists at a time — listener mode and the companion both hold it for their lifetime, and either one will politely ask the other to exit (via a named `EventWaitHandle`) if it starts up and finds the slot already taken. `/traycompanion` is not a documented end-user switch.

Settings (`/settings` mode) has a "Restart App" button that closes the settings form and relaunches `DigiSign.exe` back into whichever mode was running before Settings was opened (plain relaunch for the standalone case, or `/listen`/`/traycompanion` when opened from that mode's tray menu) — see `RunSettingsMode`'s bool return and `LicenseGenerationForm.RestartRequested`. It only enables after a successful Save in either settings tab.

## Two config files — don't confuse them

- **IP.xml** — legacy config, written by an external ERP integration (Tally-style). It's a flat, *positional* list of `<FILENAMELIST><FILENAME>` entries parsed by index in `Program.ReadXmlData` — there are no named keys, so reordering entries breaks everything. Indices 0–10 hold: input file paths, output folder, certificate CommonName, PIN, signature X/Y/width/height, `SignOnPage` (F/E/L), `OpenOutputFolder` (Y/N), and an optional `USESELFSIGNED` flag (index 10).
- **appsettings.json** — newer JSON config (via `AppSettingsLoader`) for listener-mode-only settings: `VerboseMode`, `Port` (default 8943), `InvoiceApiBaseUrl`, `InvoiceApiKey`, `EnableListenerMode`, `PrinterName`. If this file is missing, `AppSettingsLoader` auto-migrates it once from legacy `IP.xml` indices 11–15 (in that same historical ERP scheme) and writes it out. `EnableListenerMode` replaced the older, inverted `LaunchInBatchMode` flag — `AppSettingsLoader.Load` transparently migrates any on-disk file still using the old key (inverting its value) the first time it's loaded, preserving whatever mode the install was actually running before the upgrade.
- Both files are edited together through the same `LicenseGenerationForm(settingsOnly: true)` UI (`/settings` mode) — it has separate tabs for the signing/XML fields vs. the listener/API fields.

## Licensing (two independent tiers)

- **User license** (`license.txt`) — required to actually sign PDFs (both batch mode and the `/invoice-sign*` HTTP routes). `LicenseManager.ValidateLicense` device-locks it: `GetDeviceId()` hashes CPU + disk serial via WMI (`System.Management`), and `DeviceHash` in the license file is `SHA256(deviceId|licenseNumber|validUntil)` — `ValidUntil` is included in the hash specifically so it can't be edited to extend expiry. If no valid license exists, the app auto-generates `license.key` (device fingerprint) for the user to send to an admin.
- **Admin license** (`admin.license`) — separate and unrelated to the user license. Gates `/admin` mode, validated by `LicenseManager.ValidateAdminLicense` against a hardcoded secret (`AdminKey = SHA256(AdminID + "|DIGISIGN_ADMIN_SECRET")`). An admin uses this mode to turn a customer's `license.key` into their `license.txt`.
- Listener mode always starts regardless of user-license validity — licensing is enforced per HTTP request inside `HttpListenerService`, not at startup, so the tray/listener stays up even with an expired/missing license (only signing requests get rejected).

## HTTP listener mode

[HttpListenerService.cs](HttpListenerService.cs) listens on `http://localhost:{port}/` and handles four routes — `POST /invoice`, `/invoice-print`, `/invoice-sign`, `/invoice-sign-print` — matched by regex with no token in the path. The ERP sends a JSON body: `{"ClientId": "...", "Tokens": [{"TokenId": "...", "InvoiceNo": "..."}, ...]}`, i.e. one request can batch multiple invoices for the same client.

1. The request is validated synchronously (`ClientId` present, `Tokens` non-empty, every `TokenId` non-blank) — a bad batch gets an immediate `400` and no jobs are created.
2. For a valid batch, `JobTracker.CreateJob` registers one job per `TokenId` (persisted to `logs/jobs/*.json`, bounded in-memory ring buffer of the last 200, keyed by GUID) — `JobsForm` reads `JobTracker.Snapshot()` to show recent activity (both listener and batch-signing jobs, with per-row Resume/Cancel) when the user left-clicks either the listener's or the tray companion's icon.
3. The HTTP response is written immediately as `202` with the accepted job list — the ERP does not block on fetch/sign/print. Processing then happens on a background thread pool item, looping over the batch's tokens **sequentially** (not in parallel — hardware USB-token signing must not be hit concurrently across a batch; a `static SemaphoreSlim` additionally serializes the signing step across concurrent batches/requests).
4. Per token: `IDocumentDownloader.FetchInvoiceDocument(clientId, tokenId)` (`HttpDocumentDownloader`) POSTs `{ClientId, TokenId}` to `{InvoiceApiBaseUrl}/invoice/` and gets raw PDF bytes back (no metadata/doctype from the server anymore) — see [DocumentDownloader.cs](DocumentDownloader.cs). A failure here is isolated: it fails just that token's job and logging, the batch continues with the next token.
5. Signing only happens for `-sign` routes — there's no server-reported document type anymore to additionally gate on, so route alone decides.
6. Printing (`-print` routes) goes through `IPrintService` (`PdfiumPrintService`, using the MIT-licensed `PDFtoImage`/PDFium + SkiaSharp for rendering, then standard GDI+ `PrintDocument`) to `appsettings.json`'s configured `PrinterName`, or the system default if blank. (Spire.PDF was dropped — it stamped an unremovable "evaluation" watermark on every page unless licensed.)
7. After sign/print, `IDocumentDownloader.PostSignedInvoiceCallback(...)` POSTs `{ClientId, TokenId, InvoiceNo, "signed-pdf": <base64>}` to `{InvoiceApiBaseUrl}/invoice-signed/` — this fires regardless of whether the document was actually signed (plain `/invoice`/`/invoice-print` routes still call back with the fetched bytes). A callback failure is logged but doesn't undo a successful sign/print.
8. The temp download folder (`%TEMP%\digisign_{tokenId}_{timestamp}\`) is always cleaned up in a `finally`, per token.

## PDF signing internals

[DigitalSignatureService.cs](DigitalSignatureService.cs) does the actual work, via iTextSharp:

- Certificate lookup: searches `CurrentUser` then `LocalMachine` "My" stores by subject name/CN (this is how USB token certs are found), and falls back to a self-signed cert (`xmlData.SelfSignedPath`/`SelfSignedPassword`) if nothing is found and `UseSelfSigned` is set.
- Signing uses `PdfStamper.CreateSignature` + `MakeSignature.SignDetached` with a custom `IExternalSignature` (`SignatureHelper.SafeCertificateSignature`).
- **Critical, non-obvious constraint**: the `Sign()` implementation must call `rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)` — never manually `ComputeHash()` then `SignHash()`. Manual hashing double-wraps the digest and produces signatures that Adobe Reader/Edge/BouncyCastle reject with "PKCS7 parsing error" / "multiple SignerInfos", even though the signature is created "successfully". This was a real production bug — full writeup in [docs/ROOT_CAUSE_ANALYSIS.md](docs/ROOT_CAUSE_ANALYSIS.md), [docs/QUICK_REFERENCE.md](docs/QUICK_REFERENCE.md), and [docs/WORKING_PDF_SIGNER_IMPLEMENTATION.md](docs/WORKING_PDF_SIGNER_IMPLEMENTATION.md). Don't "simplify" the signing code back toward manual hashing.
- `appearance.Acro6Layers = false` and `appearance.Layer2Text = string.Empty` are both required for viewer compatibility (avoid double-rendered text / layer issues) — don't remove them.
- Timestamping goes through `SignatureHelper.ResilientTSAClient`, which tries a hardcoded list of public TSA servers in sequence and signs without a timestamp if all fail (never blocks signing on TSA availability). Each server attempt has a 15s fail-fast timeout (`PerServerTimeoutMs`, via `Task.Run`/`Task.Wait` since `TSAClientBouncyCastle` has no built-in timeout) — without it, a hanging (not just erroring) TSA server would stall each attempt for a long time before falling through to the next one. This is signing's main real-world "stops due to a server issue" risk — verified by temporarily forcing a non-routable server first in the list and confirming it fails over at exactly 15s.
- OCSP (certificate revocation) checking is wired up via `SignatureHelper.ResilientOcspClient`, gated by `EnableOcspCheck`/`OcspTimeoutSeconds` in `appsettings.json` (General tab in `/settings`). **Non-obvious**: `MakeSignature.SignDetached` here is called with only the signing certificate in the chain (`new[] { bcCert }`, no issuer cert) — OCSP validates a cert against its issuer, so with a single-cert chain iTextSharp never actually calls `ocspClient.GetEncoded(...)` at all (confirmed via an unconditional diagnostic log that never fired during a real sign). This was true before the config was added too, so OCSP is currently inert regardless of the setting — it only matters if the cert chain is ever expanded to include an issuer certificate.
- `SignOnPage` (`F`/`E`/`L`) picks first/every/last page; signature rectangle coordinates are auto-clamped inward if they'd fall outside the page bounds.
- PIN handling for the private key goes through the `X509Certificate2Extension.SetPinForPrivateKey` P/Invoke shim ([X509Certificate2Extension.cs](X509Certificate2Extension.cs)), which tries the CNG (`NCryptSetProperty`) path first and falls back to legacy CSP (`CryptSetProvParam`) — needed because modern USB tokens are CNG-only and don't support the older `RSACryptoServiceProvider.PrivateKey` API.

## Logging

[Logger.cs](Logger.cs) writes structured, leveled entries to `logs/application_log.txt`, rotating (timestamped backup) once the file hits 1MB. Separately, `Logger.LogToPlf` **overwrites** (not appends) a single-line `plf.txt` at the app root with just the outcome message — this is a status file meant for the invoking ERP process to read after a batch-signing run, not a log, so don't change it to append-mode or add extra formatting without checking what reads it.

## Versioning

[VersionInfo.cs](VersionInfo.cs) doesn't use the assembly's version number directly — it derives a synthetic build number from the assembly's build date (days since 2000-01-01) and a revision from seconds-since-midnight/2, so version strings auto-increment per build without manual bumping.
