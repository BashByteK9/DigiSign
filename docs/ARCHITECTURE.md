# DigiSign Architecture Reference

Deep internals reference — read this before making a non-trivial change. For the user-facing HTTP API contract (routes, request/response JSON), see [README.md](../README.md#http-listener-api) instead of re-deriving it here. For guidance on tone/conventions/what-not-to-do, see [CLAUDE.md](../CLAUDE.md). This doc is about *how the code is put together*, with file:line references so it stays checkable against the source.

## 1. Entry point & run modes

Everything starts in [Program.cs](../Program.cs) `Main()`, branching on `args[0]`:

| Mode | Trigger | License gate |
|---|---|---|
| Admin | `/admin` | `admin.license` |
| Settings | `/settings` | none |
| Listener | `/listen`, or no args + `EnableListenerMode=true` | none to start; per-request |
| Batch signing | no args + `EnableListenerMode=false` (default), or any other arg | `license.txt` |
| Tray companion | `/traycompanion` (internal, not a documented switch) | none |

Batch-signing mode exits the instant a document is signed (the ERP invokes it once per document), but something must keep a tray icon alive at all times. Batch mode self-spawns a hidden `/traycompanion` process whenever nothing already owns the tray-presence slot.

**`TraySingleton.cs`** — a `Global\DigiSign_TrayPresence` named `Mutex`, non-blocking `WaitOne(0)` acquire (`TryAcquire`, line 18; an `AbandonedMutexException` from a crashed prior owner counts as a successful acquire). `IsHeld()` (line 49) is a non-owning probe used to decide whether to spawn the companion at all. If two processes both want the slot, the loser calls `RequestOtherInstanceExit()` (line 95), which sets a separate `Global\DigiSign_TrayExitRequested` `EventWaitHandle`; the incumbent's `WatchForExitRequest` background thread (line 111) wakes up, resets the handle, and runs its exit callback. Neither process ever force-kills the other — it's a cooperative ask.

**`JobRecoveryService.RunStartupRecovery()`** (JobRecoveryService.cs:17) runs at startup in every long-lived entry point. It loads every on-disk job (`JobStore.LoadAll`), and for anything left in a non-terminal stage, checks whether the owning process (`OwnerProcessId` + `OwnerProcessStartTimeUtc`, guarded against PID reuse via a 2s start-time tolerance) is still actually running. If not, the job is force-transitioned to `Interrupted` regardless of what stage it was actually in when the process died — deliberately collapsing the detail, because a human should decide Resume vs. Cancel rather than the app silently guessing. Never auto-resumes anything.

Settings mode's "Restart App" button (`LicenseGenerationForm.RestartRequested`) relaunches back into whichever mode was running before Settings was opened, and only enables after a successful Save.

## 2. Config files

- **`IP.xml`** — flat, positional `<FILENAMELIST><FILENAME>` list parsed by index in `Program.ReadXmlData`. Indices 0–10: input PDF path(s), output folder, cert CommonName, PIN, signature X/Y/width/height, `SignOnPage` (F/E/L), `OpenOutputFolder` (Y/N), `USESELFSIGNED`. Label-printing's copy-layout fields (copy text, X/Y/width/height, print-all-copies) are additional entries in the same scheme, edited via `/settings`'s `tabSignature` tab.
- **`appsettings.json`** — via `AppSettingsLoader`/`AppSettings.cs`. Fields (with defaults): `VerboseMode` (false), `Port` (5000), `InvoiceApiBaseUrl` (""), `InvoiceApiKey` (""), `NoAuthApi` (false), `IncludeSignedPdfInCallback` (true), `InvoiceSignedCallbackUrl` (""), `EnableListenerMode` (false), `PrinterName` (""), `EnableOcspCheck` (true), `OcspTimeoutSeconds` (10). Missing file → one-time migration from `IP.xml` indices 11–15. A file still using the old inverted `LaunchInBatchMode` key gets that key transparently flipped into `EnableListenerMode` on load.
- Both edited together via `/settings` (`LicenseGenerationForm(settingsOnly: true)`).

## 3. Licensing

- **User license** (`license.txt`) — device-locked. `LicenseManager.ValidateLicense`: `GetDeviceId()` hashes CPU + disk serial (WMI, `System.Management`); `DeviceHash = SHA256(deviceId|licenseNumber|validUntil)`, with `validUntil` baked into the hash so it can't be edited to extend expiry. No valid license → app generates `license.key` (a device fingerprint) for the customer to send to an admin.
- **Admin license** (`admin.license`) — unrelated tier, gates `/admin`. `LicenseManager.ValidateAdminLicense` checks against `AdminKey = SHA256(AdminID + "|DIGISIGN_ADMIN_SECRET")`.
- `tools/admin-license/` — operator tooling: `DigiSign_Admin.bat` (launches `/admin` in a console), `GenerateAdminLicense.ps1` (computes `AdminKey`, writes `admin.license`), plus test/diagnostic `.bat` scripts and `.example`/`.template` samples. Mirrors `AdminKeyTester.cs`/`AdminLicenseValidator.cs` (excluded from the main build).
- Listener mode always starts regardless of license validity; licensing is enforced per HTTP request, not at process startup.

## 4. Trial/evaluation licensing

`TrialManager.cs` grants a 30-day evaluation period so a brand-new install can sign before a purchased license exists, layered on top of the licensing model in section 3 without changing it.

- `EnsureTrialStarted` (called unconditionally near the top of `Program.cs`'s `Main()`, every mode, every run) writes `trial.lic` the first time it sees this device's `LicenseManager.GetDeviceId()` — a no-op afterward. The clock starts at first-ever run of the app on this device, not at first signing attempt.
- `trial.lic` format mirrors `license.txt`: `DeviceID`, `TrialStartUtc`, `TrialHash = SHA256(DeviceID|TrialStartUtc|"DIGISIGN_TRIAL_SECRET")`. `GetTrialStatus` treats a hash mismatch, a device-ID mismatch, or an unparsable date as **expired** — never as grounds to reset or extend.
- Two call sites layer the fallback on top of an already-failed `LicenseManager.ValidateLicense`: `Program.cs`'s batch-mode startup gate, and `SigningPipeline.SignSingleFile` (shared by the listener's per-token pipeline and batch-mode job resume via `JobTracker.RegisterResumeHandler`). Both leave the existing license check and its failure messaging untouched — trial status is only ever consulted after that check has already failed.
- Surfaced via `LicenseGenerationForm.GetLicenseStatusText` (General settings tab) and `Program.GetTraySubtitleSuffix` (tray context-menu header, e.g. `DigiSign Listener — port 5000 — Trial: 12 day(s) left`) — both resolve to nothing once a valid purchased license exists.

## 5. HTTP listener — request lifecycle

Route table and JSON contract: see [README.md](../README.md#http-listener-api). This section covers what happens *after* a request is accepted, in [HttpListenerService.cs](../HttpListenerService.cs).

- `/heartbeat` and `/label-printers` are matched as standalone paths (lines 153, 167) before the regex — no job, no license check, nothing but the direct answer.
- The 4 invoice routes + `/label-print` share one regex (line 15). For the invoice routes: validate body → `JobTracker.CreateJob` once per token (line 256) → respond `202` immediately (line 259) → `ThreadPool.QueueUserWorkItem(_ => ProcessBatch(...))` (line 268) processes tokens **sequentially within a batch** (hardware token signing must never be hit concurrently).
- **`ProcessSingleToken`** (line 297) is the single pipeline both a fresh request and a Resume click drive through — it reads all context (`ClientId`, `TokenId`, `DoSign`, `DoPrint`, etc.) from the persisted `JobRecord`, not from method parameters, specifically so `ProcessResumedJob` (line 288) can call the exact same code. Each step is checkpoint-gated:
  1. `alreadySigned` (line 329) is true iff `job.PathsToPrint` is non-empty and every path still exists on disk — the resume fast-path trusts this over `job.Stage`, since `JobRecoveryService` collapses whatever stage a crashed job was actually in down to `Interrupted`.
  2. Fetch (`IDocumentDownloader.FetchInvoiceDocument`) → sign (`SigningPipeline.SignSingleFile`, only for `-sign` routes) or plain copy → `JobTracker.SetSigned` checkpoint.
  3. Cooperative cancellation is checked **only between** steps (line 409, 446) — never interrupts a step already running.
  4. Print (`IPrintService.Print`, only for `-print` routes, skipped if `job.Stage == Printed` already) → `JobTracker.SetPrinted`.
  5. Callback (`IDocumentDownloader.PostSignedInvoiceCallback`, skipped if `CallbackSuccess == true` already) — fires even for unsigned fetch/print routes with whatever bytes were fetched.
  6. `finally`: the per-token temp folder (`%TEMP%\digisign_{tokenId}_{timestamp}\`) is always deleted.
- **`/label-print`** (`HandleLabelPrintRequest`, line 499) is synchronous and outside this pipeline entirely — see section 7.

## 6. Job tracking, resume, and cancel

`JobTracker.cs` is the in-memory + on-disk model shared by listener jobs, batch-signing jobs, and label-print jobs.

- `JobRecord` (line 40) fields: identity (`JobId`, `Token`, `Route`, `Source` — `Listener`/`Batch`/`LabelPrint`), `Stage` (enum, line 10: `Received → Fetching → Signing → Signed`/`SkippedSigning` `→ Printing → Printed → Completed`/`Failed`/`Cancelled`/`Interrupted`), outcome (`Success`, `ErrorMessage`, `OutputPath`, `CallbackSuccess`/`CallbackMessage`), resume-relevant context (`ClientId`, `InvoiceNo`, `InputPath`, `PathsToPrint`, `PrinterName`, `DoSign`/`DoPrint`), cancel/resume bookkeeping (`CancellationRequested`, `ResumeCount`), and ownership (`OwnerProcessId`/`OwnerProcessStartTimeUtc`, used by `JobRecoveryService`).
- `IsResumable` (line 83): `Failed`/`Interrupted`/`Cancelled`, or `Completed` with a failed callback. `IsCancelable` (line 89): not already cancellation-requested and not in a terminal stage.
- **Persistence** — `JobStore.Save` (JobStore.cs:30): serialize → write to a `.tmp` file with `FileOptions.WriteThrough` + `FileStream.Flush(true)` → `File.Replace`/`File.Move` into place. This is explicitly documented as "the strongest durability .NET Framework 4.7.2 offers without a real write-ahead log" — a drive with a volatile write cache could still lose the last few milliseconds, an accepted risk for this low-volume desktop app. `JobStore.LoadAll` quarantines any file that fails to deserialize (renames to `.corrupt`) rather than crashing startup recovery. `JobStore.Prune(retention)` deletes old **terminal** job files only — never touches anything resumable.
- **In-memory ring buffer** — `EvictOldest()` (JobTracker.cs:403) trims once the dict exceeds 200 entries, evicting only `Completed`/`Failed`/`Cancelled` jobs (never `Interrupted` or in-flight), oldest-`Sequence`-first. This never deletes the on-disk file — that's `JobStore.Prune`'s separate, time-based job.
- **Cancel** (`RequestCancel`, line 278) sets a flag only; the pipeline checks it between steps (section 5).
- **Resume** (`ResumeJob`, line 301): requires `IsResumable`, atomically claims the job via `activeJobIds` (prevents a double-resume race), resets cancellation, increments `ResumeCount`, then dispatches to whichever handler was registered for that job's `Source` via `RegisterResumeHandler` (`HttpListenerService.ProcessResumedJob` for `Listener`, an analogous batch-mode handler for `Batch`) on a `ThreadPool` thread.

## 7. Label printing

Independent of the invoice fetch/sign/print pipeline — a raw-ZPL passthrough:

- `LabelPrintPipeline.Validate` (LabelPrintPipeline.cs:25) requires a `^XA ... ^XZ` block (case-insensitive, `^XA` must come before `^XZ`); darkness/copy-count commands (`^MD`/`^PQ`) are the caller's responsibility, passed through unmodified.
- `RawZplPrintService.PrintRaw` (LabelPrinter.cs:33): resolves the effective printer name (configured or system default), runs `PrinterStatusChecker.IsOnline` (best-effort WMI `Win32_Printer` check — `WorkOffline` flag or `PrinterStatus == 7`) before ever touching the spooler, then sends the raw bytes via `RawPrinterHelper.SendBytesToPrinter` — a `winspool.drv` P/Invoke sequence (`OpenPrinter`/`StartDocPrinter`/`StartPagePrinter`/`WritePrinter`/`EndPagePrinter`/`EndDocPrinter`) using the `RAW` datatype, bypassing GDI+/`PrintDocument` entirely (ZPL printers expect raw command bytes, not rasterized graphics). Bounded by a 15s `Task.Wait` timeout so a stuck spooler/offline printer can't hang the synchronous HTTP response indefinitely — the same pattern `SignatureHelper`'s TSA client uses.
- `HandleLabelPrintRequest` (HttpListenerService.cs:499) still creates a `JobRecord` (`Source = LabelPrint`) purely for `JobsForm` visibility, even though the HTTP response itself is synchronous.

## 8. PDF signing internals

Summarized in [CLAUDE.md](../CLAUDE.md#pdf-signing-internals); the full story of *why* the signing call must use `rsa.SignData(...)` rather than manual `ComputeHash`/`SignHash` lives in [ROOT_CAUSE_ANALYSIS.md](ROOT_CAUSE_ANALYSIS.md), [QUICK_REFERENCE.md](QUICK_REFERENCE.md), and [WORKING_PDF_SIGNER_IMPLEMENTATION.md](WORKING_PDF_SIGNER_IMPLEMENTATION.md) — don't duplicate that writeup here, and don't "simplify" the signer back toward manual hashing.

## 9. Logging

- `logs/application_log.txt` (`Logger.cs`) — structured, leveled, rotates (timestamped backup) at 1MB.
- `plf.txt` (app root) — **overwritten**, not appended, single line per batch-signing run; read by the invoking ERP process. Don't change this to append-mode.
- `logs/jobs/*.json` — one `JobRecord` per job (see section 6).

## 10. Versioning

`VersionInfo.cs` derives a synthetic build number from the assembly's build date (days since 2000-01-01) and a revision from seconds-since-midnight/2 — version strings auto-increment per build. Note: `Properties/AssemblyInfo.cs`'s actual `AssemblyVersion`/`AssemblyFileVersion` attributes are hardcoded `1.0.0.0` and are **not** what's shown to users; don't expect the assembly's own version metadata (Explorer's file properties, etc.) to match what the app displays.

## 11. Forms / UI

No `.Designer.cs` files — every form builds its controls in `InitializeComponents()`.

- **`LicenseGenerationForm.cs`** (~3000+ lines) — dual-purpose: license generation (`tabLicense`, admin-only) and, via the `settingsOnly` constructor flag, all of `/settings`. `tabApiSettings` is a top-level tab (sibling of `tabSettings`, not nested inside it); `tabSettings` nests a second `tabSettingsControl` with `tabGeneral`/`tabSignature`/`tabPreview` — detailed in section 2. `tabApiSettings`'s Update Check URL field + "Check for Updates" button only exist when `!settingsOnlyMode` (admin mode) — see section 12.
- **`JobsForm.cs`** — one `DataGridView`, polls `JobTracker.Snapshot()` every second via a `Timer`; closing hides rather than disposes (singleton, keeps polling alive across show/hide). Opened by left-clicking the tray icon.
- **`VerboseProgressForm.cs`** — batch-signing-only, shown when `/verbose`/`VerboseMode` is set; `RichTextBox` log + `ProgressBar`, auto-closes when signing finishes.
- **`TrayHostForm.cs`** — invisible 0×0 form, exists only to give the tray icon's thread a window handle for `Invoke`/`BeginInvoke` marshaling.
- Tray context menus are built directly in `Program.cs` (`RunListenMode`/`RunTrayCompanionMode`, ~lines 816–1010): disabled header line, "View Job Status...", "Settings...", "Open Logs Folder", "Exit".

## 12. Update checker / self-update

Manual-only — deliberately no automatic/background check anywhere (not at listener startup, not at tray-companion startup, no timer). `AppSettings.UpdateCheckUrl` and a "Check for Updates" button (`LicenseGenerationForm.BtnCheckForUpdates_Click`) live in `tabApiSettings` but are **admin-only**: `CreateApiSettingsTab` creates `lblUpdateCheckUrl`/`txtUpdateCheckUrl`/`btnCheckForUpdates` only when `!settingsOnlyMode`. `LoadApiSettings`, `LoadDefaultApiSettings`, and both Save handlers (`BtnSaveSettings_Click`/`BtnSaveApiSettings_Click`) null-check `txtUpdateCheckUrl` before touching it; when saving from `/settings` (where the control is `null`), the existing on-disk `UpdateCheckUrl` is reloaded and kept rather than being blanked out. Deciding when/what to ship is Ten Info Tech's call, not something exposed to customers.

- Clicking the button reads whatever's currently in the `UpdateCheckUrl` textbox (not necessarily saved yet, so a URL can be tested before committing it), runs `UpdateChecker.CheckForUpdate` (`UpdateChecker.cs`) on a `ThreadPool.QueueUserWorkItem` (so the Settings form doesn't freeze), and marshals the result back via `this.BeginInvoke`.
- `CheckForUpdate` `GET`s the URL expecting `{"version", "downloadUrl", "sha256", "notes"}` and compares `version` against `VersionInfo.FullVersion` via `System.Version`. Every failure — network error, invalid JSON, unparsable version — is caught, logged at `Warning`, and returns `null`; this mirrors the fail-open convention `SignatureHelper.cs` uses for TSA/OCSP.
- Result handling on the UI thread: `null` → "Could not check for updates" warning; not newer → "You're up to date" info box; newer → `UpdateNotificationForm` with "Update Now"/"Not Now". Both buttons just close the form — there's no dismissal state to persist since there's no repeat check to suppress.
- "Update Now" fires `UpdateNotificationForm.UpdateNowClicked`, which `LicenseGenerationForm.ApplyUpdate` handles by calling `SelfUpdater.DownloadAndApply` (`SelfUpdater.cs`):
  1. Downloads the zip, computes its SHA-256, and aborts (throwing, leaving the current install untouched) if it doesn't match `manifest.Sha256` or if the manifest omitted a checksum entirely — an update is never applied unverified.
  2. Extracts to a temp staging folder (`ZipFile.ExtractToDirectory` — `System.IO.Compression`/`System.IO.Compression.FileSystem` references added to the `.csproj` for this).
  3. Writes a PowerShell helper script to `%TEMP%` and launches it; `ApplyUpdate` then calls `Application.Exit()` — **Windows won't let a running EXE overwrite its own file**, which is the entire reason this is a two-process handoff rather than a direct in-process copy.
  4. The helper script: `Wait-Process` on the old PID (60s timeout) → `robocopy` the staged files over `AppDomain.CurrentDomain.BaseDirectory` with `/E /XF` excluding every name in `SelfUpdater.ProtectedFileNames` (`license.txt`, `license.key`, `admin.license`, `IP.xml`, `appsettings.json`, `plf.txt`, `trial.lic`) and `/XD logs` → relaunches `DigiSign.exe` with **no args** (so the restarted process picks its mode from `appsettings.json`'s `EnableListenerMode`, same as any normal launch) → deletes the staging folder and itself.
- **This exclude-list is the mechanism that satisfies "reuse license on reinstall/update"** — license, signing config, print-format settings, and trial state all live in files on that list, so they survive every update. A future installer (section 13) must use the identical list — don't let the two drift apart if either changes.
- Verified in practice: a local test manifest correctly triggered the notification, and the exclude-list was directly verified against `robocopy` — protected files were provably untouched while a stand-in binary was replaced.

## 13. Build & publish

- SDK-style `.csproj`, `net472`, `WinExe`, WinForms enabled. Key packages: `iTextSharp 5.5.13.4`, `BouncyCastle.Cryptography 2.4.0`, `PDFtoImage 5.2.1` + `SkiaSharp 3.119.2` (PDF rendering for printing), `Pkcs11Interop 5.3.0`, `Newtonsoft.Json 13.0.3`, `System.Security.Cryptography.Pkcs`/`Microsoft.Bcl.Cryptography` polyfills, `System.Management` (GAC, not NuGet — device fingerprinting).
- No ILMerge/single-file publish/obfuscation. Leftover legacy ClickOnce settings (`PublishUrl`, `Install`, `ApplicationVersion`) in the `.csproj` are unused — see [PUBLISHING.md](PUBLISHING.md) for the current (Inno Setup–based) release process instead (not yet written — the installer itself is still deferred; when it's built, it must exclude the same file list as `SelfUpdater.ProtectedFileNames`, section 12).
- `MockDocumentServer/` and `AdminKeyTester.cs`/`AdminLicenseValidator.cs` are excluded from the main build (`<Compile Remove>`) — standalone dev/ops tools, not shipped.
