
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Linq; // Para .Any()
using System.Collections.Generic; // Para List<Claim>
using System; // Para DateTime

namespace skinhunter.Services
{
    public partial class AuthTokenManager : ObservableObject
    {
        [ObservableProperty]
        private string? _currentToken;

        [ObservableProperty]
        private ClaimsPrincipal? _currentUserPrincipal;

        [ObservableProperty]
        private bool _isAuthenticated;

        public bool SetToken(string? token)
        {
            FileLoggerService.Log($"[AuthTokenManager][skinhunter] SetToken called with token (isNullOrEmpty: {string.IsNullOrEmpty(token)})");
            if (string.IsNullOrEmpty(token))
            {
                ClearTokenInternal();
                FileLoggerService.Log($"[AuthTokenManager][skinhunter] Token was null or empty.");
                return false;
            }

            var principal = DecodeSupabaseToken(token);
            if (principal?.Identity?.IsAuthenticated == true && principal.Claims.Any())
            {
                CurrentToken = token;
                CurrentUserPrincipal = principal;
                IsAuthenticated = true;
                FileLoggerService.Log($"[AuthTokenManager][skinhunter] Supabase token decoded and processed. IsAuthenticated: {IsAuthenticated}. User (sub claim): {GetClaim(ClaimTypes.NameIdentifier)}, Email: {GetClaim(ClaimTypes.Email)}");
                return true;
            }
            else
            {
                ClearTokenInternal();
                FileLoggerService.Log($"[AuthTokenManager][skinhunter] Supabase token decoding failed, no claims, or not marked authenticated by DecodeSupabaseToken.");
                return false;
            }
        }

        private void ClearTokenInternal()
        {
            CurrentToken = null;
            CurrentUserPrincipal = null;
            IsAuthenticated = false;
        }

        public void ClearToken()
        {
            ClearTokenInternal();
            FileLoggerService.Log($"[AuthTokenManager][skinhunter] Token cleared.");
        }

        private ClaimsPrincipal? DecodeSupabaseToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                FileLoggerService.Log($"[AuthTokenManager][skinhunter] DecodeSupabaseToken: Input token is null or whitespace.");
                return null;
            }
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                if (!tokenHandler.CanReadToken(token))
                {
                    FileLoggerService.Log($"[AuthTokenManager][skinhunter] Token is not a valid JWT format.");
                    return null;
                }

                var jwtToken = tokenHandler.ReadJwtToken(token);
                FileLoggerService.Log($"[AuthTokenManager] Decoded JWT. All Claims ({jwtToken.Claims.Count()}):");
                foreach (var claim in jwtToken.Claims)
                {
                    FileLoggerService.Log($"[AuthTokenManager]   -> Type: '{claim.Type}', Value: '{claim.Value}'");
                }

                var utcNow = DateTime.UtcNow;
                if (jwtToken.ValidTo < utcNow)
                {
                    FileLoggerService.Log($"[AuthTokenManager][skinhunter] Supabase token is expired. ValidTo: {jwtToken.ValidTo}, UtcNow: {utcNow}");
                    return null;
                }

                // Crear ClaimsIdentity y ClaimsPrincipal.
                // Marcamos como autenticado porque confiamos en que shlauncher lo validó (aunque no validamos firma aquí).
                var claimsForIdentity = new List<Claim>(jwtToken.Claims);

                // Asegurar que las claims estándar importantes estén si existen en el token original.
                var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub");
                if (subClaim != null && !claimsForIdentity.Any(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    claimsForIdentity.Add(new Claim(ClaimTypes.NameIdentifier, subClaim.Value));
                }

                var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "email");
                if (emailClaim != null && !claimsForIdentity.Any(c => c.Type == ClaimTypes.Email))
                {
                    claimsForIdentity.Add(new Claim(ClaimTypes.Email, emailClaim.Value));
                }

                // Si el Auth Hook en Supabase añade "login" y "is_buyer", estas ya estarán en jwtToken.Claims
                // y por ende en claimsForIdentity.

                // El "name" de la identidad (tercer parámetro) es el que se usa para Identity.Name
                // El "role" de la identidad (cuarto parámetro) es para Identity.RoleClaimType
                var claimsIdentity = new ClaimsIdentity(claimsForIdentity, "SupabaseJWT_Decoded", ClaimTypes.NameIdentifier, ClaimTypes.Role);

                FileLoggerService.Log($"[AuthTokenManager][skinhunter] Supabase token successfully decoded and ClaimsIdentity created. Claims count in identity: {claimsIdentity.Claims.Count()}");
                return new ClaimsPrincipal(claimsIdentity);
            }
            catch (Exception ex)
            {
                FileLoggerService.Log($"[AuthTokenManager][skinhunter] Supabase token decoding (read) failed: {ex.Message}");
                return null;
            }
        }

        public string? GetClaim(string claimType)
        {
            return CurrentUserPrincipal?.FindAll(claimType).FirstOrDefault()?.Value; // Usar FindAll y FirstOrDefault para ser más robusto
        }
    }
}
