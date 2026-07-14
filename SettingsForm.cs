using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

namespace DigiSign
{
    public class SettingsForm : Form
    {
        // Form controls
        private Label lblTitle;
        private TabControl tabControl;
        private TabPage tabGeneral;
        private TabPage tabSignature;
        
        // General Settings
        private Label lblInputFile;
        private TextBox txtInputFile;
        private Button btnBrowseInput;
        private Label lblOutputFolder;
        private TextBox txtOutputFolder;
        private Button btnBrowseOutput;
        private Label lblCommonName;
        private TextBox txtCommonName;
        private Label lblPin;
        private TextBox txtPin;
        private CheckBox chkShowPin;
        
        // Signature Settings
        private Label lblXCoord;
        private NumericUpDown numXCoord;
        private Label lblYCoord;
        private NumericUpDown numYCoord;
        private Label lblWidth;
        private NumericUpDown numWidth;
        private Label lblHeight;
        private NumericUpDown numHeight;
        private Label lblSignOnPage;
        private ComboBox cmbSignOnPage;
        private Label lblOpenOutputFolder;
        private ComboBox cmbOpenOutputFolder;
        private Label lblUseSelfSigned;
        private ComboBox cmbUseSelfSigned;
        
        // Buttons
        private Button btnSave;
        private Button btnCancel;
        private Button btnReset;
        
        private string xmlFilePath;
        
        public SettingsForm()
        {
            xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.xml");
            InitializeComponents();
            LoadSettings();
        }
        
        private void InitializeComponents()
        {
            // Form settings
            this.Text = "DigiSign - Settings";
            this.ClientSize = new Size(700, 600);
            this.MinimumSize = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Icon = TrayIconLoader.LoadFromEmbeddedPng("DigiSign.singer_icon.png");
            
            int margin = 20;
            int currentY = margin;
            
            // Title
            lblTitle = new Label
            {
                Text = "Application Settings",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(margin, currentY),
                Size = new Size(660, 35),
                ForeColor = Color.FromArgb(0, 102, 204)
            };
            this.Controls.Add(lblTitle);
            currentY += 45;
            
            // Separator
            Panel separator = new Panel
            {
                Location = new Point(margin, currentY),
                Size = new Size(660, 2),
                BackColor = Color.FromArgb(200, 200, 200)
            };
            this.Controls.Add(separator);
            currentY += 12;
            
            // Tab Control
            tabControl = new TabControl
            {
                Location = new Point(margin, currentY),
                Size = new Size(660, 440),
                Font = new Font("Segoe UI", 9)
            };
            this.Controls.Add(tabControl);
            
            // Create tabs
            CreateGeneralTab();
            CreateSignatureTab();
            
            currentY += 450;
            
            // Buttons
            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(100, 35),
                Location = new Point(480, currentY),
                Font = new Font("Segoe UI", 9),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(btnCancel);
            
            btnReset = new Button
            {
                Text = "Reset",
                Size = new Size(100, 35),
                Location = new Point(370, currentY),
                Font = new Font("Segoe UI", 9)
            };
            btnReset.Click += BtnReset_Click;
            this.Controls.Add(btnReset);
            
            btnSave = new Button
            {
                Text = "Save",
                Size = new Size(100, 35),
                Location = new Point(590, currentY),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
            
            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }
        
        private void CreateGeneralTab()
        {
            tabGeneral = new TabPage("General");
            tabControl.TabPages.Add(tabGeneral);
            
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
                Size = new Size(600, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabGeneral.Controls.Add(lblInputFile);
            currentY += labelHeight + 5;
            
            btnBrowseInput = new Button
            {
                Text = "Browse...",
                Size = new Size(90, textBoxHeight + 2),
                Location = new Point(530, currentY),
                Font = new Font("Segoe UI", 9)
            };
            btnBrowseInput.Click += BtnBrowseInput_Click;
            tabGeneral.Controls.Add(btnBrowseInput);
            
            txtInputFile = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(500, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            tabGeneral.Controls.Add(txtInputFile);
            currentY += textBoxHeight + spacing + 10;
            
            // Output Folder
            lblOutputFolder = new Label
            {
                Text = "Output Folder:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(600, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabGeneral.Controls.Add(lblOutputFolder);
            currentY += labelHeight + 5;
            
            btnBrowseOutput = new Button
            {
                Text = "Browse...",
                Size = new Size(90, textBoxHeight + 2),
                Location = new Point(530, currentY),
                Font = new Font("Segoe UI", 9)
            };
            btnBrowseOutput.Click += BtnBrowseOutput_Click;
            tabGeneral.Controls.Add(btnBrowseOutput);
            
            txtOutputFolder = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(500, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            tabGeneral.Controls.Add(txtOutputFolder);
            currentY += textBoxHeight + spacing + 10;
            
            // Common Name
            lblCommonName = new Label
            {
                Text = "Certificate Common Name (CN):",
                Location = new Point(leftMargin, currentY),
                Size = new Size(600, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabGeneral.Controls.Add(lblCommonName);
            currentY += labelHeight + 5;
            
            txtCommonName = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(600, textBoxHeight),
                Font = new Font("Segoe UI", 9)
            };
            tabGeneral.Controls.Add(txtCommonName);
            currentY += textBoxHeight + spacing + 10;
            
            // PIN
            lblPin = new Label
            {
                Text = "Smart Card/Token PIN:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(600, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabGeneral.Controls.Add(lblPin);
            currentY += labelHeight + 5;
            
            txtPin = new TextBox
            {
                Location = new Point(leftMargin, currentY),
                Size = new Size(600, textBoxHeight),
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
        }
        
        private void CreateSignatureTab()
        {
            tabSignature = new TabPage("Signature");
            tabControl.TabPages.Add(tabSignature);
            
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
            tabSignature.Controls.Add(numHeight);
            currentY += controlHeight + spacing + 15;
            
            // Sign On Page
            lblSignOnPage = new Label
            {
                Text = "Sign On Page:",
                Location = new Point(leftMargin, currentY),
                Size = new Size(200, labelHeight),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            tabSignature.Controls.Add(lblSignOnPage);
            
            cmbSignOnPage = new ComboBox
            {
                Location = new Point(leftMargin + 210, currentY),
                Size = new Size(200, controlHeight),
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbSignOnPage.Items.AddRange(new object[] { 
                "F - First Page", 
                "E - Each Page", 
                "L - Last Page" 
            });
            tabSignature.Controls.Add(cmbSignOnPage);
            currentY += controlHeight + spacing + 5;
            
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
        }
        
        private void ChkShowPin_CheckedChanged(object sender, EventArgs e)
        {
            if (chkShowPin.Checked)
            {
                // Show the PIN - clear the password char
                txtPin.PasswordChar = '\0';
            }
            else
            {
                // Hide the PIN - set the password char
                txtPin.PasswordChar = '*';
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
        
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(xmlFilePath))
                {
                    MessageBox.Show(
                        $"Configuration file not found:\n{xmlFilePath}\n\nDefault values will be used.",
                        "Configuration Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    LoadDefaultSettings();
                    return;
                }
                
                var xmlDoc = XDocument.Load(xmlFilePath);
                var envelope = xmlDoc.Element("ENVELOPE");
                if (envelope == null)
                {
                    LoadDefaultSettings();
                    return;
                }
                
                var fileNameLists = envelope.Element("FILENAMELIST")?.Elements("FILENAMELIST").ToList();
                if (fileNameLists == null || fileNameLists.Count < 10)
                {
                    LoadDefaultSettings();
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
                
                string signOnPage = fileNameLists[8].Element("FILENAME")?.Value ?? "L";
                cmbSignOnPage.SelectedIndex = signOnPage == "F" ? 0 : signOnPage == "E" ? 1 : 2;
                
                string openFolder = fileNameLists[9].Element("FILENAME")?.Value ?? "Y";
                cmbOpenOutputFolder.SelectedIndex = openFolder == "Y" ? 0 : 1;
                
                if (fileNameLists.Count > 10)
                {
                    string useSelfSigned = fileNameLists[10].Element("FILENAME")?.Value ?? "N";
                    cmbUseSelfSigned.SelectedIndex = useSelfSigned.ToUpper() == "Y" ? 0 : 1;
                }
                else
                {
                    cmbUseSelfSigned.SelectedIndex = 1; // Default to No
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading settings: {ex.Message}\n\nDefault values will be used.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                LoadDefaultSettings();
            }
        }
        
        private void LoadDefaultSettings()
        {
            txtInputFile.Text = "";
            txtOutputFolder.Text = @"C:\Users\Public";
            txtCommonName.Text = "";
            txtPin.Text = "";
            numXCoord.Value = 400;
            numYCoord.Value = 75;
            numWidth.Value = 150;
            numHeight.Value = 50;
            cmbSignOnPage.SelectedIndex = 2; // Last Page
            cmbOpenOutputFolder.SelectedIndex = 0; // Yes
            cmbUseSelfSigned.SelectedIndex = 1; // No
        }
        
        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(txtCommonName.Text))
                {
                    MessageBox.Show(
                        "Certificate Common Name is required.",
                        "Validation Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    tabControl.SelectedTab = tabGeneral;
                    txtCommonName.Focus();
                    return;
                }
                
                // Create XML structure
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
                                new XElement("FILENAME", cmbSignOnPage.SelectedIndex == 0 ? "F" : cmbSignOnPage.SelectedIndex == 1 ? "E" : "L"),
                                new XComment(" SignOnPage F=FIRST Page, E=Each Page, L=Last Page, default value=L ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", cmbOpenOutputFolder.SelectedIndex == 0 ? "Y" : "N"),
                                new XComment(" Open Output folder after signing: Y=output folder will be open after signing, N=Output folder will not open after signing, default value=Y ")
                            ),
                            new XElement("FILENAMELIST",
                                new XElement("FILENAME", cmbUseSelfSigned.SelectedIndex == 0 ? "Y" : "N"),
                                new XComment(" USESELFSIGNED ")
                            )
                        )
                    )
                );
                
                // Save to file
                xmlDoc.Save(xmlFilePath);
                
                MessageBox.Show(
                    "Settings saved successfully!",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                
                this.DialogResult = DialogResult.OK;
                this.Close();
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
        
        private void BtnReset_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to default values?",
                "Reset Settings",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            
            if (result == DialogResult.Yes)
            {
                LoadDefaultSettings();
            }
        }
        
        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
