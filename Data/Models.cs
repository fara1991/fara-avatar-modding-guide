namespace FaraBlenderManual.Data;

public class BlenderOperation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
}

public class BlenderShortcut
{
    public string Key { get; set; } = string.Empty;
    public string Function { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class BlenderPlugin
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
    public string Url { get; set; } = string.Empty;
}

public class ClothingTutorial
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public List<string> Tips { get; set; } = new();
}
