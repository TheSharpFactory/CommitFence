using Avalonia;
using Avalonia.Controls;

namespace CF.App.Review;

/// <summary>
/// The pieces of a rule's sentence, drawn as a finding draws them: a finding's open body and the specimen of a rule on the Rules screen
/// both use it, so there is one drawing of a rule written in words.
/// </summary>
internal sealed partial class SentencePieces : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<SentencePieceViewModel>?> PiecesProperty = AvaloniaProperty.Register<SentencePieces, IReadOnlyList<SentencePieceViewModel>?>(nameof(Pieces));

    static SentencePieces()
    {
        PiecesProperty.Changed.AddClassHandler<SentencePieces>((view, _) => view.Show());
    }

    public SentencePieces()
    {
        InitializeComponent();
    }

    public IReadOnlyList<SentencePieceViewModel>? Pieces
    {
        get
        {
            return GetValue(PiecesProperty);
        }
        set
        {
            SetValue(PiecesProperty, value);
        }
    }

    private void Show()
    {
        PieceList.ItemsSource = Pieces;
    }
}
