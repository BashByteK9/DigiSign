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
            // Form settings
            this.Text = "DigiSign - Admin License Generation";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Icon = SystemIcons.Application;

            int leftMargin = 20;
            int topMargin = 20;
            int controlWidth = 540;
            int labelHeight = 20;
            int textBoxHeight = 25;
            int spacing = 10;
            int currentY = topMargin;

            // Title
            lblTitle = new Label
            {
                Text = "Generate User License",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, 30),
                ForeColor = Color.FromArgb(0, 102, 204)
            };
            this.Controls.Add(lblTitle);
            currentY += 40;

            // Separator
            Panel separator1 = new Panel
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, 2),
                BackColor = Color.FromArgb(200, 200, 200)
            };
            this.Controls.Add(separator1);
            currentY += spacing + 5;

            // License Key Path
            lblLicenseKeyPath = new Label
            {
                Text = "License Key File (*.key):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            this.Controls.Add(lblLicenseKeyPath);
            currentY += labelHeight + 5;

            txtLicenseKeyPath = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth - 90, textBoxHeight),
                ReadOnly = true,
                BackColor = Color.White
            };
            this.Controls.Add(txtLicenseKeyPath);

            btnBrowse = new Button
            {
                Text = "Browse...",
                Location = new Point(leftMargin + controlWidth - 80, currentY - 1),
                Size = new Size(80, textBoxHeight + 2)
            };
            btnBrowse.Click += BtnBrowse_Click;
            this.Controls.Add(btnBrowse);
            currentY += textBoxHeight + spacing + 10;

            // Device Info (read-only display)
            lblDeviceInfo = new Label
            {
                Text = "Device Information:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            this.Controls.Add(lblDeviceInfo);
            currentY += labelHeight + 5;

            txtDeviceInfo = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, 60),
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 245, 245),
                Font = new Font("Courier New", 8),
                ScrollBars = ScrollBars.Vertical
            };
            this.Controls.Add(txtDeviceInfo);
            currentY += 70;

            // Separator
            Panel separator2 = new Panel
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, 2),
                BackColor = Color.FromArgb(200, 200, 200)
            };
            this.Controls.Add(separator2);
            currentY += spacing + 5;

            // Customer ID
            lblCustomerId = new Label
            {
                Text = "Customer ID: *",
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            this.Controls.Add(lblCustomerId);
            currentY += labelHeight + 5;

            txtCustomerId = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            txtCustomerId.TextChanged += ValidateForm;
            this.Controls.Add(txtCustomerId);
            currentY += textBoxHeight + spacing;

            // License Number
            lblLicenseNumber = new Label
            {
                Text = "License Number: *",
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            this.Controls.Add(lblLicenseNumber);
            currentY += labelHeight + 5;

            txtLicenseNumber = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            txtLicenseNumber.TextChanged += ValidateForm;
            this.Controls.Add(txtLicenseNumber);
            currentY += textBoxHeight + spacing;

            // Expiration Date
            lblExpirationDate = new Label
            {
                Text = "Expiration Date: *",
                Location = new Point(leftMargin, currentY),
                Size = new Size(controlWidth, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            this.Controls.Add(lblExpirationDate);
            currentY += labelHeight + 5;

            dtpExpirationDate = new DateTimePicker
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(250, textBoxHeight),
                Format = DateTimePickerFormat.Short,
                MinDate = DateTime.Now.AddDays(1),
                Value = DateTime.Now.AddYears(1),
                Font = new Font("Segoe UI", 9)
            };
            dtpExpirationDate.ValueChanged += ValidateForm;
            this.Controls.Add(dtpExpirationDate);
            currentY += textBoxHeight + spacing + 20;

            // Buttons
            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(leftMargin + controlWidth - 180, currentY),
                Size = new Size(80, 32),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9)
            };
            btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(btnCancel);

            btnGenerate = new Button
            {
                Text = "Generate License",
                Location = new Point(leftMargin + controlWidth - 90, currentY),
                Size = new Size(110, 32),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnGenerate.FlatAppearance.BorderSize = 0;
            btnGenerate.Click += BtnGenerate_Click;
            this.Controls.Add(btnGenerate);

            this.CancelButton = btnCancel;
            this.AcceptButton = btnGenerate;
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
