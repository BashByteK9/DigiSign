using System;
using System.Drawing;
using System.Windows.Forms;

namespace DigiSign
{
    /// <summary>
    /// Small non-modal "a new version is available" popup shown on listener/tray-companion
    /// startup when UpdateChecker finds a newer version. Deliberately has no persisted dismissal:
    /// "Not Now" just closes this instance - the check re-runs and the form reappears on the next
    /// launch if the install is still out of date.
    /// </summary>
    public class UpdateNotificationForm : Form
    {
        public event Action UpdateNowClicked;

        private readonly UpdateManifest manifest;

        public UpdateNotificationForm(UpdateManifest manifest)
        {
            this.manifest = manifest;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "DigiSign Update Available";
            this.ClientSize = new Size(420, 230);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = true;
            this.TopMost = true;
            this.Icon = TrayIconLoader.LoadFromEmbeddedPng("DigiSign.singer_icon.png");

            int margin = 15;
            int currentY = margin;

            var lblTitle = new Label
            {
                Text = $"A new version is available: v{manifest.Version}",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                Location = new Point(margin, currentY),
                Size = new Size(this.ClientSize.Width - 2 * margin, 25)
            };
            this.Controls.Add(lblTitle);
            currentY += 32;

            var lblCurrent = new Label
            {
                Text = $"Currently installed: v{VersionInfo.ShortVersion}",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.DimGray,
                Location = new Point(margin, currentY),
                Size = new Size(this.ClientSize.Width - 2 * margin, 20)
            };
            this.Controls.Add(lblCurrent);
            currentY += 30;

            var txtNotes = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(manifest.Notes) ? "(no release notes provided)" : manifest.Notes,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9),
                Location = new Point(margin, currentY),
                Size = new Size(this.ClientSize.Width - 2 * margin, 85)
            };
            this.Controls.Add(txtNotes);
            currentY += 95;

            var btnNotNow = new Button
            {
                Text = "Not Now",
                Size = new Size(100, 32),
                Location = new Point(this.ClientSize.Width - margin - 100, currentY),
                Font = new Font("Segoe UI", 9)
            };
            btnNotNow.Click += (s, e) => Close();
            this.Controls.Add(btnNotNow);

            var btnUpdateNow = new Button
            {
                Text = "Update Now",
                Size = new Size(120, 32),
                Location = new Point(this.ClientSize.Width - margin - 100 - 10 - 120, currentY),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnUpdateNow.FlatAppearance.BorderSize = 0;
            btnUpdateNow.Click += (s, e) =>
            {
                UpdateNowClicked?.Invoke();
                Close();
            };
            this.Controls.Add(btnUpdateNow);
        }
    }
}
