using Avalonia.Controls;

namespace CF.App.Parts;

/// <summary>
/// Where a screen's panel stands in one pane: a screen puts this mark in its main column, and the frame lays the panel there
/// when the window is too narrow for two columns. A screen that marks no place has its panel last.
/// </summary>
internal sealed class PanelPlace : Decorator
{
}
