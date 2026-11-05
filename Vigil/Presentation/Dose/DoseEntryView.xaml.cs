namespace Vigil.Presentation;

/// <summary>The dose entry (DoseModel) shared by the Redose page and the record's Dose drawer; each host sets its insets.</summary>
public sealed partial class DoseEntryView : UserControl
{
    public static readonly DependencyProperty BodyPaddingProperty = DependencyProperty.Register(
        nameof(BodyPadding), typeof(Thickness), typeof(DoseEntryView), new PropertyMetadata(default(Thickness)));

    public static readonly DependencyProperty ActionPaddingProperty = DependencyProperty.Register(
        nameof(ActionPadding), typeof(Thickness), typeof(DoseEntryView), new PropertyMetadata(default(Thickness)));

    public DoseEntryView()
    {
        this.InitializeComponent();
    }

    /// <summary>Insets of the scrolling body (the drug list and calculation cards).</summary>
    public Thickness BodyPadding { get => (Thickness)GetValue(BodyPaddingProperty); set => SetValue(BodyPaddingProperty, value); }

    /// <summary>Insets of the action bar pinned under the body (Give, or the out-of-range confirmation).</summary>
    public Thickness ActionPadding { get => (Thickness)GetValue(ActionPaddingProperty); set => SetValue(ActionPaddingProperty, value); }
}
