using System.Windows;
using System.Windows.Controls;

namespace TSCC_WASD.App.Pages;

public partial class HomePage : Page
{
    public HomePage() => InitializeComponent();

    // The drop-down's menu is not in the visual tree, so it does not inherit the view model by itself.
    private void OnMoreActionsDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        MoreActionsMenu.DataContext = e.NewValue;
}
