# StatsTool / SaleScope SQL Server setup

The application connects to the existing `SaleScope` database and uses only its three existing tables: `[User]`, `Inventory`, and `Sales`. The setup script creates the database/tables only if they do not already exist; it does not add columns or extra tables.

## Connection

Default connection string in `StatsTool/Data/DatabaseService.cs`:

```text
Server=ANDREI-LP;Database=SaleScope;Trusted_Connection=True;TrustServerCertificate=True;
```

This uses Windows Authentication and assumes the SQL Server default instance is running on `ANDREI-LP`. You can override it with the Windows environment variable `STATSTOOL_SQL`.

## First use

1. Open `DATABASE_SETUP.sql` in SQL Server Management Studio and run it. If the tables already exist, the script leaves them unchanged.
2. Open `StatsTool.sln` in Visual Studio.
3. Restore NuGet packages and build the solution.
4. Start the application, create an account through Sign Up, then log in.

Newly registered passwords are stored as salted PBKDF2 hashes. Existing plain-text password values are still accepted for compatibility with older records; those should be migrated to hashes before production use. The sample values such as `hashed_password_001` are placeholders, not recoverable passwords.

## Page/database behavior

- Login and Sign Up read/write `[User]`; new accounts receive the `Client` role.
- Inventory reads and writes only products owned by the signed-in `U_ID`.
- Daily Entry inserts or updates a product's daily `S_Quantity` and `S_DailyStock` record.
- Dashboard, Comparisons, and Insights calculate summaries from saved sales rows, scoped to the signed-in user's inventory.
- Settings updates the signed-in account. Account deletion is blocked while the account still owns inventory products, preserving sales history and foreign-key relationships.
- Product deletion is blocked when sales rows reference that product.
- Archive and persistent last-modified features are not implemented because the supplied schema has no corresponding columns.

## Daily-entry CSV format

The batch importer expects these headers:

```csv
I_ID,S_Date,S_Quantity,S_DailyStock
I0001,2026-11-24,4,26
```

The `I_ID` must belong to the signed-in user. Dates should use `yyyy-MM-dd`.
