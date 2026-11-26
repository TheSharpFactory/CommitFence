using CommunityToolkit.Mvvm.Input;

namespace CF.App.Exemptions;

internal sealed record ExemptionRowAction(string Text, IRelayCommand Command, bool OpensLine = false, bool OpensRule = false)
{
    public bool IsQuiet
    {
        get { return !OpensLine && !OpensRule; }
    }
}
