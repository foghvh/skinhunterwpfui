/// skinhunter Start of Services\SupabaseService.cs ///
using Supabase;
using Supabase.Storage; // Para IStorageFileApi y FileOptions
using Postgrest.Interfaces; // Para IPostgrestClient
using System.Collections.Generic; // Para Dictionary
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

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
            }
            else
            {
                FileLoggerService.Log("[SupabaseService.skinhunter] Warning: Attempting to get auth headers but user is not authenticated or token is missing.");
            }
            return headers;
        }

        public IPostgrestClient GetPostgrestClient()
        {
            // Crea un nuevo cliente Postgrest cada vez o reutiliza uno si las cabeceras no cambian frecuentemente.
            // Para simplicidad y asegurar el token más reciente, creamos uno nuevo.
            var options = new Postgrest.ClientOptions
            {
                Headers = GetAuthHeaders(),
                Schema = "public" // Asegúrate que el schema sea el correcto
            };
            // La URL para Postgrest es tu Supabase URL + "/rest/v1"
            return new Postgrest.Client($"{_supabaseUrl}/rest/v1", options);
        }

        // Supabase.Storage.Client podría no tener una forma fácil de instanciar con solo URL y Headers
        // en la v0.16.2 del SDK principal. La descarga de Storage podría necesitar HttpClient directamente.
        // Sin embargo, el IStorageFileApi que devuelve _supabaseClient.Storage.From() sí usa las cabeceras
        // del cliente Postgrest asociado al Supabase.Client si se configuró correctamente.

        // Alternativa: Crear un cliente Supabase completo y actualizar sus cabeceras
        // Esto es lo que intentamos antes y falló porque SetSession no funcionaba como esperábamos.
        // La nueva estrategia es que SupabaseService en skinhunter NO use un Supabase.Client singleton
        // para operaciones de datos, sino que construya clientes Postgrest/Storage con el token actual.

        // Si la descarga de Storage sigue dando problemas de autenticación,
        // consideraremos usar HttpClient directamente con el token en UserPreferencesService y SkinDetailViewModel.
        // Por ahora, vamos a asumir que el cliente Supabase global inyectado (que ahora configuraremos
        // correctamente en ApplicationHostService) es usado por Storage.

        public Supabase.Storage.Interfaces.IStorageFileApi<FileObject> GetStorageFileApi(string bucketId)
        {
            // Esto requiere que el Supabase.Client global (inyectado en otros servicios)
            // tenga su sesión/token correctamente establecido.
            var supabaseClientGlobal = App.Services.GetRequiredService<Supabase.Client>();
            return supabaseClientGlobal.Storage.From(bucketId);
        }
    }
}
/// skinhunter End of Services\SupabaseService.cs ///