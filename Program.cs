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

// Fix database state on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    try
    {
        // Do the ENTIRE migration manually via raw SQL so we control the order.
        // This handles the case where the EF migration keeps failing due to existing users.
        
        // Create CompanySettings table if it doesn't exist
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""CompanySettings"" (
                ""Id"" uuid NOT NULL,
                ""CompanyName"" text NOT NULL,
                ""ServiceTitanTenantId"" text,
                ""ServiceTitanClientId"" text,
                ""ServiceTitanClientSecret"" text,
                ""ServiceTitanAppKey"" text,
                ""AutoSyncEnabled"" boolean NOT NULL DEFAULT true,
                ""SyncIntervalMinutes"" integer NOT NULL DEFAULT 60,
                ""LastSyncAt"" timestamp with time zone,
                ""LastSyncStatus"" text,
                ""CreditCardFeePercent"" numeric NOT NULL DEFAULT 2.5,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT ""PK_CompanySettings"" PRIMARY KEY (""Id"")
            );
        ");

        // Add columns to Users if they don't exist
        db.Database.ExecuteSqlRaw(@"
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Users' AND column_name='CompanySettingsId') THEN
                    ALTER TABLE ""Users"" ADD ""CompanySettingsId"" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Users' AND column_name='FullName') THEN
                    ALTER TABLE ""Users"" ADD ""FullName"" text NOT NULL DEFAULT '';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Users' AND column_name='LastLoginAt') THEN
                    ALTER TABLE ""Users"" ADD ""LastLoginAt"" timestamp with time zone;
                END IF;
            END $$;
        ");

        // Seed CompanySettings if empty
        db.Database.ExecuteSqlRaw(@"
            INSERT INTO ""CompanySettings"" 
            (""Id"", ""CompanyName"", ""ServiceTitanTenantId"", ""ServiceTitanClientId"", 
             ""ServiceTitanClientSecret"", ""ServiceTitanAppKey"", ""AutoSyncEnabled"", 
             ""SyncIntervalMinutes"", ""CreditCardFeePercent"", ""CreatedAt"", ""UpdatedAt"")
            SELECT '11111111-1111-1111-1111-111111111111', 'Patriot Mechanical', '4146821403', 
             'cid.li8qk0ipffsz3386crc674xzz',
             'cs3.95u51txzvi0jdmb956jgicf63fcmi9tc2g958ucmj0zgfpq9rf',
             'ak1.9agbq4a1t6198nhsaveokkn28',
             true, 60, 2.5, NOW(), NOW()
            WHERE NOT EXISTS (SELECT 1 FROM ""CompanySettings"");
        ");

        // Link orphaned users BEFORE adding FK
        db.Database.ExecuteSqlRaw(@"
            UPDATE ""Users"" SET ""CompanySettingsId"" = '11111111-1111-1111-1111-111111111111' 
            WHERE ""CompanySettingsId"" = '00000000-0000-0000-0000-000000000000';
        ");

        // Add FK and index if missing
        db.Database.ExecuteSqlRaw(@"
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Users_CompanySettings_CompanySettingsId') THEN
                    ALTER TABLE ""Users"" ADD CONSTRAINT ""FK_Users_CompanySettings_CompanySettingsId"" 
                    FOREIGN KEY (""CompanySettingsId"") REFERENCES ""CompanySettings"" (""Id"") ON DELETE CASCADE;
                END IF;
            END $$;
        ");

        db.Database.ExecuteSqlRaw(@"
            CREATE INDEX IF NOT EXISTS ""IX_Users_CompanySettingsId"" ON ""Users"" (""CompanySettingsId"");
        ");

        // Fix Equipment FK if needed
        db.Database.ExecuteSqlRaw(@"
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Equipment_WorkOrders_WorkOrderId' AND constraint_type = 'FOREIGN KEY') THEN
                    ALTER TABLE ""Equipment"" ADD CONSTRAINT ""FK_Equipment_WorkOrders_WorkOrderId"" 
                    FOREIGN KEY (""WorkOrderId"") REFERENCES ""WorkOrders"" (""Id"");
                END IF;
            END $$;
        ");

        // Mark migration as applied
        db.Database.ExecuteSqlRaw(@"
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            SELECT '20260301024809_AddCompanySettingsAndUserUpgrade', '10.0.0'
            WHERE NOT EXISTS (
                SELECT 1 FROM ""__EFMigrationsHistory"" 
                WHERE ""MigrationId"" = '20260301024809_AddCompanySettingsAndUserUpgrade'
            );
        ");

        Console.WriteLine("Database migration complete.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Startup DB error: {ex.Message}");
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