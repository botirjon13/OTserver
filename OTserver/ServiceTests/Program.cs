using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
            ("historical cost", HistoricalCostIsUsedAfterProductCostChanges),
            ("ordinary receipt", OrdinaryReceiptHasNoFakeFiscalData),
            ("receipt branding", ReceiptBrandingIsSnapshotted),
            ("nasiya receipt", NasiyaReceiptStoresInitialPaymentAndBalance),
            ("debt sale", DebtSaleCreatesExpectedDebt),
            ("backup restore", BackupRestoreRestoresDatabaseSnapshot),
            ("legacy backup restore", LegacyDatabaseBackupRemainsRestorable)
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
        SaleReceipt originalReceipt = new ReceiptService().CreateAndSave(saleId, "Naqd", Admin);

        Execute("UPDATE Products SET Name='Changed pipe' WHERE Id=@id", ("@id", productId));
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
        SaleReceipt? reprintedReceipt = new ReceiptService().GetBySaleId(saleId);
        AssertTrue(reprintedReceipt != null, "original sale receipt remains available");
        AssertClose(reprintedReceipt!.TotalUZS, 26000, "original receipt total remains unchanged");
        AssertClose(reprintedReceipt.SubtotalUZS, 30000, "original receipt subtotal remains unchanged");
        AssertClose(reprintedReceipt.DiscountUZS, 4000, "original receipt discount remains unchanged");
        AssertEqual(reprintedReceipt.Items[0].ProductName, "Pipe", "original product name remains unchanged");

        ReturnReceipt? returnReceipt = new ReturnService().GetReturnReceipt(result.ReturnId);
        AssertTrue(returnReceipt != null, "separate return document exists");
        AssertEqual(returnReceipt!.OriginalReceiptNumber, originalReceipt.ReceiptNumber, "return document points to original receipt");
        AssertClose(returnReceipt.TotalUZS, 13000, "return document total");
        AssertClose(returnReceipt.CashRefundUZS, 13000, "cash refund amount");
        AssertEqual(returnReceipt.Items[0].ProductName, "Pipe", "return document uses sale-time product name");
        AssertTrue(
            new ReturnService().GetReturnHistory(DateTime.Today, DateTime.Today).Exists(x => x.ReturnId == result.ReturnId),
            "return document appears in history");
        ReceiptService.ReceiptHistoryItem? historyItem = new ReceiptService()
            .GetHistory(DateTime.Today, DateTime.Today)
            .Find(x => x.SaleId == saleId);
        AssertTrue(historyItem != null, "sale receipt appears in history");
        AssertClose(historyItem!.TotalUZS, 26000, "receipt history keeps original receipt total after return");
    }

    private static void HistoricalCostIsUsedAfterProductCostChanges()
    {
        int productId = InsertProduct("Valve", purchaseUzs: 10000, quantity: 10);
        int saleId = new SaleService().CreateSale(new Sale
        {
            Date = DateTime.Now,
            SubtotalUZS = 30000,
            TotalUZS = 30000,
            Items = new List<SaleItem>
            {
                new SaleItem { ProductId = productId, Quantity = 2, SellPriceUZS = 15000 }
            }
        }, Admin);

        Execute("UPDATE Products SET PurchasePriceUZS=20000 WHERE Id=@id", ("@id", productId));

        var saleService = new SaleService();
        var reportBeforeReturn = saleService.GetSaleReportRows(DateTime.Today, DateTime.Today, 12500, Admin);
        AssertClose(reportBeforeReturn[0].ProfitUZS, 10000, "profit before return uses sale-time cost");

        int saleItemId = ScalarInt("SELECT Id FROM SaleItems WHERE SaleId=@saleId", ("@saleId", saleId));
        var result = new ReturnService().ApplyReturn(
            saleId,
            new List<(int saleItemId, double quantity)> { (saleItemId, 1) },
            "test",
            Admin);

        AssertClose(result.ProfitReductionUZS, 5000, "return profit reduction uses sale-time cost");
        AssertSale(saleId, total: 15000, subtotal: 15000, discount: 0, profit: 5000);

        var reportAfterReturn = saleService.GetSaleReportRows(DateTime.Today, DateTime.Today, 12500, Admin);
        AssertClose(reportAfterReturn[0].ProfitUZS, 5000, "remaining profit uses sale-time cost");
    }

    private static void OrdinaryReceiptHasNoFakeFiscalData()
    {
        int productId = InsertProduct("Mixer", purchaseUzs: 10000, quantity: 3);
        int saleId = new SaleService().CreateSale(new Sale
        {
            Date = DateTime.Now,
            SubtotalUZS = 15000,
            TotalUZS = 15000,
            Items = new List<SaleItem>
            {
                new SaleItem { ProductId = productId, Quantity = 1, SellPriceUZS = 15000 }
            }
        }, Admin);

        SaleReceipt receipt = new ReceiptService().CreateAndSave(saleId, "Naqd", Admin);

        AssertEqual(receipt.ReceiptNumber, $"CHK-{saleId:D6}", "ordinary receipt number");
        AssertEqual(ScalarString("SELECT FiscalSign FROM SaleReceipts WHERE SaleId=@id", ("@id", saleId)), "", "no fake fiscal sign stored");
        AssertEqual(ScalarString("SELECT QrData FROM SaleReceipts WHERE SaleId=@id", ("@id", saleId)), "", "no fake QR data stored");
        Execute("UPDATE Products SET Name='Changed mixer' WHERE Id=@id", ("@id", productId));
        SaleReceipt? reopened = new ReceiptService().GetBySaleId(saleId);
        AssertTrue(reopened != null, "saved receipt can be reopened");
        AssertEqual(reopened!.Items[0].ProductName, "Mixer", "sale-time product name is preserved");
    }

    private static void NasiyaReceiptStoresInitialPaymentAndBalance()
    {
        int productId = InsertProduct("Tap", purchaseUzs: 8000, quantity: 3);
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

        new DebtService().CreateDebtForSale(saleId, customerId, 5000, DateTime.Today.AddDays(5), Admin);
        SaleReceipt receipt = new ReceiptService().CreateAndSave(saleId, "Nasiya (Qarz)", Admin);
        SaleReceipt? reopened = new ReceiptService().GetBySaleId(saleId);

        AssertClose(receipt.InitialPaymentUZS, 5000, "initial payment on new receipt");
        AssertClose(receipt.RemainingDebtUZS, 15000, "remaining debt on new receipt");
        AssertTrue(reopened != null, "nasiya receipt reopens");
        AssertClose(reopened!.InitialPaymentUZS, 5000, "initial payment persists on reprint");
        AssertClose(reopened.RemainingDebtUZS, 15000, "remaining debt persists on reprint");

        int saleItemId = ScalarInt("SELECT Id FROM SaleItems WHERE SaleId=@saleId", ("@saleId", saleId));
        var result = new ReturnService().ApplyReturn(
            saleId,
            new List<(int saleItemId, double quantity)> { (saleItemId, 1) },
            "test",
            Admin);
        ReturnReceipt? returnReceipt = new ReturnService().GetReturnReceipt(result.ReturnId);
        AssertTrue(returnReceipt != null, "nasiya return document exists");
        AssertClose(returnReceipt!.DebtReducedUZS, 15000, "return offsets outstanding debt");
        AssertClose(returnReceipt.CashRefundUZS, 5000, "return reimburses amount above outstanding debt");
        SaleReceipt? receiptAfterReturn = new ReceiptService().GetBySaleId(saleId);
        AssertTrue(receiptAfterReturn != null, "nasiya sale receipt remains available after return");
        AssertClose(receiptAfterReturn!.InitialPaymentUZS, 5000, "original initial payment remains unchanged after return");
        AssertClose(receiptAfterReturn.RemainingDebtUZS, 15000, "original debt balance remains unchanged after return");
    }

    private static void ReceiptBrandingIsSnapshotted()
    {
        int productId = InsertProduct("Mixer", purchaseUzs: 10000, quantity: 3);
        int saleId = new SaleService().CreateSale(new Sale
        {
            Date = DateTime.Now,
            SubtotalUZS = 15000,
            TotalUZS = 15000,
            Items = new List<SaleItem>
            {
                new SaleItem { ProductId = productId, Quantity = 1, SellPriceUZS = 15000 }
            }
        }, Admin);

        byte[] logo = { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3 };
        var settingsService = new FiscalSettingsService();
        settingsService.Save(new FiscalSettings
        {
            BusinessName = "Baraka Savdo",
            StoreAddress = "Namangan",
            StorePhone = "+998901234567",
            ThankYouMessage = "Xaridingiz uchun rahmat!",
            LogoPng = logo
        }, Admin);

        new ReceiptService().CreateAndSave(saleId, "Naqd", Admin);

        settingsService.Save(new FiscalSettings
        {
            BusinessName = "New Shop",
            StoreAddress = "New address",
            StorePhone = "+998000000000",
            ThankYouMessage = "New message"
        }, Admin);

        SaleReceipt? receipt = new ReceiptService().GetBySaleId(saleId);
        AssertTrue(receipt != null, "branded receipt can be reopened");
        AssertEqual(receipt!.BusinessName, "Baraka Savdo", "shop name is snapshotted");
        AssertEqual(receipt.StoreAddress, "Namangan", "shop address is snapshotted");
        AssertEqual(receipt.StorePhone, "+998901234567", "shop phone is snapshotted");
        AssertEqual(receipt.ThankYouMessage, "Xaridingiz uchun rahmat!", "thank-you message is snapshotted");
        AssertEqual(receipt.SellerName, Admin.Username, "seller username is snapshotted");
        AssertTrue(receipt.LogoPng.SequenceEqual(logo), "shop logo bytes are snapshotted");
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
        string imagesRoot = Path.Combine(Database.GetAppDataRoot(), "ProductImages");
        Directory.CreateDirectory(imagesRoot);
        string originalImagePath = Path.Combine(imagesRoot, "product_original.jpg");
        File.WriteAllText(originalImagePath, "original image");

        var backup = new BackupService();
        string backupPath = backup.CreateBackup();
        AssertEqual(Path.GetExtension(backupPath), ".zip", "backup bundle extension");

        using (var archive = ZipFile.OpenRead(backupPath))
        {
            AssertTrue(archive.GetEntry("database.db") != null, "backup contains database");
            AssertTrue(archive.GetEntry("ProductImages/") != null, "backup contains image folder");
            AssertTrue(archive.GetEntry("ProductImages/product_original.jpg") != null, "backup contains product image");
        }

        Execute("UPDATE Products SET Name='Changed', QuantityUSD=99 WHERE Id=@id", ("@id", productId));
        InsertProduct("Extra", purchaseUzs: 10000, quantity: 1);
        File.Delete(originalImagePath);
        File.WriteAllText(Path.Combine(imagesRoot, "product_current_only.jpg"), "current image");

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

        AssertEqual(File.ReadAllText(originalImagePath), "original image", "product image after restore");
        AssertTrue(!File.Exists(Path.Combine(imagesRoot, "product_current_only.jpg")), "restore removes images absent from backup");
    }

    private static void LegacyDatabaseBackupRemainsRestorable()
    {
        int productId = InsertProduct("Legacy", purchaseUzs: 10000, quantity: 3);
        string root = Database.GetAppDataRoot();
        string legacyBackupPath = Path.Combine(Path.GetTempPath(), $"database_legacy_{Guid.NewGuid():N}.db");
        using (var source = Database.GetConnection())
        using (var target = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = legacyBackupPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString()))
        {
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }

        string legacyImagesRoot = Path.Combine(
            Path.GetDirectoryName(legacyBackupPath)!,
            Path.GetFileNameWithoutExtension(legacyBackupPath) + "_assets",
            "ProductImages");
        Directory.CreateDirectory(legacyImagesRoot);
        File.WriteAllText(Path.Combine(legacyImagesRoot, "legacy.jpg"), "legacy image");

        Execute("UPDATE Products SET Name='Changed', QuantityUSD=99 WHERE Id=@id", ("@id", productId));
        Directory.CreateDirectory(Path.Combine(root, "ProductImages"));
        File.WriteAllText(Path.Combine(root, "ProductImages", "current.jpg"), "current image");

        try
        {
            new BackupService().RestoreBackup(legacyBackupPath);
        }
        finally
        {
            File.Delete(legacyBackupPath);
            Directory.Delete(Path.Combine(Path.GetDirectoryName(legacyBackupPath)!, Path.GetFileNameWithoutExtension(legacyBackupPath) + "_assets"), recursive: true);
        }

        using var connection = Database.GetConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Name, QuantityUSD FROM Products WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", productId);
        using var reader = cmd.ExecuteReader();
        AssertTrue(reader.Read(), "legacy product after restore");
        AssertEqual(reader.GetString(0), "Legacy", "legacy product name after restore");
        AssertClose(reader.GetDouble(1), 3, "legacy product quantity after restore");
        AssertEqual(
            File.ReadAllText(Path.Combine(root, "ProductImages", "legacy.jpg")),
            "legacy image",
            "legacy image after restore");
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

    private static string ScalarString(string sql, params (string Name, object Value)[] parameters)
    {
        return Convert.ToString(Scalar(sql, parameters), CultureInfo.InvariantCulture) ?? string.Empty;
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
