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
- **appsettings.json** — newer JSON config (via `AppSettingsLoader`) for listener-mode-only settings: `VerboseMode`, `Port` (default 5000), `InvoiceApiBaseUrl`, `InvoiceApiKey`, `NoAuthApi`, `IncludeSignedPdfInCallback`, `InvoiceSignedCallbackUrl`, `EnableListenerMode`, `PrinterName`, `EnableOcspCheck` (default `true`), `OcspTimeoutSeconds` (default `10`). If this file is missing, `AppSettingsLoader` auto-migrates it once from legacy `IP.xml` indices 11–15 (in that same historical ERP scheme) and writes it out. `EnableListenerMode` replaced the older, inverted `LaunchInBatchMode` flag — `AppSettingsLoader.Load` transparently migrates any on-disk file still using the old key (inverting its value) the first time it's loaded, preserving whatever mode the install was actually running before the upgrade. Label-printing's own layout fields (copy text, X/Y/width/height, print-all-copies) live in `IP.xml`/`XmlData`, not in `appsettings.json`.
- Both files are edited together through the same `LicenseGenerationForm(settingsOnly: true)` UI (`/settings` mode) — its top-level `tabControl` has `tabApiSettings` (port, invoice API base URL/key, show-key checkbox, No-Auth, include-signed-pdf, callback URL, listener-mode checkbox) as a sibling of `tabSettings` ("PDF Signing Settings"), which itself nests a second `tabSettingsControl` with `tabGeneral` (input PDF, output folder, cert CN, PIN, verbose mode, printer, OCSP checkbox + timeout), `tabSignature` (signature X/Y/width/height, sign-on-page, open-output-folder, self-signed cert, plus the label-printing copy fields), and `tabPreview` (live drag/resize preview canvas). `tabApiSettings`'s Update Check URL field + "Check for Updates" button are admin-only — created only when `settingsOnlyMode` is `false`, so `/settings` never shows them.

## Licensing (two independent tiers)

- **User license** (`license.txt`) — required to actually sign PDFs (both batch mode and the `/invoice-sign*` HTTP routes). `LicenseManager.ValidateLicense` device-locks it: `GetDeviceId()` hashes CPU + disk serial via WMI (`System.Management`), and `DeviceHash` in the license file is `SHA256(deviceId|licenseNumber|validUntil)` — `ValidUntil` is included in the hash specifically so it can't be edited to extend expiry. If no valid license exists, the app auto-generates `license.key` (device fingerprint) for the user to send to an admin.
- **Admin license** (`admin.license`) — separate and unrelated to the user license. Gates `/admin` mode, validated by `LicenseManager.ValidateAdminLicense` against a hardcoded secret (`AdminKey = SHA256(AdminID + "|DIGISIGN_ADMIN_SECRET")`). An admin uses this mode to turn a customer's `license.key` into their `license.txt`.
- Listener mode always starts regardless of user-license validity — licensing is enforced per HTTP request inside `HttpListenerService`, not at startup, so the tray/listener stays up even with an expired/missing license (only signing requests get rejected).

## Trial/evaluation licensing

[TrialManager.cs](TrialManager.cs) grants a 30-day evaluation period so a brand-new install can sign before a purchased license exists, without changing any existing license semantics.

- `TrialManager.EnsureTrialStarted` runs unconditionally at the top of `Program.cs`'s `Main()`, for every mode, on every run — it writes `trial.lic` (same `AppDomain.CurrentDomain.BaseDirectory`-relative convention as everything else) the first time it sees this device's `LicenseManager.GetDeviceId()`, and is a no-op afterward. The clock starts at first-ever run, not at first *signing attempt* — a customer who only runs `/listen` for a week before ever signing still has the same 30-day window.
- `trial.lic` mirrors `license.txt`'s tamper-resistance pattern: `DeviceID`, `TrialStartUtc`, and `TrialHash = SHA256(DeviceID|TrialStartUtc|"DIGISIGN_TRIAL_SECRET")`. A hash mismatch (e.g. someone hand-edits `TrialStartUtc` to "reset" the trial without knowing the secret) is treated as **expired**, never as "more time" — same class of protection as `license.txt`/`admin.license`, not hardened DRM.
- The fallback only ever engages where a purchased-license check already exists and already failed: `Program.cs`'s batch-mode startup gate, and `SigningPipeline.SignSingleFile` (shared by the listener's per-token pipeline and batch-mode job resume). Neither `LicenseManager.ValidateLicense` nor `GetLicenseExpiryDays` were touched — this is a fallback layered on top, not a change to license semantics.
- Remaining days are surfaced in two places: the `/settings` General tab (`GetLicenseStatusText`) and the tray context-menu header line (`GetTraySubtitleSuffix`, e.g. `DigiSign Listener — port 5000 — Trial: 12 day(s) left`) — both go silent (no suffix) once a valid purchased license exists.

## HTTP listener mode

[HttpListenerService.cs](HttpListenerService.cs) listens on `http://localhost:{port}/` and handles a batch-invoice route family plus two standalone utility routes:

- `POST /invoice`, `/invoice-print`, `/invoice-sign`, `/invoice-sign-print`, `/label-print` all match one regex (`^/(invoice|invoice-print|invoice-sign|invoice-sign-print|label-print)/?$`, no token in the path). The four `/invoice*` routes take the batch body below; `/label-print` is unrelated in shape (see the Label printing section) and is handled synchronously, not through the job pipeline described here.
- `GET /heartbeat` and `GET /label-printers` are matched outside that regex as standalone paths — `/heartbeat` is a liveness check, `/label-printers` lists installed printers (Windows print queue names) so the ERP can populate a printer picker before sending `/label-print` requests.

For the four `/invoice*` routes, the ERP sends a JSON body: `{"ClientId": "...", "Tokens": [{"TokenId": "...", "InvoiceNo": "..."}, ...]}`, i.e. one request can batch multiple invoices for the same client.

1. The request is validated synchronously (`ClientId` present, `Tokens` non-empty, every `TokenId` non-blank) — a bad batch gets an immediate `400` and no jobs are created.
2. For a valid batch, `JobTracker.CreateJob` registers one job per `TokenId` (persisted to `logs/jobs/*.json`, bounded in-memory ring buffer of the last 200, keyed by GUID) — `JobsForm` reads `JobTracker.Snapshot()` to show recent activity (both listener and batch-signing jobs, with per-row Resume/Cancel) when the user left-clicks either the listener's or the tray companion's icon.
3. The HTTP response is written immediately as `202` with the accepted job list — the ERP does not block on fetch/sign/print. Processing then happens on a background thread pool item, looping over the batch's tokens **sequentially** (not in parallel — hardware USB-token signing must not be hit concurrently across a batch; a `static SemaphoreSlim` additionally serializes the signing step across concurrent batches/requests).
4. Per token: `IDocumentDownloader.FetchInvoiceDocument(clientId, tokenId)` (`HttpDocumentDownloader`) POSTs `{ClientId, TokenId}` to `{InvoiceApiBaseUrl}/invoice/` and gets raw PDF bytes back (no metadata/doctype from the server anymore) — see [DocumentDownloader.cs](DocumentDownloader.cs). A failure here is isolated: it fails just that token's job and logging, the batch continues with the next token.
5. Signing only happens for `-sign` routes — there's no server-reported document type anymore to additionally gate on, so route alone decides.
6. Printing (`-print` routes) goes through `IPrintService` (`PdfiumPrintService`, using the MIT-licensed `PDFtoImage`/PDFium + SkiaSharp for rendering, then standard GDI+ `PrintDocument`) to `appsettings.json`'s configured `PrinterName`, or the system default if blank. (Spire.PDF was dropped — it stamped an unremovable "evaluation" watermark on every page unless licensed.)
7. After sign/print, `IDocumentDownloader.PostSignedInvoiceCallback(...)` POSTs `{ClientId, TokenId, InvoiceNo, "signed-pdf": <base64>}` to `{InvoiceApiBaseUrl}/invoice-signed/` — this fires regardless of whether the document was actually signed (plain `/invoice`/`/invoice-print` routes still call back with the fetched bytes). A callback failure is logged but doesn't undo a successful sign/print.
8. The temp download folder (`%TEMP%\digisign_{tokenId}_{timestamp}\`) is always cleaned up in a `finally`, per token.

## Job tracking, resume, and cancel

`JobTracker`/`JobRecord` (JobTracker.cs) is the shared model behind both listener-mode batches and batch-signing-mode runs — `JobsForm` reads `JobTracker.Snapshot()` to render recent activity for either source.

- A `JobRecord` carries: identity (`JobId`, `Token`, `Route`, `Source` — `Listener`/`Batch`/`LabelPrint`), timing (`StartedAtUtc`/`CompletedAtUtc`), a `Stage` enum (`Received → Fetching → Signing → Signed`/`SkippedSigning` `→ Printing → Printed → Completed`/`Failed`/`Cancelled`/`Interrupted`), outcome fields (`Success`, `ErrorMessage`, `OutputPath`, `CallbackSuccess`/`CallbackMessage`), input context (`ClientId`, `InvoiceNo`, `InputPath`, `PathsToPrint`, `PrinterName`, `DoSign`/`DoPrint`, `DocumentType`, `FileName`, `ProgressDetail`), cancel/resume bookkeeping (`CancellationRequested`/`CancelRequestedAtUtc`, `ResumeCount`), and ownership (`OwnerProcessId`/`OwnerProcessStartTimeUtc`, `Sequence` for eviction order).
- Persistence: `JobStore.Save` (JobStore.cs:30) writes `logs/jobs/{JobId}.json` via a temp-file + `FileOptions.WriteThrough` then rename, so a job record is never read half-written. `JobStore.Prune` handles time-based disk cleanup of old job files separately from the in-memory ring buffer below.
- In-memory ring buffer: `EvictOldest()` (JobTracker.cs:403) only trims once the in-memory dict exceeds 200 entries, and only evicts jobs already in a terminal stage (`Completed`/`Failed`/`Cancelled` — never `Interrupted` or still in-flight), oldest-by-`Sequence` first. It never deletes the on-disk JSON.
- Cancel is cooperative, not preemptive: `RequestCancel` just sets a flag that's checked between pipeline steps — a step already running (e.g. mid-signature) finishes before cancellation takes effect.
- Resume (`ResumeJob`) requires `IsResumable` (job is `Failed`/`Interrupted`/`Cancelled`, or `Completed` with a failed callback), then resets cancellation, increments `ResumeCount`, and redispatches to the source-appropriate handler (`HttpListenerService.ProcessResumedJob` or `Program.ResumeBatchJob`) on a ThreadPool thread — it re-enters the same pipeline but skips steps already checkpointed (e.g. signing is skipped if `PathsToPrint` files already exist on disk from a prior attempt).

## Label printing

Added alongside the resume/cancel work — a synchronous, job-pipeline-adjacent path for printing shipping/product labels via raw ZPL, independent of the invoice fetch/sign flow:

- `POST /label-print` takes `{"Printer": "...", "Zpl": "..."}`; the ZPL body is validated to look like `^XA...^XZ` (`LabelPrintPipeline.cs`) before anything is sent to a printer, and the response is synchronous success/fail (no `202`/job-polling involved, unlike the invoice routes) — though a `JobRecord` with `Source = LabelPrint` is still created for visibility in `JobsForm`.
- `LabelPrinter.cs` (`RawZplPrintService`/`RawPrinterHelper`) sends the raw ZPL bytes straight to the Windows spooler via `winspool.drv` P/Invoke as a `RAW` datatype job, bypassing GDI+/`PrintDocument` entirely (ZPL printers expect raw command bytes, not rendered graphics) — bounded by a 15s timeout.
- `PrinterStatusChecker.cs` checks a printer's online/offline status before a print attempt.
- `GET /label-printers` (see HTTP listener mode above) lists installed Windows printers so the ERP can pick a valid `Printer` value up front.
- The label-copy layout fields themselves (copy text, X/Y/width/height, "print all copies", copy 2/3/4) are configured in `/settings`'s `tabSignature` tab and stored in `IP.xml`/`XmlData`, not `appsettings.json`.

## Update checker / self-update

Manual only — there is no automatic/background check anywhere (not at listener startup, not at tray-companion startup, not on a timer). A "Check for Updates" button lives in `/admin` mode's API Settings tab, next to `appsettings.json`'s `UpdateCheckUrl` field (blank = nothing to check against yet) — **admin-only, not present in `/settings`**: `CreateApiSettingsTab` only creates `lblUpdateCheckUrl`/`txtUpdateCheckUrl`/`btnCheckForUpdates` when `!settingsOnlyMode`, so a customer running `/settings` never sees update controls at all (checking for/shipping updates is Ten Info Tech's call, not the customer's). `LoadApiSettings`/`LoadDefaultApiSettings` and both Save handlers null-guard every reference to these controls, and Saving from `/settings` preserves whatever `UpdateCheckUrl` is already on disk instead of blanking it. `LicenseGenerationForm.BtnCheckForUpdates_Click` checks whatever URL is currently in the textbox — so an admin can test a URL before saving it — on a `ThreadPool` thread, marshaling the result back via `BeginInvoke` so the form doesn't freeze while checking.

- [UpdateChecker.cs](UpdateChecker.cs) `GET`s the URL expecting `{"version", "downloadUrl", "sha256", "notes"}`, compares `version` against `VersionInfo.FullVersion` as a `System.Version`. Every failure (network, bad JSON, unparsable version) is swallowed and logged only — same fail-open convention as the TSA/OCSP clients in `SignatureHelper.cs`.
- If a newer version is found, [UpdateNotificationForm.cs](UpdateNotificationForm.cs) is shown with "Update Now"/"Not Now" buttons. If not, or if the check itself failed, a plain `MessageBox` says so.
- "Update Now" calls `LicenseGenerationForm.ApplyUpdate`, which calls [SelfUpdater.cs](SelfUpdater.cs) `DownloadAndApply`: downloads the zip, verifies its `sha256` (aborts with the current install untouched on mismatch or if the manifest omits it), extracts to a temp staging folder, writes a PowerShell helper script, launches it, then the app exits (`Application.Exit()`) so its own file lock releases. The helper waits for the old process to exit, `robocopy`s the staged files over the install directory with `/XF` excluding `SelfUpdater.ProtectedFileNames` (`license.txt`, `license.key`, `admin.license`, `IP.xml`, `appsettings.json`, `plf.txt`, `trial.lic`) and `/XD logs` — **this exclude-list is what guarantees license/signing/print-format settings and trial state survive an update**, and a future installer must use the same list. Windows won't let a running EXE overwrite its own file, which is why this two-process (main app → helper script) shape is necessary at all. The relaunch uses no args, so the restarted process picks its mode from `appsettings.json`'s `EnableListenerMode` as normal.

## PDF signing internals

[DigitalSignatureService.cs](DigitalSignatureService.cs) does the actual work, via iTextSharp:

- Certificate lookup: searches `CurrentUser` then `LocalMachine` "My" stores by subject name/CN (this is how USB token certs are found), and falls back to a self-signed cert (`xmlData.SelfSignedPath`/`SelfSignedPassword`) if nothing is found and `UseSelfSigned` is set.
- Signing uses `PdfStamper.CreateSignature` + `MakeSignature.SignDetached` with a custom `IExternalSignature` (`SignatureHelper.SafeCertificateSignature`).
- **Critical, non-obvious constraint**: the `Sign()` implementation must call `rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)` — never manually `ComputeHash()` then `SignHash()`. Manual hashing double-wraps the digest and produces signatures that Adobe Reader/Edge/BouncyCastle reject with "PKCS7 parsing error" / "multiple SignerInfos", even though the signature is created "successfully". This was a real production bug — full writeup in [docs/ROOT_CAUSE_ANALYSIS.md](docs/ROOT_CAUSE_ANALYSIS.md), [docs/QUICK_REFERENCE.md](docs/QUICK_REFERENCE.md), and [docs/WORKING_PDF_SIGNER_IMPLEMENTATION.md](docs/WORKING_PDF_SIGNER_IMPLEMENTATION.md). Don't "simplify" the signing code back toward manual hashing.
- `appearance.Acro6Layers = false` and `appearance.Layer2Text = string.Empty` are both required for viewer compatibility (avoid double-rendered text / layer issues) — don't remove them.
- Timestamping goes through `SignatureHelper.ResilientTSAClient`, which tries a hardcoded list of public TSA servers in sequence and signs without a timestamp if all fail (never blocks signing on TSA availability). Each server attempt has a 15s fail-fast timeout (`PerServerTimeoutMs`, via `Task.Run`/`Task.Wait` since `TSAClientBouncyCastle` has no built-in timeout) — without it, a hanging (not just erroring) TSA server would stall each attempt for a long time before falling through to the next one. This is signing's main real-world "stops due to a server issue" risk — verified by temporarily forcing a non-routable server first in the list and confirming it fails over at exactly 15s.
- OCSP (certificate revocation) checking is wired up via `SignatureHelper.ResilientOcspClient`, gated by `EnableOcspCheck`/`OcspTimeoutSeconds` in `appsettings.json` (General tab in `/settings`, `chkEnableOcspCheck`). When enabled, `DigitalSignatureService.SignPdfCore` builds a real 2+ certificate chain (`BuildOcspCertificateChain`, via `System.Security.Cryptography.X509Certificates.X509Chain`, bounded by a capped `UrlRetrievalTimeout` so a network-based chain lookup can't hang) — OCSP validates a cert against its issuer, and iTextSharp's `MakeSignature.SignDetached` only ever attempts an OCSP check when the chain passed in has 2+ certificates, so a single-cert chain (what's always used when the setting is off, or as a fallback if chain resolution fails/only resolves the leaf — e.g. a self-signed cert, which is its own issuer) causes iTextSharp to silently skip OCSP entirely. **Fail-open guarantee, two layers deep**: `ResilientOcspClient` itself catches every failure (timeout, network error, any exception) and returns `null` rather than throwing, so a failed check just means "no OCSP data attached" — never a blocked signature. On top of that, `DigitalSignatureService.SignPdf` (the public entry point, thin wrapper around `SignPdfCore`) retries the *entire* sign once with OCSP fully disabled if the OCSP-enabled attempt throws for any other reason — so an OCSP problem can change whether a revocation check happened, never whether the document got signed. When the setting is off, behavior is byte-for-byte identical to before OCSP was wired up (single-cert chain, no OCSP data, no retry logic engaged).
- `SignOnPage` (`F`/`E`/`L`) picks first/every/last page; signature rectangle coordinates are auto-clamped inward if they'd fall outside the page bounds.
- PIN handling for the private key goes through the `X509Certificate2Extension.SetPinForPrivateKey` P/Invoke shim ([X509Certificate2Extension.cs](X509Certificate2Extension.cs)), which tries the CNG (`NCryptSetProperty`) path first and falls back to legacy CSP (`CryptSetProvParam`) — needed because modern USB tokens are CNG-only and don't support the older `RSACryptoServiceProvider.PrivateKey` API.

## Logging

[Logger.cs](Logger.cs) writes structured, leveled entries to `logs/application_log.txt`, rotating (timestamped backup) once the file hits 1MB. Separately, `Logger.LogToPlf` **overwrites** (not appends) a single-line `plf.txt` at the app root with just the outcome message — this is a status file meant for the invoking ERP process to read after a batch-signing run, not a log, so don't change it to append-mode or add extra formatting without checking what reads it.

## Versioning

[VersionInfo.cs](VersionInfo.cs) doesn't use the assembly's version number directly — it derives a synthetic build number from the assembly's build date (days since 2000-01-01) and a revision from seconds-since-midnight/2, so version strings auto-increment per build without manual bumping.

## Forms / UI

All forms build their controls programmatically in `InitializeComponents()` — there are no `.Designer.cs` files.

- `LicenseGenerationForm.cs` (~3000+ lines) — dual-purpose: plain license generation (`tabLicense`) and, via the `settingsOnly` constructor flag, the entire `/settings` UI (`tabSettings`, described in the config-files section above). Its "Restart App" button only enables after a successful Save and relaunches into whatever mode was running before Settings was opened.
- `JobsForm.cs` — a single `DataGridView` (columns: Time, Source, Route/File, Token/File, Doc Type, Stage, Progress, Result, Callback, Output/Error, plus Resume/Cancel button columns) that polls `JobTracker.Snapshot()` every second via a `Timer`. Closing the form hides it rather than disposing it, so polling keeps running — it's a singleton opened by left-clicking either the listener's or the tray companion's tray icon.
- `VerboseProgressForm.cs` — `RichTextBox` progress log + `ProgressBar` + status labels, shown only in batch-signing mode when `/verbose`/`VerboseMode` is set; auto-closes when signing finishes.
- `TrayHostForm.cs` — an invisible 0×0 form that exists purely to give the tray icon's owning thread a window handle for `Invoke`/`BeginInvoke` marshaling.
- Tray context menus are built directly in `Program.cs` (`RunListenMode`/`RunTrayCompanionMode`, roughly lines 816–1010): a disabled header line (port number for listener mode, "idle (signing mode)" for the companion), then "View Job Status...", "Settings...", "Open Logs Folder", "Exit".

## Admin-license tooling

`tools/admin-license/` holds the operator-side scripts for generating/testing `admin.license` files outside the app itself: `DigiSign_Admin.bat` (launches `/admin` in a console so prompts are visible), `GenerateAdminLicense.ps1` (computes the `AdminKey` and writes the file), and a couple of test/diagnostic `.bat` scripts, plus `admin.license.example`/`.template` samples. These mirror `AdminKeyTester.cs`/`AdminLicenseValidator.cs`, which are excluded from the main build (see Build/run above).

## Repo hygiene note

`license.txt`, `license.key`, `admin.license`, `plf.txt`, and `application_log.txt` have historically been tracked in git even though they're machine-specific, generated, or secret-like — don't assume the versions in a fresh clone are meaningful test fixtures, and don't add new instances of this pattern (prefer `.example`/`.template` files for anything that should ship as a placeholder).
