
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace skinhunter.Services
{
    public class ModToolsService
    {
        private readonly string _modToolsExePath;
        private readonly string _appBaseDir;
        private readonly string _toolsDir;
        private readonly string _userDataDir;
        private readonly string _lolModInstallerPath;
        private readonly string _installedSkinsDir;
        private readonly string _profilesDir;
        private readonly string _gamePath;
        private readonly IServiceProvider _serviceProvider;
        private Process? _runOverlayProcess;

        public event Action<bool>? OverlayStatusChanged;

        public bool IsOverlayRunning => _runOverlayProcess != null && !_runOverlayProcess.HasExited;

        public ModToolsService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            string? appExePath = Path.GetDirectoryName(AppContext.BaseDirectory);
            if (string.IsNullOrEmpty(appExePath))
            {
                throw new DirectoryNotFoundException("Could not determine application executable directory.");
            }
            _appBaseDir = appExePath;

            _toolsDir = Path.Combine(_appBaseDir, "Tools");
            _modToolsExePath = Path.Combine(_toolsDir, "cslol-tools", "mod-tools.exe");

            _userDataDir = Path.Combine(_appBaseDir, "UserData");
            _lolModInstallerPath = Path.Combine(_userDataDir, "LoLModInstaller");
            _installedSkinsDir = Path.Combine(_lolModInstallerPath, "installed");
            _profilesDir = Path.Combine(_lolModInstallerPath, "profiles", "Default");

            EnsureDirectoriesExist();

            _gamePath = @"C:\Riot Games\League of Legends\Game";

            FileLoggerService.Log($"[ModToolsService] AppBaseDir: {_appBaseDir}");
            FileLoggerService.Log($"[ModToolsService] ModToolsExePath: {_modToolsExePath}");
            FileLoggerService.Log($"[ModToolsService] UserDataDir for LoLModInstaller: {_lolModInstallerPath}");
            FileLoggerService.Log($"[ModToolsService] InstalledSkinsDir: {_installedSkinsDir}");
            FileLoggerService.Log($"[ModToolsService] ProfilesDir: {_profilesDir}");
            FileLoggerService.Log($"[ModToolsService] GamePath: {_gamePath}");

            if (!File.Exists(_modToolsExePath))
            {
                FileLoggerService.Log($"[ModToolsService] CRITICAL: mod-tools.exe not found at {_modToolsExePath}. Please ensure it's in YourApp/Tools/cslol-tools/");
            }
            if (!Directory.Exists(_gamePath))
            {
                FileLoggerService.Log($"[ModToolsService] WARNING: League of Legends GamePath not found at {_gamePath}. Operations might fail.");
            }
        }

        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(_userDataDir);
            Directory.CreateDirectory(_lolModInstallerPath);
            Directory.CreateDirectory(_installedSkinsDir);
            Directory.CreateDirectory(_profilesDir);
            FileLoggerService.Log($"[ModToolsService] Ensured UserData directories exist under {_userDataDir}");
        }

        private async Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(string arguments)
        {
            if (!File.Exists(_modToolsExePath))
            {
                return (false, string.Empty, $"mod-tools.exe not found at {_modToolsExePath}");
            }

            string modToolsDirectory = Path.GetDirectoryName(_modToolsExePath) ?? _toolsDir;

            var processInfo = new ProcessStartInfo
            {
                FileName = _modToolsExePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = modToolsDirectory
            };

            FileLoggerService.Log($"[ModToolsService] Executing in '{modToolsDirectory}': {Path.GetFileName(_modToolsExePath)} {arguments}");

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return (false, string.Empty, "Failed to start mod-tools.exe process.");
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            string output = outputBuilder.ToString().Trim();
            string error = errorBuilder.ToString().Trim();

            if (process.ExitCode != 0)
            {
                FileLoggerService.Log($"[ModToolsService] Command failed. ExitCode: {process.ExitCode}, Output: {output}, Error: {error}");
                return (false, output, string.IsNullOrEmpty(error) ? $"Exited with code {process.ExitCode}" : error);
            }

            FileLoggerService.Log($"[ModToolsService] Command success. Output: {output}");
            return (true, output, error);
        }

        public async Task<(bool Success, string Message)> ImportSkinAsync(string fantomeFileName)
        {
            string sourceFilePath = Path.Combine(_installedSkinsDir, fantomeFileName);
            if (!File.Exists(sourceFilePath))
            {
                return (false, $"Skin file not found for import: {sourceFilePath}");
            }
            string arguments = $"import \"{sourceFilePath}\" \"{sourceFilePath}\" --noTFT";
            var (success, output, error) = await ExecuteCommandAsync(arguments);
            return (success, success ? $"Imported: {fantomeFileName}. Output: {output}" : $"Import failed: {error}. Output: {output}");
        }

        public async Task<(bool Success, string Message)> MakeOverlayAsync(List<string> fantomeFileNames)
        {
            if (!Directory.Exists(_gamePath))
            {
                return (false, $"League of Legends game directory not found: {_gamePath}");
            }
            string modsArgument = string.Join("/", fantomeFileNames);
            string arguments = $"mkoverlay \"{_installedSkinsDir}\" \"{_profilesDir}\" --game:\"{_gamePath}\"";
            if (!string.IsNullOrEmpty(modsArgument))
            {
                arguments += $" --mods:{modsArgument}";
            }

            var (success, output, error) = await ExecuteCommandAsync(arguments);
            return (success, success ? $"Overlay created/updated. Output: {output}" : $"Overlay creation failed: {error}. Output: {output}");
        }

        public async void StartRunOverlayWithInstalledSkins()
        {
            var userPrefs = _serviceProvider.GetRequiredService<UserPreferencesService>();
            var installedSkins = userPrefs.GetInstalledSkins().Select(s => s.FileName).ToList();
            if (installedSkins.Any())
            {
                await MakeOverlayAsync(installedSkins);
            }
            StartRunOverlay();
        }

        public (bool Success, string Message) StartRunOverlay()
        {
            if (IsOverlayRunning)
            {
                return (true, "Overlay process is already running.");
            }
            if (!Directory.Exists(_gamePath))
            {
                return (false, $"League of Legends game directory not found: {_gamePath}");
            }
            if (!Directory.Exists(Path.GetDirectoryName(_modToolsExePath)))
            {
                return (false, $"mod-tools directory not found: {Path.GetDirectoryName(_modToolsExePath)}");
            }

            string arguments = $"runoverlay \"{_profilesDir}\" --game:\"{_gamePath}\" configless";

            var processInfo = new ProcessStartInfo
            {
                FileName = _modToolsExePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_modToolsExePath)
            };

            try
            {
                _runOverlayProcess = Process.Start(processInfo);
                if (_runOverlayProcess == null)
                {
                    return (false, "Failed to start runoverlay process.");
                }
                _runOverlayProcess.EnableRaisingEvents = true;
                _runOverlayProcess.Exited += (s, e) => OverlayStatusChanged?.Invoke(false);
                OverlayStatusChanged?.Invoke(true);
                FileLoggerService.Log($"[ModToolsService] Started runoverlay PID: {_runOverlayProcess.Id}");
                return (true, $"Overlay process started with PID: {_runOverlayProcess.Id}.");
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[ModToolsService] Exception starting runoverlay: {ex.Message}");
                return (false, $"Exception starting overlay: {ex.Message}");
            }
        }

        public (bool Success, string Message) StopRunOverlay()
        {
            if (IsOverlayRunning)
            {
                try
                {
                    FileLoggerService.Log($"[ModToolsService] Attempting to kill tracked runoverlay PID: {_runOverlayProcess!.Id}");
                    _runOverlayProcess.Kill(true);
                    _runOverlayProcess.WaitForExit(5000);
                    _runOverlayProcess.Dispose();
                    _runOverlayProcess = null;
                    OverlayStatusChanged?.Invoke(false);
                    return (true, "Tracked overlay process stopped.");
                }
                catch (Exception ex)
                {
                    FileLoggerService.Log($"[ModToolsService] Exception stopping tracked runoverlay: {ex.Message}");
                    _runOverlayProcess = null;
                    OverlayStatusChanged?.Invoke(false);
                    return KillModToolsByName();
                }
            }
            FileLoggerService.Log($"[ModToolsService] No tracked overlay process to stop. Attempting kill by name.");
            return KillModToolsByName();
        }

        public (bool Success, string Message) KillModToolsByName()
        {
            string processName = Path.GetFileNameWithoutExtension(_modToolsExePath);
            bool wasRunning = false;
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (!processes.Any())
                {
                    FileLoggerService.Log($"[ModToolsService] No '{processName}' processes found running by name.");
                    OverlayStatusChanged?.Invoke(false);
                    return (true, $"No '{processName}' processes found.");
                }
                wasRunning = true;
                FileLoggerService.Log($"[ModToolsService] Found {processes.Length} '{processName}' process(es) to kill by name.");
                foreach (var process in processes)
                {
                    try
                    {
                        FileLoggerService.Log($"[ModToolsService] Killing '{processName}' PID {process.Id}");
                        process.Kill(true);
                        process.WaitForExit(2000);
                        FileLoggerService.Log($"[ModToolsService] Successfully killed '{processName}' PID {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        FileLoggerService.Log($"[ModToolsService] Failed to kill '{processName}' process PID {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                if (wasRunning) OverlayStatusChanged?.Invoke(false);
                return (true, $"Attempted to kill all '{processName}' processes by name.");
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[ModToolsService] Error killing '{processName}' by name: {ex.Message}");
                if (wasRunning) OverlayStatusChanged?.Invoke(false);
                return (false, $"Error killing '{processName}' by name: {ex.Message}");
            }
        }

        public void ClearInstalledSkinsDirectory()
        {
            if (Directory.Exists(_installedSkinsDir))
            {
                FileLoggerService.Log($"[ModToolsService] Clearing installed skins directory: {_installedSkinsDir}");
                var files = Directory.GetFiles(_installedSkinsDir, "*.fantome");
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        FileLoggerService.Log($"[ModToolsService] Deleted: {file}");
                    }
                    catch (Exception ex)
                    {
                        FileLoggerService.Log($"[ModToolsService] Error deleting file {file}: {ex.Message}");
                    }
                }
            }
        }
        public string GetInstalledSkinsDirectory() => _installedSkinsDir;
    }
}
