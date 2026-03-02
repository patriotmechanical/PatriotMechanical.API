using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Application.Services
{
    public class ServiceTitanService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;

        private string? _accessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        // Cached credentials
        private string? _cachedTenantId;
        private string? _cachedClientId;
        private string? _cachedClientSecret;
        private string? _cachedAppKey;
        private DateTime _credentialsCachedAt = DateTime.MinValue;

        public ServiceTitanService(HttpClient httpClient, IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _serviceProvider = serviceProvider;
        }

        private async Task LoadCredentialsAsync()
        {
            if (_cachedTenantId != null && DateTime.UtcNow - _credentialsCachedAt < TimeSpan.FromMinutes(5))
                return;

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var company = await context.CompanySettings.FirstOrDefaultAsync();
            if (company == null)
                throw new InvalidOperationException("No company settings found.");

            _cachedTenantId = company.ServiceTitanTenantId
                ?? throw new InvalidOperationException("ServiceTitan Tenant ID not configured.");
            _cachedClientId = company.ServiceTitanClientId
                ?? throw new InvalidOperationException("ServiceTitan Client ID not configured.");
            _cachedClientSecret = company.ServiceTitanClientSecret
                ?? throw new InvalidOperationException("ServiceTitan Client Secret not configured.");
            _cachedAppKey = company.ServiceTitanAppKey
                ?? throw new InvalidOperationException("ServiceTitan App Key not configured.");

            _credentialsCachedAt = DateTime.UtcNow;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
                return _accessToken;

            await LoadCredentialsAsync();

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://auth.servicetitan.io/connect/token");

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

        // ═══════════════════════════════════════════════════════════════
        // JOBS - List endpoint (for manual refresh - uses modifiedOnOrAfter)
        // ═══════════════════════════════════════════════════════════════

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

        /// <summary>
        /// Fetches a page of recently modified jobs using the list endpoint.
        /// Unlike the export endpoint, this uses modifiedOnOrAfter filter
        /// so it immediately returns recent changes without continuation tokens.
        /// Used by the dashboard manual refresh button.
        /// </summary>
        public async Task<string> GetRecentJobsPageAsync(DateTime modifiedSince, int page = 1, int pageSize = 200)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/jpm/v2/tenant/{tenantId}/jobs?page={page}&pageSize={pageSize}&includeTotal=true&modifiedOnOrAfter={modifiedSince:O}"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // JOBS - Export endpoint (for background incremental sync)
        // ═══════════════════════════════════════════════════════════════

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

        public async Task<string> GetRawJobByIdAsync(long jobId)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/jpm/v2/tenant/{tenantId}/jobs/{jobId}"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<Dictionary<long, string>> GetJobTypeMapAsync()
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var map = new Dictionary<long, string>();
            int page = 1;
            bool hasMore;

            do
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{baseUrl}/jpm/v2/tenant/{tenantId}/job-types?page={page}&pageSize=200"
                );

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("ST-App-Key", appKey);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var raw = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

                foreach (var jt in parsed.GetProperty("data").EnumerateArray())
                {
                    var id = jt.GetProperty("id").GetInt64();
                    var name = jt.GetProperty("name").GetString() ?? "Unknown";
                    map[id] = name;
                }

                hasMore = parsed.GetProperty("hasMore").GetBoolean();
                page++;
            } while (hasMore);

            return map;
        }

        // ═══════════════════════════════════════════════════════════════
        // CUSTOMERS
        // ═══════════════════════════════════════════════════════════════

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

        public async Task<string> ExportCustomerContactsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/crm/v2/tenant/{tenantId}/export/customers/contacts";

            if (!string.IsNullOrWhiteSpace(continuationToken))
                url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> ExportLocationsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/crm/v2/tenant/{tenantId}/export/locations";

            if (!string.IsNullOrWhiteSpace(continuationToken))
                url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> ExportLocationContactsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/crm/v2/tenant/{tenantId}/export/locations/contacts";

            if (!string.IsNullOrWhiteSpace(continuationToken))
                url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // APPOINTMENTS
        // ═══════════════════════════════════════════════════════════════

        public async Task<string> ExportAppointmentsAsync(string? continuationToken = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrl();
            var tenantId = await GetTenantId();
            var appKey = await GetAppKey();

            var url = $"{baseUrl}/jpm/v2/tenant/{tenantId}/export/appointments";

            if (!string.IsNullOrWhiteSpace(continuationToken))
                url += $"?from={continuationToken}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ST-App-Key", appKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // INVOICES
        // ═══════════════════════════════════════════════════════════════

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
    }
}