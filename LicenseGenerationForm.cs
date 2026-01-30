using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace DigiSign
{
    public class LicenseGenerationForm : Form
    {
        // Form controls
        private Label lblTitle;
        private Label lblLicenseKeyPath;
        private TextBox txtLicenseKeyPath;
        private Button btnBrowse;
        private Label lblDeviceInfo;
        private TextBox txtDeviceInfo;
        private Label lblCustomerId;
        private TextBox txtCustomerId;
        private Label lblLicenseNumber;
        private TextBox txtLicenseNumber;
        private Label lblExpirationDate;
        private DateTimePicker dtpExpirationDate;
        private Button btnGenerate;
        private Button btnCancel;

        // Public properties
        public string LicenseKeyPath { get; private set; }
        public string CustomerId { get; private set; }
        public string LicenseNumber { get; private set; }
        public DateTime ExpirationDate { get; private set; }
        public bool WasCancelled { get; private set; }

        public LicenseGenerationForm()
        {
            InitializeComponents();
            WasCancelled = true; // Default to cancelled unless user clicks Generate
        }

        private void InitializeComponents()
        {
            // Form settings - LARGER and RESIZABLE
            this.Text = "DigiSign - Admin License Generation";
            this.ClientSize = new Size(750, 650);
            this.MinimumSize = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;  // RESIZABLE
            this.MaximizeBox = true;  // CAN MAXIMIZE
            this.MinimizeBox = true;
            this.Icon = SystemIcons.Application;
            this.AutoScroll = true;  // Auto scroll if needed

            int leftMargin = 25;
            int rightMargin = 25;
            int topMargin = 25;
            int bottomMargin = 70;
            int labelHeight = 22;
            int textBoxHeight = 26;
            int spacing = 12;
            int currentY = topMargin;

            // Calculate control width dynamically
            int GetControlWidth() => this.ClientSize.Width - leftMargin - rightMargin;

            // Title
            lblTitle = new Label
            {
                Text = "Generate User License",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), 35),
                ForeColor = Color.FromArgb(0, 102, 204),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblTitle);
            currentY += 45;

            // Separator
            Panel separator1 = new Panel
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), 2),
                BackColor = Color.FromArgb(200, 200, 200),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(separator1);
            currentY += spacing + 8;

            // License Key Path
            lblLicenseKeyPath = new Label
            {
                Text = "License Key File (*.key):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblLicenseKeyPath);
            currentY += labelHeight + 6;

            btnBrowse = new Button
            {
                Text = "Browse...",
                Size = new Size(100, textBoxHeight + 2),
                Font = new Font("Segoe UI", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnBrowse.Click += BtnBrowse_Click;
            this.Controls.Add(btnBrowse);

            txtLicenseKeyPath = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth() - 110, textBoxHeight),
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(txtLicenseKeyPath);

            // Position browse button next to textbox
            btnBrowse.Location = new Point(this.ClientSize.Width - rightMargin - 100, currentY - 1);
            currentY += textBoxHeight + spacing + 12;

            // Device Info (read-only display)
            lblDeviceInfo = new Label
            {
                Text = "Device Information:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblDeviceInfo);
            currentY += labelHeight + 6;

            txtDeviceInfo = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), 80),
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 245, 245),
                Font = new Font("Courier New", 9),
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(txtDeviceInfo);
            currentY += 92;

            // Separator
            Panel separator2 = new Panel
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), 2),
                BackColor = Color.FromArgb(200, 200, 200),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(separator2);
            currentY += spacing + 8;

            // Customer ID
            lblCustomerId = new Label
            {
                Text = "Customer ID: *",
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblCustomerId);
            currentY += labelHeight + 6;

            txtCustomerId = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), textBoxHeight),
                Font = new Font("Segoe UI", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtCustomerId.TextChanged += ValidateForm;
            this.Controls.Add(txtCustomerId);
            currentY += textBoxHeight + spacing;

            // License Number
            lblLicenseNumber = new Label
            {
                Text = "License Number: *",
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblLicenseNumber);
            currentY += labelHeight + 6;

            txtLicenseNumber = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), textBoxHeight),
                Font = new Font("Segoe UI", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtLicenseNumber.TextChanged += ValidateForm;
            this.Controls.Add(txtLicenseNumber);
            currentY += textBoxHeight + spacing;

            // Expiration Date
            lblExpirationDate = new Label
            {
                Text = "Expiration Date: *",
                Location = new Point(leftMargin, currentY),
                Size = new Size(GetControlWidth(), labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblExpirationDate);
            currentY += labelHeight + 6;

            dtpExpirationDate = new DateTimePicker
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(320, textBoxHeight),
                Format = DateTimePickerFormat.Long,
                MinDate = DateTime.Now.AddDays(1),
                Value = DateTime.Now.AddYears(1),
                Font = new Font("Segoe UI", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            dtpExpirationDate.ValueChanged += ValidateForm;
            this.Controls.Add(dtpExpirationDate);

            // Buttons - Fixed at bottom
            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(110, 40),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 10),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                TabIndex = 5
            };
            btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(btnCancel);

            btnGenerate = new Button
            {
                Text = "Generate License",
                Size = new Size(150, 40),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                TabIndex = 6
            };
            btnGenerate.FlatAppearance.BorderSize = 0;
            btnGenerate.Click += BtnGenerate_Click;
            this.Controls.Add(btnGenerate);

            // Position buttons at bottom
            PositionButtons();
            this.Resize += (s, e) => PositionButtons();

            this.CancelButton = btnCancel;
            this.AcceptButton = btnGenerate;
        }

        private void PositionButtons()
        {
            int rightMargin = 25;
            int bottomMargin = 25;

            btnGenerate.Location = new Point(
                this.ClientSize.Width - rightMargin - btnGenerate.Width,
                this.ClientSize.Height - bottomMargin - btnGenerate.Height
            );

            btnCancel.Location = new Point(
                btnGenerate.Left - 10 - btnCancel.Width,
                this.ClientSize.Height - bottomMargin - btnCancel.Height
            );
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "Select license.key File";
                    dialog.Filter = "License Key Files (*.key)|*.key|All Files (*.*)|*.*";
                    dialog.DefaultExt = "key";
                    dialog.FileName = "license.key";
                    dialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    dialog.CheckFileExists = true;
                    dialog.CheckPathExists = true;

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        txtLicenseKeyPath.Text = dialog.FileName;
                        LoadDeviceInfoFromKey(dialog.FileName);
                        ValidateForm(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error selecting file: {ex.Message}",
                    "File Selection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void LoadDeviceInfoFromKey(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    txtDeviceInfo.Text = "Error: File not found.";
                    return;
                }

                var keyData = new System.Collections.Generic.Dictionary<string, string>();
                var lines = File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        keyData[parts[0].Trim()] = parts[1].Trim();
                    }
                }

                if (!keyData.ContainsKey("DeviceID"))
                {
                    txtDeviceInfo.Text = "Error: Invalid license.key file - missing DeviceID.";
                    return;
                }

                string deviceId = keyData["DeviceID"];
                string machineName = keyData.ContainsKey("MachineName") ? keyData["MachineName"] : "Unknown";
                string userName = keyData.ContainsKey("UserName") ? keyData["UserName"] : "Unknown";
                string generatedOn = keyData.ContainsKey("GeneratedOn") ? keyData["GeneratedOn"] : "Unknown";

                txtDeviceInfo.Text = $"Device ID: {deviceId}\r\n" +
                                   $"Machine Name: {machineName}\r\n" +
                                   $"User Name: {userName}\r\n" +
                                   $"Generated On: {generatedOn}";
            }
            catch (Exception ex)
            {
                txtDeviceInfo.Text = $"Error reading file: {ex.Message}";
            }
        }

        private void ValidateForm(object sender, EventArgs e)
        {
            bool isValid = !string.IsNullOrWhiteSpace(txtLicenseKeyPath.Text) &&
                          File.Exists(txtLicenseKeyPath.Text) &&
                          !string.IsNullOrWhiteSpace(txtCustomerId.Text) &&
                          !string.IsNullOrWhiteSpace(txtLicenseNumber.Text) &&
                          dtpExpirationDate.Value > DateTime.Now;

            btnGenerate.Enabled = isValid;
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            // Validate inputs one more time
            if (string.IsNullOrWhiteSpace(txtCustomerId.Text))
            {
                MessageBox.Show("Customer ID is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCustomerId.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtLicenseNumber.Text))
            {
                MessageBox.Show("License Number is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLicenseNumber.Focus();
                return;
            }

            if (dtpExpirationDate.Value <= DateTime.Now)
            {
                MessageBox.Show("Expiration date must be in the future.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dtpExpirationDate.Focus();
                return;
            }

            if (!File.Exists(txtLicenseKeyPath.Text))
            {
                MessageBox.Show("License key file not found. Please select a valid file.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnBrowse.Focus();
                return;
            }

            // Store values
            LicenseKeyPath = txtLicenseKeyPath.Text;
            CustomerId = txtCustomerId.Text.Trim();
            LicenseNumber = txtLicenseNumber.Text.Trim();
            ExpirationDate = dtpExpirationDate.Value.Date;
            WasCancelled = false;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            WasCancelled = true;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
