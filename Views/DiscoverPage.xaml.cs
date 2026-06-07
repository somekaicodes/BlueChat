using BlueChat.ViewModels;

namespace BlueChat.Views;

public partial class DiscoverPage : ContentPage
{
    public DiscoverPage(DiscoverViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
