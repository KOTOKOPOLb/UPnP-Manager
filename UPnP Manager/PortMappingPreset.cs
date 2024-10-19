namespace UPnP_Manager;

public class PortMappingPreset
{
    public string PortName { get; set; }
    public int PortNumber { get; set; }
    public string PortType { get; set; }
    public bool IsActive { get; set; }
    public string SourceApplication { get; set; }
}