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
using Supabase;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace skinhunter.Services
{
    public class ModToolsService : IDisposable
    {
        private readonly string _modToolsExePath;
        private readonly Client _supabaseClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _installedSkinsDir;
        private readonly string _profilesDir;
        private readonly string _gamePath;

        private Process? _runOverlayProcess;
        private readonly SemaphoreSlim _processLock = new(1, 1);
        private readonly CancellationTokenSource _queueCts = new();
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _commandQueue = new();

        public event Action<string, bool>? CommandOutputReceived;
        public event Action<bool>? OverlayStatusChanged;
        public bool IsOverlayRunning { get; private set; }

        public ModToolsService(IServiceProvider serviceProvider, Client supabaseClient)
        {
            _serviceProvider = serviceProvider;
            _supabaseClient = supabaseClient;
            string appExePath = Path.GetDirectoryName(AppContext.BaseDirectory) ?? throw new DirectoryNotFoundException("Could not determine application base directory.");
            _modToolsExePath = Path.Combine(appExePath, "Tools", "cslol-tools", "mod-tools.exe");
            string userDataDir = Path.Combine(appExePath, "UserData", "LoLModInstaller");
            _installedSkinsDir = Path.Combine(userDataDir, "installed");
            _profilesDir = Path.Combine(userDataDir, "profiles", "Default");

            var userPrefs = _serviceProvider.GetRequiredService<UserPreferencesService>();
            _gamePath = userPrefs.GetGamePath() ?? @"C:\Riot Games\League of Legends\Game";

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

        private Task EnqueueCommand(Func<CancellationToken, Task> commandAction)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _commandQueue.Enqueue(async (token) =>
            {
                try
                {
                    await commandAction(token);
                    tcs.SetResult();
                }
                catch (OperationCanceledException)
                {
                    tcs.SetCanceled();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
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
                        FileLoggerService.Log($"[ModToolsService] Error executing queued command: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
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

                process.OutputDataReceived += (s, e) => { if (e.Data != null) { outputBuilder.AppendLine(e.Data); CommandOutputReceived?.Invoke(e.Data, false); } };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) { errorBuilder.AppendLine(e.Data); CommandOutputReceived?.Invoke("An error occurred with mod-tools.", true); } };

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
        public Task QueueSyncAndRebuild() => EnqueueCommand(SynchronizeAndRebuildInternalAsync);
        public Task QueueInstallAndRebuild(InstalledSkinInfo skinInfo, byte[] fantomeBytes, ISnackbarService snackbarService) => EnqueueCommand(c => InstallSkinInternalAsync(skinInfo, fantomeBytes, snackbarService, c));
        public Task QueueUninstallSkins(IEnumerable<InstalledSkinInfo> skinsToUninstall, ISnackbarService snackbarService) => EnqueueCommand(c => UninstallSkinsInternalAsync(skinsToUninstall, snackbarService, c));

        private async Task SynchronizeAndRebuildInternalAsync(CancellationToken cancellationToken)
        {
            await StopRunOverlayInternalAsync(cancellationToken);

            var userPrefs = _serviceProvider.GetRequiredService<UserPreferencesService>();
            var installedSkins = userPrefs.GetInstalledSkins();
            var successfullyInstalledSkins = new List<InstalledSkinInfo>();

            if (!installedSkins.Any())
            {
                FileLoggerService.Log("[ModToolsService] No skins to sync. Ensuring overlay is stopped.");
                return;
            }

            FileLoggerService.Log($"[ModToolsService] Starting skin synchronization for {installedSkins.Count} skins.");
            CommandOutputReceived?.Invoke("Starting skin synchronization...", false);

            foreach (var skinInfo in installedSkins)
            {
                if (cancellationToken.IsCancellationRequested) return;

                CommandOutputReceived?.Invoke($"Verifying: {skinInfo.SkinName}", false);
                bool isInstalledSuccessfully = false;
                var skinDestinationDir = Path.Combine(_installedSkinsDir, skinInfo.FolderName);

                if (Directory.Exists(skinDestinationDir))
                {
                    isInstalledSuccessfully = true;
                    FileLoggerService.Log($"[ModToolsService] Skin '{skinInfo.SkinName}' already exists locally.");
                }
                else
                {
                    FileLoggerService.Log($"[ModToolsService] Skin '{skinInfo.SkinName}' is missing locally. Attempting installation.");
                    CommandOutputReceived?.Invoke($"Downloading: {skinInfo.SkinName}...", false);

                    int maxRetries = 2;
                    for (int retry = 0; retry < maxRetries; retry++)
                    {
                        try
                        {
                            await DownloadAndInstallMissingSkinAsync(skinInfo, cancellationToken);
                            isInstalledSuccessfully = true;
                            FileLoggerService.Log($"[ModToolsService] Successfully synced skin '{skinInfo.SkinName}' on attempt {retry + 1}.");
                            CommandOutputReceived?.Invoke($"Synced: {skinInfo.SkinName}", false);
                            break;
                        }
                        catch (Exception ex)
                        {
                            FileLoggerService.Log($"[ModToolsService] Failed to sync skin '{skinInfo.SkinName}' on attempt {retry + 1}. Error: {ex.Message}");
                            CommandOutputReceived?.Invoke($"Error syncing {skinInfo.SkinName} (attempt {retry + 1})", true);

                            await TryDeleteDirectoryAsync(skinDestinationDir);
                            await TryDeleteFileAsync(Path.Combine(_installedSkinsDir, skinInfo.FileName));

                            if (retry < maxRetries - 1)
                            {
                                await Task.Delay(1000, cancellationToken);
                            }
                        }
                    }
                }

                if (isInstalledSuccessfully)
                {
                    successfullyInstalledSkins.Add(skinInfo);
                }
                else
                {
                    FileLoggerService.Log($"[ModToolsService] CRITICAL: Failed to install skin '{skinInfo.SkinName}' after all retries. It will be excluded from this build.");
                    CommandOutputReceived?.Invoke($"Failed to install {skinInfo.SkinName}. Excluding.", true);
                }
            }

            FileLoggerService.Log($"[ModToolsService] Skin synchronization finished. Successfully installed {successfullyInstalledSkins.Count}/{installedSkins.Count} skins.");

            if (successfullyInstalledSkins.Any())
            {
                await RebuildAndRunOverlayForSkinsAsync(successfullyInstalledSkins, cancellationToken);
            }
            else
            {
                FileLoggerService.Log("[ModToolsService] No skins were successfully installed. Skipping overlay build.");
            }
        }

        private async Task StopRunOverlayInternalAsync(CancellationToken cancellationToken)
        {
            Process? processToStop = null;

            await _processLock.WaitAsync(cancellationToken);
            try
            {
                if (_runOverlayProcess != null && !_runOverlayProcess.HasExited)
                {
                    processToStop = _runOverlayProcess;
                    _runOverlayProcess = null;
                }
            }
            finally
            {
                _processLock.Release();
            }

            if (processToStop != null)
            {
                try
                {
                    FileLoggerService.Log($"[ModToolsService] Stopping runoverlay process PID: {processToStop.Id}");
                    processToStop.Kill(true);
                    await processToStop.WaitForExitAsync(cancellationToken);
                    FileLoggerService.Log($"[ModToolsService] Process PID {processToStop.Id} has been stopped.");
                }
                catch (Exception ex)
                {
                    FileLoggerService.Log($"[ModToolsService] Exception while stopping process PID {processToStop.Id}: {ex.Message}");
                }
                finally
                {
                    processToStop.Dispose();
                }
            }
            else
            {
                FileLoggerService.Log("[ModToolsService] StopRunOverlayInternalAsync called but no active process was found.");
            }

            if (IsOverlayRunning)
            {
                IsOverlayRunning = false;
                OverlayStatusChanged?.Invoke(false);
            }

            await Task.Delay(250, cancellationToken);
        }

        private async Task RebuildAndRunOverlayForSkinsAsync(List<InstalledSkinInfo> skins, CancellationToken cancellationToken)
        {
            FileLoggerService.Log($"Rebuilding overlay for {skins.Count} skins.");
            await MkOverlayAsync(cancellationToken, skins);
            await RunOverlayAsync(cancellationToken);
        }

        private async Task InstallSkinInternalAsync(InstalledSkinInfo skinInfo, byte[] fantomeBytes, ISnackbarService snackbarService, CancellationToken cancellationToken)
        {
            await StopRunOverlayInternalAsync(cancellationToken);

            snackbarService.Show("Installing...", $"Installing skin '{skinInfo.SkinName}'...", ControlAppearance.Secondary, new SymbolIcon(SymbolRegular.ArrowDownload24), TimeSpan.FromSeconds(15));

            string fantomeFilePath = Path.Combine(_installedSkinsDir, skinInfo.FileName);
            string skinDestinationDir = Path.Combine(_installedSkinsDir, skinInfo.FolderName);

            await TryDeleteDirectoryAsync(skinDestinationDir);
            await TryDeleteFileAsync(fantomeFilePath);

            Directory.CreateDirectory(skinDestinationDir);
            await File.WriteAllBytesAsync(fantomeFilePath, fantomeBytes, cancellationToken);

            var (success, output) = await ImportFantomeAsync(fantomeFilePath, skinDestinationDir, cancellationToken);

            await TryDeleteFileAsync(fantomeFilePath);

            if (!success)
            {
                FileLoggerService.Log($"[ModToolsService] mod-tools import failed for {skinInfo.SkinName}. Output: {output}. Cleaning up failed installation.");
                await TryDeleteDirectoryAsync(skinDestinationDir);
                snackbarService.Show("Installation Failed", $"Could not install '{skinInfo.SkinName}'.", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
                throw new Exception($"Failed to import skin: {skinInfo.SkinName}.");
            }

            snackbarService.Show("Success!", $"Skin '{skinInfo.SkinName}' installed.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(5));

            var userPrefs = _serviceProvider.GetRequiredService<UserPreferencesService>();
            await userPrefs.AddInstalledSkinAsync(skinInfo);

            await SynchronizeAndRebuildInternalAsync(cancellationToken);
        }

        private async Task UninstallSkinsInternalAsync(IEnumerable<InstalledSkinInfo> skinsToUninstall, ISnackbarService snackbarService, CancellationToken cancellationToken)
        {
            await StopRunOverlayInternalAsync(cancellationToken);
            snackbarService.Show("Uninstalling...", $"Removing {skinsToUninstall.Count()} skin(s).", ControlAppearance.Secondary, new SymbolIcon(SymbolRegular.Delete24), TimeSpan.FromSeconds(15));
            var userPrefs = _serviceProvider.GetRequiredService<UserPreferencesService>();
            foreach (var skin in skinsToUninstall)
            {
                var skinDir = Path.Combine(_installedSkinsDir, skin.FolderName);
                await TryDeleteDirectoryAsync(skinDir);
                await userPrefs.RemoveInstalledSkinAsync(skin);
            }
            snackbarService.Show("Success!", "Selected skins have been uninstalled.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(5));
            await SynchronizeAndRebuildInternalAsync(cancellationToken);
        }

        private async Task DownloadAndInstallMissingSkinAsync(InstalledSkinInfo skinInfo, CancellationToken cancellationToken)
        {
            string fantomeFilePath = Path.Combine(_installedSkinsDir, skinInfo.FileName);
            string skinDestinationDir = Path.Combine(_installedSkinsDir, skinInfo.FolderName);

            string supabasePath;
            if (!string.IsNullOrEmpty(skinInfo.ChromaName) && skinInfo.SkinOrChromaId > 10000)
            {
                supabasePath = $"campeones/{skinInfo.ChampionId}/{skinInfo.SkinOrChromaId}.fantome";
            }
            else
            {
                int skinNum = skinInfo.SkinOrChromaId % 1000;
                supabasePath = $"campeones/{skinInfo.ChampionId}/{skinNum}.fantome";
            }

            FileLoggerService.Log($"[ModToolsService] Downloading from Supabase path: {supabasePath}");
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? fileBytes = await _supabaseClient.Storage.From("campeones").Download(supabasePath, null);

            if (fileBytes == null || fileBytes.Length == 0)
            {
                throw new Exception($"Download failed or file is empty from Supabase. Path: {supabasePath}");
            }
            FileLoggerService.Log($"[ModToolsService] Downloaded {fileBytes.Length} bytes.");

            Directory.CreateDirectory(Path.GetDirectoryName(fantomeFilePath));
            await File.WriteAllBytesAsync(fantomeFilePath, fileBytes, cancellationToken);

            var (success, output) = await ImportFantomeAsync(fantomeFilePath, skinDestinationDir, cancellationToken);

            await TryDeleteFileAsync(fantomeFilePath);

            if (!success)
            {
                FileLoggerService.Log($"[ModToolsService] mod-tools import failed for {skinInfo.SkinName}. Output: {output}. Cleaning up failed installation.");
                await TryDeleteDirectoryAsync(skinDestinationDir);
                throw new Exception($"Failed to import skin: {skinInfo.SkinName}.");
            }
        }

        private async Task RunOverlayAsync(CancellationToken cancellationToken)
        {
            await _processLock.WaitAsync(cancellationToken);
            try
            {
                if (_runOverlayProcess != null)
                {
                    FileLoggerService.Log("[ModToolsService] RunOverlayAsync called but a process already exists. Aborting start.");
                    return;
                }

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

                _runOverlayProcess.OutputDataReceived += (s, e) => { if (e.Data != null) CommandOutputReceived?.Invoke(e.Data, false); };
                _runOverlayProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) CommandOutputReceived?.Invoke("An error occurred with mod-tools.", true); };
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
                _runOverlayProcess?.Dispose();
                _runOverlayProcess = null;
            }
            finally
            {
                _processLock.Release();
            }
        }

        private void OnRunOverlayProcessExited(object? sender, EventArgs e)
        {
            var exitedProcess = sender as Process;
            FileLoggerService.Log($"[ModToolsService] Process PID {exitedProcess?.Id.ToString() ?? "Unknown"} has exited.");

            if (exitedProcess != null)
            {
                exitedProcess.Exited -= OnRunOverlayProcessExited;
                exitedProcess.Dispose();
            }

            if (ReferenceEquals(_runOverlayProcess, exitedProcess))
            {
                _runOverlayProcess = null;
            }

            if (IsOverlayRunning)
            {
                IsOverlayRunning = false;
                Application.Current?.Dispatcher.Invoke(() => OverlayStatusChanged?.Invoke(false));
            }
        }

        private async Task<(bool, string)> ImportFantomeAsync(string fantomePath, string destDir, CancellationToken cancellationToken)
        {
            var args = $"import \"{fantomePath}\" \"{destDir}\"";
            return await ExecuteShortLivedCommandAsync(args, cancellationToken);
        }

        private async Task MkOverlayAsync(CancellationToken cancellationToken, List<InstalledSkinInfo> installed)
        {
            CommandOutputReceived?.Invoke("Building game files...", false);
            if (installed != null && installed.Any())
            {
                var modsArg = $"--mods:{string.Join("/", installed.Select(s => s.FolderName))}";
                var args = $"mkoverlay \"{_installedSkinsDir}\" \"{_profilesDir}\" --game:\"{_gamePath}\" {modsArg}";

                var (success, output) = await ExecuteShortLivedCommandAsync(args, cancellationToken);
                if (!success)
                {
                    FileLoggerService.Log($"[ModToolsService] CRITICAL: mkoverlay failed. Output: {output}. The overlay may not work correctly.");
                    CommandOutputReceived?.Invoke("Error: Failed to build game files.", true);
                }
            }
        }

        private static async Task TryDeleteFileAsync(string path)
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (IOException) { await Task.Delay(200); try { File.Delete(path); } catch (Exception ex) { FileLoggerService.Log($"Failed to delete file {path}: {ex.Message}"); } }
                catch (Exception ex) { FileLoggerService.Log($"Failed to delete file {path}: {ex.Message}"); }
            }
        }

        private static async Task TryDeleteDirectoryAsync(string path)
        {
            if (Directory.Exists(path))
            {
                try { Directory.Delete(path, true); }
                catch (IOException) { await Task.Delay(200); try { Directory.Delete(path, true); } catch (Exception ex) { FileLoggerService.Log($"Failed to delete directory {path}: {ex.Message}"); } }
                catch (Exception ex) { FileLoggerService.Log($"Failed to delete directory {path}: {ex.Message}"); }
            }
        }

        public void Dispose()
        {
            _queueCts.Cancel();
            _queueCts.Dispose();
            StopRunOverlayAsync().Wait(TimeSpan.FromSeconds(5));
            _processLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}