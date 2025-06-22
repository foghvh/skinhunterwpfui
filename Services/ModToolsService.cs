using Microsoft.Extensions.DependencyInjection;
using skinhunter.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace skinhunter.Services
{
    public class ModToolsService : IDisposable
    {
        private readonly string _modToolsExePath;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _installedSkinsDir;
        private readonly string _profilesDir;
        private readonly string _gamePath;

        private Process? _runOverlayProcess;
        private readonly SemaphoreSlim _processLock = new(1, 1);
        private readonly CancellationTokenSource _queueCts = new();
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _commandQueue = new();

        public event Action<string>? CommandOutputReceived;
        public event Action<bool>? OverlayStatusChanged;
        public bool IsOverlayRunning { get; private set; }

        public ModToolsService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            string appExePath = Path.GetDirectoryName(AppContext.BaseDirectory) ?? throw new DirectoryNotFoundException("Could not determine application base directory.");
            _modToolsExePath = Path.Combine(appExePath, "Tools", "cslol-tools", "mod-tools.exe");
            string userDataDir = Path.Combine(appExePath, "UserData", "LoLModInstaller");
            _installedSkinsDir = Path.Combine(userDataDir, "installed");
            _profilesDir = Path.Combine(userDataDir, "profiles", "Default");
            _gamePath = @"C:\Riot Games\League of Legends\Game";

            EnsureDirectoriesExist();
            _ = ProcessQueueAsync(_queueCts.Token);
        }

        private void EnsureDirectoriesExist()
        {
            try
            {
                Directory.CreateDirectory(_installedSkinsDir);
                Directory.CreateDirectory(_profilesDir);
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[ModToolsService] Failed to create directories: {ex.Message}");
            }
        }

        private Task EnqueueCommand(Func<CancellationToken, Task> command)
        {
            _commandQueue.Enqueue(command);
            return Task.CompletedTask;
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_commandQueue.TryDequeue(out var command))
                {
                    try
                    {
                        await command(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        FileLoggerService.Log($"[ModToolsService] Error executing command: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                    }
                }
                else
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

        private async Task<(bool Success, string Output)> ExecuteShortLivedCommandAsync(string arguments, CancellationToken cancellationToken)
        {
            await _processLock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_modToolsExePath))
                {
                    return (false, "mod-tools.exe not found.");
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = _modToolsExePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    WorkingDirectory = Path.GetDirectoryName(_modToolsExePath)
                };

                FileLoggerService.Log($"[ModToolsService] Executing: \"{processInfo.FileName}\" {processInfo.Arguments}");

                using var process = Process.Start(processInfo);
                if (process == null) return (false, "Failed to start process.");

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) { outputBuilder.AppendLine(e.Data); CommandOutputReceived?.Invoke(e.Data); } };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) { errorBuilder.AppendLine(e.Data); CommandOutputReceived?.Invoke(e.Data); } };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                string finalOutput = string.IsNullOrWhiteSpace(errorBuilder.ToString()) ? outputBuilder.ToString() : errorBuilder.ToString();
                FileLoggerService.Log($"[ModToolsService] Process exited with code {process.ExitCode}. Output: {finalOutput}");
                return (process.ExitCode == 0, finalOutput);
            }
            finally
            {
                _processLock.Release();
            }
        }

        public Task StopRunOverlayAsync() => EnqueueCommand(StopRunOverlayInternalAsync);
        public Task QueueRebuildWithInstalledSkins() => EnqueueCommand(RebuildAndRunOverlayInternalAsync);
        public Task QueueInstallAndRebuild(InstalledSkinInfo skinInfo, byte[] fantomeBytes) => EnqueueCommand(c => InstallSkinInternalAsync(skinInfo, fantomeBytes, c));
        public Task QueueUninstallSkins(IEnumerable<InstalledSkinInfo> skinsToUninstall) => EnqueueCommand(c => UninstallSkinsInternalAsync(skinsToUninstall, c));

        private async Task StopRunOverlayInternalAsync(CancellationToken cancellationToken)
        {
            await _processLock.WaitAsync(cancellationToken);
            try
            {
                if (_runOverlayProcess != null && !_runOverlayProcess.HasExited)
                {
                    FileLoggerService.Log($"[ModToolsService] Stopping runoverlay process PID: {_runOverlayProcess.Id}");
                    _runOverlayProcess.Kill(true);
                    await _runOverlayProcess.WaitForExitAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[ModToolsService] Error stopping overlay: {ex.Message}");
            }
            finally
            {
                if (_runOverlayProcess != null)
                {
                    _runOverlayProcess.Exited -= OnRunOverlayProcessExited;
                    _runOverlayProcess.Dispose();
                    _runOverlayProcess = null;
                }

                if (IsOverlayRunning)
                {
                    IsOverlayRunning = false;
                    OverlayStatusChanged?.Invoke(false);
                }
                _processLock.Release();
            }
        }

        private async Task RebuildAndRunOverlayInternalAsync(CancellationToken cancellationToken)
        {
            await StopRunOverlayInternalAsync(cancellationToken);
            await MkOverlayAsync(cancellationToken);

            var userPrefs = _serviceProvider.GetRequiredService<UserPreferencesService>();
            if (userPrefs.GetInstalledSkins().Any())
            {
                await RunOverlayAsync(cancellationToken);
            }
        }

        private async Task InstallSkinInternalAsync(InstalledSkinInfo skinInfo, byte[] fantomeBytes, CancellationToken cancellationToken)
        {
            await StopRunOverlayInternalAsync(cancellationToken);

            string fantomeFilePath = Path.Combine(_installedSkinsDir, skinInfo.FileName);
            string skinDestinationDir = Path.Combine(_installedSkinsDir, skinInfo.FolderName);

            Directory.CreateDirectory(skinDestinationDir);
            await File.WriteAllBytesAsync(fantomeFilePath, fantomeBytes, cancellationToken);

            await ImportFantomeAsync(fantomeFilePath, skinDestinationDir, cancellationToken);

            await TryDeleteFileAsync(fantomeFilePath);

            var userPrefs = _serviceProvider.GetRequiredService<UserPreferencesService>();
            await userPrefs.AddInstalledSkinAsync(skinInfo);

            await RebuildAndRunOverlayInternalAsync(cancellationToken);
        }

        private async Task UninstallSkinsInternalAsync(IEnumerable<InstalledSkinInfo> skinsToUninstall, CancellationToken cancellationToken)
        {
            await StopRunOverlayInternalAsync(cancellationToken);
            var userPrefs = _serviceProvider.GetRequiredService<UserPreferencesService>();
            foreach (var skin in skinsToUninstall)
            {
                var skinDir = Path.Combine(_installedSkinsDir, skin.FolderName);
                await TryDeleteDirectoryAsync(skinDir);
                await userPrefs.RemoveInstalledSkinAsync(skin);
            }
            await RebuildAndRunOverlayInternalAsync(cancellationToken);
        }

        private async Task RunOverlayAsync(CancellationToken cancellationToken)
        {
            await _processLock.WaitAsync(cancellationToken);
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _modToolsExePath,
                    Arguments = $"runoverlay \"{_profilesDir}\" --game:\"{_gamePath}\" configless",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_modToolsExePath)
                };

                FileLoggerService.Log($"[ModToolsService] Starting long-lived process: \"{startInfo.FileName}\" {startInfo.Arguments}");
                _runOverlayProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                _runOverlayProcess.OutputDataReceived += (s, e) => { if (e.Data != null) CommandOutputReceived?.Invoke(e.Data); };
                _runOverlayProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) CommandOutputReceived?.Invoke(e.Data); };
                _runOverlayProcess.Exited += OnRunOverlayProcessExited;

                _runOverlayProcess.Start();
                _runOverlayProcess.BeginOutputReadLine();
                _runOverlayProcess.BeginErrorReadLine();

                IsOverlayRunning = true;
                OverlayStatusChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[ModToolsService] Failed to start runoverlay process: {ex.Message}");
            }
            finally
            {
                _processLock.Release();
            }
        }

        private void OnRunOverlayProcessExited(object? sender, EventArgs e)
        {
            FileLoggerService.Log("[ModToolsService] runoverlay process has exited.");
            if (_runOverlayProcess != null)
            {
                _runOverlayProcess.Exited -= OnRunOverlayProcessExited;
                _runOverlayProcess.Dispose();
                _runOverlayProcess = null;
            }
            IsOverlayRunning = false;
            OverlayStatusChanged?.Invoke(false);
        }

        private async Task ImportFantomeAsync(string fantomePath, string destDir, CancellationToken cancellationToken)
        {
            var args = $"import \"{fantomePath}\" \"{destDir}\"";
            await ExecuteShortLivedCommandAsync(args, cancellationToken);
        }

        private async Task MkOverlayAsync(CancellationToken cancellationToken)
        {
            var userPrefs = _serviceProvider.GetRequiredService<UserPreferencesService>();
            var installed = userPrefs.GetInstalledSkins();
            if (installed.Any())
            {
                var modsArg = $"--mods:{string.Join("/", installed.Select(s => s.FolderName))}";
                var args = $"mkoverlay \"{_installedSkinsDir}\" \"{_profilesDir}\" --game:\"{_gamePath}\" {modsArg}";
                await ExecuteShortLivedCommandAsync(args, cancellationToken);
            }
        }

        private static async Task TryDeleteFileAsync(string path)
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (IOException) { await Task.Delay(200); try { File.Delete(path); } catch (Exception ex) { FileLoggerService.Log($"Failed to delete file {path}: {ex.Message}"); } }
            }
        }

        private static async Task TryDeleteDirectoryAsync(string path)
        {
            if (Directory.Exists(path))
            {
                try { Directory.Delete(path, true); }
                catch (IOException) { await Task.Delay(200); try { Directory.Delete(path, true); } catch (Exception ex) { FileLoggerService.Log($"Failed to delete directory {path}: {ex.Message}"); } }
            }
        }

        public void Dispose()
        {
            _queueCts.Cancel();
            _queueCts.Dispose();
            StopRunOverlayAsync().Wait(1000);
            _processLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}