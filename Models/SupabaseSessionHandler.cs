using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using skinhunter.Services;

namespace skinhunter.Models
{
    public class SupabaseSessionHandler : IGotrueSessionPersistence<Session>
    {
        private readonly string _sessionFilePath;

        public SupabaseSessionHandler()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "SkinHunter");
            Directory.CreateDirectory(appFolder);
            _sessionFilePath = Path.Combine(appFolder, "session.dat");
        }

        public void DestroySession()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    File.Delete(_sessionFilePath);
                    FileLoggerService.Log("[SessionHandler] Session file destroyed.");
                }
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[SessionHandler] Failed to destroy session file: {ex.Message}");
            }
        }

        public void SaveSession(Session session)
        {
            try
            {
                var json = JsonSerializer.Serialize(session);
                var bytesToProtect = Encoding.UTF8.GetBytes(json);
                var encryptedJson = ProtectedData.Protect(bytesToProtect, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_sessionFilePath, encryptedJson);
                FileLoggerService.Log("[SessionHandler] Session saved and encrypted successfully.");
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[SessionHandler] Failed to save session: {ex.Message}");
            }
        }

        public Session? LoadSession()
        {
            try
            {
                if (!File.Exists(_sessionFilePath))
                {
                    FileLoggerService.Log("[SessionHandler] Session file not found.");
                    return null;
                }

                var encryptedJson = File.ReadAllBytes(_sessionFilePath);
                if (encryptedJson.Length == 0) return null;

                var unprotectedBytes = ProtectedData.Unprotect(encryptedJson, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(unprotectedBytes);

                FileLoggerService.Log("[SessionHandler] Session loaded and decrypted successfully.");
                return JsonSerializer.Deserialize<Session>(json);
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[SessionHandler] Failed to load session: {ex.Message}");
                DestroySession();
                return null;
            }
        }
    }
}