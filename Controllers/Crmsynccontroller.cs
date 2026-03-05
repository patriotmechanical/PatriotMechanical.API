using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Application.Services;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;
using System.Text.Json;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("crm")]
    public class CrmSyncController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ServiceTitanService _service;

        public CrmSyncController(AppDbContext context, ServiceTitanService service)
        {
            _context = context;
            _service = service;
        }

        // POST /crm/sync — syncs customers, contacts, locations, location contacts
        [HttpPost("sync")]
        public async Task<IActionResult> SyncAll()
        {
            try
            {
                var results = new Dictionary<string, int>();

                results["customerContacts"] = await SyncCustomerContacts();
                results["locations"] = await SyncLocations();
                results["locationContacts"] = await SyncLocationContacts();

                return Ok(new { message = "CRM sync complete", results });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRM Sync Error] {ex.Message}");
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        private async Task<int> SyncCustomerContacts()
        {
            int count = 0;
            string? token = null;
            bool hasMore;

            // Build lookup: ST customer ID -> our customer Guid
            var customerMap = await _context.Customers
                .Where(c => c.ServiceTitanCustomerId > 0)
                .ToDictionaryAsync(c => c.ServiceTitanCustomerId, c => c.Id);

            do
            {
                var raw = await _service.ExportCustomerContactsAsync(token);
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);
                hasMore = parsed.GetProperty("hasMore").GetBoolean();
                token = parsed.GetProperty("continueFrom").GetString();

                foreach (var item in parsed.GetProperty("data").EnumerateArray())
                {
                    if (!item.TryGetProperty("active", out var activeProp) || !activeProp.GetBoolean())
                        continue;

                    var stContactId = item.GetProperty("id").GetInt64();
                    var stCustomerId = item.GetProperty("customerId").GetInt64();
                    var type = item.TryGetProperty("type", out var tp) ? tp.GetString() ?? "" : "";
                    var value = item.TryGetProperty("value", out var vp) ? vp.GetString() ?? "" : "";
                    var memo = item.TryGetProperty("memo", out var mp) && mp.ValueKind == JsonValueKind.String
                        ? mp.GetString() : null;

                    if (string.IsNullOrWhiteSpace(value)) continue;
                    if (!customerMap.TryGetValue(stCustomerId, out var customerId)) continue;

                    var existing = await _context.CustomerContacts
                        .FirstOrDefaultAsync(c => c.ServiceTitanContactId == stContactId);

                    if (existing != null)
                    {
                        existing.Type = type;
                        existing.Value = value;
                        existing.Memo = memo;
                    }
                    else
                    {
                        _context.CustomerContacts.Add(new CustomerContact
                        {
                            Id = Guid.NewGuid(),
                            CustomerId = customerId,
                            ServiceTitanContactId = stContactId,
                            Type = type,
                            Value = value,
                            Memo = memo,
                            Active = true
                        });
                        count++;
                    }
                }

                await _context.SaveChangesAsync();

            } while (hasMore);

            return count;
        }

        private async Task<int> SyncLocations()
        {
            int count = 0;
            string? token = null;
            bool hasMore;

            var customerMap = await _context.Customers
                .Where(c => c.ServiceTitanCustomerId > 0)
                .ToDictionaryAsync(c => c.ServiceTitanCustomerId, c => c.Id);

            do
            {
                var raw = await _service.ExportLocationsAsync(token);
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);
                hasMore = parsed.GetProperty("hasMore").GetBoolean();
                token = parsed.GetProperty("continueFrom").GetString();

                foreach (var item in parsed.GetProperty("data").EnumerateArray())
                {
                    if (!item.TryGetProperty("active", out var activeProp) || !activeProp.GetBoolean())
                        continue;

                    var stLocationId = item.GetProperty("id").GetInt64();
                    var stCustomerId = item.GetProperty("customerId").GetInt64();
                    if (!customerMap.TryGetValue(stCustomerId, out var customerId)) continue;

                    var name = item.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                    var addr = item.TryGetProperty("address", out var ap) ? ap : default;

                    string? street = null, unit = null, city = null, state = null, zip = null;
                    if (addr.ValueKind == JsonValueKind.Object)
                    {
                        street = addr.TryGetProperty("street", out var s) ? s.GetString() : null;
                        unit   = addr.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                        city   = addr.TryGetProperty("city", out var c) ? c.GetString() : null;
                        state  = addr.TryGetProperty("state", out var st) ? st.GetString() : null;
                        zip    = addr.TryGetProperty("zip", out var z) ? z.GetString() : null;
                    }

                    var existing = await _context.CustomerLocations
                        .FirstOrDefaultAsync(l => l.ServiceTitanLocationId == stLocationId);

                    if (existing != null)
                    {
                        existing.Name = name;
                        existing.Street = street;
                        existing.Unit = unit;
                        existing.City = city;
                        existing.State = state;
                        existing.Zip = zip;
                    }
                    else
                    {
                        _context.CustomerLocations.Add(new CustomerLocation
                        {
                            Id = Guid.NewGuid(),
                            CustomerId = customerId,
                            ServiceTitanLocationId = stLocationId,
                            Name = name,
                            Street = street,
                            Unit = unit,
                            City = city,
                            State = state,
                            Zip = zip,
                            Active = true
                        });
                        count++;
                    }
                }

                await _context.SaveChangesAsync();

            } while (hasMore);

            return count;
        }

        private async Task<int> SyncLocationContacts()
        {
            int count = 0;
            string? token = null;
            bool hasMore;

            var locationMap = await _context.CustomerLocations
                .ToDictionaryAsync(l => l.ServiceTitanLocationId, l => l.Id);

            do
            {
                var raw = await _service.ExportLocationContactsAsync(token);
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);
                hasMore = parsed.GetProperty("hasMore").GetBoolean();
                token = parsed.GetProperty("continueFrom").GetString();

                foreach (var item in parsed.GetProperty("data").EnumerateArray())
                {
                    if (!item.TryGetProperty("active", out var activeProp) || !activeProp.GetBoolean())
                        continue;

                    var stContactId = item.GetProperty("id").GetInt64();
                    var stLocationId = item.GetProperty("locationId").GetInt64();
                    var type = item.TryGetProperty("type", out var tp) ? tp.GetString() ?? "" : "";
                    var value = item.TryGetProperty("value", out var vp) ? vp.GetString() ?? "" : "";
                    var memo = item.TryGetProperty("memo", out var mp) && mp.ValueKind == JsonValueKind.String
                        ? mp.GetString() : null;

                    if (string.IsNullOrWhiteSpace(value)) continue;
                    if (!locationMap.TryGetValue(stLocationId, out var locationId)) continue;

                    var existing = await _context.LocationContacts
                        .FirstOrDefaultAsync(c => c.ServiceTitanContactId == stContactId);

                    if (existing != null)
                    {
                        existing.Type = type;
                        existing.Value = value;
                        existing.Memo = memo;
                    }
                    else
                    {
                        _context.LocationContacts.Add(new LocationContact
                        {
                            Id = Guid.NewGuid(),
                            LocationId = locationId,
                            ServiceTitanContactId = stContactId,
                            Type = type,
                            Value = value,
                            Memo = memo,
                            Active = true
                        });
                        count++;
                    }
                }

                await _context.SaveChangesAsync();

            } while (hasMore);

            return count;
        }
    }
}