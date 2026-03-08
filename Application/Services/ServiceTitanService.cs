using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Application.Services
{
    public class ServiceTitanService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        private string? _accessToken;
        private DateTime _tokenExpiry;

        // Cached credentials so we don't hit DB on every call
        private string? _cachedClientId;
        private string? _cachedClientSecret;
        private string? _cachedTenantId;
        private string? _cachedAppKey;
        private DateTime _credsCachedAt;

        public ServiceTitanService(AppDbContext context, IConfiguration config)
        {
            _httpClient = new HttpClient();
            _context = context;
            _config = config;
        }

        // ─── LOAD CREDS FROM DB (falls back to appsettings.json for migration) ───
        private async Task LoadCredentialsAsync()
        {
            // Re-cache every 5 minutes
            if (_cachedClientId != null && DateTime.UtcNow - _credsCachedAt < TimeSpan.FromMinutes(5))
                return;

            var company = await _context.CompanySettings.FirstOrDefaultAsync();

            if (company != null && company.IsServiceTitanConfigured)
            {
                _cachedClientId = company.ServiceTitanClientId;
                _cachedClientSecret = company.ServiceTitanClientSecret;
                _cachedTenantId = company.ServiceTitanTenantId;
                _cachedAppKey = company.ServiceTitanAppKey;
            }
            else
            {
                // Fallback to appsettings.json (for backward compatibility during migration)
                _cachedClientId = _config["ServiceTitan:ClientId"];
                _cachedClientSecret = _config["ServiceTitan:ClientSecret"];
                _cachedTenantId = _config["ServiceTitan:TenantId"];
                _cachedAppKey = _config["ServiceTitan:ApplicationKey"];
            }

            _credsCachedAt = DateTime.UtcNow;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
                return _accessToken;

            await LoadCredentialsAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "https://auth.servicetitan.io/connect/token");

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", _cachedClientId! },
                { "client_secret", _cachedClientSecret! }
            });

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);

            _accessToken = tokenResponse.GetProperty("access_token").GetString();
            var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

            return _accessToken!;
        }

        private async Task<string> GetBaseUrl()
        {
            await LoadCredentialsAsync();
            return "https://api.servicetitan.io";
        }

        private async Task<string> GetTenantId()
        {
            await LoadCredentialsAsync();
            return _cachedTenantId!;
        }

        private async Task<string> GetAppKey()
        {
            await LoadCredentialsAsync();
            return _cachedAppKey!;
        }

        public async Task<string> GetJobsRawAsync(DateTime lastSyncUtc)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/jpm/v2/tenant/{tenantId}/jobs?page=1&pageSize=50&includeTotal=true&modifiedOnOrAfter={lastSyncUtc:O}"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> ExportCustomersAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/crm/v2/tenant/{tenantId}/export/customers";

            if (!string.IsNullOrWhiteSpace(continuationToken))
                url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> ExportJobsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/jpm/v2/tenant/{tenantId}/export/jobs";

            if (!string.IsNullOrWhiteSpace(continuationToken))
                url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> ExportInvoicesAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/accounting/v2/tenant/{tenantId}/export/invoices";

            if (!string.IsNullOrWhiteSpace(continuationToken))
                url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

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

        // ═══════════════════════════════════════════════════════════════
        // RECENT JOBS (List API with modifiedOnOrAfter for status refresh)
        // ═══════════════════════════════════════════════════════════════

        public async Task<string> GetRecentJobsPageAsync(DateTime modifiedSince, int page = 1, int pageSize = 200)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/jpm/v2/tenant/{tenantId}/jobs?page={page}&pageSize={pageSize}&modifiedOnOrAfter={modifiedSince:O}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // RAW JOB BY ID
        // ═══════════════════════════════════════════════════════════════

        public async Task<string> GetRawJobByIdAsync(long jobId)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/jpm/v2/tenant/{tenantId}/jobs/{jobId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // JOB TYPE MAP
        // ═══════════════════════════════════════════════════════════════

        public async Task<Dictionary<long, string>> GetJobTypeMapAsync()
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var map = new Dictionary<long, string>();
            int page = 1;

            while (true)
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{baseUrl}/jpm/v2/tenant/{tenantId}/job-types?page={page}&pageSize=200");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("ST-App-Key", appKey);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var raw = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);
                var items = parsed.GetProperty("data");

                if (items.GetArrayLength() == 0) break;

                foreach (var item in items.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt64();
                    var name = item.GetProperty("name").GetString() ?? "Unknown";
                    map[id] = name;
                }

                page++;
            }

            return map;
        }

        // ═══════════════════════════════════════════════════════════════
        // CRM EXPORTS (Contacts & Locations)
        // ═══════════════════════════════════════════════════════════════

        private async Task<string> ExportGenericAsync(string path, string token)
        {
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/{path.Replace("{tenantId}", tenantId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> ExportCustomerContactsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var tenantId = await GetTenantId();
            var url = $"crm/v2/tenant/{tenantId}/export/customers/contacts";
            if (!string.IsNullOrWhiteSpace(continuationToken)) url += $"?from={continuationToken}";
            return await ExportGenericAsync(url, token);
        }

        public async Task<string> ExportLocationsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var tenantId = await GetTenantId();
            var url = $"crm/v2/tenant/{tenantId}/export/locations";
            if (!string.IsNullOrWhiteSpace(continuationToken)) url += $"?from={continuationToken}";
            return await ExportGenericAsync(url, token);
        }

        public async Task<string> ExportLocationContactsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var tenantId = await GetTenantId();
            var url = $"crm/v2/tenant/{tenantId}/export/locations/contacts";
            if (!string.IsNullOrWhiteSpace(continuationToken)) url += $"?from={continuationToken}";
            return await ExportGenericAsync(url, token);
        }

        // ═══════════════════════════════════════════════════════════════
        // APPOINTMENTS EXPORT
        // ═══════════════════════════════════════════════════════════════

        public async Task<string> ExportAppointmentsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/jpm/v2/tenant/{tenantId}/export/appointments";
            if (!string.IsNullOrWhiteSpace(continuationToken)) url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // APPOINTMENTS LIST API (per-job lookup for technician check)
        // ═══════════════════════════════════════════════════════════════

        public async Task<string> GetAppointmentsForJobAsync(long jobId)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/jpm/v2/tenant/{tenantId}/appointments?jobId={jobId}&active=True&pageSize=50";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // APPOINTMENT ASSIGNMENTS (Dispatch API - tech assignment check)
        // ═══════════════════════════════════════════════════════════════

        public async Task<string> GetAppointmentAssignmentsForJobAsync(long jobId)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/dispatch/v2/tenant/{tenantId}/appointment-assignments?jobId={jobId}&active=True&pageSize=50";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> ExportAppointmentAssignmentsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/dispatch/v2/tenant/{tenantId}/export/appointment-assignments";
            if (!string.IsNullOrWhiteSpace(continuationToken)) url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetAppointmentsAsync(DateTime startsOnOrAfter, DateTime startsOnOrBefore)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/jpm/v2/tenant/{tenantId}/appointments?pageSize=500&startsOnOrAfter={startsOnOrAfter:O}&startsOnOrBefore={startsOnOrBefore:O}"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}