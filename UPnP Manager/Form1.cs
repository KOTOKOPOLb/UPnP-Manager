using System.Resources;
using System.Text.Json;
using Open.Nat;

namespace UPnP_Manager
{
    public partial class Form1 : Form
    {
        private ResourceManager rm;

        public Form1()
        {
            InitializeComponent();
            SetLanguage();
            SetupDataGridView();
        }

        private void SetLanguage()
        {
            rm = new ResourceManager("UPnP_Manager.Strings", typeof(Form1).Assembly);
            addButton.Text = rm.GetString("Add");
            removeButton.Text = rm.GetString("Remove");
            forwardPortButton.Text = rm.GetString("Forward");
            saveButton.Text = rm.GetString("Save");
            loadButton.Text = rm.GetString("Load");
        }

        private void SetupDataGridView()
        {
            dataGridView1.Columns.Add("PortName", rm.GetString("PortName"));
            dataGridView1.Columns.Add("PortNumber", rm.GetString("Port"));

            var portTypeColumn = new DataGridViewComboBoxColumn
            {
                Name = "PortType",
                HeaderText = rm.GetString("PortType"),
                Items = { "TCP", "UDP" }
            };
            dataGridView1.Columns.Add(portTypeColumn);

            var checkboxColumn = new DataGridViewCheckBoxColumn
            {
                Name = "Active",
                HeaderText = rm.GetString("Active")
            };
            dataGridView1.Columns.Add(checkboxColumn);

            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void addButton_Click(object sender, EventArgs e) => dataGridView1.Rows.Add();

        private async void removeButton_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedCells.Count > 0)
            {
                var selectedRow = dataGridView1.SelectedCells[0].OwningRow;
                if (!selectedRow.IsNewRow)
                {
                    if (selectedRow.Cells["PortNumber"].Value != null &&
                        selectedRow.Cells["PortType"].Value != null &&
                        int.TryParse(selectedRow.Cells["PortNumber"].Value.ToString(), out int portNumber))
                    {
                        string portType = selectedRow.Cells["PortType"].Value.ToString();
                        await ClosePort(portNumber, portType);
                    }

                    dataGridView1.Rows.Remove(selectedRow);
                }
            }
        }

        private async void forwardPortButton_Click(object sender, EventArgs e)
        {
            forwardPortButton.Enabled = false;

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["PortNumber"].Value != null &&
                    row.Cells["PortType"].Value != null &&
                    row.Cells["PortName"].Value != null &&
                    bool.TryParse(row.Cells["Active"].Value?.ToString(), out bool isActive) &&
                    int.TryParse(row.Cells["PortNumber"].Value.ToString(), out int portNumber))
                {
                    string portName = row.Cells["PortName"].Value.ToString();
                    string portType = row.Cells["PortType"].Value.ToString();

                    if (isActive)
                        await ForwardPort(portNumber, portType, portName);
                    else
                        await ClosePort(portNumber, portType);
                }
            }

            forwardPortButton.Enabled = true;
        }

        private void UpdateStatusStrip(string message) => toolStripStatusLabel1.Text = message;

        private async Task ForwardPort(int port, string protocol, string description)
        {
            var nat = new NatDiscoverer();
            var cts = new CancellationTokenSource(5000);

            try
            {
                var device = await nat.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                if (protocol.ToUpper() == "TCP")
                    await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, description));
                else if (protocol.ToUpper() == "UDP")
                    await device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, description));

                UpdateStatusStrip(string.Format(rm.GetString("PortMapped"), port, protocol));
            }
            catch (Exception ex)
            {
                MessageBox.Show(rm.GetString("Error") + ": " + ex.Message);
                UpdateStatusStrip(rm.GetString("Error") + ": " + ex.Message);
            }
        }

        private async Task ClosePort(int port, string protocol)
        {
            var nat = new NatDiscoverer();
            var cts = new CancellationTokenSource(5000);

            try
            {
                var device = await nat.DiscoverDeviceAsync(PortMapper.Upnp, cts);

                if (protocol.ToUpper() == "TCP")
                    await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, port, port));
                else if (protocol.ToUpper() == "UDP")
                    await device.DeletePortMapAsync(new Mapping(Protocol.Udp, port, port));

                UpdateStatusStrip(string.Format(rm.GetString("PortClosed"), port, protocol));
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(rm.GetString("ErrorRemovingPort"), ex.Message));
                UpdateStatusStrip(string.Format(rm.GetString("ErrorRemovingPort"), ex.Message));
            }
        }

        private void loadButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.Title = rm.GetString("Load");
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string json = File.ReadAllText(openFileDialog.FileName);
                    var presets = JsonSerializer.Deserialize<List<PortMappingPreset>>(json);

                    if (presets != null && presets.Any(p => p.SourceApplication == "UPnP_Manager"))
                    {
                        dataGridView1.Rows.Clear();
                        foreach (var preset in presets)
                        {
                            var rowIndex = dataGridView1.Rows.Add();
                            var row = dataGridView1.Rows[rowIndex];

                            row.Cells["PortName"].Value = preset.PortName;
                            row.Cells["PortNumber"].Value = preset.PortNumber;
                            row.Cells["PortType"].Value = preset.PortType;
                            row.Cells["Active"].Value = preset.IsActive;
                        }

                        MessageBox.Show(rm.GetString("PresetsLoaded"));
                    }
                    else
                        MessageBox.Show(rm.GetString("NotUPnPManage"));
                }
            }
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            var presets = new List<PortMappingPreset>();

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (!row.IsNewRow &&
                    row.Cells["PortNumber"].Value != null &&
                    row.Cells["PortType"].Value != null &&
                    int.TryParse(row.Cells["PortNumber"].Value.ToString(), out int portNumber))
                {
                    presets.Add(new PortMappingPreset
                    {
                        PortName = row.Cells["PortName"].Value?.ToString() ?? string.Empty,
                        PortNumber = portNumber,
                        PortType = row.Cells["PortType"].Value?.ToString() ?? string.Empty,
                        IsActive = row.Cells["Active"].Value != null && (bool)row.Cells["Active"].Value,
                        SourceApplication = "UPnP_Manager"
                    });
                }
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                saveFileDialog.Title = rm.GetString("Save");
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string json = JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(saveFileDialog.FileName, json);
                    MessageBox.Show(rm.GetString("PresetsSaved"));
                }
            }
        }
    }
}
