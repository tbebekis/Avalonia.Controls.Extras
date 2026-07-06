namespace Avalonia.Controls;

/// <summary>
/// Describes serializable user settings for a group grid layout.
/// </summary>
public class GroupGridSettings
{
    // ● constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="GroupGridSettings"/> class.
    /// </summary>
    public GroupGridSettings()
    {
    }

    // ● properties
    /// <summary>
    /// Gets or sets the settings name.
    /// </summary>
    public string Name { get; set; } = "Default";
    /// <summary>
    /// Gets or sets the column settings.
    /// </summary>
    public List<GroupGridColumnSettings> Columns { get; set; } = new();
}
