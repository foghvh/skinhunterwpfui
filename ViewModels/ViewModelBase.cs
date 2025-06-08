
namespace skinhunter.ViewModels
{
    public abstract partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotLoading))]
        private bool _isLoading;

        public bool IsNotLoading => !IsLoading;
    }
}