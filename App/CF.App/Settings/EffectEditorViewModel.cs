using CF.Core.Findings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Settings;

/// <summary>An editable severity word and its last saved effect.</summary>
internal sealed partial class EffectEditorViewModel(string word, Action<string, Severity?> save) : ObservableObject
{
    public string Word { get; } = word;
    public string Name { get { return Word + " effect"; } }

    [ObservableProperty]
    public partial StrictnessCellViewModel Cell { get; set; } = new StrictnessCellViewModel(null, true);

    public void Choose(Severity? effect)
    {
        save(Word, effect);
    }
}
