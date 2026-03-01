# Admin Center & Auth Upgrade — Deployment Guide

## What Changed

### New Features
- **Login screen** — JWT-based authentication (replaces cookie auth)
- **Setup wizard** — First-time setup for new installs (company name, admin account, ServiceTitan creds)
- **Admin Center** — Settings page to manage company info, ServiceTitan credentials, sync settings, and password
- **ServiceTitan creds in DB** — No longer hardcoded in appsettings.json; stored in CompanySettings table
- **Toast notifications** — Visual feedback for all actions
- **Polished dark UI** — Consistent dark theme across all views

### Files to Replace/Add

**REPLACE these existing files:**
```
Models/User.cs                    → User.cs (added FullName, CompanySettingsId, LastLoginAt)
Infrastructure/Data/AppDbContext.cs → AppDbContext.cs (added CompanySettings DbSet + relationship)
Controllers/AuthController.cs     → AuthController.cs (JWT login, setup, change password)
Application/Services/ServiceTitanService.cs → ServiceTitanService.cs (reads creds from DB)
Program.cs                        → Program.cs (JWT auth replaces cookie auth)
appsettings.json                  → appsettings.json (added Jwt section)
wwwroot/index.html                → index.html (login, setup wizard, admin center, updated views)
wwwroot/styles.css                → styles.css (complete dark theme overhaul)
wwwroot/app.js                    → app.js (auth flow, admin center, api helper with JWT)
```

**ADD these new files:**
```
Models/CompanySettings.cs          → New entity for company config + ST credentials
Controllers/AdminController.cs     → Settings management API
Migrations/AddCompanySettingsAndUserUpgrade.cs → Database migration
```

## Step-by-Step Deployment

### 1. Install the JWT NuGet Package
```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package System.IdentityModel.Tokens.Jwt
```

### 2. Update appsettings.json

Add the `Jwt` section. **IMPORTANT: Change the Key to something unique for production!**
```json
{
  "Jwt": {
    "Key": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "PatriotMechanical",
    "Audience": "PatriotMechanicalApp"
  }
}
```

For Railway, add these as environment variables:
```
Jwt__Key=YourSuperSecretKeyThatIsAtLeast32CharactersLong!
Jwt__Issuer=PatriotMechanical
Jwt__Audience=PatriotMechanicalApp
```

### 3. Copy All Files Into Your Project
Replace and add files as listed above.

### 4. Run the Migration

**Option A: Using dotnet CLI** (recommended)
```bash
dotnet ef database update
```

**Option B: If using the raw migration file**
The migration will:
- Create the `CompanySettings` table
- Add `FullName`, `LastLoginAt`, `CompanySettingsId` to `Users`
- Seed a default CompanySettings row with your current ServiceTitan creds
- Link all existing users to that company

### 5. Deploy to Railway
```bash
git add .
git commit -m "Add admin center, JWT auth, setup wizard"
git push
```

### 6. First Login After Deploy

Since the migration seeds your existing ServiceTitan creds into the CompanySettings table,
everything will keep working. Your existing user account will work with the new login screen.

If you need to create a fresh admin account, you can either:
- Clear the CompanySettings table and the setup wizard will appear
- Or use the existing user credentials

## Architecture Notes

### Auth Flow
```
User visits site
  → GET /auth/status
    → No company? → Show Setup Wizard
    → Has company + has JWT in localStorage? → Validate with GET /auth/me → Enter app
    → Has company + no JWT? → Show Login
```

### ServiceTitan Credential Flow
```
Previously:  ServiceTitanService → reads appsettings.json
Now:         ServiceTitanService → reads CompanySettings table → falls back to appsettings.json
```

The fallback to appsettings.json means you won't break anything during the transition.
Once the migration runs and seeds your creds into the DB, the DB values take priority.

### JWT Token
- Stored in localStorage as "jwt"
- Sent on every API request via Authorization header
- Expires after 7 days
- Contains: userId, email, companyId

## Environment Variables for Railway

Make sure these are set in Railway:
```
ConnectionStrings__DefaultConnection=your_postgres_connection_string
Jwt__Key=CHANGE_THIS_TO_A_RANDOM_32_CHAR_STRING
Jwt__Issuer=PatriotMechanical
Jwt__Audience=PatriotMechanicalApp
```

Generate a secure JWT key:
```bash
openssl rand -base64 32
```

## What's Next

After this is deployed and working, the next steps toward monetization would be:
1. **Docker packaging** — Dockerfile for per-customer deployment
2. **License key system** — Simple validation on startup
3. **Cash flow forecasting** — The killer feature
4. **Profit leakage alerts** — Makes the app proactive
