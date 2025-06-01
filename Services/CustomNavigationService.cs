using skinhunter.ViewModels;
using skinhunter.Models;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Wpf.Ui;
using skinhunter.ViewModels.Windows;
using skinhunter.ViewModels.Dialogs;
using skinhunter.Views.Pages;
// No es necesario Wpf.Ui.Controls si no se interactúa directamente con INavigationView aquí

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
        // Ya no se necesita GetAndConsumeLastNavigationParameter
    }

    public class CustomNavigationService : ICustomNavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationService _wpfUiNavigationService;
        private MainWindowViewModel? _mainWindowViewModelCache;

        public CustomNavigationService(IServiceProvider serviceProvider, INavigationService wpfUiNavigationService)
        {
            _serviceProvider = serviceProvider;
            _wpfUiNavigationService = wpfUiNavigationService;
        }

        private MainWindowViewModel MainVM => _mainWindowViewModelCache ??= _serviceProvider.GetRequiredService<MainWindowViewModel>();

        public void NavigateToChampionDetail(int championId)
        {
            _wpfUiNavigationService.Navigate(typeof(ChampionDetailPage), championId);
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
            await omnisearchVM.EnsureDataLoadedAsync();
        }

        public void CloseDialog()
        {
            MainVM.DialogViewModel = null;
        }

        public void CloseOmnisearchDialog()
        {
            var omniVM = MainVM.OmnisearchDialogViewModel;
            if (omniVM != null)
            {
                omniVM.IsFilterPopupOpen = false;
            }
            MainVM.OmnisearchDialogViewModel = null;
        }

        public void GoBack()
        {
            if (MainVM.DialogViewModel != null || MainVM.OmnisearchDialogViewModel != null)
            {
                CloseDialog();
                CloseOmnisearchDialog();
                return;
            }
            _wpfUiNavigationService.GoBack(); // Usar directamente el método de la interfaz
        }
    }
}