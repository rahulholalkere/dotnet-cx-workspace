using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;


public class LiferayJwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LiferayJwtMiddleware> _logger;
    private readonly HttpClient _httpClient = new();

    /*
        private readonly string Oauth2JWKSURI = "http://localhost:8080/o/oauth2/jwks";
        private readonly string LxcDXPMainDomain = "localhost:8080";
        private readonly string ExternalReferenceCode = "liferay-sample-etc-dot-net-oauth-application-user-agent";
        private readonly string LxcDXPServerProtocol = "http";
        private readonly string UriPath = "/o/oauth2/jwks";*/
    private readonly LiferaySettings _liferaySettings;

    public LiferayJwtMiddleware(RequestDelegate next, ILogger<LiferayJwtMiddleware> logger, IOptions<LiferaySettings> liferaySettings)
    {
        _next = next;
        _logger = logger;
        _liferaySettings = liferaySettings.Value;

    }

    public async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine("JWT middleware hit");

        if (context.Request.Path.StartsWithSegments("/ready"))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = 401;
            _logger.LogError("No authorization header");
            await context.Response.WriteAsync("No authorization header");
            return;
        }

        var bearerToken = authHeader["Bearer ".Length..].Trim();

        try
        {
/*
            _logger.LogInformation("Try entered...");
            Console.WriteLine($"LxcDXPServerProtocol: {_liferaySettings?.LxcDXPServerProtocol}");
            Console.WriteLine($"LxcDXPMainDomain: {_liferaySettings?.LxcDXPMainDomain}");
            Console.WriteLine($"OAuth: {_liferaySettings?.OAuth}");
            Console.WriteLine($"Oauth2JWKSURI: {_liferaySettings?.OAuth?.Oauth2JWKSURI}");
*/
            var jwksResponse = await _httpClient.GetAsync(
                $"{_liferaySettings.LxcDXPServerProtocol}://{_liferaySettings.LxcDXPMainDomain}{_liferaySettings.OAuth.Oauth2JWKSURI}"
            );

            if (!jwksResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Error fetching JWKS: {Status} {Text}", jwksResponse.StatusCode, jwksResponse.ReasonPhrase);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid authorization header");
                return;
            }

            var jwksJson = await jwksResponse.Content.ReadAsStringAsync();
            var jwks = JsonDocument.Parse(jwksJson).RootElement.GetProperty("keys")[0];

            var rsa = CreateRsaSecurityKey(jwks);
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = rsa,
                ValidateIssuer = false,
                ValidateAudience = false,
                RequireExpirationTime = false,
                ValidateLifetime = false, // TODO: Use refresh token
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            };

            var principal = tokenHandler.ValidateToken(bearerToken, validationParams, out var validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;

            var clientIdFromToken = jwtToken.Payload["client_id"]?.ToString();

            var appResponse = await _httpClient.GetAsync($"{_liferaySettings.LxcDXPServerProtocol}://{_liferaySettings.LxcDXPMainDomain}/o/oauth2/application?externalReferenceCode={_liferaySettings.OAuth.ApplicationExternalReferenceCodes}");
            var appJson = JsonDocument.Parse(await appResponse.Content.ReadAsStringAsync());
            var clientIdExpected = appJson.RootElement.GetProperty("client_id").GetString();


            if (clientIdFromToken == clientIdExpected)
            {
                context.Items["jwt"] = jwtToken;
                await _next(context);
            }
            else
            {
                _logger.LogWarning("JWT token client_id does not match expected value");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid authorization");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating JWT token");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid authorization header");
        }
    }

    private static RsaSecurityKey CreateRsaSecurityKey(JsonElement key)
    {
        var modulus = key.GetProperty("n").GetString();
        var exponent = key.GetProperty("e").GetString();

        var rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Base64UrlDecode(modulus),
            Exponent = Base64UrlDecode(exponent)
        });

        return new RsaSecurityKey(rsa);
    }

    public static byte[] Base64UrlDecode(string base64Url)
    {
        // S'assurer que la chaîne est bien une string (au cas où elle vient d'une autre source comme config ou JSON)
        string base64UrlString = base64Url.ToString();

        // Ajouter le padding nécessaire si absent
        int padding = 4 - (base64UrlString.Length % 4);
        if (padding != 4)
        {
            base64UrlString += new string('=', padding);
        }

        // Convertir les caractères URL-safe en base64 standard
        string base64 = base64UrlString.Replace('-', '+').Replace('_', '/');

        // Décoder en tableau de bytes
        return Convert.FromBase64String(base64);
    }

}
