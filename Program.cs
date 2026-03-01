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

// JWT Auth
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

// ─── Run startup migrations ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""BoardColumns"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""Name"" text NOT NULL,
                ""SortOrder"" integer NOT NULL DEFAULT 0,
                ""Color"" text NOT NULL DEFAULT '#334155',
                ""IsDefault"" boolean NOT NULL DEFAULT false,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS ""BoardCards"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""BoardColumnId"" uuid NOT NULL REFERENCES ""BoardColumns""(""Id"") ON DELETE CASCADE,
                ""WorkOrderId"" uuid NULL REFERENCES ""WorkOrders""(""Id"") ON DELETE SET NULL,
                ""JobNumber"" text NOT NULL,
                ""CustomerName"" text,
                ""SortOrder"" integer NOT NULL DEFAULT 0,
                ""AddedAt"" timestamp with time zone NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS ""BoardCardNotes"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""BoardCardId"" uuid NOT NULL REFERENCES ""BoardCards""(""Id"") ON DELETE CASCADE,
                ""Text"" text NOT NULL,
                ""Author"" text,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS ""IX_BoardCards_BoardColumnId"" ON ""BoardCards""(""BoardColumnId"");
            CREATE INDEX IF NOT EXISTS ""IX_BoardCardNotes_BoardCardId"" ON ""BoardCardNotes""(""BoardCardId"");

            CREATE TABLE IF NOT EXISTS ""WarrantyClaims"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""PartName"" text NOT NULL,
                ""PartModelNumber"" text,
                ""PartSerialNumber"" text,
                ""UnitModelNumber"" text,
                ""UnitSerialNumber"" text,
                ""CustomerId"" uuid NULL REFERENCES ""Customers""(""Id"") ON DELETE SET NULL,
                ""CustomerName"" text,
                ""JobNumber"" text,
                ""ReturnJobNumber"" text,
                ""Supplier"" text,
                ""Manufacturer"" text,
                ""RmaNumber"" text,
                ""Status"" text NOT NULL DEFAULT 'Diagnosis',
                ""ClaimType"" text NOT NULL DEFAULT 'Replacement',
                ""CreditAmount"" numeric,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                ""ClaimFiledDate"" timestamp with time zone,
                ""ApprovedDate"" timestamp with time zone,
                ""ExpectedShipDate"" timestamp with time zone,
                ""PartReceivedDate"" timestamp with time zone,
                ""InstalledDate"" timestamp with time zone,
                ""DefectiveReturnedDate"" timestamp with time zone,
                ""ClosedDate"" timestamp with time zone,
                ""DefectivePartReturned"" boolean NOT NULL DEFAULT false,
                ""IsClosed"" boolean NOT NULL DEFAULT false,
                ""IsDemo"" boolean NOT NULL DEFAULT false
            );

            CREATE TABLE IF NOT EXISTS ""WarrantyClaimNotes"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""WarrantyClaimId"" uuid NOT NULL REFERENCES ""WarrantyClaims""(""Id"") ON DELETE CASCADE,
                ""Text"" text NOT NULL,
                ""Author"" text,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS ""IX_WarrantyClaimNotes_WarrantyClaimId"" ON ""WarrantyClaimNotes""(""WarrantyClaimId"");

            ALTER TABLE ""WarrantyClaims"" ADD COLUMN IF NOT EXISTS ""IsDemo"" boolean NOT NULL DEFAULT false;

            CREATE TABLE IF NOT EXISTS ""TodoItems"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""Title"" text NOT NULL,
                ""Description"" text,
                ""IsCompleted"" boolean NOT NULL DEFAULT false,
                ""IsDemo"" boolean NOT NULL DEFAULT false,
                ""SortOrder"" integer NOT NULL DEFAULT 0,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                ""CompletedAt"" timestamp with time zone
            );
        ");
        Console.WriteLine("[Startup] Board tables ready.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Migration warning: {ex.Message}");
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