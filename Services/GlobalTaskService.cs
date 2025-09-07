using skinhunter.ViewModels.Windows;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Wpf.Ui.Controls;

namespace skinhunter.Services
{
    public class GlobalTaskService
    {
        private readonly MainWindowViewModel _mainWindowViewModel;
        private readonly SemaphoreSlim _taskSemaphore = new(1, 1);

        public GlobalTaskService(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async Task RunTask(Func<Task> taskToRun, string loadingMessage)
        {
            await _taskSemaphore.WaitAsync();
            try
            {
                _mainWindowViewModel.IsGloballyLoading = true;
                _mainWindowViewModel.GlobalLoadingMessage = loadingMessage;

                await taskToRun();
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[GlobalTaskService] Error running global task: {ex.Message}");
                await _mainWindowViewModel.ShowGlobalError("An unexpected error occurred.");
            }
            finally
            {
                _mainWindowViewModel.IsGloballyLoading = false;
                _taskSemaphore.Release();
            }
        }

        public async Task RunGlobalSequence(Func<Action<string>, Task> sequence)
        {
            await _taskSemaphore.WaitAsync();
            try
            {
                _mainWindowViewModel.IsGloballyLoading = true;
                Action<string> messageSetter = (msg) =>
                {
                    _mainWindowViewModel.GlobalLoadingMessage = msg;
                };

                await sequence(messageSetter);
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[GlobalTaskService] Error running global sequence: {ex.Message}");
                await _mainWindowViewModel.ShowGlobalError("A critical error occurred during startup.");
            }
            finally
            {
                _mainWindowViewModel.IsGloballyLoading = false;
                _taskSemaphore.Release();
            }
        }
    }
}