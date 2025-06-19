using Supabase;
using Supabase.Storage;
using Postgrest.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace skinhunter.Services
{
    public class SupabaseService
    {
        private readonly AuthTokenManager _authTokenManager;
        private readonly string _supabaseUrl = "https://odlqwkgewzxxmbsqutja.supabase.co";
        private readonly string _supabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im9kbHF3a2dld3p4eG1ic3F1dGphIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzQyMTM2NzcsImV4cCI6MjA0OTc4OTY3N30.qka6a71bavDeUQgy_BKoVavaClRQa_gT36Au7oO9AF0";

        public SupabaseService(AuthTokenManager authTokenManager)
        {
            _authTokenManager = authTokenManager;
            FileLoggerService.Log("[SupabaseService] Initialized.");
        }

        private Dictionary<string, string> GetAuthHeaders()
        {
            var headers = new Dictionary<string, string>
            {
                { "apikey", _supabaseAnonKey }
            };
            if (_authTokenManager.IsAuthenticated && !string.IsNullOrEmpty(_authTokenManager.CurrentToken))
            {
                headers["Authorization"] = $"Bearer {_authTokenManager.CurrentToken}";
                FileLoggerService.Log("[SupabaseService] Added Authorization header.");
            }
            else
            {
                FileLoggerService.Log("[SupabaseService] Warning: Getting auth headers but user is not authenticated or token is missing.");
            }
            return headers;
        }

        public IPostgrestClient GetPostgrestClient()
        {
            FileLoggerService.Log("[SupabaseService] Creating Postgrest client.");
            var options = new Postgrest.ClientOptions
            {
                Headers = GetAuthHeaders(),
                Schema = "public"
            };
            return new Postgrest.Client($"{_supabaseUrl}/rest/v1", options);
        }

        public Supabase.Storage.Interfaces.IStorageFileApi<FileObject> GetStorageFileApi(string bucketId)
        {
            FileLoggerService.Log($"[SupabaseService] Getting StorageFileApi for bucket: {bucketId}");
            // Get the globally injected Supabase.Client
            var supabaseClientGlobal = App.Services.GetRequiredService<Supabase.Client>();
            // Ensure the global client has the latest token set if necessary.
            // The current App.xaml.cs DI setup injects the client once.
            // UserPreferencesService's HttpClient is configured per request with the latest token,
            // which is more robust for API calls.
            // For Storage downloads using the Supabase-csharp SDK's Storage client,
            // the authentication depends on how the Supabase.Client instance itself is managed
            // and if its session/token is updated.
            // SkinDetailViewModel now uses the injected Supabase.Client directly for download,
            // assuming its internal Storage client is configured correctly or relies on
            // the base client's initial configuration or later updates.
            // Let's log the current token status for the global client if possible (not easily exposed).
            // Trusting the SDK's internal handling for now.
            return supabaseClientGlobal.Storage.From(bucketId);
        }
    }
}