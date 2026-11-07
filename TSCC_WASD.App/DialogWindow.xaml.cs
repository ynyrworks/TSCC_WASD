using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using Button = Wpf.Ui.Controls.Button;

namespace TSCC_WASD.App;

/// <summary>The window behind <see cref="AppDialog"/>: message, optional icon and the buttons for a <see cref="MessageBoxButton"/> set.</summary>
public partial class DialogWindow : FluentWindow
{
    /// <summary>The answer; stays the dismiss answer (Esc or the close button) until a button is clicked.</summary>
    public MessageBoxResult Result { get; private set; }

    internal IReadOnlyList<Button> Buttons => ButtonRow.Children.OfType<Button>().ToList();

    public DialogWindow(string text, string caption, MessageBoxButton buttons, MessageBoxImage image)
    {
        Result = AppDialog.DismissResult(buttons);
        InitializeComponent();
        Title = caption;
        Bar.Title = caption;
        MessageText.Text = text;
        if (GlyphFor(image) is { } symbol)
        {
            Glyph.Symbol = symbol;
            Glyph.SetResourceReference(ForegroundProperty, image is MessageBoxImage.Error or MessageBoxImage.Warning
                ? "SystemFillColorCautionBrush" : "AccentTextFillColorPrimaryBrush");
            Glyph.Visibility = Visibility.Visible;
        }
        foreach (var answer in AppDialog.ButtonsFor(buttons))
        {
            bool first = ButtonRow.Children.Count == 0;
            var button = new Button
            {
                Content = AppDialog.ButtonText(answer), MinWidth = 88, Margin = new Thickness(first ? 0 : 8, 0, 0, 0),
                IsDefault = first, Tag = answer
            };
            if (first) button.Appearance = ControlAppearance.Primary;
            button.Click += (_, _) => Finish(answer);
            ButtonRow.Children.Add(button);
        }
        // Esc answers like the close button: Cancel, OK for a plain message, No for a Yes/No question.
        InputBindings.Add(new KeyBinding(new Dismiss(() => Close()), Key.Escape, ModifierKeys.None));
        Loaded += (_, _) => Buttons.FirstOrDefault()?.Focus();
    }

    private void Finish(MessageBoxResult result)
    {
        Result = result;
        Close();
    }

    private static SymbolRegular? GlyphFor(MessageBoxImage image) => image switch
    {
        MessageBoxImage.Error => SymbolRegular.ErrorCircle24,
        MessageBoxImage.Warning => SymbolRegular.Warning24,
        MessageBoxImage.Question => SymbolRegular.QuestionCircle24,
        MessageBoxImage.Information => SymbolRegular.Info24,
        _ => null
    };

    private sealed class Dismiss(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }
}
