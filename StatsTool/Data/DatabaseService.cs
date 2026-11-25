using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;

namespace StatsTool.Data
{
    public static class DatabaseService
    {
        private static string ConnectionString =>
            Environment.GetEnvironmentVariable("STATSTOOL_SQL")
            ?? "Server=ANDREI-LP;Database=SaleScope;Trusted_Connection=True;TrustServerCertificate=True;";

        private static SqlConnection Open()
        {
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        public sealed class Account
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Email { get; set; } = "";
        }

        public sealed class Product
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";
            public string Unit { get; set; } = "";
            public DateTime DateAdded { get; set; }
            public int CurrentStock { get; set; }
            public int CurrentWeekUnits { get; set; }
            public DateTime? LastSalesDate { get; set; }
        }

        public sealed class DailySale
        {
            public string ProductId { get; set; } = "";
            public string ProductName { get; set; } = "";
            public string Category { get; set; } = "";
            public string Unit { get; set; } = "";
            public DateTime Date { get; set; }
            public int Quantity { get; set; }
            public int ClosingStock { get; set; }
        }

        public sealed class WeeklySale
        {
            public string ProductId { get; set; } = "";
            public string ProductName { get; set; } = "";
            public string Unit { get; set; } = "";
            public DateTime WeekStart { get; set; }
            public int Quantity { get; set; }
            public int LatestStock { get; set; }
        }

        public static bool TestConnection(out string message)
        {
            try
            {
                using var connection = Open();
                message = "Connected to SaleScope.";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public static Account? Authenticate(string login, string password)
        {
            using var connection = Open();
            using var command = new SqlCommand(
                "SELECT U_ID, U_Name, U_Email, U_Password FROM [User] WHERE U_Email=@login OR U_Name=@login",
                connection);
            command.Parameters.Add("@login", SqlDbType.VarChar, 255).Value = login;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string storedHash = reader.GetString(3);
                if (VerifyPassword(password, storedHash))
                {
                    return new Account
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        Email = reader.GetString(2)
                    };
                }
            }
            return null;
        }

        public static void Register(string name, string email, string password)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                using (var exists = new SqlCommand(
                    "SELECT COUNT(*) FROM [User] WHERE U_Email=@email", connection, transaction))
                {
                    exists.Parameters.Add("@email", SqlDbType.VarChar, 255).Value = email;
                    if ((int)exists.ExecuteScalar()! > 0)
                        throw new InvalidOperationException("That email address is already registered.");
                }

                string id = NextId(connection, transaction, "[User]", "U_ID", "U");
                using var command = new SqlCommand(
                    "INSERT INTO [User] (U_ID,U_Email,U_Password,U_Name,U_Role) VALUES (@id,@email,@password,@name,@role)",
                    connection, transaction);
                command.Parameters.Add("@id", SqlDbType.VarChar, 255).Value = id;
                command.Parameters.Add("@email", SqlDbType.VarChar, 255).Value = email;
                command.Parameters.Add("@password", SqlDbType.VarChar, 255).Value = HashPassword(password);
                command.Parameters.Add("@name", SqlDbType.VarChar, 255).Value = name;
                command.Parameters.Add("@role", SqlDbType.VarChar, 30).Value = "Client";
                command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        public static Account GetAccount(string userId)
        {
            using var connection = Open();
            using var command = new SqlCommand(
                "SELECT U_ID,U_Name,U_Email FROM [User] WHERE U_ID=@id", connection);
            command.Parameters.Add("@id", SqlDbType.VarChar, 255).Value = userId;
            using var reader = command.ExecuteReader();
            if (!reader.Read()) throw new InvalidOperationException("The account could not be found.");
            return new Account { Id = reader.GetString(0), Name = reader.GetString(1), Email = reader.GetString(2) };
        }

        public static void UpdateAccount(string userId, string name, string email, string? newPassword)
        {
            using var connection = Open();
            string sql = string.IsNullOrWhiteSpace(newPassword)
                ? "UPDATE [User] SET U_Name=@name,U_Email=@email WHERE U_ID=@id"
                : "UPDATE [User] SET U_Name=@name,U_Email=@email,U_Password=@password WHERE U_ID=@id";
            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@id", SqlDbType.VarChar, 255).Value = userId;
            command.Parameters.Add("@name", SqlDbType.VarChar, 255).Value = name;
            command.Parameters.Add("@email", SqlDbType.VarChar, 255).Value = email;
            if (!string.IsNullOrWhiteSpace(newPassword))
                command.Parameters.Add("@password", SqlDbType.VarChar, 255).Value = HashPassword(newPassword);
            try { command.ExecuteNonQuery(); }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            { throw new InvalidOperationException("That email address is already in use."); }
        }

        public static void DeleteAccount(string userId)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                using (var inventory = new SqlCommand(
                    "SELECT COUNT(*) FROM Inventory WITH (UPDLOCK,HOLDLOCK) WHERE U_ID=@user",
                    connection, transaction))
                {
                    inventory.Parameters.Add("@user", SqlDbType.VarChar, 255).Value = userId;
                    int productCount = Convert.ToInt32(inventory.ExecuteScalar());
                    if (productCount > 0)
                    {
                        throw new InvalidOperationException(
                            "This account still owns inventory products. Remove or transfer those products first. " +
                            "The account and its sales history were not deleted.");
                    }
                }

                using (var account = new SqlCommand(
                    "DELETE FROM [User] WHERE U_ID=@user", connection, transaction))
                {
                    account.Parameters.Add("@user", SqlDbType.VarChar, 255).Value = userId;
                    if (account.ExecuteNonQuery() == 0)
                        throw new InvalidOperationException("Account not found.");
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static List<Product> GetProducts(string userId, DateTime? weekStart = null)
        {
            DateTime monday = weekStart ?? GetWeekStart(DateTime.Today);
            using var connection = Open();
            using var command = new SqlCommand(@"
SELECT i.I_ID,ISNULL(i.I_ProductName,''),ISNULL(i.I_Category,''),ISNULL(i.I_Unit,''),
       ISNULL(i.I_DateAdded,CONVERT(date,GETDATE())),
       ISNULL(w.WeekUnits,0),
       ISNULL(lastSale.S_DailyStock,0),
       lastSale.S_Date
FROM Inventory i
OUTER APPLY (
    SELECT SUM(s.S_Quantity) AS WeekUnits
    FROM Sales s
    WHERE s.I_ID=i.I_ID AND s.S_Date>=@week AND s.S_Date<DATEADD(day,7,@week)
) w
OUTER APPLY (
    SELECT TOP 1 s.S_DailyStock,s.S_Date
    FROM Sales s WHERE s.I_ID=i.I_ID
    ORDER BY s.S_Date DESC
) lastSale
WHERE i.U_ID=@user
ORDER BY i.I_ProductName;", connection);
            command.Parameters.Add("@week", SqlDbType.Date).Value = monday;
            command.Parameters.Add("@user", SqlDbType.VarChar, 255).Value = userId;
            var result = new List<Product>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Product
                {
                    Id = reader.GetString(0), Name = reader.GetString(1),
                    Category = reader.GetString(2), Unit = reader.GetString(3),
                    DateAdded = reader.GetDateTime(4), CurrentWeekUnits = reader.GetInt32(5),
                    CurrentStock = reader.GetInt32(6),
                    LastSalesDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
                });
            }
            return result;
        }

        public static string SaveProduct(string userId, string? productId, string name, string category, string unit, DateTime dateAdded)
        {
            using var connection = Open();
            if (string.IsNullOrWhiteSpace(productId))
            {
                using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    string id = NextId(connection, transaction, "Inventory", "I_ID", "I");
                    using var command = new SqlCommand(
                        "INSERT INTO Inventory(I_ID,U_ID,I_ProductName,I_Category,I_Unit,I_DateAdded) VALUES(@id,@user,@name,@category,@unit,@date)",
                        connection, transaction);
                    AddProductParameters(command, id, userId, name, category, unit, dateAdded);
                    command.ExecuteNonQuery();
                    transaction.Commit();
                    return id;
                }
                catch { transaction.Rollback(); throw; }
            }
            else
            {
                using var command = new SqlCommand(
                    "UPDATE Inventory SET I_ProductName=@name,I_Category=@category,I_Unit=@unit WHERE I_ID=@id AND U_ID=@user",
                    connection);
                command.Parameters.Add("@name", SqlDbType.VarChar, 255).Value = name;
                command.Parameters.Add("@category", SqlDbType.VarChar, 255).Value = category;
                command.Parameters.Add("@unit", SqlDbType.VarChar, 50).Value = unit;
                command.Parameters.Add("@id", SqlDbType.VarChar, 255).Value = productId;
                command.Parameters.Add("@user", SqlDbType.VarChar, 255).Value = userId;
                if (command.ExecuteNonQuery() == 0) throw new InvalidOperationException("Product not found.");
                return productId;
            }
        }

        private static void AddProductParameters(SqlCommand command, string id, string userId, string name, string category, string unit, DateTime dateAdded)
        {
            command.Parameters.Add("@id", SqlDbType.VarChar, 255).Value = id;
            command.Parameters.Add("@user", SqlDbType.VarChar, 255).Value = userId;
            command.Parameters.Add("@name", SqlDbType.VarChar, 255).Value = name;
            command.Parameters.Add("@category", SqlDbType.VarChar, 255).Value = category;
            command.Parameters.Add("@unit", SqlDbType.VarChar, 50).Value = unit;
            command.Parameters.Add("@date", SqlDbType.Date).Value = dateAdded.Date;
        }

        public static void DeleteProduct(string userId, string productId)
        {
            using var connection = Open();
            using var command = new SqlCommand(
                "DELETE FROM Inventory WHERE I_ID=@id AND U_ID=@user", connection);
            command.Parameters.Add("@id", SqlDbType.VarChar, 255).Value = productId;
            command.Parameters.Add("@user", SqlDbType.VarChar, 255).Value = userId;
            try
            {
                if (command.ExecuteNonQuery() == 0) throw new InvalidOperationException("Product not found.");
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                throw new InvalidOperationException("This product has sales history and cannot be deleted. Keep it in Inventory to preserve its records.");
            }
        }

        public static void SaveDailySale(string userId, string productId, DateTime date, int quantity, int closingStock)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                using (var owner = new SqlCommand(
                    "SELECT COUNT(*) FROM Inventory WHERE I_ID=@product AND U_ID=@user", connection, transaction))
                {
                    owner.Parameters.Add("@product", SqlDbType.VarChar, 255).Value = productId;
                    owner.Parameters.Add("@user", SqlDbType.VarChar, 255).Value = userId;
                    if ((int)owner.ExecuteScalar()! == 0) throw new InvalidOperationException("Product not found in your inventory.");
                }

                string? salesId;
                using (var existing = new SqlCommand(
                    "SELECT S_ID FROM Sales WITH (UPDLOCK,HOLDLOCK) WHERE I_ID=@product AND S_Date=@date",
                    connection, transaction))
                {
                    existing.Parameters.Add("@product", SqlDbType.VarChar, 255).Value = productId;
                    existing.Parameters.Add("@date", SqlDbType.Date).Value = date.Date;
                    salesId = existing.ExecuteScalar() as string;
                }

                if (salesId == null)
                {
                    salesId = NextId(connection, transaction, "Sales", "S_ID", "S");
                    using var insert = new SqlCommand(
                        "INSERT INTO Sales(S_ID,I_ID,S_Date,S_Quantity,S_DailyStock) VALUES(@id,@product,@date,@qty,@stock)",
                        connection, transaction);
                    insert.Parameters.Add("@id", SqlDbType.VarChar, 255).Value = salesId;
                    insert.Parameters.Add("@product", SqlDbType.VarChar, 255).Value = productId;
                    insert.Parameters.Add("@date", SqlDbType.Date).Value = date.Date;
                    insert.Parameters.Add("@qty", SqlDbType.Int).Value = quantity;
                    insert.Parameters.Add("@stock", SqlDbType.Int).Value = closingStock;
                    insert.ExecuteNonQuery();
                }
                else
                {
                    using var update = new SqlCommand(
                        "UPDATE Sales SET S_Quantity=@qty,S_DailyStock=@stock WHERE S_ID=@id",
                        connection, transaction);
                    update.Parameters.Add("@qty", SqlDbType.Int).Value = quantity;
                    update.Parameters.Add("@stock", SqlDbType.Int).Value = closingStock;
                    update.Parameters.Add("@id", SqlDbType.VarChar, 255).Value = salesId;
                    update.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        public static List<DailySale> GetDailySales(string userId)
        {
            using var connection = Open();
            using var command = new SqlCommand(@"
SELECT i.I_ID,ISNULL(i.I_ProductName,''),ISNULL(i.I_Category,''),ISNULL(i.I_Unit,''),s.S_Date,ISNULL(s.S_Quantity,0),ISNULL(s.S_DailyStock,0)
FROM Sales s INNER JOIN Inventory i ON i.I_ID=s.I_ID
WHERE i.U_ID=@user ORDER BY s.S_Date,i.I_ProductName;", connection);
            command.Parameters.Add("@user", SqlDbType.VarChar, 255).Value = userId;
            var rows = new List<DailySale>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
                rows.Add(new DailySale
                {
                    ProductId = reader.GetString(0), ProductName = reader.GetString(1),
                    Category = reader.GetString(2), Unit = reader.GetString(3),
                    Date = reader.GetDateTime(4), Quantity = reader.GetInt32(5),
                    ClosingStock = reader.GetInt32(6)
                });
            return rows;
        }

        public static List<WeeklySale> GetWeeklySales(string userId)
        {
            var daily = GetDailySales(userId);
            return daily.GroupBy(x => new { x.ProductId, x.ProductName, x.Unit, Week = GetWeekStart(x.Date) })
                .Select(g => new WeeklySale
                {
                    ProductId = g.Key.ProductId, ProductName = g.Key.ProductName,
                    Unit = g.Key.Unit, WeekStart = g.Key.Week,
                    Quantity = g.Sum(x => x.Quantity),
                    LatestStock = g.OrderByDescending(x => x.Date).First().ClosingStock
                }).OrderBy(x => x.WeekStart).ThenBy(x => x.ProductName).ToList();
        }

        public static DateTime GetWeekStart(DateTime date)
        {
            int days = ((int)date.DayOfWeek + 6) % 7;
            return date.Date.AddDays(-days);
        }

        private static string NextId(SqlConnection connection, SqlTransaction transaction, string table, string column, string prefix)
        {
            string sql = $"SELECT ISNULL(MAX(TRY_CONVERT(INT,SUBSTRING([{column}],2,254))),0)+1 FROM [dbo].[{table}] WITH (UPDLOCK,HOLDLOCK) WHERE [{column}] LIKE @prefix + '%'";
            using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@prefix", SqlDbType.VarChar, 1).Value = prefix;
            int number = Convert.ToInt32(command.ExecuteScalar());
            return prefix + number.ToString("D4");
        }

        private static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120000, HashAlgorithmName.SHA256, 32);
            return "120000:" + Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
        }

        private static bool VerifyPassword(string password, string stored)
        {
            try
            {
                string[] parts = stored.Split(':');
                if (parts.Length != 3)
                {
                    // Older records may contain plain-text values. This fallback
                    // keeps existing accounts usable; new registrations are hashed.
                    return string.Equals(password, stored, StringComparison.Ordinal);
                }

                int iterations = int.Parse(parts[0]);
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] expected = Convert.FromBase64String(parts[2]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                    password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }
    }
}
