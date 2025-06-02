using skinhunter.ViewModels;
using skinhunter.Models;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Wpf.Ui;
using skinhunter.ViewModels.Windows;
using skinhunter.ViewModels.Dialogs;
using skinhunter.Views.Pages;
using System.Diagnostics;

namespace skinhunter.Services
{
    public interface ICustomNavigationService
    {
        void NavigateToChampionDetail(int championId);
        void ShowSkinDetailDialog(Skin skin);
        void ShowOmnisearchDialog();
        void CloseDialog();
        void CloseOmnisearchDialog();
        void GoBack();
        object? ConsumeNavigationParameter();
    }

    public class CustomNavigationService : ICustomNavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationService _wpfUiNavigationService;
        private MainWindowViewModel? _mainWindowViewModelCache;
        private object? _pendingNavigationParameter;

        public CustomNavigationService(IServiceProvider serviceProvider, INavigationService wpfUiNavigationService)
        {
            _serviceProvider = serviceProvider;
            _wpfUiNavigationService = wpfUiNavigationService;
        }

        private MainWindowViewModel MainVM => _mainWindowViewModelCache ??= _serviceProvider.GetRequiredService<MainWindowViewModel>();

        public void NavigateToChampionDetail(int championId)
        {
            Debug.WriteLine($"[CustomNavigationService.NavigateToChampionDetail] Storing parameter: {championId} and navigating to ChampionDetailPage");
            _pendingNavigationParameter = championId;
            _wpfUiNavigationService.Navigate(typeof(ChampionDetailPage));
        }

        public object? ConsumeNavigationParameter()
        {
            var parameter = _pendingNavigationParameter;
            _pendingNavigationParameter = null;
            Debug.WriteLine($"[CustomNavigationService.ConsumeNavigationParameter] Parameter consumed: {parameter}");
            return parameter;
        }

        public void ShowSkinDetailDialog(Skin skin)
        {
            var skinDetailVM = _serviceProvider.GetRequiredService<SkinDetailViewModel>();
            MainVM.DialogViewModel = skinDetailVM;
            _ = skinDetailVM.LoadSkinAsync(skin);
        }

        public async void ShowOmnisearchDialog()
        {
            var omnisearchVM = _serviceProvider.GetRequiredService<OmnisearchViewModel>();
            MainVM.OmnisearchDialogViewModel = omnisearchVM;
            if (omnisearchVM is not null)
            {
                await omnisearchVM.EnsureDataLoadedAsync();
            }
        }

        public void CloseDialog()
        {
            MainVM.DialogViewModel = null;
        }

        public void CloseOmnisearchDialog()
        {
            var omniVM = MainVM.OmnisearchDialogViewModel;
            if (omniVM is not null)
            {
                omniVM.IsFilterPopupOpen = false;
            }
            MainVM.OmnisearchDialogViewModel = null;
        }

        public void GoBack()
        {
            if (MainVM.DialogViewModel is not null || MainVM.OmnisearchDialogViewModel is not null)
            {
                CloseDialog();
                CloseOmnisearchDialog();
                return;
            }
            _wpfUiNavigationService.GoBack();
        }
    }
}