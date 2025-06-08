namespace skinhunter.ViewModels.Pages // Este debe coincidir con el using en App.xaml.cs
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _counter;

        [RelayCommand]
        private void CounterIncrement()
        {
            Counter++;
        }
    }
}