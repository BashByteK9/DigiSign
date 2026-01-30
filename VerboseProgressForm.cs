using System;
using System.Drawing;
using System.Windows.Forms;
using System.Text;

namespace DigiSign
{
    public class VerboseProgressForm : Form
    {
        private RichTextBox txtProgress;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Label lblCurrentStep;
        private Button btnClose;
        private Button btnWait;
        private Timer autoCloseTimer;
        private int autoCloseCountdown = 2;
        private bool shouldAutoClose = false;
        private bool hasErrors = false;

        public VerboseProgressForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = $"{VersionInfo.TitleWithVersion} - Verbose Progress";
            this.ClientSize = new Size(900, 700);
            this.MinimumSize = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Icon = SystemIcons.Application;
            this.FormClosing += VerboseProgressForm_FormClosing;

            int margin = 15;
            int currentY = margin;

            // Title/Current Step Label
            lblCurrentStep = new Label
            {
                Text = "Initializing...",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(margin, currentY),
                Size = new Size(this.ClientSize.Width - (2 * margin), 30),
                ForeColor = Color.FromArgb(0, 102, 204),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblCurrentStep);
            currentY += 40;

            // Progress Bar
            progressBar = new ProgressBar
            {
                Location = new Point(margin, currentY),
                Size = new Size(this.ClientSize.Width - (2 * margin), 25),
                Minimum = 0,
                Maximum = 10,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(progressBar);
            currentY += 35;

            // Status Label
            lblStatus = new Label
            {
                Text = "Starting...",
                Font = new Font("Segoe UI", 9),
                Location = new Point(margin, currentY),
                Size = new Size(this.ClientSize.Width - (2 * margin), 20),
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblStatus);
            currentY += 30;

            // Rich Text Box for detailed output
            txtProgress = new RichTextBox
            {
                Location = new Point(margin, currentY),
                Size = new Size(this.ClientSize.Width - (2 * margin), this.ClientSize.Height - currentY - 70),
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                BackColor = Color.FromArgb(240, 240, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };
            this.Controls.Add(txtProgress);

            // Close Button
            btnClose = new Button
            {
                Text = "Close",
                Size = new Size(100, 35),
                Font = new Font("Segoe UI", 10),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false
            };
            btnClose.Location = new Point(this.ClientSize.Width - margin - btnClose.Width, this.ClientSize.Height - margin - btnClose.Height);
            btnClose.Click += BtnClose_Click;
            this.Controls.Add(btnClose);

            // Wait/Stop Button - prevents auto-close
            btnWait = new Button
            {
                Text = "Wait",
                Size = new Size(100, 35),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false,
                BackColor = Color.FromArgb(255, 193, 7),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            btnWait.FlatAppearance.BorderSize = 0;
            btnWait.Location = new Point(btnClose.Left - 10 - btnWait.Width, this.ClientSize.Height - margin - btnWait.Height);
            btnWait.Click += BtnWait_Click;
            this.Controls.Add(btnWait);

            // Auto-close timer
            autoCloseTimer = new Timer();
            autoCloseTimer.Interval = 1000; // 1 second
            autoCloseTimer.Tick += AutoCloseTimer_Tick;
        }

        private void VerboseProgressForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Only allow closing if processing is complete or user clicks X
            if (!btnClose.Enabled && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BtnWait_Click(object sender, EventArgs e)
        {
            // Stop auto-close timer
            if (autoCloseTimer.Enabled)
            {
                autoCloseTimer.Stop();
                shouldAutoClose = false;
                btnWait.Enabled = false;
                btnWait.Text = "Stopped";
                btnWait.BackColor = Color.Gray;
                UpdateStatus("Auto-close cancelled - Click Close to exit");
                AppendText("\nAuto-close cancelled by user.\n", Color.Orange);
            }
        }

        private void AutoCloseTimer_Tick(object sender, EventArgs e)
        {
            autoCloseCountdown--;
            
            // Update button text with countdown
            if (btnWait.Enabled)
            {
                btnWait.Text = $"Stop [{autoCloseCountdown}]";
            }
            
            // Update status label
            UpdateStatus($"Auto-closing in {autoCloseCountdown} second{(autoCloseCountdown > 1 ? "s" : "")}...");
            
            if (autoCloseCountdown <= 0)
            {
                autoCloseTimer.Stop();
                this.Invoke(new Action(() => this.Close()));
            }
        }

        public void UpdateProgress(int step, string stepName)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateProgress(step, stepName)));
                return;
            }

            progressBar.Value = Math.Min(step, progressBar.Maximum);
            lblCurrentStep.Text = $"[{step}/10] {stepName}";
            AppendText($"\n[{step}/10] {stepName}\n", Color.FromArgb(0, 102, 204), true);
        }

        public void UpdateStatus(string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateStatus(status)));
                return;
            }

            lblStatus.Text = status;
        }

        public void AppendText(string text, Color? color = null, bool bold = false)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendText(text, color, bold)));
                return;
            }

            txtProgress.SelectionStart = txtProgress.TextLength;
            txtProgress.SelectionLength = 0;
            txtProgress.SelectionColor = color ?? Color.Black;
            txtProgress.SelectionFont = new Font(txtProgress.Font, bold ? FontStyle.Bold : FontStyle.Regular);
            txtProgress.AppendText(text);
            txtProgress.SelectionColor = txtProgress.ForeColor;
            txtProgress.ScrollToCaret();
        }

        public void AppendSuccess(string text)
        {
            AppendText("        \u2713 " + text + "\n", Color.Green); // ? checkmark
        }

        public void AppendError(string text)
        {
            AppendText("        \u2717 " + text + "\n", Color.Red); // ? cross mark
        }

        public void AppendWarning(string text)
        {
            AppendText("        \u26A0 " + text + "\n", Color.Orange); // ? warning sign
        }

        public void AppendInfo(string text)
        {
            AppendText("        \u2022 " + text + "\n", Color.Black); // • bullet point
        }

        public void AppendDetail(string text)
        {
            AppendText("          \u2192 " + text + "\n", Color.Gray); // ? arrow
        }

        public void ShowSummary(int successCount, int failCount)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowSummary(successCount, failCount)));
                return;
            }

            // Track if there are errors
            hasErrors = failCount > 0;

            AppendText("\n" + new string('\u2550', 50) + "\n", Color.Gray, true); // ? double horizontal line
            AppendText("SUMMARY:\n", Color.Black, true);
            AppendText($"  \u2713 Successful: {successCount}\n", Color.Green, true); // ? checkmark
            if (failCount > 0)
            {
                AppendText($"  \u2717 Failed: {failCount}\n", Color.Red, true); // ? cross mark
            }
            AppendText(new string('\u2550', 50) + "\n", Color.Gray, true); // ? double horizontal line
        }

        public void ProcessingComplete(bool autoClose = true, int errorCount = 0)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ProcessingComplete(autoClose, errorCount)));
                return;
            }

            // Set hasErrors based on errorCount parameter (includes ALL errors: validation, signing, etc.)
            hasErrors = errorCount > 0;
            
            shouldAutoClose = autoClose;
            btnClose.Enabled = true;
            progressBar.Value = progressBar.Maximum;
            lblCurrentStep.Text = "Processing Complete";
            lblCurrentStep.ForeColor = hasErrors ? Color.Orange : Color.Green;

            if (shouldAutoClose)
            {
                // Use 15 seconds if there are errors, 2 seconds for success
                autoCloseCountdown = hasErrors ? 15 : 2;
                
                
                btnWait.Enabled = true; // Enable Stop button
                btnWait.Text = $"Stop [{autoCloseCountdown}]"; // Show initial countdown
                
                if (hasErrors)
                {
                    AppendText($"\n\u26A0 Errors detected ({errorCount} error{(errorCount > 1 ? "s" : "")}) - Auto-closing in {autoCloseCountdown} seconds...\n", Color.Orange); // ? warning
                    AppendText("Click 'Stop' button to prevent auto-close.\n", Color.Gray);
                }
                else
                {
                    AppendText($"\nAuto-closing in {autoCloseCountdown} seconds...\n", Color.Gray);
                    AppendText("Click 'Stop' button to prevent auto-close.\n", Color.Gray);
                }
                
                autoCloseTimer.Start();
            }
            else
            {
                UpdateStatus("Complete - Click Close to exit");
            }
        }

        public void ProcessingFailed(string error)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ProcessingFailed(error)));
                return;
            }

            btnClose.Enabled = true;
            lblCurrentStep.Text = "Processing Failed";
            lblCurrentStep.ForeColor = Color.Red;
            UpdateStatus(error);
            AppendError($"FATAL ERROR: {error}");
        }
    }
}
