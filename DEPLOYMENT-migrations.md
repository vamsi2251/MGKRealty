# Database migrations & data persistence

The API uses **EF Core migrations** applied automatically at startup
(`LayoutManagementAPI/Program.cs` → `db.Database.Migrate()`). The SQLite file lives on the
`api-data` Docker volume (`docker-compose.yml`, `Data Source=/data/realestate.db`), so data
survives container rebuilds. Migrations let schema changes apply to the existing database
**without dropping data** — never delete `realestate.db` to pick up a schema change again.

## Day-to-day: changing the schema
1. Edit the model / `RealEstateDbContext`.
2. From `LayoutManagementAPI/`: `dotnet ef migrations add <Name>` and commit the generated
   `Migrations/*` files.
3. Deploy. `Migrate()` applies the new migration to the existing DB on startup.

## One-time baseline for a database created by the old `EnsureCreated()`
A database made before migrations has the tables but no `__EFMigrationsHistory`, so
`Migrate()` would try to recreate existing tables and fail. Baseline it once (preserves all
data) **before deploying the `Migrate()` build**. In the api container:

```sql
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL);
INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
VALUES ('20261113071644_InitialCreate','8.0.20');
```

Run against `/data/realestate.db` (e.g. `sqlite3 /data/realestate.db < baseline.sql`).
Precondition: the DB's current schema matches `InitialCreate`. If it is on an older schema,
baseline to the matching migration instead, then let later migrations bring it current.

> The local dev database has already been baselined with the row above.
