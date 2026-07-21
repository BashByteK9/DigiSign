using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DigiSign
{
    /// <summary>
    /// Shows recent/active jobs from both listener mode (per-token) and batch-signing mode (per-file),
    /// with per-row Resume/Cancel buttons. Opened from either the listener's or the tray companion's
    /// tray icon - reused as a singleton across clicks, hidden (not closed) on the window's X button so
    /// its polling continues in the background for the life of the process.
    /// </summary>
    public class JobsForm : Form
    {
        private DataGridView grid;
        private Timer refreshTimer;

        // Transient, click-scoped visual accelerant only - the real source of truth is always the
        // fresh JobTracker.Snapshot() read on the next tick; these just mask the ~1s poll gap so a
        // button doesn't look clickable for a moment after it's already been acted on.
        private readonly HashSet<string> optimisticallyDisabledResume = new HashSet<string>();
        private readonly HashSet<string> optimisticallyDisabledCancel = new HashSet<string>();

        public JobsForm()
        {
            InitializeComponents();
            RefreshJobs();
        }

        private void InitializeComponents()
        {
            this.Text = $"{VersionInfo.TitleWithVersion} - Jobs";
            // Wide/tall enough (and MinimumSize floored the same) that all columns - including the
            // rightmost Resume/Cancel buttons - are visible without horizontal scrolling, and the
            // window can't be shrunk back down into needing it either.
            this.ClientSize = new Size(1220, 500);
            this.MinimumSize = new Size(1220, 300);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = TrayIconLoader.LoadFromEmbeddedPng("DigiSign.singer_icon.png");
            this.FormClosing += JobsForm_FormClosing;

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                BackgroundColor = SystemColors.Window,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };

            AddTextColumn("Time", 100);
            AddTextColumn("Source", 65);
            AddTextColumn("RouteOrFile", 110, "Route / File");
            AddTextColumn("TokenOrFile", 100, "Token / File");
            AddTextColumn("DocType", 65, "Doc Type");
            AddTextColumn("Stage", 90);
            AddTextColumn("Progress", 150);
            AddTextColumn("Result", 85);
            AddTextColumn("Callback", 90);
            AddTextColumn("OutputOrError", 170, "Output / Error");

            grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "Resume",
                HeaderText = "",
                Text = "Resume",
                UseColumnTextForButtonValue = true,
                Width = 70
            });
            grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "Cancel",
                HeaderText = "",
                Text = "Cancel",
                UseColumnTextForButtonValue = true,
                Width = 70
            });

            grid.CellContentClick += Grid_CellContentClick;
            grid.CellClick += Grid_CellClick;
            grid.CellMouseEnter += Grid_CellMouseEnter;
            grid.CellMouseLeave += Grid_CellMouseLeave;
            this.Controls.Add(grid);

            refreshTimer = new Timer { Interval = 1000 };
            refreshTimer.Tick += (s, e) => RefreshJobs();
            refreshTimer.Start();
        }

        private void AddTextColumn(string name, int width, string headerText = null)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = headerText ?? name,
                Width = width,
                ReadOnly = true
            });
        }

        private void JobsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            string jobId = grid.Rows[e.RowIndex].Tag as string;
            if (jobId == null)
                return;

            string columnName = grid.Columns[e.ColumnIndex].Name;

            if (columnName == "Cancel")
            {
                // Always re-check live state rather than trusting the cell's current visual style -
                // that style can lag the real state by up to one poll tick.
                var job = JobTracker.GetJob(jobId);
                if (job == null || !job.IsCancelable)
                    return;

                var confirm = MessageBox.Show(
                    "Cancel this job? It will stop before its next step - a step already in progress (signing/printing) will finish first.",
                    "Cancel Job",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                    return;

                JobTracker.RequestCancel(jobId);
                optimisticallyDisabledCancel.Add(jobId);
                RefreshJobs();
            }
            else if (columnName == "Resume")
            {
                var job = JobTracker.GetJob(jobId);
                if (job == null || !job.IsResumable)
                    return;

                // Only mask the button optimistically when the resume actually started - on
                // NotResumable/AlreadyRunning/NotFound nothing changed, and adding it to the set
                // anyway would leave the button stuck disabled forever (it only clears once
                // !job.IsResumable, which a no-op resume never causes).
                var outcome = JobTracker.ResumeJob(jobId);
                if (outcome == ResumeOutcome.Started)
                    optimisticallyDisabledResume.Add(jobId);
                RefreshJobs();
            }
        }

        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (grid.Columns[e.ColumnIndex].Name != "OutputOrError")
                return;

            string path = grid.Rows[e.RowIndex].Cells["OutputOrError"].Tag as string;
            if (string.IsNullOrEmpty(path))
                return;

            if (!File.Exists(path))
            {
                MessageBox.Show($"Output file no longer exists:\n{path}", "File Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not open output folder for '{path}': {ex.Message}");
                MessageBox.Show($"Could not open the output folder:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Grid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string path = grid.Columns[e.ColumnIndex].Name == "OutputOrError"
                ? grid.Rows[e.RowIndex].Cells["OutputOrError"].Tag as string
                : null;
            if (!string.IsNullOrEmpty(path))
            {
                grid.Cursor = Cursors.Hand;
            }
        }

        private void Grid_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            grid.Cursor = Cursors.Default;
        }

        private void RefreshJobs()
        {
            var jobs = JobTracker.Snapshot();

            string selectedJobId = grid.CurrentRow?.Tag as string;
            string topVisibleJobId = grid.Rows.Count > 0 && grid.FirstDisplayedScrollingRowIndex >= 0
                ? grid.Rows[grid.FirstDisplayedScrollingRowIndex].Tag as string
                : null;
            // Horizontal scroll/focus previously weren't captured here at all, so restoring the
            // selected row's CurrentCell below (always column 0) forced the grid back to the leftmost
            // column on every 1s refresh - this preserves both instead.
            int focusedColumnIndex = grid.CurrentCell != null ? grid.CurrentCell.ColumnIndex : 0;
            int firstDisplayedColumnIndex = grid.Rows.Count > 0 ? grid.FirstDisplayedScrollingColumnIndex : -1;

            grid.SuspendLayout();
            grid.Rows.Clear();

            foreach (var job in jobs)
            {
                // Drop stale optimistic entries once the real state agrees - keeps the set from growing
                // unbounded and lets a job become actionable again later (e.g. Resumed, then Failed again).
                if (!job.IsResumable)
                    optimisticallyDisabledResume.Remove(job.JobId);
                if (!job.IsCancelable)
                    optimisticallyDisabledCancel.Remove(job.JobId);

                string routeOrFile = job.Source == JobSource.Batch ? "(batch)" : (job.Route ?? "");
                string tokenOrFile = job.Source == JobSource.Batch
                    ? (job.FileName ?? (string.IsNullOrEmpty(job.InputPath) ? "" : Path.GetFileName(job.InputPath)))
                    : job.Token;
                string resultText = ResultText(job);
                string outputOrError = job.Success == false ? job.ErrorMessage : job.OutputPath;
                string callbackText = job.CallbackSuccess == null ? "-" : (job.CallbackSuccess.Value ? "OK" : $"FAILED: {job.CallbackMessage}");
                string progressText = job.ProgressDetail ?? "";

                int rowIndex = grid.Rows.Add(
                    job.StartedAtUtc.ToLocalTime().ToString("HH:mm:ss"),
                    job.Source.ToString(),
                    routeOrFile,
                    tokenOrFile ?? "",
                    job.DocumentType ?? "",
                    job.Stage.ToString(),
                    progressText,
                    resultText,
                    callbackText,
                    outputOrError ?? "",
                    "",
                    "");

                var row = grid.Rows[rowIndex];
                row.Tag = job.JobId;

                // Columns narrowed in this pass (Progress/Result/Callback/Output-Error) can truncate -
                // a tooltip keeps the full text reachable via hover instead of losing it outright.
                row.Cells["Progress"].ToolTipText = progressText;
                row.Cells["Result"].ToolTipText = resultText;
                row.Cells["Callback"].ToolTipText = callbackText;
                row.Cells["OutputOrError"].ToolTipText = outputOrError ?? "";

                // Only a successful job's OutputOrError cell holds an openable file path - style it
                // like a link and stash the path for Grid_CellClick/Grid_CellMouseEnter to use.
                bool hasOutputPath = job.Success == true && !string.IsNullOrEmpty(job.OutputPath);
                row.Cells["OutputOrError"].Tag = hasOutputPath ? job.OutputPath : null;
                if (hasOutputPath)
                {
                    row.Cells["OutputOrError"].Style.ForeColor = Color.Blue;
                    row.Cells["OutputOrError"].Style.Font = new Font(grid.Font, FontStyle.Underline);
                }

                ApplyRowColor(row, job);

                bool resumeEnabled = job.IsResumable && !optimisticallyDisabledResume.Contains(job.JobId);
                bool cancelEnabled = job.IsCancelable && !optimisticallyDisabledCancel.Contains(job.JobId);
                StyleButtonCell(row.Cells["Resume"], resumeEnabled);
                StyleButtonCell(row.Cells["Cancel"], cancelEnabled);
            }

            RestoreSelectionAndScroll(selectedJobId, topVisibleJobId, focusedColumnIndex, firstDisplayedColumnIndex);

            grid.ResumeLayout();
        }

        private void RestoreSelectionAndScroll(string selectedJobId, string topVisibleJobId, int focusedColumnIndex, int firstDisplayedColumnIndex)
        {
            if (selectedJobId != null)
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if ((string)row.Tag == selectedJobId)
                    {
                        int columnIndex = Math.Min(Math.Max(focusedColumnIndex, 0), row.Cells.Count - 1);
                        grid.CurrentCell = row.Cells[columnIndex];
                        break;
                    }
                }
            }

            if (topVisibleJobId != null)
            {
                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    if ((string)grid.Rows[i].Tag == topVisibleJobId)
                    {
                        grid.FirstDisplayedScrollingRowIndex = i;
                        break;
                    }
                }
            }

            // Setting CurrentCell above can itself auto-scroll the grid to bring that cell into view,
            // which would undo horizontal position restoration if it ran first - so this runs last to
            // have the final say on horizontal scroll position.
            if (firstDisplayedColumnIndex >= 0 && firstDisplayedColumnIndex < grid.Columns.Count && grid.Rows.Count > 0)
            {
                grid.FirstDisplayedScrollingColumnIndex = firstDisplayedColumnIndex;
            }
        }

        private static string ResultText(JobRecord job)
        {
            // Checked first: Stage/Success reflect the last terminal outcome and don't move until the
            // background resume actually reaches a checkpoint, so without this a resuming job kept
            // showing its old "CANCELLED"/"FAILED" result throughout the resume.
            if (job.IsActive) return "RESUMING...";
            if (job.CancellationRequested && job.Stage != JobStage.Cancelled) return "CANCELLING...";
            if (job.Stage == JobStage.Interrupted) return "INTERRUPTED";
            if (job.Stage == JobStage.Cancelled) return "CANCELLED";
            if (job.Success == null) return "...";
            return job.Success.Value ? "OK" : "FAILED";
        }

        private static void ApplyRowColor(DataGridViewRow row, JobRecord job)
        {
            // Same priority as ResultText above - an in-flight resume/cancel takes precedence over
            // whatever stale terminal Stage/Success the record still shows.
            if (job.IsActive)
                row.DefaultCellStyle.ForeColor = Color.DodgerBlue;
            else if (job.CancellationRequested && job.Stage != JobStage.Cancelled)
                row.DefaultCellStyle.ForeColor = Color.DarkGoldenrod;
            else if (job.Stage == JobStage.Interrupted)
                row.DefaultCellStyle.ForeColor = Color.DarkOrange;
            else if (job.Stage == JobStage.Cancelled)
                row.DefaultCellStyle.ForeColor = Color.Gray;
            else if (job.Success == false)
                row.DefaultCellStyle.ForeColor = Color.Red;
            else if (job.Success == true)
                row.DefaultCellStyle.ForeColor = Color.Green;
        }

        private static void StyleButtonCell(DataGridViewCell cell, bool enabled)
        {
            // ReadOnly is a best-effort visual/functional guard only - DataGridView's handling of
            // ReadOnly on button cells is inconsistent across framework versions, so the click handler
            // above always re-checks live job state rather than trusting this.
            cell.ReadOnly = !enabled;
            cell.Style.ForeColor = enabled ? SystemColors.ControlText : SystemColors.GrayText;
            cell.Style.BackColor = enabled ? SystemColors.Control : SystemColors.ButtonFace;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer?.Stop();
                refreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
