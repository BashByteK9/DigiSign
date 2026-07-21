using System;
using System.Collections.Generic;
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
            this.ClientSize = new Size(1080, 500);
            this.MinimumSize = new Size(820, 300);
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
            AddTextColumn("RouteOrFile", 130, "Route / File");
            AddTextColumn("TokenOrFile", 110, "Token / File");
            AddTextColumn("DocType", 65, "Doc Type");
            AddTextColumn("Stage", 90);
            AddTextColumn("Progress", 190);
            AddTextColumn("Result", 90);
            AddTextColumn("Callback", 100);
            AddTextColumn("OutputOrError", 200, "Output / Error");

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

                JobTracker.ResumeJob(jobId);
                optimisticallyDisabledResume.Add(jobId);
                RefreshJobs();
            }
        }

        private void RefreshJobs()
        {
            var jobs = JobTracker.Snapshot();

            string selectedJobId = grid.CurrentRow?.Tag as string;
            string topVisibleJobId = grid.Rows.Count > 0 && grid.FirstDisplayedScrollingRowIndex >= 0
                ? grid.Rows[grid.FirstDisplayedScrollingRowIndex].Tag as string
                : null;

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

                int rowIndex = grid.Rows.Add(
                    job.StartedAtUtc.ToLocalTime().ToString("HH:mm:ss"),
                    job.Source.ToString(),
                    routeOrFile,
                    tokenOrFile ?? "",
                    job.DocumentType ?? "",
                    job.Stage.ToString(),
                    job.ProgressDetail ?? "",
                    resultText,
                    callbackText,
                    outputOrError ?? "",
                    "",
                    "");

                var row = grid.Rows[rowIndex];
                row.Tag = job.JobId;

                ApplyRowColor(row, job);

                bool resumeEnabled = job.IsResumable && !optimisticallyDisabledResume.Contains(job.JobId);
                bool cancelEnabled = job.IsCancelable && !optimisticallyDisabledCancel.Contains(job.JobId);
                StyleButtonCell(row.Cells["Resume"], resumeEnabled);
                StyleButtonCell(row.Cells["Cancel"], cancelEnabled);
            }

            RestoreSelectionAndScroll(selectedJobId, topVisibleJobId);

            grid.ResumeLayout();
        }

        private void RestoreSelectionAndScroll(string selectedJobId, string topVisibleJobId)
        {
            if (selectedJobId != null)
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if ((string)row.Tag == selectedJobId)
                    {
                        grid.CurrentCell = row.Cells[0];
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
        }

        private static string ResultText(JobRecord job)
        {
            if (job.Stage == JobStage.Interrupted) return "INTERRUPTED";
            if (job.Stage == JobStage.Cancelled) return "CANCELLED";
            if (job.Success == null) return "...";
            return job.Success.Value ? "OK" : "FAILED";
        }

        private static void ApplyRowColor(DataGridViewRow row, JobRecord job)
        {
            // Stage-based checks take priority over the Success-based fallback, since an Interrupted
            // or Cancelled job may still have Success == null (it never got the chance to complete).
            if (job.Stage == JobStage.Interrupted)
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
