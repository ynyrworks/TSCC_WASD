using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TSCC_WASD.App;

/// <summary>
/// The coach mark: a highlight frame around the step's target and a card with the text and buttons. It sits over the
/// pages without a background, so the app stays usable behind it.
/// </summary>
public partial class TourOverlay : UserControl
{
    private TourController? _tour;
    private Func<TourStep, IReadOnlyList<FrameworkElement>>? _find;
    private Rect? _lastTarget;
    private bool _placed;

    public TourOverlay()
    {
        InitializeComponent();
        Card.SizeChanged += (_, _) => _placed = false; // new text: place again
        LayoutUpdated += (_, _) => { if (_tour is { IsActive: true }) Place(bringIntoView: false); };
    }

    public void Attach(TourController tour, ICommand install, Func<TourStep, IReadOnlyList<FrameworkElement>> find)
    {
        _tour = tour;
        _find = find;
        DataContext = tour;
        InstallButton.Command = install;
        SkipButton.Click += (_, _) => tour.Skip();
        BackButton.Click += (_, _) => tour.Back();
        NextButton.Click += (_, _) => tour.Next();
        tour.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TourController.IsActive))
                Visibility = tour.IsActive ? Visibility.Visible : Visibility.Collapsed;
        };
    }

    internal Border CardElement => Card;
    internal Border HighlightElement => Highlight;
    internal Button NextElement => NextButton;

    /// <summary>Anchors the current step once the page has been laid out, and puts the keyboard focus on Next.</summary>
    public void ShowStep()
    {
        _placed = false;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            if (_tour is not { IsActive: true }) return;
            Place(bringIntoView: true);
            NextButton.Focus();
        });
    }

    internal void Place(bool bringIntoView)
    {
        if (_tour is null || _find is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var elements = _find(_tour.Step);
        if (bringIntoView) foreach (var element in elements) element.BringIntoView();
        var target = TourAnchors.Union(elements, this);
        if (_placed && target == _lastTarget) return;
        _placed = true;
        _lastTarget = target;
        Card.Measure(new Size(Card.Width, ActualHeight));
        var area = new Size(ActualWidth, ActualHeight);
        Point at;
        if (target is { } rect)
        {
            var frame = Rect.Inflate(rect, 4, 4);
            Canvas.SetLeft(Highlight, frame.Left);
            Canvas.SetTop(Highlight, frame.Top);
            Highlight.Width = frame.Width;
            Highlight.Height = frame.Height;
            Highlight.Visibility = Visibility.Visible;
            at = TourAnchors.CardPosition(frame, Card.DesiredSize, area);
        }
        else
        {
            // No anchor (welcome step, or the target is not on the page showing right now): a centred card.
            Highlight.Visibility = Visibility.Collapsed;
            at = TourAnchors.Centered(Card.DesiredSize, area);
        }
        Canvas.SetLeft(Card, at.X);
        Canvas.SetTop(Card, at.Y);
    }
}
