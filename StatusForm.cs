using System;
using System.Drawing;
using System.Windows.Forms;

namespace DigiSign
{
    public class StatusForm : Form
    {
        private ListView listView;
        private Timer refreshTimer;

        public StatusForm()
        {
            InitializeComponents();
            RefreshJobs();
        }

        private void InitializeComponents()
        {
            this.Text = $"{VersionInfo.TitleWithVersion} - Status";
            this.ClientSize = new Size(950, 500);
            this.MinimumSize = new Size(700, 300);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = TrayIconLoader.LoadFromEmbeddedPng("DigiSign.singer_icon.png");
            this.FormClosing += StatusForm_FormClosing;

            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9)
            };
            listView.Columns.Add("Time", 130);
            listView.Columns.Add("Route", 110);
            listView.Columns.Add("Token", 100);
            listView.Columns.Add("Doc Type", 70);
            listView.Columns.Add("Stage", 100);
            listView.Columns.Add("Progress", 220);
            listView.Columns.Add("Result", 60);
            listView.Columns.Add("Callback", 100);
            listView.Columns.Add("Output / Error", 250);
            this.Controls.Add(listView);

            refreshTimer = new Timer { Interval = 1000 };
            refreshTimer.Tick += (s, e) => RefreshJobs();
            refreshTimer.Start();
        }

        private void StatusForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void RefreshJobs()
        {
            var jobs = JobTracker.Snapshot();

            listView.BeginUpdate();
            listView.Items.Clear();
            foreach (var job in jobs)
            {
                string resultText = job.Success == null ? "..." : (job.Success.Value ? "OK" : "FAILED");
                string outputOrError = job.Success == false ? job.ErrorMessage : job.OutputPath;
                string callbackText = job.CallbackSuccess == null ? "-" : (job.CallbackSuccess.Value ? "OK" : $"FAILED: {job.CallbackMessage}");

                var item = new ListViewItem(job.StartedAtUtc.ToLocalTime().ToString("HH:mm:ss"));
                item.SubItems.Add(job.Route);
                item.SubItems.Add(job.Token);
                item.SubItems.Add(job.DocumentType ?? "");
                item.SubItems.Add(job.Stage.ToString());
                item.SubItems.Add(job.ProgressDetail ?? "");
                item.SubItems.Add(resultText);
                item.SubItems.Add(callbackText);
                item.SubItems.Add(outputOrError ?? "");

                if (job.Success == false)
                    item.ForeColor = Color.Red;
                else if (job.Success == true)
                    item.ForeColor = Color.Green;

                listView.Items.Add(item);
            }
            listView.EndUpdate();
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
