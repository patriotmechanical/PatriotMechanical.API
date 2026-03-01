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