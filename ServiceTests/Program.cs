using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;

namespace ServiceTests;

internal static class Program
{
    private static readonly AppUser Admin = new AppUser
    {
        Id = 1,
        Username = "test-admin",
        Role = DatabaseHelper.RoleAdmin
    };

    private static int Main()
    {
        var tests = new (string Name, Action Body)[]
        {
            ("sale", SaleCreatesExpectedTotalsAndStock),
            ("discount sale", DiscountSaleUsesNetPriceAndDiscountMetadata),
            ("return", ReturnUsesSameNetPriceModelAsSale),
            ("debt sale", DebtSaleCreatesExpectedDebt),
            ("backup restore", BackupRestoreRestoresDatabaseSnapshot)
        };

        int failed = 0;
        foreach (var test in tests)
        {
            try
            {
                ResetDatabase(test.Name);
                test.Body();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL {test.Name}: {ex.Message}");
            }
        }

        return failed == 0 ? 0 : 1;
    }

    private static void SaleCreatesExpectedTotalsAndStock()
    {
        int productId = InsertProduct("Mixer", purchaseUzs: 10000, quantity: 10);

        int saleId = new SaleService().CreateSale(new Sale
        {
            Date = DateTime.Now,
            SubtotalUZS = 30000,
            DiscountType = "None",
            DiscountValue = 0,
            DiscountUZS = 0,
            TotalUZS = 30000,
            Items = new List<SaleItem>
            {
                new SaleItem { ProductId = productId, Quantity = 2, SellPriceUZS = 15000, DiscountUZS = 0 }
            }
        }, Admin);

        AssertSale(saleId, total: 30000, subtotal: 30000, discount: 0, profit: 10000);
        AssertProductQuantity(productId, 8);
    }

    private static void DiscountSaleUsesNetPriceAndDiscountMetadata()
    {
        int productId = InsertProduct("Valve", purchaseUzs: 10000, quantity: 10);

        int saleId = new SaleService().CreateSale(new Sale
        {
            Date = DateTime.Now,
            SubtotalUZS = 30000,
            DiscountType = "Amount",
            DiscountValue = 4000,
            DiscountUZS = 4000,
            TotalUZS = 26000,
            Items = new List<SaleItem>
            {
                new SaleItem { ProductId = productId, Quantity = 2, SellPriceUZS = 13000, DiscountUZS = 4000 }
            }
        }, Admin);

        AssertSale(saleId, total: 26000, subtotal: 30000, discount: 4000, profit: 6000);
        AssertProductQuantity(productId, 8);
    }

    private static void ReturnUsesSameNetPriceModelAsSale()
    {
        int productId = InsertProduct("Pipe", purchaseUzs: 10000, quantity: 10);
        int saleId = new SaleService().CreateSale(new Sale
        {
            Date = DateTime.Now,
            SubtotalUZS = 30000,
            DiscountType = "Amount",
            DiscountValue = 4000,
            DiscountUZS = 4000,
            TotalUZS = 26000,
            Items = new List<SaleItem>
            {
                new SaleItem { ProductId = productId, Quantity = 2, SellPriceUZS = 13000, DiscountUZS = 4000 }
            }
        }, Admin);

        int saleItemId = ScalarInt("SELECT Id FROM SaleItems WHERE SaleId=@saleId", ("@saleId", saleId));
        var result = new ReturnService().ApplyReturn(
            saleId,
            new List<(int saleItemId, double quantity)> { (saleItemId, 1) },
            "test",
            Admin);

        AssertClose(result.SubtotalUZS, 15000, "return subtotal");
        AssertClose(result.DiscountUZS, 2000, "return discount");
        AssertClose(result.TotalUZS, 13000, "return total");
        AssertClose(result.ProfitReductionUZS, 3000, "return profit reduction");
        AssertSale(saleId, total: 13000, subtotal: 15000, discount: 2000, profit: 3000);
        AssertProductQuantity(productId, 9);
    }

    private static void DebtSaleCreatesExpectedDebt()
    {
        int productId = InsertProduct("Tap", purchaseUzs: 8000, quantity: 10);
        int customerId = new CustomerService().FindOrCreate("Ali Valiyev", "+998900000000", "");
        int saleId = new SaleService().CreateSale(new Sale
        {
            Date = DateTime.Now,
            SubtotalUZS = 20000,
            TotalUZS = 20000,
            Items = new List<SaleItem>
            {
                new SaleItem { ProductId = productId, Quantity = 1, SellPriceUZS = 20000 }
            }
        }, Admin);

        int debtId = new DebtService().CreateDebtForSale(saleId, customerId, 5000, DateTime.Today.AddDays(5), Admin);

        using var connection = Database.GetConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT TotalAmountUZS, PaidAmountUZS, RemainingAmountUZS, Status FROM Debts WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", debtId);
        using var reader = cmd.ExecuteReader();
        AssertTrue(reader.Read(), "debt row exists");
        AssertClose(reader.GetDouble(0), 20000, "debt total");
        AssertClose(reader.GetDouble(1), 5000, "debt paid");
        AssertClose(reader.GetDouble(2), 15000, "debt remaining");
        AssertEqual(reader.GetString(3), "Open", "debt status");
    }

    private static void BackupRestoreRestoresDatabaseSnapshot()
    {
        int productId = InsertProduct("Original", purchaseUzs: 10000, quantity: 3);
        var backup = new BackupService();
        string backupPath = backup.CreateBackup();

        Execute("UPDATE Products SET Name='Changed', QuantityUSD=99 WHERE Id=@id", ("@id", productId));
        InsertProduct("Extra", purchaseUzs: 10000, quantity: 1);

        backup.RestoreBackup(backupPath);

        using var connection = Database.GetConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*), MIN(Name), MIN(QuantityUSD) FROM Products";
        using var reader = cmd.ExecuteReader();
        AssertTrue(reader.Read(), "products after restore");
        AssertEqual(reader.GetInt32(0), 1, "product count after restore");
        AssertEqual(reader.GetString(1), "Original", "product name after restore");
        AssertClose(reader.GetDouble(2), 3, "product qty after restore");
    }

    private static void ResetDatabase(string testName)
    {
        string safeName = testName.Replace(" ", "_", StringComparison.Ordinal);
        string root = Path.Combine(Path.GetTempPath(), "OsontrackServiceTests", safeName + "_" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("OSONTRACK_APPDATA_ROOT", root);
        DbInitializer.Initialize();
        new DatabaseHelper().CreateUsersTable();
    }

    private static int InsertProduct(string name, double purchaseUzs, double quantity)
    {
        using var connection = Database.GetConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Products (Name, PurchaseCurrency, PurchasePrice, PurchasePriceUZS, PurchasePriceUSD, QuantityUSD, ImagePath)
            VALUES (@name, 'UZS', @purchase, @purchase, @purchaseUsd, @quantity, '');
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@purchase", purchaseUzs);
        cmd.Parameters.AddWithValue("@purchaseUsd", purchaseUzs / 12500.0);
        cmd.Parameters.AddWithValue("@quantity", quantity);
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void AssertSale(int saleId, double total, double subtotal, double discount, double profit)
    {
        using var connection = Database.GetConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT TotalUZS, SubtotalUZS, DiscountUZS, ProfitUZS FROM Sales WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", saleId);
        using var reader = cmd.ExecuteReader();
        AssertTrue(reader.Read(), "sale row exists");
        AssertClose(reader.GetDouble(0), total, "sale total");
        AssertClose(reader.GetDouble(1), subtotal, "sale subtotal");
        AssertClose(reader.GetDouble(2), discount, "sale discount");
        AssertClose(reader.GetDouble(3), profit, "sale profit");
    }

    private static void AssertProductQuantity(int productId, double expected)
    {
        double actual = ScalarDouble("SELECT QuantityUSD FROM Products WHERE Id=@id", ("@id", productId));
        AssertClose(actual, expected, "product quantity");
    }

    private static int ScalarInt(string sql, params (string Name, object Value)[] parameters)
    {
        return Convert.ToInt32(Scalar(sql, parameters), CultureInfo.InvariantCulture);
    }

    private static double ScalarDouble(string sql, params (string Name, object Value)[] parameters)
    {
        return Convert.ToDouble(Scalar(sql, parameters), CultureInfo.InvariantCulture);
    }

    private static object Scalar(string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = Database.GetConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var parameter in parameters)
        {
            cmd.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        return cmd.ExecuteScalar() ?? throw new Exception("Scalar returned null.");
    }

    private static void Execute(string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = Database.GetConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var parameter in parameters)
        {
            cmd.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        cmd.ExecuteNonQuery();
    }

    private static void AssertClose(double actual, double expected, string message)
    {
        if (Math.Abs(actual - expected) > 0.001)
        {
            throw new Exception($"{message}: expected {expected}, actual {actual}");
        }
    }

    private static void AssertEqual<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            throw new Exception($"{message}: expected {expected}, actual {actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
