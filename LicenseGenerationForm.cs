using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PDFtoImage;
using SkiaSharp;

namespace DigiSign
{
    public class LicenseGenerationForm : Form
    {
        private const string SystemDefaultPrinterLabel = "(System Default)";

        // Form controls
        private Label lblTitle;
        private TabControl tabControl;
        
        // License Generation Tab
        private TabPage tabLicense;
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
        
        // Settings Tab - General
        private TabPage tabSettings;
        private TabControl tabSettingsControl;
        private TabPage tabGeneral;
        private Label lblInputFile;
        private TextBox txtInputFile;
        private Button btnBrowseInput;
        private Label lblOutputFolder;
        private TextBox txtOutputFolder;
        private Button btnBrowseOutput;
        private Label lblTestInvoiceNo;
        private TextBox txtTestInvoiceNo;
        private Label lblCommonName;
        private TextBox txtCommonName;
        private Label lblPin;
        private TextBox txtPin;
        private CheckBox chkShowPin;
        private CheckBox chkVerboseMode;
        private CheckBox chkBatchMode;
        private CheckBox chkEnableOcspCheck;
        private Label lblOcspTimeoutSeconds;
        private NumericUpDown numOcspTimeoutSeconds;

        // API Settings Tab (listener / invoice-label download configuration)
        private TabPage tabApiSettings;
        private Label lblListenerPort;
        private NumericUpDown numListenerPort;
        private Label lblInvoiceApiBaseUrl;
        private TextBox txtInvoiceApiBaseUrl;
        private Label lblInvoiceApiKey;
        private TextBox txtInvoiceApiKey;
        private CheckBox chkShowApiKey;
        private CheckBox chkNoAuthApi;
        private CheckBox chkIncludeSignedPdfInCallback;
        private Label lblInvoiceSignedCallbackUrl;
        private TextBox txtInvoiceSignedCallbackUrl;
        private Label lblApiSettingsNote;
        private Label lblPrinterName;
        private ComboBox cmbPrinterName;
        private Button btnSaveApiSettings;

        // Settings Tab - Signature
        private TabPage tabSignature;
        private Label lblXCoord;
        private NumericUpDown numXCoord;
        private Label lblYCoord;
        private NumericUpDown numYCoord;
        private Label lblWidth;
        private NumericUpDown numWidth;
        private Label lblHeight;
        private NumericUpDown numHeight;
        private Label lblOpenOutputFolder;
        private ComboBox cmbOpenOutputFolder;
        private Label lblUseSelfSigned;
        private ComboBox cmbUseSelfSigned;
        private Label lblCopy1Label;
        private TextBox txtCopy1Label;
        private Label lblCopyPosition;
        private Label lblCopyX;
        private NumericUpDown numCopyX;
        private Label lblCopyY;
        private NumericUpDown numCopyY;
        private Label lblCopyWidth;
        private NumericUpDown numCopyWidth;
        private Label lblCopyHeight;
        private NumericUpDown numCopyHeight;
        private CheckBox chkExtraCopies;
        private CheckBox chkPrintAllCopies;
        private Label lblCopy2Label;
        private TextBox txtCopy2Label;
        private Label lblCopy3Label;
        private TextBox txtCopy3Label;
        private Label lblCopy4Label;
        private TextBox txtCopy4Label;
        private Button btnSaveSettings;
        private Button btnResetSettings;
        
        // Settings Tab - Preview
        private TabPage tabPreview;
        private PictureBox picPreview;
        private Button btnRefreshPreview;
        private Button btnSignPdf;
        private Label lblPreviewInfo;
        private ComboBox cmbPreviewPage;
        private Label lblZoom;
        private Button btnZoomIn;
        private Button btnZoomOut;
        private Button btnZoomReset;
        private float zoomLevel = 1.0f;
        
        // Interactive signature placement
        private bool isDraggingSignature = false;
        private bool isResizingSignature = false;
        private ResizeHandle activeResizeHandle = ResizeHandle.None;
        private Point lastMousePosition;
        private RectangleF signatureRect;
        private const int HANDLE_SIZE = 8;

        // Store actual PDF dimensions for accurate resize handle positioning
        private float currentPdfWidth = 595f;  // Default to A4
        private float currentPdfHeight = 842f; // Default to A4
        
        private enum ResizeHandle
        {
            None,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Top,
            Bottom,
            Left,
            Right,
            Move
        }
        
        // Bottom buttons
        private Button btnGenerate;
        private Button btnCancel;
        
        // XML file path
        private string xmlFilePath;
        
        // Mode flags
        private bool settingsOnlyMode = false;

        // Public properties
        public string LicenseKeyPath { get; private set; }
        public string CustomerId { get; private set; }
        public string LicenseNumber { get; private set; }
        public DateTime ExpirationDate { get; private set; }
        public bool WasCancelled { get; private set; }

        public LicenseGenerationForm(bool settingsOnly = false)
        {
            settingsOnlyMode = settingsOnly;
            xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.xml");
            InitializeComponents();
            LoadSigningSettings();
            LoadApiSettings();
            WasCancelled = true; // Default to cancelled unless user clicks Generate
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = settingsOnlyMode 
                ? $"{VersionInfo.TitleWithVersion} - Settings" 
                : $"{VersionInfo.TitleWithVersion} - Admin Panel";
            this.ClientSize = new Size(800, 650);
            this.MinimumSize = new Size(800, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Icon = TrayIconLoader.LoadFromEmbeddedPng("DigiSign.singer_icon.png");
            
            int margin = 20;
            int currentY = margin;
            
            // Title
            lblTitle = new Label
            {
                Text = settingsOnlyMode 
                    ? $"{VersionInfo.TitleWithVersion} - PDF Signing Settings" 
                    : $"{VersionInfo.TitleWithVersion} - Administration",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(margin, currentY),
                Size = new Size(760, 35),
                ForeColor = Color.FromArgb(0, 102, 204),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblTitle);
            currentY += 45;
            
            // Separator
            Panel separator = new Panel
            {
                Location = new Point(margin, currentY),
                Size = new Size(760, 2),
                BackColor = Color.FromArgb(200, 200, 200),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(separator);
            currentY += 12;
            
            // Tab Control
            tabControl = new TabControl
            {
                Location = new Point(margin, currentY),
                Size = new Size(760, 570),
                Font = new Font("Segoe UI", 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(tabControl);
            
            // Create tabs - only include License tab if NOT in settings-only mode
            if (!settingsOnlyMode)
            {
                CreateLicenseGenerationTab();
            }
            CreateSettingsTab();
            CreateApiSettingsTab();

            // Select Settings tab by default in settings-only mode
            if (settingsOnlyMode && tabControl.TabPages.Count > 0)
            {
                tabControl.SelectedIndex = 0; // Settings tab is first when license tab is hidden
            }
        }
        
        private void CreateLicenseGenerationTab()
        {
            tabLicense = new TabPage("License Generation");
            tabControl.TabPages.Add(tabLicense);
            
            int leftMargin = 20;
            int currentY = 20;
            int labelHeight = 22;
            int textBoxHeight = 26;
            int spacing = 12;
            
            // License Key Path
            lblLicenseKeyPath = new Label
            {
                Text = "License Key File (*.key):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            tabLicense.Controls.Add(lblLicenseKeyPath);
            currentY += labelHeight + 6;
            
            btnBrowse = new Button
            {
                Text = "Browse...",
                Size = new Size(100, textBoxHeight + 2),
                Location = new Point(620, currentY),
                Font = new Font("Segoe UI", 9)
            };
            btnBrowse.Click += BtnBrowse_Click;
            tabLicense.Controls.Add(btnBrowse);
            
            txtLicenseKeyPath = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(590, textBoxHeight),
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            tabLicense.Controls.Add(txtLicenseKeyPath);
            currentY += textBoxHeight + spacing + 12;
            
            // Device Info
            lblDeviceInfo = new Label
            {
                Text = "Device Information:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            tabLicense.Controls.Add(lblDeviceInfo);
            currentY += labelHeight + 6;
            
            txtDeviceInfo = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, 80),
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 245, 245),
                Font = new Font("Courier New", 9),
                ScrollBars = ScrollBars.Vertical
            };
            tabLicense.Controls.Add(txtDeviceInfo);
            currentY += 92;
            
            // Separator
            Panel separator = new Panel
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, 2),
                BackColor = Color.FromArgb(200, 200, 200)
            };
            tabLicense.Controls.Add(separator);
            currentY += spacing + 8;
            
            // Customer ID
            lblCustomerId = new Label
            {
                Text = "Customer ID: *",
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            tabLicense.Controls.Add(lblCustomerId);
            currentY += labelHeight + 6;
            
            txtCustomerId = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            txtCustomerId.TextChanged += ValidateForm;
            tabLicense.Controls.Add(txtCustomerId);
            currentY += textBoxHeight + spacing;
            
            // License Number
            lblLicenseNumber = new Label
            {
                Text = "License Number: *",
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            tabLicense.Controls.Add(lblLicenseNumber);
            currentY += labelHeight + 6;
            
            txtLicenseNumber = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            txtLicenseNumber.TextChanged += ValidateForm;
            tabLicense.Controls.Add(txtLicenseNumber);
            currentY += textBoxHeight + spacing;
            
            // Expiration Date
            lblExpirationDate = new Label
            {
                Text = "Expiration Date: *",
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, labelHeight),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            tabLicense.Controls.Add(lblExpirationDate);
            currentY += labelHeight + 6;
            
            dtpExpirationDate = new DateTimePicker
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(320, textBoxHeight),
                Format = DateTimePickerFormat.Long,
                MinDate = DateTime.Now.AddDays(1),
                Value = DateTime.Now.AddYears(1),
                Font = new Font("Segoe UI", 9)
            };
            dtpExpirationDate.ValueChanged += ValidateForm;
            tabLicense.Controls.Add(dtpExpirationDate);
            currentY += textBoxHeight + spacing + 20;
            
            // Separator before buttons
            Panel separator2 = new Panel
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(700, 2),
                BackColor = Color.FromArgb(200, 200, 200)
            };
            tabLicense.Controls.Add(separator2);
            currentY += 15;
            
            // Buttons at bottom of tab
            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(110, 40),
                Location = new Point(500, currentY),
                Font = new Font("Segoe UI", 10),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += BtnCancel_Click;
            tabLicense.Controls.Add(btnCancel);
            
            btnGenerate = new Button
            {
                Text = "Generate License",
                Size = new Size(150, 40),
                Location = new Point(620, currentY),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnGenerate.FlatAppearance.BorderSize = 0;
            btnGenerate.Click += BtnGenerate_Click;
            tabLicense.Controls.Add(btnGenerate);
            
            // Set form button defaults (only in admin mode, not settings-only mode)
            if (!settingsOnlyMode)
            {
                this.AcceptButton = btnGenerate;
                this.CancelButton = btnCancel;
            }
        }
        
        private void CreateSettingsTab()
        {
            tabSettings = new TabPage("PDF Signing Settings");
            tabControl.TabPages.Add(tabSettings);
            
            // Inner tab control for Settings
            tabSettingsControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(720, 450),
                Font = new Font("Segoe UI", 9)
            };
            tabSettings.Controls.Add(tabSettingsControl);
            
            CreateGeneralSettingsTab();
            CreateSignatureSettingsTab();
            CreatePreviewTab();

            // Settings buttons
            // Sign PDF button on the left
            btnSignPdf = new Button
            {
                Text = "Sign PDF",
                Size = new Size(120, 35),
                Location = new Point(20, 470),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSignPdf.FlatAppearance.BorderSize = 0;
            btnSignPdf.Click += BtnSignPdf_Click;
            tabSettings.Controls.Add(btnSignPdf);

            lblTestInvoiceNo = new Label
            {
                Text = "Test Invoice No:",
                Location = new Point(160, 478),
                Size = new Size(95, 20),
                Font = new Font("Segoe UI", 9)
            };
            tabSettings.Controls.Add(lblTestInvoiceNo);

            txtTestInvoiceNo = new TextBox
            {
                Location = new Point(258, 474),
                Size = new Size(110, 24),
                Font = new Font("Segoe UI", 9),
                Text = GenerateRandomInvoiceNo()
            };
            tabSettings.Controls.Add(txtTestInvoiceNo);

            btnResetSettings = new Button
            {
                Text = "Reset to Defaults",
                Size = new Size(130, 35),
                Location = new Point(470, 470),
                Font = new Font("Segoe UI", 9)
            };
            btnResetSettings.Click += BtnResetSettings_Click;
            tabSettings.Controls.Add(btnResetSettings);

            btnSaveSettings = new Button
            {
                Text = "Save Settings",
                Size = new Size(120, 35),
                Location = new Point(610, 470),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSaveSettings.FlatAppearance.BorderSize = 0;
            btnSaveSettings.Click += BtnSaveSettings_Click;
            tabSettings.Controls.Add(btnSaveSettings);
        }

        private void CreateApiSettingsTab()
        {
            tabApiSettings = new TabPage("API Settings");
            tabControl.TabPages.Add(tabApiSettings);

            int leftMargin = 20;
            int currentY = 20;
            int labelHeight = 20;
            int textBoxHeight = 24;
            int spacing = 10;

            lblApiSettingsNote = new Label
            {
                Text = "Used only when running 'DigiSign.exe /listen'. The invoice/label download API is a placeholder - configure it once the real API is available.",
                Location = new Point(leftMargin, currentY),
                Size = new Size(720, 40),
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            tabApiSettings.Controls.Add(lblApiSettingsNote);
            currentY += 50;

            // Listener Port
            lblListenerPort = new Label
            {
                Text = "Listener Port:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabApiSettings.Controls.Add(lblListenerPort);

            numListenerPort = new NumericUpDown
            {
                Location = new Point(leftMargin + 210, currentY),
                Size = new Size(120, textBoxHeight),
                Font = new Font("Segoe UI", 9),
                Minimum = 1024,
                Maximum = 65535,
                DecimalPlaces = 0,
                Value = 8943
            };
            tabApiSettings.Controls.Add(numListenerPort);
            currentY += textBoxHeight + spacing + 10;

            // Invoice/Label API Base URL
            lblInvoiceApiBaseUrl = new Label
            {
                Text = "Invoice/Label Download API Base URL:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabApiSettings.Controls.Add(lblInvoiceApiBaseUrl);
            currentY += labelHeight + 5;

            txtInvoiceApiBaseUrl = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            tabApiSettings.Controls.Add(txtInvoiceApiBaseUrl);
            currentY += textBoxHeight + spacing + 10;

            // Invoice/Label API Key
            lblInvoiceApiKey = new Label
            {
                Text = "Invoice/Label Download API Key:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabApiSettings.Controls.Add(lblInvoiceApiKey);
            currentY += labelHeight + 5;

            txtInvoiceApiKey = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, textBoxHeight),
                Font = new Font("Segoe UI", 9),
                PasswordChar = '*'
            };
            tabApiSettings.Controls.Add(txtInvoiceApiKey);
            currentY += textBoxHeight + 5;

            chkShowApiKey = new CheckBox
            {
                Text = "Show API Key",
                Location = new Point(leftMargin, currentY),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9)
            };
            chkShowApiKey.CheckedChanged += ChkShowApiKey_CheckedChanged;
            tabApiSettings.Controls.Add(chkShowApiKey);
            currentY += 30;

            chkNoAuthApi = new CheckBox
            {
                Text = "No Auth (API key not required)",
                Location = new Point(leftMargin, currentY),
                Size = new Size(300, 20),
                Font = new Font("Segoe UI", 9)
            };
            chkNoAuthApi.CheckedChanged += ChkNoAuthApi_CheckedChanged;
            tabApiSettings.Controls.Add(chkNoAuthApi);
            currentY += 40;

            chkIncludeSignedPdfInCallback = new CheckBox
            {
                Text = "Include signed PDF (base64) in signed-callback response",
                Location = new Point(leftMargin, currentY),
                Size = new Size(400, 20),
                Font = new Font("Segoe UI", 9)
            };
            tabApiSettings.Controls.Add(chkIncludeSignedPdfInCallback);
            currentY += 40;

            lblInvoiceSignedCallbackUrl = new Label
            {
                Text = "Signed-Callback Response URL (optional - blank = reuse the base URL above):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabApiSettings.Controls.Add(lblInvoiceSignedCallbackUrl);
            currentY += labelHeight + 5;

            txtInvoiceSignedCallbackUrl = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            tabApiSettings.Controls.Add(txtInvoiceSignedCallbackUrl);
            currentY += textBoxHeight + spacing + 10;

            // Batch Mode toggle
            chkBatchMode = new CheckBox
            {
                Text = "Launch in batch signing mode (instead of listener/tray) when started with no arguments",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, 20),
                Font = new Font("Segoe UI", 9)
            };
            tabApiSettings.Controls.Add(chkBatchMode);
            currentY += 40;

            btnSaveApiSettings = new Button
            {
                Text = "Save API Settings",
                Size = new Size(150, 35),
                Location = new Point(leftMargin, currentY),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSaveApiSettings.FlatAppearance.BorderSize = 0;
            btnSaveApiSettings.Click += BtnSaveApiSettings_Click;
            tabApiSettings.Controls.Add(btnSaveApiSettings);

            UpdateApiKeyEnabled();
        }

        private void CreateGeneralSettingsTab()
        {
            tabGeneral = new TabPage("General");
            tabGeneral.AutoScroll = true;
            tabSettingsControl.TabPages.Add(tabGeneral);
            
            int leftMargin = 20;
            int currentY = 20;
            int labelHeight = 20;
            int textBoxHeight = 24;
            int spacing = 10;
            
            // Input File
            lblInputFile = new Label
            {
                Text = "Input PDF File:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabGeneral.Controls.Add(lblInputFile);
            currentY += labelHeight + 5;
            
            btnBrowseInput = new Button
            {
                Text = "Browse...",
                Size = new Size(90, textBoxHeight + 2),
                Location = new Point(590, currentY),
                Font = new Font("Segoe UI", 9)
            };
            btnBrowseInput.Click += BtnBrowseInput_Click;
            tabGeneral.Controls.Add(btnBrowseInput);
            
            txtInputFile = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(560, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            txtInputFile.TextChanged += (s, e) => 
            {
                // Update preview if on preview tab
                if (tabControl.SelectedTab == tabSettings && tabSettingsControl.SelectedTab == tabPreview)
                {
                    UpdatePreview();
                }
            };
            tabGeneral.Controls.Add(txtInputFile);
            currentY += textBoxHeight + spacing + 10;
            
            // Output Folder
            lblOutputFolder = new Label
            {
                Text = "Output Folder:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabGeneral.Controls.Add(lblOutputFolder);
            currentY += labelHeight + 5;
            
            btnBrowseOutput = new Button
            {
                Text = "Browse...",
                Size = new Size(90, textBoxHeight + 2),
                Location = new Point(590, currentY),
                Font = new Font("Segoe UI", 9)
            };
            btnBrowseOutput.Click += BtnBrowseOutput_Click;
            tabGeneral.Controls.Add(btnBrowseOutput);
            
            txtOutputFolder = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(560, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            tabGeneral.Controls.Add(txtOutputFolder);
            currentY += textBoxHeight + spacing + 10;
            
            // Common Name
            lblCommonName = new Label
            {
                Text = "Certificate Common Name (CN):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabGeneral.Controls.Add(lblCommonName);
            currentY += labelHeight + 5;
            
            txtCommonName = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            tabGeneral.Controls.Add(txtCommonName);
            currentY += textBoxHeight + spacing + 10;
            
            // PIN
            lblPin = new Label
            {
                Text = "Smart Card/Token PIN:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabGeneral.Controls.Add(lblPin);
            currentY += labelHeight + 5;
            
            txtPin = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, textBoxHeight),
                Font = new Font("Segoe UI", 9),
                PasswordChar = '*'
            };
            tabGeneral.Controls.Add(txtPin);
            currentY += textBoxHeight + 5;
            
            chkShowPin = new CheckBox
            {
                Text = "Show PIN",
                Location = new Point(leftMargin, currentY),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9)
            };
            chkShowPin.CheckedChanged += ChkShowPin_CheckedChanged;
            tabGeneral.Controls.Add(chkShowPin);
            currentY += 30;

            // Verbose Mode
            chkVerboseMode = new CheckBox
            {
                Text = "Enable Verbose Mode (detailed signing logs)",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204)
            };
            tabGeneral.Controls.Add(chkVerboseMode);
            currentY += 40;

            // Printer selection
            lblPrinterName = new Label
            {
                Text = "Printer (for print actions):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabGeneral.Controls.Add(lblPrinterName);
            currentY += labelHeight + 5;

            cmbPrinterName = new ComboBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(400, textBoxHeight),
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbPrinterName.Items.Add(SystemDefaultPrinterLabel);
            foreach (string printerName in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                cmbPrinterName.Items.Add(printerName);
            tabGeneral.Controls.Add(cmbPrinterName);
            currentY += textBoxHeight + spacing + 10;

            // OCSP (certificate authority revocation) check
            chkEnableOcspCheck = new CheckBox
            {
                Text = "Check certificate revocation status (OCSP) before signing",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, 20),
                Font = new Font("Segoe UI", 9),
                Checked = true
            };
            tabGeneral.Controls.Add(chkEnableOcspCheck);
            currentY += 30;

            lblOcspTimeoutSeconds = new Label
            {
                Text = "OCSP check timeout (seconds):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(220, labelHeight),
                Font = new Font("Segoe UI", 9)
            };
            tabGeneral.Controls.Add(lblOcspTimeoutSeconds);

            numOcspTimeoutSeconds = new NumericUpDown
            {
                Location = new Point(leftMargin + 230, currentY - 2),
                Size = new Size(80, textBoxHeight),
                Font = new Font("Segoe UI", 9),
                Minimum = 1,
                Maximum = 60,
                DecimalPlaces = 0,
                Value = 10
            };
            tabGeneral.Controls.Add(numOcspTimeoutSeconds);
            currentY += textBoxHeight + spacing + 10;
        }
        
        private void CreateSignatureSettingsTab()
        {
            tabSignature = new TabPage("Signature");
            tabSignature.AutoScroll = true;
            tabSettingsControl.TabPages.Add(tabSignature);

            int leftMargin = 20;
            int currentY = 20;
            int labelHeight = 20;
            int controlHeight = 24;
            int spacing = 10;
            
            // X Coordinate
            lblXCoord = new Label
            {
                Text = "X Coordinate (pixels):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabSignature.Controls.Add(lblXCoord);
            
            numXCoord = new NumericUpDown
            {
                Location = new Point(leftMargin + 210, currentY),
                Size = new Size(120, controlHeight),
                Font = new Font("Segoe UI", 9),
                Minimum = 0,
                Maximum = 10000,
                DecimalPlaces = 0
            };
            numXCoord.ValueChanged += PreviewSettings_Changed;
            tabSignature.Controls.Add(numXCoord);
            currentY += controlHeight + spacing + 5;
            
            // Y Coordinate
            lblYCoord = new Label
            {
                Text = "Y Coordinate (pixels):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabSignature.Controls.Add(lblYCoord);
            
            numYCoord = new NumericUpDown
            {
                Location = new Point(leftMargin + 210, currentY),
                Size = new Size(120, controlHeight),
                Font = new Font("Segoe UI", 9),
                Minimum = 0,
                Maximum = 10000,
                DecimalPlaces = 0
            };
            numYCoord.ValueChanged += PreviewSettings_Changed;
            tabSignature.Controls.Add(numYCoord);
            currentY += controlHeight + spacing + 5;
            
            // Width
            lblWidth = new Label
            {
                Text = "Signature Width (pixels):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabSignature.Controls.Add(lblWidth);
            
            numWidth = new NumericUpDown
            {
                Location = new Point(leftMargin + 210, currentY),
                Size = new Size(120, controlHeight),
                Font = new Font("Segoe UI", 9),
                Minimum = 10,
                Maximum = 10000,
                DecimalPlaces = 0
            };
            numWidth.ValueChanged += PreviewSettings_Changed;
            tabSignature.Controls.Add(numWidth);
            currentY += controlHeight + spacing + 5;
            
            // Height
            lblHeight = new Label
            {
                Text = "Signature Height (pixels):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabSignature.Controls.Add(lblHeight);
            
            numHeight = new NumericUpDown
            {
                Location = new Point(leftMargin + 210, currentY),
                Size = new Size(120, controlHeight),
                Font = new Font("Segoe UI", 9),
                Minimum = 10,
                Maximum = 10000,
                DecimalPlaces = 0
            };
            numHeight.ValueChanged += PreviewSettings_Changed;
            tabSignature.Controls.Add(numHeight);
            currentY += controlHeight + spacing + 15;

            // Open Output Folder
            lblOpenOutputFolder = new Label
            {
                Text = "Open Output Folder After Signing:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabSignature.Controls.Add(lblOpenOutputFolder);
            
            cmbOpenOutputFolder = new ComboBox
            {
                Location = new Point(leftMargin + 210, currentY),
                Size = new Size(200, controlHeight),
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbOpenOutputFolder.Items.AddRange(new object[] { "Y - Yes", "N - No" });
            tabSignature.Controls.Add(cmbOpenOutputFolder);
            currentY += controlHeight + spacing + 5;
            
            // Use Self-Signed Certificate
            lblUseSelfSigned = new Label
            {
                Text = "Use Self-Signed Certificate:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabSignature.Controls.Add(lblUseSelfSigned);
            
            cmbUseSelfSigned = new ComboBox
            {
                Location = new Point(leftMargin + 210, currentY),
                Size = new Size(200, controlHeight),
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbUseSelfSigned.Items.AddRange(new object[] { "Y - Yes", "N - No" });
            tabSignature.Controls.Add(cmbUseSelfSigned);
            currentY += controlHeight + spacing + 15;

            // Copy 1 label (mandatory - always stamped)
            lblCopy1Label = new Label
            {
                Text = "Copy Label:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabSignature.Controls.Add(lblCopy1Label);

            txtCopy1Label = new TextBox
            {
                Location = new Point(leftMargin + 210, currentY),
                Size = new Size(200, controlHeight),
                Font = new Font("Segoe UI", 9),
                Text = "Original for Buyer"
            };
            txtCopy1Label.Leave += TxtCopy1Label_Leave;
            tabSignature.Controls.Add(txtCopy1Label);
            currentY += controlHeight + spacing + 5;

            // Shared copy label position
            lblCopyPosition = new Label
            {
                Text = "Copy Label Position:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(64, 64, 64)
            };
            tabSignature.Controls.Add(lblCopyPosition);
            currentY += labelHeight + spacing;

            lblCopyX = new Label { Text = "X Coordinate of Label (pixels):", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopyX);
            numCopyX = new NumericUpDown { Location = new Point(leftMargin + 210, currentY), Size = new Size(120, controlHeight), Font = new Font("Segoe UI", 9), Minimum = 0, Maximum = 10000, DecimalPlaces = 0 };
            tabSignature.Controls.Add(numCopyX);
            currentY += controlHeight + spacing + 5;

            lblCopyY = new Label { Text = "Y Coordinate of Label (pixels):", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopyY);
            numCopyY = new NumericUpDown { Location = new Point(leftMargin + 210, currentY), Size = new Size(120, controlHeight), Font = new Font("Segoe UI", 9), Minimum = 0, Maximum = 10000, DecimalPlaces = 0 };
            tabSignature.Controls.Add(numCopyY);
            currentY += controlHeight + spacing + 5;

            lblCopyWidth = new Label { Text = "Width of Label (pixels):", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopyWidth);
            numCopyWidth = new NumericUpDown { Location = new Point(leftMargin + 210, currentY), Size = new Size(120, controlHeight), Font = new Font("Segoe UI", 9), Minimum = 10, Maximum = 10000, DecimalPlaces = 0 };
            tabSignature.Controls.Add(numCopyWidth);
            currentY += controlHeight + spacing + 5;

            lblCopyHeight = new Label { Text = "Height of Label (pixels):", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopyHeight);
            numCopyHeight = new NumericUpDown { Location = new Point(leftMargin + 210, currentY), Size = new Size(120, controlHeight), Font = new Font("Segoe UI", 9), Minimum = 10, Maximum = 10000, DecimalPlaces = 0 };
            tabSignature.Controls.Add(numCopyHeight);
            currentY += controlHeight + spacing + 15;

            // Extra Copies
            chkExtraCopies = new CheckBox
            {
                Text = "Extra Copies",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            chkExtraCopies.CheckedChanged += ChkExtraCopies_CheckedChanged;
            tabSignature.Controls.Add(chkExtraCopies);
            currentY += controlHeight + spacing + 5;

            chkPrintAllCopies = new CheckBox
            {
                Text = "Print all copies",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9)
            };
            tabSignature.Controls.Add(chkPrintAllCopies);
            currentY += controlHeight + spacing + 5;

            lblCopy2Label = new Label { Text = "Copy 2 Label:", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopy2Label);
            txtCopy2Label = new TextBox { Location = new Point(leftMargin + 210, currentY), Size = new Size(200, controlHeight), Font = new Font("Segoe UI", 9), Text = "Duplicate for Transporter" };
            tabSignature.Controls.Add(txtCopy2Label);
            currentY += controlHeight + spacing + 5;

            lblCopy3Label = new Label { Text = "Copy 3 Label:", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopy3Label);
            txtCopy3Label = new TextBox { Location = new Point(leftMargin + 210, currentY), Size = new Size(200, controlHeight), Font = new Font("Segoe UI", 9), Text = "Duplicate for Supplier" };
            tabSignature.Controls.Add(txtCopy3Label);
            currentY += controlHeight + spacing + 5;

            lblCopy4Label = new Label { Text = "Copy 4 Label:", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopy4Label);
            txtCopy4Label = new TextBox { Location = new Point(leftMargin + 210, currentY), Size = new Size(200, controlHeight), Font = new Font("Segoe UI", 9), Text = "Extra Copy" };
            tabSignature.Controls.Add(txtCopy4Label);

            UpdateExtraCopiesVisibility();
        }

        private void TxtCopy1Label_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCopy1Label.Text))
                txtCopy1Label.Text = "Original for Buyer";
        }

        private void ChkExtraCopies_CheckedChanged(object sender, EventArgs e)
        {
            UpdateExtraCopiesVisibility();
        }

        private void UpdateExtraCopiesVisibility()
        {
            bool enabled = chkExtraCopies.Checked;
            chkPrintAllCopies.Enabled = enabled;
            lblCopy2Label.Enabled = txtCopy2Label.Enabled = enabled;
            lblCopy3Label.Enabled = txtCopy3Label.Enabled = enabled;
            lblCopy4Label.Enabled = txtCopy4Label.Enabled = enabled;
        }

        private void CreatePreviewTab()
        {
            tabPreview = new TabPage("Preview");
            tabSettingsControl.TabPages.Add(tabPreview);
            
            int leftMargin = 20;
            int currentY = 15;
            
            // Info label
            lblPreviewInfo = new Label
            {
                Text = "Preview of signature placement (use mouse wheel to zoom)",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(64, 64, 64)
            };
            tabPreview.Controls.Add(lblPreviewInfo);
            currentY += 30;
            
            // Page selector
            Label lblPage = new Label
            {
                Text = "Page:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(45, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabPreview.Controls.Add(lblPage);
            
            cmbPreviewPage = new ComboBox
            {
                Location = new Point(leftMargin + 50, currentY),
                Size = new Size(100, 24),
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbPreviewPage.Items.Add("Page 1");
            cmbPreviewPage.SelectedIndex = 0;
            cmbPreviewPage.SelectedIndexChanged += (s, e) => UpdatePreview();
            tabPreview.Controls.Add(cmbPreviewPage);
            
            // Zoom controls
            lblZoom = new Label
            {
                Text = "Zoom: 100%",
                Location = new Point(leftMargin + 170, currentY + 3),
                Size = new Size(80, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabPreview.Controls.Add(lblZoom);
            
            btnZoomOut = new Button
            {
                Text = "�",  // En dash (better visibility)
                Location = new Point(leftMargin + 255, currentY - 2),
                Size = new Size(30, 28),
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            btnZoomOut.Click += BtnZoomOut_Click;
            tabPreview.Controls.Add(btnZoomOut);
            
            btnZoomReset = new Button
            {
                Text = "100%",
                Location = new Point(leftMargin + 290, currentY - 2),
                Size = new Size(50, 28),
                Font = new Font("Segoe UI", 8)
            };
            btnZoomReset.Click += BtnZoomReset_Click;
            tabPreview.Controls.Add(btnZoomReset);
            
            btnZoomIn = new Button
            {
                Text = "+",
                Location = new Point(leftMargin + 345, currentY - 2),
                Size = new Size(30, 28),
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };
            btnZoomIn.Click += BtnZoomIn_Click;
            tabPreview.Controls.Add(btnZoomIn);
            
            // Refresh button
            btnRefreshPreview = new Button
            {
                Text = "Refresh",
                Location = new Point(leftMargin + 395, currentY - 2),
                Size = new Size(80, 28),
                Font = new Font("Segoe UI", 9)
            };
            btnRefreshPreview.Click += (s, e) => UpdatePreview();
            tabPreview.Controls.Add(btnRefreshPreview);
            currentY += 40;
            
            // Preview panel with scrollbars
            Panel previewPanel = new Panel
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, 320),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true,
                BackColor = Color.LightGray
            };
            tabPreview.Controls.Add(previewPanel);
            
            // Preview picture box inside panel
            picPreview = new PictureBox
            {
                Location = new Point(0, 0),
                SizeMode = PictureBoxSizeMode.AutoSize,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            
            // Enable mouse events for interactive signature placement
            picPreview.MouseDown += PicPreview_MouseDown;
            picPreview.MouseMove += PicPreview_MouseMove;
            picPreview.MouseUp += PicPreview_MouseUp;
            picPreview.Paint += PicPreview_Paint;
            
            // Enable mouse wheel zoom
            picPreview.MouseWheel += PicPreview_MouseWheel;
            previewPanel.MouseWheel += PicPreview_MouseWheel;
            
            previewPanel.Controls.Add(picPreview);
            
            // Add instruction label
            Label lblInstruction = new Label
            {
                Text = "?? Tip: Drag the signature box to move it, drag corners/edges to resize",
                Location = new Point(leftMargin, currentY + 330),
                Size = new Size(660, 20),
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            tabPreview.Controls.Add(lblInstruction);

            // Initialize with blank preview
            UpdatePreview();
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
        
        private void BtnBrowseInput_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Input PDF File";
                dialog.Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*";
                dialog.DefaultExt = "pdf";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtInputFile.Text = dialog.FileName;
                }
            }
        }
        
        private void BtnBrowseOutput_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Output Folder";
                dialog.ShowNewFolderButton = true;
                
                if (!string.IsNullOrEmpty(txtOutputFolder.Text) && Directory.Exists(txtOutputFolder.Text))
                {
                    dialog.SelectedPath = txtOutputFolder.Text;
                }
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtOutputFolder.Text = dialog.SelectedPath;
                }
            }
        }
        
        private void ChkShowPin_CheckedChanged(object sender, EventArgs e)
        {
            if (chkShowPin.Checked)
            {
                txtPin.PasswordChar = '\0';
            }
            else
            {
                txtPin.PasswordChar = '*';
            }
        }

        private void ChkShowApiKey_CheckedChanged(object sender, EventArgs e)
        {
            txtInvoiceApiKey.PasswordChar = chkShowApiKey.Checked ? '\0' : '*';
        }

        private void ChkNoAuthApi_CheckedChanged(object sender, EventArgs e)
        {
            UpdateApiKeyEnabled();
        }

        private void UpdateApiKeyEnabled()
        {
            bool enabled = !chkNoAuthApi.Checked;
            lblInvoiceApiKey.Enabled = enabled;
            txtInvoiceApiKey.Enabled = enabled;
            chkShowApiKey.Enabled = enabled;
        }

        private void LoadSigningSettings()
        {
            try
            {
                if (!File.Exists(xmlFilePath))
                {
                    LoadDefaultSigningSettings();
                    return;
                }

                var xmlDoc = XDocument.Load(xmlFilePath);
                var envelope = xmlDoc.Element("ENVELOPE");
                if (envelope == null)
                {
                    LoadDefaultSigningSettings();
                    return;
                }

                var fileNameLists = envelope.Element("FILENAMELIST")?.Elements("FILENAMELIST").ToList();
                if (fileNameLists == null || fileNameLists.Count < 10)
                {
                    LoadDefaultSigningSettings();
                    return;
                }

                // Load values
                txtInputFile.Text = fileNameLists[0].Element("FILENAME")?.Value ?? "";
                txtOutputFolder.Text = fileNameLists[1].Element("FILENAME")?.Value ?? "";
                txtCommonName.Text = fileNameLists[2].Element("FILENAME")?.Value ?? "";
                txtPin.Text = fileNameLists[3].Element("FILENAME")?.Value ?? "";

                numXCoord.Value = decimal.Parse(fileNameLists[4].Element("FILENAME")?.Value ?? "400");
                numYCoord.Value = decimal.Parse(fileNameLists[5].Element("FILENAME")?.Value ?? "75");
                numWidth.Value = decimal.Parse(fileNameLists[6].Element("FILENAME")?.Value ?? "150");
                numHeight.Value = decimal.Parse(fileNameLists[7].Element("FILENAME")?.Value ?? "50");

                // Index 8: reserved/unused (was Sign On Page) - kept only for positional compatibility

                string openFolder = fileNameLists[9].Element("FILENAME")?.Value ?? "Y";
                cmbOpenOutputFolder.SelectedIndex = openFolder == "Y" ? 0 : 1;

                if (fileNameLists.Count > 10)
                {
                    string useSelfSigned = fileNameLists[10].Element("FILENAME")?.Value ?? "N";
                    cmbUseSelfSigned.SelectedIndex = useSelfSigned.ToUpper() == "Y" ? 0 : 1;
                }
                else
                {
                    cmbUseSelfSigned.SelectedIndex = 1;
                }

                if (fileNameLists.Count > 16)
                {
                    string copy1Label = fileNameLists[16].Element("FILENAME")?.Value;
                    txtCopy1Label.Text = string.IsNullOrWhiteSpace(copy1Label) ? "Original for Buyer" : copy1Label;

                    string extraCopiesFlag = fileNameLists[17].Element("FILENAME")?.Value ?? "N";
                    chkExtraCopies.Checked = extraCopiesFlag.ToUpper() == "Y";

                    string printAllFlag = fileNameLists[18].Element("FILENAME")?.Value ?? "N";
                    chkPrintAllCopies.Checked = printAllFlag.ToUpper() == "Y";

                    txtCopy2Label.Text = fileNameLists[19].Element("FILENAME")?.Value ?? "Duplicate for Transporter";
                    txtCopy3Label.Text = fileNameLists[20].Element("FILENAME")?.Value ?? "Duplicate for Supplier";
                    txtCopy4Label.Text = fileNameLists[21].Element("FILENAME")?.Value ?? "Extra Copy";

                    numCopyX.Value = decimal.Parse(fileNameLists[22].Element("FILENAME")?.Value ?? "380");
                    numCopyY.Value = decimal.Parse(fileNameLists[23].Element("FILENAME")?.Value ?? "730");
                    numCopyWidth.Value = decimal.Parse(fileNameLists[24].Element("FILENAME")?.Value ?? "180");
                    numCopyHeight.Value = decimal.Parse(fileNameLists[25].Element("FILENAME")?.Value ?? "35");
                }
                else
                {
                    LoadDefaultCopyLabelSettings();
                }

                UpdateExtraCopiesVisibility();
            }
            catch (Exception)
            {
                LoadDefaultSigningSettings();
            }
        }

        private void LoadApiSettings()
        {
            var settings = AppSettingsLoader.Load(AppSettingsLoader.DefaultPath, xmlFilePath);

            chkVerboseMode.Checked = settings.VerboseMode;

            int port = settings.Port;
            numListenerPort.Value = port >= 1024 && port <= 65535 ? port : 8943;

            txtInvoiceApiBaseUrl.Text = settings.InvoiceApiBaseUrl ?? "";
            txtInvoiceApiKey.Text = settings.InvoiceApiKey ?? "";
            chkNoAuthApi.Checked = settings.NoAuthApi;
            UpdateApiKeyEnabled();
            chkIncludeSignedPdfInCallback.Checked = settings.IncludeSignedPdfInCallback;
            txtInvoiceSignedCallbackUrl.Text = settings.InvoiceSignedCallbackUrl ?? "";
            chkBatchMode.Checked = settings.LaunchInBatchMode;

            if (string.IsNullOrWhiteSpace(settings.PrinterName))
            {
                cmbPrinterName.SelectedItem = SystemDefaultPrinterLabel;
            }
            else
            {
                int index = cmbPrinterName.Items.IndexOf(settings.PrinterName);
                if (index >= 0)
                {
                    cmbPrinterName.SelectedIndex = index;
                }
                else
                {
                    // Configured printer is no longer installed - surface it without crashing.
                    cmbPrinterName.Items.Add($"{settings.PrinterName} (not found)");
                    cmbPrinterName.SelectedIndex = cmbPrinterName.Items.Count - 1;
                }
            }

            chkEnableOcspCheck.Checked = settings.EnableOcspCheck;
            int ocspTimeout = settings.OcspTimeoutSeconds;
            numOcspTimeoutSeconds.Value = ocspTimeout >= 1 && ocspTimeout <= 60 ? ocspTimeout : 10;
        }

        private void LoadDefaultSigningSettings()
        {
            txtInputFile.Text = "";
            txtOutputFolder.Text = @"C:\Users\Public";
            txtCommonName.Text = "";
            txtPin.Text = "";
            numXCoord.Value = 400;
            numYCoord.Value = 75;
            numWidth.Value = 150;
            numHeight.Value = 50;
            cmbOpenOutputFolder.SelectedIndex = 0;
            cmbUseSelfSigned.SelectedIndex = 1;
            LoadDefaultCopyLabelSettings();
            UpdateExtraCopiesVisibility();
        }

        private void LoadDefaultCopyLabelSettings()
        {
            txtCopy1Label.Text = "Original for Buyer";
            chkExtraCopies.Checked = false;
            chkPrintAllCopies.Checked = false;
            txtCopy2Label.Text = "Duplicate for Transporter";
            txtCopy3Label.Text = "Duplicate for Supplier";
            txtCopy4Label.Text = "Extra Copy";
            numCopyX.Value = 380;
            numCopyY.Value = 730;
            numCopyWidth.Value = 180;
            numCopyHeight.Value = 35;
        }

        private void LoadDefaultApiSettings()
        {
            chkVerboseMode.Checked = false; // Default to not verbose
            numListenerPort.Value = 8943;
            txtInvoiceApiBaseUrl.Text = "";
            txtInvoiceApiKey.Text = "";
            chkNoAuthApi.Checked = false;
            UpdateApiKeyEnabled();
            chkIncludeSignedPdfInCallback.Checked = true;
            txtInvoiceSignedCallbackUrl.Text = "";
            chkBatchMode.Checked = false; // Default to listener/tray mode
            cmbPrinterName.SelectedItem = SystemDefaultPrinterLabel;
            chkEnableOcspCheck.Checked = true;
            numOcspTimeoutSeconds.Value = 10;
        }
        
        private void BtnSaveSettings_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtCommonName.Text))
                {
                    MessageBox.Show(
                        "Certificate Common Name is required.",
                        "Validation Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    tabControl.SelectedTab = tabSettings;
                    tabSettingsControl.SelectedTab = tabGeneral;
                    txtCommonName.Focus();
                    return;
                }
                
                var xmlDoc = new XDocument(
                    new XElement("ENVELOPE",
                        new XElement("FILENAMELIST",
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", txtInputFile.Text)
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", txtOutputFolder.Text)
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", txtCommonName.Text)
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", txtPin.Text)
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", numXCoord.Value.ToString("0"))
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", numYCoord.Value.ToString("0"))
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", numWidth.Value.ToString("0"))
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", numHeight.Value.ToString("0"))
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", "L"),
                                new XComment(" Reserved/unused (was SignOnPage) - kept only for positional compatibility ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", cmbOpenOutputFolder.SelectedIndex == 0 ? "Y" : "N"),
                                new XComment(" Open Output folder after signing: Y=output folder will be open after signing, N=Output folder will not open after signing, default value=Y ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", cmbUseSelfSigned.SelectedIndex == 0 ? "Y" : "N"),
                                new XComment(" USESELFSIGNED ")
                            ),
                            new XElement("FILENAMELIST", new XElement("FILENAME", ""), new XComment(" Reserved (legacy appsettings.json migration slot) ")),
                            new XElement("FILENAMELIST", new XElement("FILENAME", ""), new XComment(" Reserved (legacy appsettings.json migration slot) ")),
                            new XElement("FILENAMELIST", new XElement("FILENAME", ""), new XComment(" Reserved (legacy appsettings.json migration slot) ")),
                            new XElement("FILENAMELIST", new XElement("FILENAME", ""), new XComment(" Reserved (legacy appsettings.json migration slot) ")),
                            new XElement("FILENAMELIST", new XElement("FILENAME", ""), new XComment(" Reserved (legacy appsettings.json migration slot) ")),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", string.IsNullOrWhiteSpace(txtCopy1Label.Text) ? "Original for Buyer" : txtCopy1Label.Text),
                                new XComment(" Copy 1 label (mandatory) ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", chkExtraCopies.Checked ? "Y" : "N"),
                                new XComment(" ExtraCopiesEnabled ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", chkPrintAllCopies.Checked ? "Y" : "N"),
                                new XComment(" PrintAllCopies ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", txtCopy2Label.Text),
                                new XComment(" Copy 2 label (optional, blank = skip) ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", txtCopy3Label.Text),
                                new XComment(" Copy 3 label (optional, blank = skip) ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", txtCopy4Label.Text),
                                new XComment(" Copy 4 label (optional, blank = skip) ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", numCopyX.Value.ToString("0"))
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", numCopyY.Value.ToString("0"))
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", numCopyWidth.Value.ToString("0"))
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", numCopyHeight.Value.ToString("0"))
                            )
                        )
                    )
                );

                xmlDoc.Save(xmlFilePath);

                AppSettingsLoader.Save(new AppSettings
                {
                    VerboseMode = chkVerboseMode.Checked,
                    Port = (int)numListenerPort.Value,
                    InvoiceApiBaseUrl = txtInvoiceApiBaseUrl.Text,
                    InvoiceApiKey = txtInvoiceApiKey.Text,
                    NoAuthApi = chkNoAuthApi.Checked,
                    IncludeSignedPdfInCallback = chkIncludeSignedPdfInCallback.Checked,
                    InvoiceSignedCallbackUrl = txtInvoiceSignedCallbackUrl.Text,
                    LaunchInBatchMode = chkBatchMode.Checked,
                    PrinterName = cmbPrinterName.SelectedItem?.ToString() == SystemDefaultPrinterLabel
                        ? ""
                        : cmbPrinterName.SelectedItem?.ToString() ?? "",
                    EnableOcspCheck = chkEnableOcspCheck.Checked,
                    OcspTimeoutSeconds = (int)numOcspTimeoutSeconds.Value
                });

                MessageBox.Show(
                    "Settings saved successfully!\n\nThe new settings will be used for PDF signing operations.",
                    "Settings Saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving settings: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
        
        private void BtnSaveApiSettings_Click(object sender, EventArgs e)
        {
            try
            {
                AppSettingsLoader.Save(new AppSettings
                {
                    VerboseMode = chkVerboseMode.Checked,
                    Port = (int)numListenerPort.Value,
                    InvoiceApiBaseUrl = txtInvoiceApiBaseUrl.Text,
                    InvoiceApiKey = txtInvoiceApiKey.Text,
                    NoAuthApi = chkNoAuthApi.Checked,
                    IncludeSignedPdfInCallback = chkIncludeSignedPdfInCallback.Checked,
                    InvoiceSignedCallbackUrl = txtInvoiceSignedCallbackUrl.Text,
                    LaunchInBatchMode = chkBatchMode.Checked,
                    PrinterName = cmbPrinterName.SelectedItem?.ToString() == SystemDefaultPrinterLabel
                        ? ""
                        : cmbPrinterName.SelectedItem?.ToString() ?? "",
                    EnableOcspCheck = chkEnableOcspCheck.Checked,
                    OcspTimeoutSeconds = (int)numOcspTimeoutSeconds.Value
                });

                MessageBox.Show(
                    "API settings saved successfully!\n\nRestart the listener ('DigiSign.exe /listen') for changes to take effect.",
                    "Settings Saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving API settings: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void BtnResetSettings_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to default values?",
                "Reset Settings",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            
            if (result == DialogResult.Yes)
            {
                LoadDefaultSigningSettings();
                LoadDefaultApiSettings();
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
        
        private void PreviewSettings_Changed(object sender, EventArgs e)
        {
            // Update preview when settings change
            if (tabControl.SelectedTab == tabSettings && tabSettingsControl.SelectedTab == tabPreview)
            {
                UpdatePreview();
            }
        }
        
        private void BtnZoomIn_Click(object sender, EventArgs e)
        {
            if (zoomLevel < 3.0f)
            {
                zoomLevel += 0.25f;
                UpdatePreview();
                UpdateZoomLabel();
            }
        }
        
        private void BtnZoomOut_Click(object sender, EventArgs e)
        {
            if (zoomLevel > 0.25f)
            {
                zoomLevel -= 0.25f;
                UpdatePreview();
                UpdateZoomLabel();
            }
        }
        
        private void BtnZoomReset_Click(object sender, EventArgs e)
        {
            zoomLevel = 1.0f;
            UpdatePreview();
            UpdateZoomLabel();
        }
        
        private void PicPreview_MouseWheel(object sender, MouseEventArgs e)
        {
            // Zoom with mouse wheel
            if (e.Delta > 0 && zoomLevel < 3.0f)
            {
                zoomLevel += 0.1f;
            }
            else if (e.Delta < 0 && zoomLevel > 0.25f)
            {
                zoomLevel -= 0.1f;
            }
            
            UpdatePreview();
            UpdateZoomLabel();
        }
        
        private void PicPreview_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            
            // Get signature rectangle in screen coordinates
            RectangleF sigRect = GetSignatureScreenRect();
            
            // Check if clicking on resize handle
            ResizeHandle handle = GetResizeHandleAtPoint(e.Location, sigRect);
            
            if (handle != ResizeHandle.None)
            {
                if (handle == ResizeHandle.Move)
                {
                    isDraggingSignature = true;
                    isResizingSignature = false;
                }
                else
                {
                    isResizingSignature = true;
                    isDraggingSignature = false;
                    activeResizeHandle = handle;
                }
                
                lastMousePosition = e.Location;
                picPreview.Cursor = GetCursorForHandle(handle);
                picPreview.Invalidate();
            }
        }
        
        private void PicPreview_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingSignature || isResizingSignature)
            {
                // Calculate delta
                int deltaX = e.X - lastMousePosition.X;
                int deltaY = e.Y - lastMousePosition.Y;
                
                if (isDraggingSignature)
                {
                    // Move signature
                    UpdateSignaturePosition(deltaX, deltaY);
                }
                else if (isResizingSignature)
                {
                    // Resize signature
                    ResizeSignature(deltaX, deltaY, activeResizeHandle);
                }
                
                lastMousePosition = e.Location;
                picPreview.Invalidate(); // Trigger Paint event for visual feedback
            }
            else
            {
                // Update cursor based on hover
                RectangleF sigRect = GetSignatureScreenRect();
                ResizeHandle handle = GetResizeHandleAtPoint(e.Location, sigRect);
                picPreview.Cursor = GetCursorForHandle(handle);
            }
        }
        
        private void PicPreview_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingSignature || isResizingSignature)
            {
                isDraggingSignature = false;
                isResizingSignature = false;
                activeResizeHandle = ResizeHandle.None;
                
                // Update the preview to refresh
                UpdatePreview();
                
                picPreview.Cursor = Cursors.Hand;
            }
        }
        
        private void PicPreview_Paint(object sender, PaintEventArgs e)
        {
            // Draw resize handles if dragging or resizing
            if (isDraggingSignature || isResizingSignature)
            {
                RectangleF sigRect = GetSignatureScreenRect();
                DrawResizeHandles(e.Graphics, sigRect);
            }
        }
        
        private RectangleF GetSignatureScreenRect()
        {
            // Use actual PDF dimensions instead of hardcoded values
            float pdfWidth = currentPdfWidth;
            float pdfHeight = currentPdfHeight;

            // Get signature settings in PDF coordinates
            float x = (float)numXCoord.Value;
            float y = (float)numYCoord.Value;
            float width = (float)numWidth.Value;
            float height = (float)numHeight.Value;

            // Apply zoom scale
            float scale = zoomLevel;

            // Convert to screen coordinates
            float screenX = x * scale;
            float screenY = (pdfHeight - y - height) * scale; // PDF coords start at bottom-left
            float screenWidth = width * scale;
            float screenHeight = height * scale;

            return new RectangleF(screenX, screenY, screenWidth, screenHeight);
        }
        
        private ResizeHandle GetResizeHandleAtPoint(Point p, RectangleF rect)
        {
            int tolerance = HANDLE_SIZE / 2 + 2;
            
            // Check corners first (priority)
            if (IsPointNearCorner(p, rect.Left, rect.Top, tolerance))
                return ResizeHandle.TopLeft;
            if (IsPointNearCorner(p, rect.Right, rect.Top, tolerance))
                return ResizeHandle.TopRight;
            if (IsPointNearCorner(p, rect.Left, rect.Bottom, tolerance))
                return ResizeHandle.BottomLeft;
            if (IsPointNearCorner(p, rect.Right, rect.Bottom, tolerance))
                return ResizeHandle.BottomRight;
            
            // Check edges
            if (Math.Abs(p.Y - rect.Top) < tolerance && p.X > rect.Left + tolerance && p.X < rect.Right - tolerance)
                return ResizeHandle.Top;
            if (Math.Abs(p.Y - rect.Bottom) < tolerance && p.X > rect.Left + tolerance && p.X < rect.Right - tolerance)
                return ResizeHandle.Bottom;
            if (Math.Abs(p.X - rect.Left) < tolerance && p.Y > rect.Top + tolerance && p.Y < rect.Bottom - tolerance)
                return ResizeHandle.Left;
            if (Math.Abs(p.X - rect.Right) < tolerance && p.Y > rect.Top + tolerance && p.Y < rect.Bottom - tolerance)
                return ResizeHandle.Right;
            
            // Check if inside rectangle (move)
            if (rect.Contains(p))
                return ResizeHandle.Move;
            
            return ResizeHandle.None;
        }
        
        private bool IsPointNearCorner(Point p, float cornerX, float cornerY, int tolerance)
        {
            return Math.Abs(p.X - cornerX) < tolerance && Math.Abs(p.Y - cornerY) < tolerance;
        }
        
        private Cursor GetCursorForHandle(ResizeHandle handle)
        {
            switch (handle)
            {
                case ResizeHandle.TopLeft:
                case ResizeHandle.BottomRight:
                    return Cursors.SizeNWSE;
                case ResizeHandle.TopRight:
                case ResizeHandle.BottomLeft:
                    return Cursors.SizeNESW;
                case ResizeHandle.Top:
                case ResizeHandle.Bottom:
                    return Cursors.SizeNS;
                case ResizeHandle.Left:
                case ResizeHandle.Right:
                    return Cursors.SizeWE;
                case ResizeHandle.Move:
                    return Cursors.SizeAll;
                default:
                    return Cursors.Hand;
            }
        }
        
        private void UpdateSignaturePosition(int deltaX, int deltaY)
        {
            // Convert screen delta to PDF delta
            float pdfDeltaX = deltaX / zoomLevel;
            float pdfDeltaY = -deltaY / zoomLevel; // Invert Y because PDF coords are bottom-up
            
            // Update X coordinate
            decimal newX = numXCoord.Value + (decimal)pdfDeltaX;
            newX = Math.Max(numXCoord.Minimum, Math.Min(numXCoord.Maximum, newX));
            numXCoord.Value = newX;
            
            // Update Y coordinate
            decimal newY = numYCoord.Value + (decimal)pdfDeltaY;
            newY = Math.Max(numYCoord.Minimum, Math.Min(numYCoord.Maximum, newY));
            numYCoord.Value = newY;
        }
        
        private void ResizeSignature(int deltaX, int deltaY, ResizeHandle handle)
        {
            // Convert screen delta to PDF delta
            float pdfDeltaX = deltaX / zoomLevel;
            float pdfDeltaY = -deltaY / zoomLevel; // Invert Y
            
            decimal currentX = numXCoord.Value;
            decimal currentY = numYCoord.Value;
            decimal currentWidth = numWidth.Value;
            decimal currentHeight = numHeight.Value;
            
            switch (handle)
            {
                case ResizeHandle.TopLeft:
                    // Move top-left corner
                    numXCoord.Value = Math.Max(numXCoord.Minimum, Math.Min(numXCoord.Maximum, currentX + (decimal)pdfDeltaX));
                    numYCoord.Value = Math.Max(numYCoord.Minimum, Math.Min(numYCoord.Maximum, currentY + (decimal)pdfDeltaY));
                    numWidth.Value = Math.Max(numWidth.Minimum, currentWidth - (decimal)pdfDeltaX);
                    numHeight.Value = Math.Max(numHeight.Minimum, currentHeight - (decimal)pdfDeltaY);
                    break;
                    
                case ResizeHandle.TopRight:
                    // Move top-right corner
                    numYCoord.Value = Math.Max(numYCoord.Minimum, Math.Min(numYCoord.Maximum, currentY + (decimal)pdfDeltaY));
                    numWidth.Value = Math.Max(numWidth.Minimum, currentWidth + (decimal)pdfDeltaX);
                    numHeight.Value = Math.Max(numHeight.Minimum, currentHeight - (decimal)pdfDeltaY);
                    break;
                    
                case ResizeHandle.BottomLeft:
                    // Move bottom-left corner
                    numXCoord.Value = Math.Max(numXCoord.Minimum, Math.Min(numXCoord.Maximum, currentX + (decimal)pdfDeltaX));
                    numWidth.Value = Math.Max(numWidth.Minimum, currentWidth - (decimal)pdfDeltaX);
                    numHeight.Value = Math.Max(numHeight.Minimum, currentHeight + (decimal)pdfDeltaY);
                    break;
                    
                case ResizeHandle.BottomRight:
                    // Move bottom-right corner
                    numWidth.Value = Math.Max(numWidth.Minimum, currentWidth + (decimal)pdfDeltaX);
                    numHeight.Value = Math.Max(numHeight.Minimum, currentHeight + (decimal)pdfDeltaY);
                    break;
                    
                case ResizeHandle.Top:
                    numYCoord.Value = Math.Max(numYCoord.Minimum, Math.Min(numYCoord.Maximum, currentY + (decimal)pdfDeltaY));
                    numHeight.Value = Math.Max(numHeight.Minimum, currentHeight - (decimal)pdfDeltaY);
                    break;
                    
                case ResizeHandle.Bottom:
                    numHeight.Value = Math.Max(numHeight.Minimum, currentHeight + (decimal)pdfDeltaY);
                    break;
                    
                case ResizeHandle.Left:
                    numXCoord.Value = Math.Max(numXCoord.Minimum, Math.Min(numXCoord.Maximum, currentX + (decimal)pdfDeltaX));
                    numWidth.Value = Math.Max(numWidth.Minimum, currentWidth - (decimal)pdfDeltaX);
                    break;
                    
                case ResizeHandle.Right:
                    numWidth.Value = Math.Max(numWidth.Minimum, currentWidth + (decimal)pdfDeltaX);
                    break;
            }
        }
        
        private void DrawResizeHandles(Graphics g, RectangleF rect)
        {
            // Draw handles at corners and edges
            using (SolidBrush handleBrush = new SolidBrush(Color.White))
            using (Pen handlePen = new Pen(Color.FromArgb(0, 120, 215), 2))
            {
                // Corner handles
                DrawHandle(g, rect.Left, rect.Top, handleBrush, handlePen);
                DrawHandle(g, rect.Right, rect.Top, handleBrush, handlePen);
                DrawHandle(g, rect.Left, rect.Bottom, handleBrush, handlePen);
                DrawHandle(g, rect.Right, rect.Bottom, handleBrush, handlePen);
                
                // Edge handles
                DrawHandle(g, rect.Left + rect.Width / 2, rect.Top, handleBrush, handlePen);
                DrawHandle(g, rect.Left + rect.Width / 2, rect.Bottom, handleBrush, handlePen);
                DrawHandle(g, rect.Left, rect.Top + rect.Height / 2, handleBrush, handlePen);
                DrawHandle(g, rect.Right, rect.Top + rect.Height / 2, handleBrush, handlePen);
            }
        }
        
        private void DrawHandle(Graphics g, float x, float y, Brush brush, Pen pen)
        {
            float halfSize = HANDLE_SIZE / 2f;
            RectangleF handleRect = new RectangleF(x - halfSize, y - halfSize, HANDLE_SIZE, HANDLE_SIZE);
            g.FillRectangle(brush, handleRect);
            g.DrawRectangle(pen, handleRect.X, handleRect.Y, handleRect.Width, handleRect.Height);
        }
        
        private void UpdateZoomLabel()
        {
            lblZoom.Text = $"Zoom: {(int)(zoomLevel * 100)}%";
        }
        
        private void UpdatePreview()
        {
            try
            {
                // Get current page to preview
                int pageNumber = cmbPreviewPage.SelectedIndex + 1;
                
                // Get input PDF from General settings tab
                string inputPdf = txtInputFile.Text;
                Bitmap previewBitmap = null;
                bool usedMockPdf = false;
                
                // Try to load the PDF from General settings
                if (!string.IsNullOrEmpty(inputPdf))
                {
                    // Check if file exists
                    if (File.Exists(inputPdf))
                    {
                        try
                        {
                            // Try to load actual PDF
                            previewBitmap = RenderPdfPageWithSignature(inputPdf, pageNumber);
                            usedMockPdf = false;
                            
                            // Success - update info label
                            lblPreviewInfo.Text = $"Preview: {Path.GetFileName(inputPdf)} (use mouse wheel to zoom)";
                            lblPreviewInfo.ForeColor = Color.FromArgb(64, 64, 64);
                        }
                        catch (Exception ex)
                        {
                            // If PDF is invalid, fall back to mock PDF
                            previewBitmap = CreateMockPdfPreview();
                            usedMockPdf = true;
                            
                            // Update info label to show error
                            lblPreviewInfo.Text = $"? Cannot read PDF file. Using mock preview. Error: {ex.Message}";
                            lblPreviewInfo.ForeColor = Color.Red;
                        }
                    }
                    else
                    {
                        // File doesn't exist - use mock PDF
                        previewBitmap = CreateMockPdfPreview();
                        usedMockPdf = true;
                        
                        lblPreviewInfo.Text = $"? File not found: {Path.GetFileName(inputPdf)}. Using mock preview.";
                        lblPreviewInfo.ForeColor = Color.Orange;
                    }
                }
                else
                {
                    // No file selected - use mock PDF
                    previewBitmap = CreateMockPdfPreview();
                    usedMockPdf = true;
                    
                    lblPreviewInfo.Text = "No input file selected. Using mock preview (use mouse wheel to zoom)";
                    lblPreviewInfo.ForeColor = Color.FromArgb(64, 64, 64);
                }
                
                // Display in picture box
                if (picPreview.Image != null)
                {
                    picPreview.Image.Dispose();
                }
                picPreview.Image = previewBitmap;
                
                // Update zoom label
                UpdateZoomLabel();
            }
            catch (Exception ex)
            {
                // Show error in preview
                Bitmap errorBitmap = new Bitmap(660, 320);
                using (Graphics g = Graphics.FromImage(errorBitmap))
                {
                    g.Clear(Color.White);
                    using (Font font = new Font("Segoe UI", 9))
                    {
                        g.DrawString($"Error generating preview:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                            font, Brushes.Red, new RectangleF(10, 10, 640, 300));
                    }
                }
                
                if (picPreview.Image != null)
                {
                    picPreview.Image.Dispose();
                }
                picPreview.Image = errorBitmap;
                
                lblPreviewInfo.Text = "? Error generating preview";
                lblPreviewInfo.ForeColor = Color.Red;
            }
        }
        
        private Bitmap RenderPdfPageWithSignature(string pdfPath, int pageNumber)
        {
            // Base size for A4 at 72 DPI
            int baseWidth = 595;
            int baseHeight = 842;
            
            // Apply zoom
            int width = (int)(baseWidth * zoomLevel);
            int height = (int)(baseHeight * zoomLevel);
            
            // Create a bitmap to render the PDF page
            Bitmap bitmap = new Bitmap(width, height);
            
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                
                try
                {
                    // Use PDFtoImage (PDFium + SkiaSharp) to render actual PDF content
                    byte[] pdfBytes = File.ReadAllBytes(pdfPath);

                    // Update page count in combo box if needed
                    int totalPages = Conversion.GetPageCount(pdfBytes);
                    if (cmbPreviewPage.Items.Count != totalPages)
                    {
                        int selectedIndex = cmbPreviewPage.SelectedIndex;
                        cmbPreviewPage.Items.Clear();
                        for (int i = 1; i <= totalPages; i++)
                        {
                            cmbPreviewPage.Items.Add($"Page {i}");
                        }
                        cmbPreviewPage.SelectedIndex = Math.Min(selectedIndex, totalPages - 1);
                    }

                    // Ensure page number is valid
                    if (pageNumber > totalPages)
                    {
                        pageNumber = totalPages;
                    }

                    // Get page size
                    SizeF pdfPageSize = Conversion.GetPageSize(pdfBytes, pageNumber - 1);
                    float pdfWidth = pdfPageSize.Width;
                    float pdfHeight = pdfPageSize.Height;

                    // Store actual PDF dimensions for resize handle positioning
                    currentPdfWidth = pdfWidth;
                    currentPdfHeight = pdfHeight;

                    // Render the PDF page to an image using PDFtoImage
                    var renderOptions = new RenderOptions(Width: width, Height: height, WithAnnotations: true, WithFormFill: true);
                    using (SKBitmap pageBitmap = Conversion.ToImage(pdfBytes, pageNumber - 1, password: null, options: renderOptions))
                    using (var pngStream = new MemoryStream())
                    {
                        pageBitmap.Encode(pngStream, SKEncodedImageFormat.Png, 100);
                        pngStream.Position = 0;

                        using (System.Drawing.Image pageImage = System.Drawing.Image.FromStream(pngStream))
                        {
                            // Draw the rendered PDF page onto our bitmap, scaled to fit
                            g.DrawImage(pageImage, 0, 0, width, height);
                        }
                    }

                    // Draw semi-transparent info overlay at top
                    using (Font titleFont = new Font("Segoe UI", 9 * zoomLevel, FontStyle.Bold))
                    using (Font infoFont = new Font("Segoe UI", 7 * zoomLevel))
                    using (SolidBrush semitransparentBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(80, 80, 80)))
                    {
                        // Semi-transparent background for text (smaller now)
                        float bannerHeight = 50 * zoomLevel;
                        g.FillRectangle(semitransparentBrush, 0, 0, width, bannerHeight);
                        
                        g.DrawString($"?? {Path.GetFileName(pdfPath)}", 
                            titleFont, textBrush, 
                            new PointF(8 * zoomLevel, 6 * zoomLevel));
                        
                        g.DrawString($"Page {pageNumber} of {totalPages} � {pdfWidth:F0} x {pdfHeight:F0} pt", 
                            infoFont, textBrush, 
                            new PointF(8 * zoomLevel, 25 * zoomLevel));
                    }
                    
                    // Draw grid for reference (subtle)
                    DrawGrid(g, width, height);
                    
                    // Calculate scale factor
                    float scale = zoomLevel;
                    
                    // Draw signature rectangle at specified position
                    DrawSignatureRectangle(g, scale, pdfWidth, pdfHeight);
                }
                catch (Exception ex)
                {
                    // If PDFtoImage rendering fails, fall back to iTextSharp metadata-only view
                    try
                    {
                        using (var reader = new iTextSharp.text.pdf.PdfReader(pdfPath))
                        {
                            // Update page count
                            int totalPages = reader.NumberOfPages;
                            if (cmbPreviewPage.Items.Count != totalPages)
                            {
                                int selectedIndex = cmbPreviewPage.SelectedIndex;
                                cmbPreviewPage.Items.Clear();
                                for (int i = 1; i <= totalPages; i++)
                                {
                                    cmbPreviewPage.Items.Add($"Page {i}");
                                }
                                cmbPreviewPage.SelectedIndex = Math.Min(selectedIndex, totalPages - 1);
                            }
                            
                            if (pageNumber > totalPages)
                            {
                                pageNumber = totalPages;
                            }
                            
                            var pageSize = reader.GetPageSizeWithRotation(pageNumber);
                            float pdfWidth = pageSize.Width;
                            float pdfHeight = pageSize.Height;

                            // Store actual PDF dimensions for resize handle positioning
                            currentPdfWidth = pdfWidth;
                            currentPdfHeight = pdfHeight;
                            
                            // Draw placeholder representation with error message
                            g.FillRectangle(Brushes.WhiteSmoke, 0, 0, width, height);
                            g.DrawRectangle(new Pen(Color.DarkGray, 2), 0, 0, width - 1, height - 1);
                            
                            using (Font titleFont = new Font("Segoe UI", 10 * zoomLevel, FontStyle.Bold))
                            using (Font infoFont = new Font("Segoe UI", 8 * zoomLevel))
                            using (Font errorFont = new Font("Segoe UI", 8 * zoomLevel, FontStyle.Italic))
                            {
                                g.DrawString($"?? {Path.GetFileName(pdfPath)}", 
                                    titleFont, Brushes.DarkRed, 
                                    new PointF(10 * zoomLevel, 10 * zoomLevel));
                                
                                g.DrawString($"Page {pageNumber} of {totalPages} � Size: {pdfWidth:F0} x {pdfHeight:F0} pt", 
                                    infoFont, Brushes.Gray, 
                                    new PointF(10 * zoomLevel, 35 * zoomLevel));
                                
                                g.DrawString($"? Cannot render PDF content", 
                                    errorFont, Brushes.Orange, 
                                    new PointF(10 * zoomLevel, 60 * zoomLevel));
                                
                                g.DrawString($"Error: {ex.Message}", 
                                    errorFont, Brushes.Red, 
                                    new PointF(10 * zoomLevel, 80 * zoomLevel));
                                
                                g.DrawString($"Layout preview mode - signature placement shown below", 
                                    errorFont, Brushes.Gray, 
                                    new PointF(10 * zoomLevel, 100 * zoomLevel));
                            }
                            
                            // Draw simulated content
                            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(220, 200, 200, 200)))
                            {
                                float lineHeight = 15 * zoomLevel;
                                float margin = 50 * zoomLevel;
                                float contentWidth = width - (2 * margin);
                                
                                for (float y = margin + 120 * zoomLevel; y < height - margin; y += lineHeight)
                                {
                                    float lineWidth = contentWidth * (0.7f + (float)(new Random().NextDouble() * 0.3));
                                    g.FillRectangle(textBrush, margin, y, lineWidth, 8 * zoomLevel);
                                }
                            }
                            
                            DrawGrid(g, width, height);
                            float scale = zoomLevel;
                            DrawSignatureRectangle(g, scale, pdfWidth, pdfHeight);
                        }
                    }
                    catch
                    {
                        // Both methods failed - throw to trigger mock PDF
                        throw;
                    }
                }
            }
            
            return bitmap;
        }
        
        private Bitmap CreateMockPdfPreview()
        {
            // Base size for A4 at 72 DPI
            int baseWidth = 595;
            int baseHeight = 842;

            // Store dimensions for resize handle positioning
            currentPdfWidth = baseWidth;
            currentPdfHeight = baseHeight;

            // Apply zoom
            int width = (int)(baseWidth * zoomLevel);
            int height = (int)(baseHeight * zoomLevel);
            
            // Create a mock A4 page
            Bitmap bitmap = new Bitmap(width, height);
            
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                
                // Draw page border
                g.DrawRectangle(Pens.DarkGray, 0, 0, width - 1, height - 1);
                
                // Draw mock content
                using (Font titleFont = new Font("Segoe UI", 16 * zoomLevel, FontStyle.Bold))
                using (Font subtitleFont = new Font("Segoe UI", 10 * zoomLevel))
                using (Font textFont = new Font("Segoe UI", 9 * zoomLevel))
                using (Font smallFont = new Font("Segoe UI", 8 * zoomLevel, FontStyle.Italic))
                {
                    g.DrawString("Mock PDF Document", 
                        titleFont, Brushes.Black, 
                        new PointF(50 * zoomLevel, 50 * zoomLevel));
                    
                    g.DrawString("Signature Placement Preview", 
                        subtitleFont, Brushes.Gray, 
                        new PointF(50 * zoomLevel, 90 * zoomLevel));
                    
                    g.DrawString("Select an input PDF file in the General tab for actual PDF preview", 
                        smallFont, Brushes.LightGray, 
                        new PointF(50 * zoomLevel, 120 * zoomLevel));
                    
                    // Draw some mock text lines
                    for (int i = 0; i < 20; i++)
                    {
                        g.DrawString($"Sample text line {i + 1} - Lorem ipsum dolor sit amet", 
                            textFont, Brushes.LightGray, 
                            new PointF(50 * zoomLevel, (160 + i * 20) * zoomLevel));
                    }
                }
                
                // Draw grid for reference
                DrawGrid(g, width, height);
                
                // Draw signature rectangle
                float scale = zoomLevel;
                DrawSignatureRectangle(g, scale, baseWidth, baseHeight);
            }
            
            return bitmap;
        }
        
        private void DrawGrid(Graphics g, int width, int height)
        {
            // Draw light grid for reference (every 50 pixels at zoom 1.0)
            using (Pen gridPen = new Pen(Color.FromArgb(30, 200, 200, 200)))
            {
                int gridSpacing = (int)(50 * zoomLevel);
                
                // Vertical lines
                for (int x = 0; x < width; x += gridSpacing)
                {
                    g.DrawLine(gridPen, x, 0, x, height);
                }
                
                // Horizontal lines
                for (int y = 0; y < height; y += gridSpacing)
                {
                    g.DrawLine(gridPen, 0, y, width, y);
                }
            }
        }
        
        private void DrawSignatureRectangle(Graphics g, float scale, float pdfWidth, float pdfHeight)
        {
            // Get signature settings
            float x = (float)numXCoord.Value * scale;
            float y = (float)numYCoord.Value * scale;
            float width = (float)numWidth.Value * scale;
            float height = (float)numHeight.Value * scale;

            // Adjust Y coordinate (PDF coordinates start from bottom-left, screen from top-left)
            float scaledPdfHeight = pdfHeight * scale;
            float adjustedY = scaledPdfHeight - y - height;

            // Draw signature rectangle with white/light background (matching actual PDF)
            using (Brush fillBrush = new SolidBrush(Color.FromArgb(250, 250, 250, 250)))
            {
                g.FillRectangle(fillBrush, x, adjustedY, width, height);
            }

            // Draw solid border (matching actual PDF)
            using (Pen borderPen = new Pen(Color.Black, 1 * scale))
            {
                g.DrawRectangle(borderPen, x, adjustedY, width, height);
            }

            // Draw signature text preview - matching actual PDF text rendering
            // Actual PDF draws from top-left with padding, not centered
            float padding = 3 * scale;
            float fontSizeCN = 10 * scale;
            float fontSizeText = 9 * scale;
            float lineHeight = fontSizeCN + 3 * scale;
            float currentY = adjustedY + padding;

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // Draw Common Name (bold, larger font)
            if (!string.IsNullOrWhiteSpace(txtCommonName.Text))
            {
                using (Font cnFont = new Font("Times New Roman", fontSizeCN, FontStyle.Bold))
                {
                    g.DrawString(txtCommonName.Text, 
                        cnFont, 
                        Brushes.Black, 
                        new PointF(x + padding, currentY));
                    currentY += lineHeight;
                }
            }

            // Draw empty line (matching actual PDF)
            currentY += fontSizeText;

            // Draw Date
            using (Font textFont = new Font("Helvetica", fontSizeText, FontStyle.Regular))
            {
                string dateText = $"Date: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                g.DrawString(dateText, 
                    textFont, 
                    Brushes.Black, 
                    new PointF(x + padding, currentY));
            }

            // Draw dimension labels (for debugging/preview)
            using (Font labelFont = new Font("Segoe UI", 7 * scale))
            {
                g.DrawString($"X: {numXCoord.Value}, Y: {numYCoord.Value}", 
                    labelFont, 
                    Brushes.Blue, 
                    new PointF(x, adjustedY - 15 * scale));

                g.DrawString($"{numWidth.Value} x {numHeight.Value} px", 
                    labelFont, 
                    Brushes.Blue, 
                    new PointF(x, adjustedY + height + 5 * scale));
            }
        }

        private void BtnSignPdf_Click(object sender, EventArgs e)
        {
            try
            {
                // Get input PDF path
                string inputPdfPath = txtInputFile.Text;

                // Check if we have a valid input file or should use mock PDF
                bool usingMockPdf = string.IsNullOrEmpty(inputPdfPath) || !File.Exists(inputPdfPath);

                if (usingMockPdf)
                {
                    // Ask user if they want to select a PDF or create a blank one
                    DialogResult result = MessageBox.Show(
                        "No input PDF file is selected.\n\n" +
                        "Click 'Yes' to select a PDF file to sign\n" +
                        "Click 'No' to create and sign a blank PDF document",
                        "Select PDF",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Cancel)
                        return;

                    if (result == DialogResult.Yes)
                    {
                        // Let user select a PDF file
                        using (OpenFileDialog openDialog = new OpenFileDialog())
                        {
                            openDialog.Title = "Select PDF to Sign";
                            openDialog.Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*";
                            openDialog.DefaultExt = "pdf";
                            openDialog.CheckFileExists = true;

                            if (openDialog.ShowDialog() != DialogResult.OK)
                                return;

                            inputPdfPath = openDialog.FileName;
                            usingMockPdf = false;
                        }
                    }
                    else // DialogResult.No - create blank PDF
                    {
                        // Create a temporary blank PDF
                        inputPdfPath = Path.Combine(Path.GetTempPath(), $"blank_{Guid.NewGuid()}.pdf");
                        CreateBlankPdf(inputPdfPath);
                        usingMockPdf = true; // Still considered mock since we created it
                    }
                }

                // Automatically name the output from the invoice number - no save-dialog prompt
                string invoiceNo = string.IsNullOrWhiteSpace(txtTestInvoiceNo.Text)
                    ? GenerateRandomInvoiceNo()
                    : txtTestInvoiceNo.Text.Trim();
                string outputFolder = string.IsNullOrWhiteSpace(txtOutputFolder.Text)
                    ? Path.GetTempPath()
                    : txtOutputFolder.Text;
                Directory.CreateDirectory(outputFolder);
                string safeFileName = string.Join("_", $"{invoiceNo}.pdf".Split(Path.GetInvalidFileNameChars()));
                string outputPdfPath = Path.Combine(outputFolder, safeFileName);

                // Get certificate and signing parameters
                string commonName = txtCommonName.Text;
                string pin = txtPin.Text;

                if (string.IsNullOrWhiteSpace(commonName))
                {
                    MessageBox.Show(
                        "Please enter a certificate Common Name in the General settings tab.",
                        "Certificate Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Create signature configuration from current settings
                var signatureConfig = new SignatureConfiguration(
                    (float)numXCoord.Value,
                    (float)numYCoord.Value,
                    (float)numWidth.Value,
                    (float)numHeight.Value)
                {
                    Copy1Label = txtCopy1Label.Text,
                    ExtraCopiesEnabled = chkExtraCopies.Checked,
                    PrintAllCopies = chkPrintAllCopies.Checked,
                    Copy2Label = txtCopy2Label.Text,
                    Copy3Label = txtCopy3Label.Text,
                    Copy4Label = txtCopy4Label.Text,
                    CopyLabelX = (float)numCopyX.Value,
                    CopyLabelY = (float)numCopyY.Value,
                    CopyLabelWidth = (float)numCopyWidth.Value,
                    CopyLabelHeight = (float)numCopyHeight.Value
                };

                // Load XML data for certificate fallback options
                string xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.xml");
                var xmlData = ReadXmlDataFromForm();

                // Use DigitalSignatureService to sign
                var signatureService = new DigitalSignatureService();

                // Show progress
                this.Cursor = Cursors.WaitCursor;
                btnSignPdf.Enabled = false;
                btnSignPdf.Text = "Signing...";
                Application.DoEvents();

                bool verboseMode = chkVerboseMode.Checked;
                VerboseProgressForm verboseForm = null;

                if (verboseMode)
                {
                    verboseForm = new VerboseProgressForm();
                    verboseForm.Show();
                    verboseForm.AppendText("═══════════════════════════════════════════════════════════\n", Color.Gray, true);
                    verboseForm.AppendText($"{VersionInfo.TitleWithVersion} - VERBOSE MODE\n", Color.FromArgb(0, 102, 204), true);
                    verboseForm.AppendText("═══════════════════════════════════════════════════════════\n\n", Color.Gray, true);
                    verboseForm.UpdateProgress(1, "Loading certificate...");
                    verboseForm.AppendDetail($"Common Name: {commonName}");
                    Application.DoEvents();
                }

                try
                {
                    // Load certificate
                    var cert = signatureService.LoadCertificate(commonName, pin, xmlData);

                    if (cert == null)
                    {
                        if (verboseMode)
                        {
                            verboseForm.AppendError($"Certificate not found: {commonName}");
                            verboseForm.ProcessingComplete(true, 1);
                        }

                        MessageBox.Show(
                            $"Certificate '{commonName}' not found.\n\n" +
                            "Please ensure:\n" +
                            "1. USB token is connected (if using USB certificate)\n" +
                            "2. Certificate Common Name is correct\n" +
                            "3. Certificate is installed in Windows certificate store",
                            "Certificate Not Found",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    if (verboseMode)
                    {
                        verboseForm.AppendSuccess("Certificate loaded");
                        verboseForm.AppendDetail($"Subject: {cert.Subject}");
                        verboseForm.AppendDetail($"Expiry: {cert.NotAfter:yyyy-MM-dd}");
                        verboseForm.UpdateProgress(5, "Signing PDF...");
                        verboseForm.AppendDetail($"Input: {Path.GetFileName(inputPdfPath)}");
                        verboseForm.AppendDetail($"Output: {Path.GetFileName(outputPdfPath)}");
                        Application.DoEvents();
                    }

                    // Sign the PDF
                    signatureService.SignPdf(inputPdfPath, outputPdfPath, cert, signatureConfig, pin, outputFolder, signatureConfig.Copy1Label);

                    if (verboseMode)
                    {
                        verboseForm.AppendSuccess("PDF signed successfully");
                        verboseForm.AppendDetail($"Saved to: {outputPdfPath}");
                        verboseForm.ProcessingComplete(true, 0);
                    }

                    // Success
                    MessageBox.Show(
                        $"PDF signed successfully!\n\nSaved to:\n{outputPdfPath}",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Ask if user wants to open the folder
                    DialogResult openFolder = MessageBox.Show(
                        "Would you like to open the output folder?",
                        "Open Folder",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (openFolder == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{outputPdfPath}\"");
                    }
                }
                catch (Exception)
                {
                    if (verboseMode)
                    {
                        verboseForm.AppendError("PDF signing failed - see error dialog for details");
                        verboseForm.ProcessingComplete(true, 1);
                    }
                    throw;
                }
                finally
                {
                    // Clean up temp file if we created one
                    if (usingMockPdf && inputPdfPath.StartsWith(Path.GetTempPath()) && File.Exists(inputPdfPath))
                    {
                        try { File.Delete(inputPdfPath); } catch { }
                    }

                    // Restore button state
                    this.Cursor = Cursors.Default;
                    btnSignPdf.Enabled = true;
                    btnSignPdf.Text = "Sign PDF";
                }
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                btnSignPdf.Enabled = true;
                btnSignPdf.Text = "Sign PDF";

                MessageBox.Show(
                    $"Error signing PDF:\n\n{ex.Message}\n\nSee logs for more details.",
                    "Signing Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                Logger.Error($"Error signing PDF from preview: {ex.Message}", ex);
            }
        }

        private static string GenerateRandomInvoiceNo() => "TEST-" + new Random().Next(100000, 999999);

        private void CreateBlankPdf(string outputPath)
        {
            // Create a simple blank A4 PDF
            var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4);
            using (var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, new FileStream(outputPath, FileMode.Create)))
            {
                document.Open();
                document.Add(new iTextSharp.text.Paragraph("Blank Document"));
                document.Add(new iTextSharp.text.Paragraph("\n\n"));
                document.Add(new iTextSharp.text.Paragraph("This is a blank PDF document created for signing."));
                document.Close();
            }
        }

        private XmlData ReadXmlDataFromForm()
        {
            // Create XmlData from form values for certificate loading
            return new XmlData
            {
                CommonName = txtCommonName.Text,
                Pin = txtPin.Text,
                XCoordinate = (float)numXCoord.Value,
                YCoordinate = (float)numYCoord.Value,
                Width = (float)numWidth.Value,
                Height = (float)numHeight.Value,
                UseSelfSigned = cmbUseSelfSigned.SelectedItem?.ToString() == "Yes",
                SelfSignedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selfsigned.pfx"),
                SelfSignedPassword = "password",
                Copy1Label = txtCopy1Label.Text,
                ExtraCopiesEnabled = chkExtraCopies.Checked,
                PrintAllCopies = chkPrintAllCopies.Checked,
                Copy2Label = txtCopy2Label.Text,
                Copy3Label = txtCopy3Label.Text,
                Copy4Label = txtCopy4Label.Text,
                CopyLabelX = (float)numCopyX.Value,
                CopyLabelY = (float)numCopyY.Value,
                CopyLabelWidth = (float)numCopyWidth.Value,
                CopyLabelHeight = (float)numCopyHeight.Value
            };
        }
    }
}
