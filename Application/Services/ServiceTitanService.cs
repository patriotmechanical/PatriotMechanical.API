using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Application.Services
{
    public class ServiceTitanService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;
        private string? _cachedToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public ServiceTitanService(HttpClient httpClient, IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _serviceProvider = serviceProvider;
        }

        private AppDbContext GetDbContext()
        {
            var scope = _serviceProvider.CreateScope();
            return scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        private async Task<(string TenantId, string ClientId, string ClientSecret, string AppKey)> GetCredentialsAsync()
        {
            using var db = GetDbContext();
            var settings = await db.CompanySettings.FirstOrDefaultAsync();
            if (settings == null)
                throw new InvalidOperationException("No company settings configured.");

            return (
                settings.ServiceTitanTenantId ?? "",
                settings.ServiceTitanClientId ?? "",
                settings.ServiceTitanClientSecret ?? "",
                settings.ServiceTitanAppKey ?? ""
            );
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
                return _cachedToken;

            var creds = await GetCredentialsAsync();
            var body = new { grant_type = "client_credentials", client_id = creds.ClientId, client_secret = creds.ClientSecret };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://auth.servicetitan.io/connect/token", content);
            response.EnsureSuccessStatusCode();

            var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
            _cachedToken = json.GetProperty("access_token").GetString()!;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32() - 60);

            return _cachedToken;
        }

        private async Task<string> GetBaseUrl()
        {
            return "https://api.servicetitan.io";
        }

        private async Task<string> GetTenantId()
        {
            var creds = await GetCredentialsAsync();
            return creds.TenantId;
        }

        private async Task<string> GetAppKey()
        {
            var creds = await GetCredentialsAsync();
            return creds.AppKey;
        }

        public async Task<Dictionary<long, string>> GetJobTypeMapAsync()
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl}/jpm/v2/tenant/{tenantId}/job-types?pageSize=250");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
            var map = new Dictionary<long, string>();

            foreach (var item in json.GetProperty("data").EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt64();
                var name = item.GetProperty("name").GetString() ?? "Unknown";
                map[id] = name;
            }

            return map;
        }

        public async Task<string> GetJobsRawAsync(DateTime lastSyncUtc)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var since = lastSyncUtc.ToString("o");
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl}/jpm/v2/tenant/{tenantId}/jobs?modifiedOnOrAfter={since}&pageSize=50");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // ─── Generic export helper ────────────────────────────────────

        private async Task<string> ExportGenericAsync(string path, string? continuationToken)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/{path.Replace("{tenantId}", tenantId)}";

            if (!string.IsNullOrWhiteSpace(continuationToken))
                url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public Task<string> ExportCustomersAsync(string? continuationToken = null)
            => ExportGenericAsync("crm/v2/tenant/{tenantId}/export/customers", continuationToken);

        public Task<string> ExportJobsAsync(string? continuationToken = null)
            => ExportGenericAsync("jpm/v2/tenant/{tenantId}/export/jobs", continuationToken);

        public Task<string> ExportInvoicesAsync(string? continuationToken = null)
            => ExportGenericAsync("accounting/v2/tenant/{tenantId}/export/invoices", continuationToken);

        public Task<string> ExportCustomerContactsAsync(string? continuationToken = null)
            => ExportGenericAsync("crm/v2/tenant/{tenantId}/export/customers/contacts", continuationToken);

        public Task<string> ExportLocationsAsync(string? continuationToken = null)
            => ExportGenericAsync("crm/v2/tenant/{tenantId}/export/locations", continuationToken);

        public Task<string> ExportLocationContactsAsync(string? continuationToken = null)
            => ExportGenericAsync("crm/v2/tenant/{tenantId}/export/locations/contacts", continuationToken);

        // ─── Paged invoices ───────────────────────────────────────────

        public async Task<string> GetInvoicesPageAsync(int page = 1)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/accounting/v2/tenant/{tenantId}/invoices?page={page}&pageSize=50&includeTotal=true"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}