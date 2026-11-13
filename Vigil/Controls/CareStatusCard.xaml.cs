namespace Vigil.Controls;

// xaml-lint: allow builtin - a composed view (status label, timer, sparkline, values) with a tone visual-state group, not a card container; its surface is a ShadowContainer for the inset shadow
public sealed partial class CareStatusCard : UserControl
{
    public CareStatusCard()
    {
        InitializeComponent();
    }
}
