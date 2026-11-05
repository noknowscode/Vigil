using Microsoft.UI.Xaml.Media.Animation;
namespace Vigil.Controls;
public sealed partial class CareMark : UserControl
{
    public static readonly DependencyProperty AcknowledgeProperty = DependencyProperty.Register(nameof(Acknowledge), typeof(bool), typeof(CareMark), new PropertyMetadata(false, (d, _) => ((CareMark)d).UpdateMotion()));
    public static readonly DependencyProperty ReduceMotionProperty = DependencyProperty.Register(nameof(ReduceMotion), typeof(bool), typeof(CareMark), new PropertyMetadata(false, (d, _) => ((CareMark)d).UpdateMotion()));
    public bool Acknowledge { get => (bool)GetValue(AcknowledgeProperty); set => SetValue(AcknowledgeProperty, value); }
    public bool ReduceMotion { get => (bool)GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }
    private Storyboard? _motion;
    public CareMark()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateMotion();
        Unloaded += (_, _) => _motion?.Stop();
    }
    private void UpdateMotion()
    {
        _motion?.Stop();
        if (!Acknowledge || ReduceMotion || Leaves is null) return;
        var turn = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(2), RepeatBehavior = new RepeatBehavior(5) };
        turn.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 0 });
        turn.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.6)), Value = 12, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        turn.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2)), Value = 0, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        Storyboard.SetTarget(turn, Leaves);
        Storyboard.SetTargetProperty(turn, "(UIElement.RenderTransform).(CompositeTransform.Rotation)");
        _motion = new Storyboard(); _motion.Children.Add(turn); _motion.Begin();
    }
}
