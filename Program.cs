using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PatriotMechanical.API.Infrastructure.Data;
using PatriotMechanical.API.Application.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "https://patriotmechanical.app"
                )
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// JWT Auth (replaces cookie auth)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Services
builder.Services.AddScoped<ServiceTitanService>();
builder.Services.AddScoped<JobCostCalculator>();
builder.Services.AddScoped<PricingEngine>();
builder.Services.AddScoped<JobCostingService>();
builder.Services.AddScoped<ServiceTitanSyncEngine>();
builder.Services.AddHostedService<ServiceTitanBackgroundService>();

var app = builder.Build();

// Show detailed errors so we can diagnose 500s
app.UseDeveloperExceptionPage();

// ─── Run startup migrations ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE ""BoardColumns"" ADD COLUMN IF NOT EXISTS ""ColumnRole"" text NULL;
            ALTER TABLE ""BoardColumns"" ADD COLUMN IF NOT EXISTS ""ServiceTitanHoldReasonId"" bigint NULL;
        ");
        Console.WriteLine("[Startup] Schema migrations applied.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Migration warning: {ex.Message}");
    }

    // ─── One-time: delete hardcoded default columns so board starts fresh ───
    // Only runs if the old defaults (IsDefault=true) still exist with no ST hold reason ID
    try
    {
        var defaultCols = await db.BoardColumns
            .Where(c => c.IsDefault && c.ServiceTitanHoldReasonId == null)
            .ToListAsync();

        if (defaultCols.Any())
        {
            // Move any cards off these columns before deleting
            var defaultColIds = defaultCols.Select(c => c.Id).ToList();
            var orphanCards = await db.Set<PatriotMechanical.API.Domain.Entities.BoardCard>()
                .Where(c => defaultColIds.Contains(c.BoardColumnId))
                .ToListAsync();
            db.RemoveRange(orphanCards);
            db.RemoveRange(defaultCols);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Startup] Removed {defaultCols.Count} hardcoded default columns and {orphanCards.Count} orphan cards.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Default column cleanup warning: {ex.Message}");
    }
}

// IMPORTANT ORDER
app.UseCors("AllowFrontend");

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();