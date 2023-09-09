using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.Xml;
using System.IO;

namespace lucidcode.LucidScribe.Plugin.Hypnodyne.ZMax
{
    public partial class PortForm : Form
    {
        public static string m_strPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\lucidcode\\Lucid Scribe\\";
        public String SelectedPort = "";
        public String Algorithm = "REM Detector";
        public int Threshold = 500;
        private Boolean loaded = false;
        public Boolean TCMP = false;

        public PortForm()
        {
            InitializeComponent();
        }

        private void PortForm_Load(object sender, EventArgs e)
        {
          LoadSettings();
          loaded = true;
        }

        public void UpdateData(string data)
        {
            if (dataTextbox.InvokeRequired)
            {
                dataTextbox.BeginInvoke((MethodInvoker)delegate () {
                    if (dataTextbox.Text.Length > 1024) dataTextbox.Text = "";
                    dataTextbox.Text += data; 
                });
            }
            else
            {
                if (dataTextbox.Text.Length > 1024) dataTextbox.Text = "";
                dataTextbox.Text += data;
            }
        }

        private void LoadSettings()
        {
          if (!File.Exists(m_strPath + "Plugins\\Hypnodyne.ZMax.User.lsd"))
          {
            String defaultSettings = "<LucidScribeData>";
            defaultSettings += "<Plugin>";
            defaultSettings += "<Port>8000</Port>";
            defaultSettings += "<Algorithm>REM Detector</Algorithm>";
            defaultSettings += "<Threshold>800</Threshold>";
            defaultSettings += "</Plugin>";
            defaultSettings += "</LucidScribeData>";
            File.WriteAllText(m_strPath + "Plugins\\Hypnodyne.ZMax.User.lsd", defaultSettings);
          }

          XmlDocument xmlSettings = new XmlDocument();
          xmlSettings.Load(m_strPath + "Plugins\\Hypnodyne.ZMax.User.lsd");

          txtPort.Text = xmlSettings.DocumentElement.SelectSingleNode("//Port").InnerText;
          cmbAlgorithm.Text = xmlSettings.DocumentElement.SelectSingleNode("//Algorithm").InnerText;
          cmbThreshold.Text = xmlSettings.DocumentElement.SelectSingleNode("//Threshold").InnerText;
                     
          if (xmlSettings.DocumentElement.SelectSingleNode("//TCMP") != null && xmlSettings.DocumentElement.SelectSingleNode("//TCMP").InnerText == "1")
          {
            chkTCMP.Checked = true;
            TCMP = true;
          }
        }

        private void SaveSettings()
        {
          String settingsXML = "<LucidScribeData>";
          settingsXML += "<Plugin>";
          settingsXML += "<Port>" + txtPort.Text + "</Port>";
          settingsXML += "<Algorithm>" + cmbAlgorithm.Text + "</Algorithm>";
          settingsXML += "<Threshold>" + cmbThreshold.Text + "</Threshold>";

          if (chkTCMP.Checked)
          {
            settingsXML += "<TCMP>1</TCMP>";
          }
          else
          {
            settingsXML += "<TCMP>0</TCMP>";
          }

          settingsXML += "</Plugin>";
          settingsXML += "</LucidScribeData>";
          File.WriteAllText(m_strPath + "Plugins\\Hypnodyne.ZMax.User.lsd", settingsXML);
        }

        private void cmbAlgorithm_SelectedIndexChanged(object sender, EventArgs e)
        {
          Algorithm = cmbAlgorithm.Text;
          if (loaded) { SaveSettings(); }
        }

        private void cmbThreshold_SelectedIndexChanged(object sender, EventArgs e)
        {
          Threshold = Convert.ToInt32(cmbThreshold.Text);
          if (loaded) { SaveSettings(); }
        }

        private void chkTCMP_CheckedChanged(object sender, EventArgs e)
        {
          if (!loaded) { return; }

          TCMP = chkTCMP.Checked;
          SaveSettings();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
          try
          {
            SelectedPort = txtPort.Text;
            SaveSettings();
            DialogResult = DialogResult.OK;
            Close();
          }
          catch (Exception ex)
          {
              MessageBox.Show(ex.Message, "LucidScribe.ZMax.Connect()", MessageBoxButtons.OK, MessageBoxIcon.Error);
          }
        }
    }
}
