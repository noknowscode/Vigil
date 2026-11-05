namespace Vigil.Presentation;

// xaml-lint: allow builtin - a data template with its own visual states (strip, stepper, details) driven by VisualStateManagerExtensions; the card surfaces inside are ShadowContainers (exact card/tray shadows)

/// <summary>Today patient card. States come from <see cref="PatientCard.VisualStates"/> (utu:VisualStateManagerExtensions).</summary>
public sealed partial class PatientCardView : UserControl
{
    public PatientCardView()
    {
        InitializeComponent();
    }
}