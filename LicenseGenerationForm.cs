using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PDFtoImage;
using SkiaSharp;
using iTextSharp.text.pdf;

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
        private CheckBox chkEnableListenerMode;
        private CheckBox chkEnableOcspCheck;
        private Label lblOcspTimeoutSeconds;
        private NumericUpDown numOcspTimeoutSeconds;
        private Label lblLicenseStatus;

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
        private Label lblUpdateCheckUrl;
        private TextBox txtUpdateCheckUrl;
        private Button btnCheckForUpdates;
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
        private Panel previewPanel;
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
        
        // Interactive placement - generalized over multiple draggable/resizable boxes
        // (currently: the signature box and the copy-label box)
        private class DraggableBox
        {
            public string Id;
            public NumericUpDown NumX, NumY, NumWidth, NumHeight;
            public Func<string> Caption;
            public Func<string> PreviewText;
            public Color BorderColor;
        }
        private DraggableBox _signatureBox;
        private DraggableBox _copyLabelBox;
        private DraggableBox[] AllBoxes => new[] { _copyLabelBox, _signatureBox }; // draw order: signature on top
        private DraggableBox[] HitTestOrder => new[] { _signatureBox, _copyLabelBox }; // signature wins on overlap

        private DraggableBox activeBox;
        private DraggableBox hoverBox;
        private bool isDraggingBox = false;
        private bool isResizingBox = false;
        private ResizeHandle activeResizeHandle = ResizeHandle.None;
        private Point lastMousePosition;
        private const int HANDLE_SIZE = 8;

        // Cached fonts used to draw box overlays, rebuilt only when zoomLevel changes
        private float _overlayFontsScale = -1f;
        private Font _overlayFontCN;
        private Font _overlayFontText;
        private Font _overlayFontLabel;
        private BaseFont _previewBaseFontCN;

        // Cache of the last rendered PDF/mock page background (boxes are drawn separately on top in Paint)
        private Bitmap _cachedBackground;
        private string _cachedBackgroundKey;

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

        // Persistent restart-into-previous-mode button (settings-only mode)
        private Button btnRestartApp;

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
        public bool RestartRequested { get; private set; }

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
            this.ClientSize = settingsOnlyMode ? new Size(800, 700) : new Size(800, 650);
            this.MinimumSize = settingsOnlyMode ? new Size(800, 700) : new Size(800, 650);
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

            // Persistent "Restart App" button - only meaningful in settings-only mode, where there's a
            // "previously running mode" to relaunch back into. Placed below the tab control (not inside
            // any tab) so it's reachable regardless of which tab/sub-tab is focused.
            if (settingsOnlyMode)
            {
                btnRestartApp = new Button
                {
                    Text = "Restart App",
                    Size = new Size(150, 36),
                    Location = new Point(this.ClientSize.Width - margin - 150, currentY + 570 + 14),
                    Font = new Font("Segoe UI", 10),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    Enabled = false
                };
                btnRestartApp.Click += BtnRestartApp_Click;
                this.Controls.Add(btnRestartApp);
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
                Text = "Used whenever DigiSign runs in listener mode - either via 'DigiSign.exe /listen', or by default when the checkbox below is enabled.",
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
                Value = 5000
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

            // Update checking is an admin-only operation (Ten Info Tech decides when/what to ship) -
            // these controls only exist in /admin mode, never in the customer-facing /settings mode.
            if (!settingsOnlyMode)
            {
                lblUpdateCheckUrl = new Label
                {
                    Text = "Update Check URL (optional - blank = update checking disabled):",
                    Location = new Point(leftMargin, currentY),
                    Size = new Size(660, labelHeight),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold)
                };
                tabApiSettings.Controls.Add(lblUpdateCheckUrl);
                currentY += labelHeight + 5;

                txtUpdateCheckUrl = new TextBox
                {
                    Location = new Point(leftMargin, currentY),
                    Size = new Size(660, textBoxHeight),
                    Font = new Font("Segoe UI", 9)
                };
                tabApiSettings.Controls.Add(txtUpdateCheckUrl);
                currentY += textBoxHeight + spacing;

                btnCheckForUpdates = new Button
                {
                    Text = "Check for Updates",
                    Size = new Size(160, 30),
                    Location = new Point(leftMargin, currentY),
                    Font = new Font("Segoe UI", 9)
                };
                btnCheckForUpdates.Click += BtnCheckForUpdates_Click;
                tabApiSettings.Controls.Add(btnCheckForUpdates);
                currentY += 30 + spacing + 10;
            }

            // Listener Mode toggle
            chkEnableListenerMode = new CheckBox
            {
                Text = "Run as background listener (tray + HTTP API) when started with no arguments",
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, 20),
                Font = new Font("Segoe UI", 9)
            };
            tabApiSettings.Controls.Add(chkEnableListenerMode);
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

            lblLicenseStatus = new Label
            {
                Text = GetLicenseStatusText(),
                Location = new Point(leftMargin, currentY),
                Size = new Size(660, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.DimGray
            };
            tabGeneral.Controls.Add(lblLicenseStatus);
            currentY += textBoxHeight + spacing;
        }

        /// <summary>Short one-line license/trial summary shown on the General settings tab - "Licensed until ...", "Trial: N day(s) remaining", or "Trial expired - a license is required".</summary>
        private static string GetLicenseStatusText()
        {
            string licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.txt");
            if (File.Exists(licensePath) && LicenseManager.ValidateLicense(licensePath))
            {
                int daysRemaining = LicenseManager.GetLicenseExpiryDays(licensePath);
                return daysRemaining >= 0
                    ? $"License status: Licensed ({daysRemaining} day(s) remaining)"
                    : "License status: Licensed";
            }

            string trialPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TrialManager.TrialFileName);
            var trialStatus = TrialManager.GetTrialStatus(trialPath);
            return trialStatus.IsActive
                ? $"License status: Trial ({trialStatus.DaysRemaining} day(s) remaining)"
                : "License status: Trial expired - a license is required";
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
            txtCopy1Label.TextChanged += PreviewSettings_Changed;
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
            numCopyX.ValueChanged += PreviewSettings_Changed;
            tabSignature.Controls.Add(numCopyX);
            currentY += controlHeight + spacing + 5;

            lblCopyY = new Label { Text = "Y Coordinate of Label (pixels):", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopyY);
            numCopyY = new NumericUpDown { Location = new Point(leftMargin + 210, currentY), Size = new Size(120, controlHeight), Font = new Font("Segoe UI", 9), Minimum = 0, Maximum = 10000, DecimalPlaces = 0 };
            numCopyY.ValueChanged += PreviewSettings_Changed;
            tabSignature.Controls.Add(numCopyY);
            currentY += controlHeight + spacing + 5;

            lblCopyWidth = new Label { Text = "Width of Label (pixels):", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopyWidth);
            numCopyWidth = new NumericUpDown { Location = new Point(leftMargin + 210, currentY), Size = new Size(120, controlHeight), Font = new Font("Segoe UI", 9), Minimum = 10, Maximum = 10000, DecimalPlaces = 0 };
            numCopyWidth.ValueChanged += PreviewSettings_Changed;
            tabSignature.Controls.Add(numCopyWidth);
            currentY += controlHeight + spacing + 5;

            lblCopyHeight = new Label { Text = "Height of Label (pixels):", Location = new Point(leftMargin, currentY), Size = new Size(200, labelHeight), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            tabSignature.Controls.Add(lblCopyHeight);
            numCopyHeight = new NumericUpDown { Location = new Point(leftMargin + 210, currentY), Size = new Size(120, controlHeight), Font = new Font("Segoe UI", 9), Minimum = 10, Maximum = 10000, DecimalPlaces = 0 };
            numCopyHeight.ValueChanged += PreviewSettings_Changed;
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
                Text = "Preview of signature and copy-label placement (Ctrl+wheel to zoom, wheel to scroll)",
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
            previewPanel = new Panel
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
            
            // Enable mouse wheel zoom - only on picPreview; if unhandled (plain wheel, no Ctrl) the
            // WM_MOUSEWHEEL message bubbles to previewPanel automatically, which then scrolls natively.
            // (Previously this handler was ALSO subscribed on previewPanel, causing a double zoom-step
            // per physical wheel notch.)
            picPreview.MouseWheel += PicPreview_MouseWheel;

            previewPanel.Controls.Add(picPreview);

            // Add instruction label
            Label lblInstruction = new Label
            {
                Text = "?? Tip: drag either box to move it, drag corners/edges to resize. Blue = signature, green = copy label.",
                Location = new Point(leftMargin, currentY + 330),
                Size = new Size(660, 20),
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            tabPreview.Controls.Add(lblInstruction);

            // Set up the two draggable/resizable overlay boxes sharing the same hit-test/drag/paint
            // pipeline (see DraggableBox, HitTestBoxAt, DrawBoxOverlay). Both tabs' NumericUpDowns
            // already exist at this point since CreateSignatureSettingsTab() runs before this method.
            _signatureBox = new DraggableBox
            {
                Id = "signature",
                NumX = numXCoord,
                NumY = numYCoord,
                NumWidth = numWidth,
                NumHeight = numHeight,
                Caption = () => "Signature",
                BorderColor = Color.FromArgb(0, 120, 215) // blue
            };
            _copyLabelBox = new DraggableBox
            {
                Id = "copyLabel",
                NumX = numCopyX,
                NumY = numCopyY,
                NumWidth = numCopyWidth,
                NumHeight = numCopyHeight,
                Caption = () => "Copy Label",
                // Copies 2-4 share this exact same rectangle and differ only in text, so only Copy 1's
                // text (the mandatory/representative one) is shown live in the preview.
                PreviewText = () => string.IsNullOrWhiteSpace(txtCopy1Label.Text) ? "Original for Buyer" : txtCopy1Label.Text,
                BorderColor = Color.FromArgb(0, 150, 60) // green
            };

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
            numListenerPort.Value = port >= 1024 && port <= 65535 ? port : 5000;

            txtInvoiceApiBaseUrl.Text = settings.InvoiceApiBaseUrl ?? "";
            txtInvoiceApiKey.Text = settings.InvoiceApiKey ?? "";
            chkNoAuthApi.Checked = settings.NoAuthApi;
            UpdateApiKeyEnabled();
            chkIncludeSignedPdfInCallback.Checked = settings.IncludeSignedPdfInCallback;
            txtInvoiceSignedCallbackUrl.Text = settings.InvoiceSignedCallbackUrl ?? "";
            if (txtUpdateCheckUrl != null)
                txtUpdateCheckUrl.Text = settings.UpdateCheckUrl ?? "";
            chkEnableListenerMode.Checked = settings.EnableListenerMode;

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
            numListenerPort.Value = 5000;
            txtInvoiceApiBaseUrl.Text = "";
            txtInvoiceApiKey.Text = "";
            chkNoAuthApi.Checked = false;
            UpdateApiKeyEnabled();
            chkIncludeSignedPdfInCallback.Checked = true;
            txtInvoiceSignedCallbackUrl.Text = "";
            if (txtUpdateCheckUrl != null)
                txtUpdateCheckUrl.Text = "";
            chkEnableListenerMode.Checked = false; // Default to batch/signing mode
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
                    // Update Check URL is only editable in /admin mode; in /settings mode (where the
                    // control doesn't exist) preserve whatever's already on disk instead of blanking it.
                    UpdateCheckUrl = txtUpdateCheckUrl?.Text ?? AppSettingsLoader.Load(AppSettingsLoader.DefaultPath, xmlFilePath).UpdateCheckUrl,
                    EnableListenerMode = chkEnableListenerMode.Checked,
                    PrinterName = cmbPrinterName.SelectedItem?.ToString() == SystemDefaultPrinterLabel
                        ? ""
                        : cmbPrinterName.SelectedItem?.ToString() ?? "",
                    EnableOcspCheck = chkEnableOcspCheck.Checked,
                    OcspTimeoutSeconds = (int)numOcspTimeoutSeconds.Value
                });

                if (btnRestartApp != null)
                    btnRestartApp.Enabled = true;

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
                    // Update Check URL is only editable in /admin mode; in /settings mode (where the
                    // control doesn't exist) preserve whatever's already on disk instead of blanking it.
                    UpdateCheckUrl = txtUpdateCheckUrl?.Text ?? AppSettingsLoader.Load(AppSettingsLoader.DefaultPath, xmlFilePath).UpdateCheckUrl,
                    EnableListenerMode = chkEnableListenerMode.Checked,
                    PrinterName = cmbPrinterName.SelectedItem?.ToString() == SystemDefaultPrinterLabel
                        ? ""
                        : cmbPrinterName.SelectedItem?.ToString() ?? "",
                    EnableOcspCheck = chkEnableOcspCheck.Checked,
                    OcspTimeoutSeconds = (int)numOcspTimeoutSeconds.Value
                });

                if (btnRestartApp != null)
                    btnRestartApp.Enabled = true;

                MessageBox.Show(
                    "API settings saved successfully!\n\nUse \"Restart App\" below to apply them immediately, or restart the listener ('DigiSign.exe /listen') yourself.",
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

        /// <summary>
        /// Manual, on-demand update check - checking is never automatic (no background check runs
        /// at listener/tray-companion startup). Uses whatever URL is currently in the textbox, so an
        /// admin can test a URL before saving it. Runs the HTTP call on a ThreadPool thread and
        /// marshals back via BeginInvoke so the Settings form doesn't freeze while checking.
        /// </summary>
        private void BtnCheckForUpdates_Click(object sender, EventArgs e)
        {
            string url = txtUpdateCheckUrl.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(
                    "Enter an Update Check URL above first.",
                    "Check for Updates",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            btnCheckForUpdates.Enabled = false;
            btnCheckForUpdates.Text = "Checking...";

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                UpdateCheckResult result = null;
                try
                {
                    result = UpdateChecker.CheckForUpdate(url);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Manual update check failed: {ex.Message}");
                }

                if (this.IsDisposed) return;
                this.BeginInvoke(new Action(() =>
                {
                    btnCheckForUpdates.Enabled = true;
                    btnCheckForUpdates.Text = "Check for Updates";

                    if (result == null)
                    {
                        MessageBox.Show(
                            "Could not check for updates - see the log for details.",
                            "Check for Updates",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    if (!result.IsUpdateAvailable)
                    {
                        MessageBox.Show(
                            $"You're up to date (current version: v{VersionInfo.ShortVersion}).",
                            "Check for Updates",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    var notification = new UpdateNotificationForm(result.Manifest);
                    notification.UpdateNowClicked += () => ApplyUpdate(result.Manifest);
                    notification.Show(this);
                }));
            });
        }

        /// <summary>Downloads/verifies/applies the update, then exits so the update helper can replace this process's files. Relaunches with no args - the restarted process picks its mode from appsettings.json's EnableListenerMode as usual.</summary>
        private void ApplyUpdate(UpdateManifest manifest)
        {
            try
            {
                SelfUpdater.DownloadAndApply(manifest, "");
                MessageBox.Show(
                    "Update downloaded and verified - DigiSign will restart now.",
                    "Check for Updates",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Application.Exit();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply update", ex);
                MessageBox.Show(
                    $"Failed to apply update:\n{ex.Message}\n\nThe current installation was left unchanged.",
                    "Check for Updates",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
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

        private void BtnRestartApp_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "This will restart DigiSign so the saved settings take effect. Continue?",
                "Restart App",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            RestartRequested = true;
            this.Close();
        }
        
        private void PreviewSettings_Changed(object sender, EventArgs e)
        {
            // Box position/size or label text changed - the PDF background is unaffected,
            // so just repaint the overlay instead of re-rendering the whole page (see PicPreview_Paint).
            if (tabControl.SelectedTab == tabSettings && tabSettingsControl.SelectedTab == tabPreview)
            {
                picPreview.Invalidate();
            }
        }

        private float ClampZoom(float zoom)
        {
            return Math.Max(0.25f, Math.Min(3.0f, zoom));
        }

        // Re-renders the background at newZoom while keeping the content under focalPointInPanelClientCoords
        // (in previewPanel client coordinates) visually stationary.
        private void ZoomAroundPoint(float newZoom, Point focalPointInPanelClientCoords)
        {
            newZoom = ClampZoom(newZoom);
            float oldZoom = zoomLevel;
            if (Math.Abs(newZoom - oldZoom) < 0.0001f)
                return;

            Point currentScroll = previewPanel.AutoScrollPosition; // getter returns a NEGATED offset
            PointF contentPtBefore = new PointF(
                focalPointInPanelClientCoords.X - currentScroll.X,
                focalPointInPanelClientCoords.Y - currentScroll.Y);

            zoomLevel = newZoom;
            UpdatePreview(); // re-renders background at the new pixel size; picPreview (AutoSize) resizes accordingly

            float ratio = newZoom / oldZoom;
            PointF contentPtAfter = new PointF(contentPtBefore.X * ratio, contentPtBefore.Y * ratio);

            int desiredScrollX = Math.Max(0, (int)(contentPtAfter.X - focalPointInPanelClientCoords.X));
            int desiredScrollY = Math.Max(0, (int)(contentPtAfter.Y - focalPointInPanelClientCoords.Y));
            previewPanel.AutoScrollPosition = new Point(desiredScrollX, desiredScrollY); // setter expects a POSITIVE offset
        }

        private Point PanelViewportCenter()
        {
            return new Point(previewPanel.ClientSize.Width / 2, previewPanel.ClientSize.Height / 2);
        }

        private void BtnZoomIn_Click(object sender, EventArgs e)
        {
            ZoomAroundPoint(zoomLevel + 0.25f, PanelViewportCenter());
        }

        private void BtnZoomOut_Click(object sender, EventArgs e)
        {
            ZoomAroundPoint(zoomLevel - 0.25f, PanelViewportCenter());
        }

        private void BtnZoomReset_Click(object sender, EventArgs e)
        {
            ZoomAroundPoint(1.0f, PanelViewportCenter());
        }

        private void PicPreview_MouseWheel(object sender, MouseEventArgs e)
        {
            // Ctrl+wheel zooms (anchored on the cursor); plain wheel is left unhandled so the
            // WM_MOUSEWHEEL message bubbles to previewPanel, whose native AutoScroll scrolls it.
            if (Control.ModifierKeys.HasFlag(Keys.Control))
            {
                float newZoom = zoomLevel + (e.Delta > 0 ? 0.1f : -0.1f);
                Point focalPanelPt = previewPanel.PointToClient(picPreview.PointToScreen(e.Location));
                ZoomAroundPoint(newZoom, focalPanelPt);

                HandledMouseEventArgs handledArgs = e as HandledMouseEventArgs;
                if (handledArgs != null)
                    handledArgs.Handled = true;
            }
        }

        // Hit-tests both boxes at point p. Resize handles take priority over interior "Move" hits
        // across both boxes (so a precise handle grab on either box always wins); when only interior
        // hits are found, the signature box wins on overlap (HitTestOrder is signature-first).
        private DraggableBox HitTestBoxAt(Point p, out ResizeHandle handle)
        {
            foreach (DraggableBox box in HitTestOrder)
            {
                ResizeHandle h = GetResizeHandleAtPoint(p, GetScreenRect(box));
                if (h != ResizeHandle.None && h != ResizeHandle.Move)
                {
                    handle = h;
                    return box;
                }
            }

            foreach (DraggableBox box in HitTestOrder)
            {
                ResizeHandle h = GetResizeHandleAtPoint(p, GetScreenRect(box));
                if (h == ResizeHandle.Move)
                {
                    handle = h;
                    return box;
                }
            }

            handle = ResizeHandle.None;
            return null;
        }

        private void PicPreview_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            ResizeHandle handle;
            DraggableBox box = HitTestBoxAt(e.Location, out handle);

            if (box != null && handle != ResizeHandle.None)
            {
                activeBox = box;
                if (handle == ResizeHandle.Move)
                {
                    isDraggingBox = true;
                    isResizingBox = false;
                }
                else
                {
                    isResizingBox = true;
                    isDraggingBox = false;
                    activeResizeHandle = handle;
                }

                lastMousePosition = e.Location;
                picPreview.Cursor = GetCursorForHandle(handle);
                picPreview.Invalidate();
            }
        }

        private void PicPreview_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingBox || isResizingBox)
            {
                // Calculate delta
                int deltaX = e.X - lastMousePosition.X;
                int deltaY = e.Y - lastMousePosition.Y;

                if (isDraggingBox)
                {
                    MoveBox(activeBox, deltaX, deltaY);
                }
                else if (isResizingBox)
                {
                    ResizeBox(activeBox, deltaX, deltaY, activeResizeHandle);
                }

                lastMousePosition = e.Location;
                picPreview.Invalidate(); // Overlay-only repaint - no PDF re-render, see PicPreview_Paint
            }
            else
            {
                // Update cursor + hover-highlighted box based on hover position
                ResizeHandle handle;
                DraggableBox box = HitTestBoxAt(e.Location, out handle);
                picPreview.Cursor = GetCursorForHandle(handle);

                DraggableBox newHover = handle != ResizeHandle.None ? box : null;
                if (newHover != hoverBox)
                {
                    hoverBox = newHover;
                    picPreview.Invalidate();
                }
            }
        }

        private void PicPreview_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingBox || isResizingBox)
            {
                isDraggingBox = false;
                isResizingBox = false;
                activeResizeHandle = ResizeHandle.None;
                activeBox = null;

                picPreview.Cursor = Cursors.Hand;
                picPreview.Invalidate(); // Box position/size changed, not the PDF background - overlay-only repaint
            }
        }

        private void PicPreview_Paint(object sender, PaintEventArgs e)
        {
            // Overlays (box outline/text/caption) are drawn fresh every paint - during drag,
            // on every numeric-field keystroke, and at rest - instead of being baked into the
            // cached PDF background bitmap (see UpdatePreview/RenderPdfPageBackground).
            foreach (DraggableBox box in AllBoxes)
            {
                if (box != null)
                    DrawBoxOverlay(e.Graphics, box, zoomLevel, currentPdfWidth, currentPdfHeight);
            }

            DraggableBox handleBox = activeBox ?? hoverBox;
            if (handleBox != null)
            {
                DrawResizeHandles(e.Graphics, GetScreenRect(handleBox));
            }
        }

        private RectangleF GetScreenRect(DraggableBox box)
        {
            // Use actual PDF dimensions instead of hardcoded values
            float pdfWidth = currentPdfWidth;
            float pdfHeight = currentPdfHeight;

            // Get box settings in PDF coordinates
            float x = (float)box.NumX.Value;
            float y = (float)box.NumY.Value;
            float width = (float)box.NumWidth.Value;
            float height = (float)box.NumHeight.Value;

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
        
        private void MoveBox(DraggableBox box, int deltaX, int deltaY)
        {
            // Convert screen delta to PDF delta
            float pdfDeltaX = deltaX / zoomLevel;
            float pdfDeltaY = -deltaY / zoomLevel; // Invert Y because PDF coords are bottom-up

            decimal newX = box.NumX.Value + (decimal)pdfDeltaX;
            newX = Math.Max(box.NumX.Minimum, Math.Min(box.NumX.Maximum, newX));
            box.NumX.Value = newX;

            decimal newY = box.NumY.Value + (decimal)pdfDeltaY;
            newY = Math.Max(box.NumY.Minimum, Math.Min(box.NumY.Maximum, newY));
            box.NumY.Value = newY;
        }

        private void ResizeBox(DraggableBox box, int deltaX, int deltaY, ResizeHandle handle)
        {
            // Convert screen delta to PDF delta
            float pdfDeltaX = deltaX / zoomLevel;
            float pdfDeltaY = -deltaY / zoomLevel; // Invert Y

            decimal currentX = box.NumX.Value;
            decimal currentY = box.NumY.Value;
            decimal currentWidth = box.NumWidth.Value;
            decimal currentHeight = box.NumHeight.Value;

            switch (handle)
            {
                case ResizeHandle.TopLeft:
                    // Move top-left corner
                    box.NumX.Value = Math.Max(box.NumX.Minimum, Math.Min(box.NumX.Maximum, currentX + (decimal)pdfDeltaX));
                    box.NumY.Value = Math.Max(box.NumY.Minimum, Math.Min(box.NumY.Maximum, currentY + (decimal)pdfDeltaY));
                    box.NumWidth.Value = Math.Max(box.NumWidth.Minimum, currentWidth - (decimal)pdfDeltaX);
                    box.NumHeight.Value = Math.Max(box.NumHeight.Minimum, currentHeight - (decimal)pdfDeltaY);
                    break;

                case ResizeHandle.TopRight:
                    // Move top-right corner
                    box.NumY.Value = Math.Max(box.NumY.Minimum, Math.Min(box.NumY.Maximum, currentY + (decimal)pdfDeltaY));
                    box.NumWidth.Value = Math.Max(box.NumWidth.Minimum, currentWidth + (decimal)pdfDeltaX);
                    box.NumHeight.Value = Math.Max(box.NumHeight.Minimum, currentHeight - (decimal)pdfDeltaY);
                    break;

                case ResizeHandle.BottomLeft:
                    // Move bottom-left corner
                    box.NumX.Value = Math.Max(box.NumX.Minimum, Math.Min(box.NumX.Maximum, currentX + (decimal)pdfDeltaX));
                    box.NumWidth.Value = Math.Max(box.NumWidth.Minimum, currentWidth - (decimal)pdfDeltaX);
                    box.NumHeight.Value = Math.Max(box.NumHeight.Minimum, currentHeight + (decimal)pdfDeltaY);
                    break;

                case ResizeHandle.BottomRight:
                    // Move bottom-right corner
                    box.NumWidth.Value = Math.Max(box.NumWidth.Minimum, currentWidth + (decimal)pdfDeltaX);
                    box.NumHeight.Value = Math.Max(box.NumHeight.Minimum, currentHeight + (decimal)pdfDeltaY);
                    break;

                case ResizeHandle.Top:
                    box.NumY.Value = Math.Max(box.NumY.Minimum, Math.Min(box.NumY.Maximum, currentY + (decimal)pdfDeltaY));
                    box.NumHeight.Value = Math.Max(box.NumHeight.Minimum, currentHeight - (decimal)pdfDeltaY);
                    break;

                case ResizeHandle.Bottom:
                    box.NumHeight.Value = Math.Max(box.NumHeight.Minimum, currentHeight + (decimal)pdfDeltaY);
                    break;

                case ResizeHandle.Left:
                    box.NumX.Value = Math.Max(box.NumX.Minimum, Math.Min(box.NumX.Maximum, currentX + (decimal)pdfDeltaX));
                    box.NumWidth.Value = Math.Max(box.NumWidth.Minimum, currentWidth - (decimal)pdfDeltaX);
                    break;

                case ResizeHandle.Right:
                    box.NumWidth.Value = Math.Max(box.NumWidth.Minimum, currentWidth + (decimal)pdfDeltaX);
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
        
        // Computes a cache key for the rendered background so drag/keystroke overlay repaints
        // (which call picPreview.Invalidate(), not this method) never trigger a PDF re-decode.
        private string ComputeBackgroundCacheKey(string inputPdf, int pageNumber)
        {
            string fileStamp;
            if (!string.IsNullOrEmpty(inputPdf) && File.Exists(inputPdf))
            {
                fileStamp = inputPdf + "|" + File.GetLastWriteTimeUtc(inputPdf).Ticks;
            }
            else
            {
                fileStamp = "mock";
            }
            return $"{fileStamp}|{pageNumber}|{Math.Round(zoomLevel, 2)}";
        }

        private void UpdatePreview()
        {
            try
            {
                // Get current page to preview
                int pageNumber = cmbPreviewPage.SelectedIndex + 1;

                // Get input PDF from General settings tab
                string inputPdf = txtInputFile.Text;
                string key = ComputeBackgroundCacheKey(inputPdf, pageNumber);

                if (key != _cachedBackgroundKey || _cachedBackground == null)
                {
                    Bitmap newBackground;

                    // Try to load the PDF from General settings
                    if (!string.IsNullOrEmpty(inputPdf))
                    {
                        // Check if file exists
                        if (File.Exists(inputPdf))
                        {
                            try
                            {
                                // Try to load actual PDF
                                newBackground = RenderPdfPageBackground(inputPdf, pageNumber);

                                // Success - update info label
                                lblPreviewInfo.Text = $"Preview: {Path.GetFileName(inputPdf)} (Ctrl+wheel to zoom, wheel to scroll)";
                                lblPreviewInfo.ForeColor = Color.FromArgb(64, 64, 64);
                            }
                            catch (Exception ex)
                            {
                                // If PDF is invalid, fall back to mock PDF
                                newBackground = CreateMockPdfPreview();

                                // Update info label to show error
                                lblPreviewInfo.Text = $"? Cannot read PDF file. Using mock preview. Error: {ex.Message}";
                                lblPreviewInfo.ForeColor = Color.Red;
                            }
                        }
                        else
                        {
                            // File doesn't exist - use mock PDF
                            newBackground = CreateMockPdfPreview();

                            lblPreviewInfo.Text = $"? File not found: {Path.GetFileName(inputPdf)}. Using mock preview.";
                            lblPreviewInfo.ForeColor = Color.Orange;
                        }
                    }
                    else
                    {
                        // No file selected - use mock PDF
                        newBackground = CreateMockPdfPreview();

                        lblPreviewInfo.Text = "No input file selected. Using mock preview (Ctrl+wheel to zoom, wheel to scroll)";
                        lblPreviewInfo.ForeColor = Color.FromArgb(64, 64, 64);
                    }

                    // Replace the cached background (boxes are drawn separately, live, in PicPreview_Paint)
                    if (_cachedBackground != null)
                    {
                        _cachedBackground.Dispose();
                    }
                    _cachedBackground = newBackground;
                    _cachedBackgroundKey = key;
                    picPreview.Image = _cachedBackground;
                }

                UpdateZoomLabel();
                picPreview.Invalidate();
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

                if (_cachedBackground != null)
                {
                    _cachedBackground.Dispose();
                }
                _cachedBackground = errorBitmap;
                _cachedBackgroundKey = null;
                picPreview.Image = errorBitmap;

                lblPreviewInfo.Text = "? Error generating preview";
                lblPreviewInfo.ForeColor = Color.Red;
            }
        }

        private Bitmap RenderPdfPageBackground(string pdfPath, int pageNumber)
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

            // Get the REAL page size before allocating the bitmap, so the bitmap is sized to the
            // page's actual aspect ratio instead of a hardcoded A4 box (previously caused stretching
            // on any non-A4 page).
            SizeF pdfPageSize = Conversion.GetPageSize(pdfBytes, pageNumber - 1);
            float pdfWidth = pdfPageSize.Width;
            float pdfHeight = pdfPageSize.Height;

            // Store actual PDF dimensions for resize handle / overlay positioning
            currentPdfWidth = pdfWidth;
            currentPdfHeight = pdfHeight;

            int width = Math.Max(1, (int)(pdfWidth * zoomLevel));
            int height = Math.Max(1, (int)(pdfHeight * zoomLevel));

            // Create a bitmap to render the PDF page
            Bitmap bitmap = new Bitmap(width, height);
            bitmap.SetResolution(72f, 72f); // 1 PDF point == 1 pixel at zoom 1.0, matching the box/text layout math below

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                try
                {
                    // Render the PDF page to an image using PDFtoImage
                    var renderOptions = new RenderOptions(Width: width, Height: height, WithAnnotations: true, WithFormFill: true, WithAspectRatio: true);
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
                }
                catch (Exception ex)
                {
                    // If PDFtoImage rendering fails, fall back to iTextSharp metadata-only view.
                    // NOTE: the bitmap is already sized from the real pdfWidth/pdfHeight above, so
                    // this placeholder renders into a correctly-proportioned bitmap (no re-allocation).
                    try
                    {
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

                            g.DrawString($"Layout preview mode - box placement shown below",
                                errorFont, Brushes.Gray,
                                new PointF(10 * zoomLevel, 100 * zoomLevel));
                        }

                        // Draw simulated content
                        using (SolidBrush simTextBrush = new SolidBrush(Color.FromArgb(220, 200, 200, 200)))
                        {
                            float lineHeight = 15 * zoomLevel;
                            float margin = 50 * zoomLevel;
                            float contentWidth = width - (2 * margin);

                            for (float lineY = margin + 120 * zoomLevel; lineY < height - margin; lineY += lineHeight)
                            {
                                float lineWidth = contentWidth * (0.7f + (float)(new Random().NextDouble() * 0.3));
                                g.FillRectangle(simTextBrush, margin, lineY, lineWidth, 8 * zoomLevel);
                            }
                        }

                        DrawGrid(g, width, height);
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
            bitmap.SetResolution(72f, 72f); // keep GDI+ font/point math consistent with the real-PDF path

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

                    g.DrawString("Signature and Copy Label Placement Preview",
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

        // Mirrors DigitalSignatureService.ValidateAndAdjustCoordinates' exact bounds check, so the
        // preview can warn about a placement that will otherwise be silently auto-repositioned at sign time.
        private bool IsBoxOutOfBounds(DraggableBox box)
        {
            float x = (float)box.NumX.Value;
            float y = (float)box.NumY.Value;
            float w = (float)box.NumWidth.Value;
            float h = (float)box.NumHeight.Value;
            return x < 0 || y < 0 || x + w > currentPdfWidth || y + h > currentPdfHeight;
        }

        // Overlay fonts are cached and only rebuilt when zoomLevel changes, since DrawBoxOverlay now
        // runs on every drag tick / numeric-field keystroke (not just at final render).
        private void EnsureOverlayFonts(float scale)
        {
            if (_overlayFontCN != null && Math.Abs(_overlayFontsScale - scale) < 0.0001f)
                return;

            if (_overlayFontCN != null) _overlayFontCN.Dispose();
            if (_overlayFontText != null) _overlayFontText.Dispose();
            if (_overlayFontLabel != null) _overlayFontLabel.Dispose();

            // "Times New Roman" Bold and "Arial" are the closest installed GDI+ analogs to iTextSharp's
            // BaseFont.TIMES_BOLD / BaseFont.HELVETICA used by the real signer. The previous "Helvetica"
            // family name doesn't exist on Windows and GDI+ silently substituted Microsoft Sans Serif.
            _overlayFontCN = new Font("Times New Roman", 10 * scale, FontStyle.Bold);
            _overlayFontText = new Font("Arial", 9 * scale, FontStyle.Regular);
            _overlayFontLabel = new Font("Segoe UI", 7 * scale);
            _overlayFontsScale = scale;
        }

        private BaseFont GetPreviewBaseFontCN()
        {
            if (_previewBaseFontCN == null)
                _previewBaseFontCN = BaseFont.CreateFont(BaseFont.TIMES_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            return _previewBaseFontCN;
        }

        // Draws one box's fill/border/text/caption. Used for both the signature box and the copy-label
        // box - replaces the old signature-only DrawSignatureRectangle. Called from PicPreview_Paint on
        // every repaint (drag, numeric-field edits, hover), never from the PDF-rendering methods above,
        // so overlay state always reflects the live NumericUpDown/text values with no PDF re-decode.
        private void DrawBoxOverlay(Graphics g, DraggableBox box, float scale, float pdfWidth, float pdfHeight)
        {
            float x = (float)box.NumX.Value * scale;
            float y = (float)box.NumY.Value * scale;
            float width = (float)box.NumWidth.Value * scale;
            float height = (float)box.NumHeight.Value * scale;

            // Adjust Y coordinate (PDF coordinates start from bottom-left, screen from top-left)
            float scaledPdfHeight = pdfHeight * scale;
            float adjustedY = scaledPdfHeight - y - height;

            bool outOfBounds = IsBoxOutOfBounds(box);
            Color color = outOfBounds ? Color.Red : box.BorderColor;

            // Draw box fill (matching actual PDF)
            using (Brush fillBrush = new SolidBrush(Color.FromArgb(250, 250, 250, 250)))
            {
                g.FillRectangle(fillBrush, x, adjustedY, width, height);
            }

            // Draw border - red/dashed when the box falls outside the page bounds (see IsBoxOutOfBounds)
            using (Pen borderPen = new Pen(color, (outOfBounds ? 2f : 1f) * scale))
            {
                if (outOfBounds)
                    borderPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawRectangle(borderPen, x, adjustedY, width, height);
            }

            EnsureOverlayFonts(scale);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // Text layout mirrors DigitalSignatureService.DrawSignatureAppearance/DrawCopyLabel's
            // constants (padding=3, fontSize=10/9, leading=fontSize+3/+1) and wraps using the exact
            // same font-metric logic (DigitalSignatureService.WrapText) so the preview's line breaks
            // match what actually gets stamped into the signed PDF.
            float padding = 3 * scale;
            float maxTextWidthPt = Math.Max(1f, (float)box.NumWidth.Value - 6f);
            float leadingCNPt = 10f + 3f;
            float maxYScreen = adjustedY + height - padding;
            float currentY = adjustedY + padding;

            if (box.Id == "signature")
            {
                string cn = txtCommonName.Text != null ? txtCommonName.Text.Trim() : null;
                if (!string.IsNullOrEmpty(cn))
                {
                    foreach (string line in DigitalSignatureService.WrapText(cn, GetPreviewBaseFontCN(), 10f, maxTextWidthPt))
                    {
                        if (currentY > maxYScreen) break;
                        g.DrawString(line, _overlayFontCN, Brushes.Black, new PointF(x + padding, currentY));
                        currentY += leadingCNPt * scale;
                    }
                }

                // Blank line (matching actual PDF spacing before the date line)
                currentY += 9f * scale;

                if (currentY <= maxYScreen)
                {
                    string dateText = $"Date: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                    g.DrawString(dateText, _overlayFontText, Brushes.Black, new PointF(x + padding, currentY));
                }
            }
            else
            {
                string labelText = box.PreviewText != null ? box.PreviewText() : null;
                labelText = labelText != null ? labelText.Trim() : null;
                if (!string.IsNullOrEmpty(labelText))
                {
                    // Right-aligned to match DigitalSignatureService.DrawCopyLabel's Element.ALIGN_RIGHT
                    foreach (string line in DigitalSignatureService.WrapText(labelText, GetPreviewBaseFontCN(), 10f, maxTextWidthPt))
                    {
                        if (currentY > maxYScreen) break;
                        float lineWidth = g.MeasureString(line, _overlayFontCN).Width;
                        g.DrawString(line, _overlayFontCN, Brushes.Black, new PointF(x + width - padding - lineWidth, currentY));
                        currentY += leadingCNPt * scale;
                    }
                }
            }

            // Caption pill identifying which box this is (and flagging out-of-bounds placement)
            string caption = box.Caption != null ? box.Caption() : box.Id;
            if (outOfBounds)
                caption += "  (outside page bounds - auto-repositioned at sign time)";

            SizeF captionSize = g.MeasureString(caption, _overlayFontLabel);
            using (SolidBrush captionBg = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
            {
                g.FillRectangle(captionBg, x, adjustedY - captionSize.Height - 2 * scale, captionSize.Width + 4 * scale, captionSize.Height);
            }
            using (SolidBrush captionTextBrush = new SolidBrush(color))
            {
                g.DrawString(caption, _overlayFontLabel, captionTextBrush, new PointF(x + 2 * scale, adjustedY - captionSize.Height - 2 * scale));
            }

            using (SolidBrush dimBrush = new SolidBrush(color))
            {
                g.DrawString($"{box.NumWidth.Value} x {box.NumHeight.Value} pt",
                    _overlayFontLabel, dimBrush, new PointF(x, adjustedY + height + 2 * scale));
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
