namespace Vigil.Controls;

/// <summary>Pad stepper. Pure dependency properties: no handlers, the model owns every change.</summary>
public sealed partial class VitalStepper : UserControl
{
    public static readonly DependencyProperty LabelProperty = Register(nameof(Label), "");
    public static readonly DependencyProperty UnitProperty = Register(nameof(Unit), "");
    public static readonly DependencyProperty GlyphProperty = Register(nameof(Glyph), "");
    public static readonly DependencyProperty FlagProperty = Register(nameof(Flag), "");
    public static readonly DependencyProperty TextProperty = Register(nameof(Text), "");
    public static readonly DependencyProperty FieldProperty = Register(nameof(Field), "");
    public static readonly DependencyProperty SpokenNameProperty = Register(nameof(SpokenName), "");

    public static readonly DependencyProperty GlyphBrushProperty = DependencyProperty.Register(
        nameof(GlyphBrush), typeof(Brush), typeof(VitalStepper), new PropertyMetadata(null));

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(VitalStepper), new PropertyMetadata(null));

    public VitalStepper()
    {
        this.InitializeComponent();
    }

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string Flag { get => (string)GetValue(FlagProperty); set => SetValue(FlagProperty, value); }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Draft field name passed to the step command ("Hr|+").</summary>
    public string Field { get => (string)GetValue(FieldProperty); set => SetValue(FieldProperty, value); }

    /// <summary>Spoken name, e.g. "heart rate, beats per minute".</summary>
    public string SpokenName { get => (string)GetValue(SpokenNameProperty); set => SetValue(SpokenNameProperty, value); }

    public Brush? GlyphBrush { get => (Brush?)GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public string IncrementParameter => $"{Field}|+";

    public string DecrementParameter => $"{Field}|-";

    /// <summary>"Increase heart rate, beats per minute"; the spoken name comes localized through the stepper's x:Uid.</summary>
    public string IncrementName => Loc.F("Stepper_Increase", "Increase {0}", SpokenName);

    public string DecrementName => Loc.F("Stepper_Decrease", "Decrease {0}", SpokenName);

    public string AccessibleName => SpokenName;

    private static DependencyProperty Register(string name, string fallback) =>
        DependencyProperty.Register(name, typeof(string), typeof(VitalStepper), new PropertyMetadata(fallback));
}
