using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Forms;
using SantexnikaSRM.Models;
using SantexnikaSRM.Services;
using SantexnikaSRM.Utils;

namespace ServiceTests;

internal static class Program
{
    private static readonly AppUser Admin = new AppUser
    {
        Id = 1,
        Username = "test-admin",
        Role = DatabaseHelper.RoleAdmin
    };

    [STAThread]
    private static int Main()
    {
        var tests = new (string Name, Action Body)[]
        {
            ("sale", SaleCreatesExpectedTotalsAndStock),
            ("discount sale", DiscountSaleUsesNetPriceAndDiscountMetadata),
            ("receipt branding snapshot", ReceiptBrandingAndSellerAreSnapshotted),
            ("return", ReturnUsesSameNetPriceModelAsSale),
            ("debt return settlement", ReturnSettlementSeparatesDebtAndCash),
            ("historical cost", HistoricalCostIsUsedAfterProductCostChanges),
            ("debt sale", DebtSaleCreatesExpectedDebt),
            ("home dashboard", HomeDashboardShowsRoleAppropriateMetrics),
            ("home dashboard status visuals", HomeDashboardStatusVisualsMatchState),
            ("dashboard image bounds", DashboardImagesAreNotClipped),
            ("backup restore", BackupRestoreRestoresDatabaseSnapshot),
            ("legacy backup restore", LegacyDatabaseBackupRemainsRestorable),
            ("encrypted cloud backup", EncryptedCloudBackupRoundTripsAndRejectsTampering)
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

        ReturnReceipt? returnReceipt = new ReturnService().GetReturnReceipt(result.ReturnId);
        AssertTrue(returnReceipt != null, "return receipt can be reopened");
        AssertEqual(returnReceipt!.OriginalReceiptNumber, $"CHK-{saleId:D6}", "return receipt original number");
        AssertClose(returnReceipt.TotalUZS, result.TotalUZS, "return receipt total");
        AssertTrue(
            new ReturnService().GetReturnHistory(DateTime.Today, DateTime.Today).Exists(x => x.ReturnId == result.ReturnId),
            "return receipt appears in history");
    }

    private static void ReceiptBrandingAndSellerAreSnapshotted()
    {
        int productId = InsertProduct("Original name", purchaseUzs: 10000, quantity: 5);
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

        byte[] logoPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l4sAAAAASUVORK5CYII=");
        new FiscalSettingsService().Save(new FiscalSettings
        {
            BusinessName = "Test do'kon",
            TIN = "123456789",
            StoreAddress = "Toshkent",
            StorePhone = "+998 90 123 45 67",
            ThankYouMessage = "Xaridingiz uchun rahmat",
            LogoPng = logoPng
        }, Admin);

        SaleReceipt created = new ReceiptService().CreateAndSave(saleId, "Naqd", Admin);
        AssertEqual(created.BusinessName, "Test do'kon", "created receipt store name");
        AssertEqual(created.SellerName, Admin.Username, "created receipt seller");
        AssertTrue(created.LogoPng.Length > 0, "created receipt logo");
        AssertEqual(created.FiscalSign, "", "fake fiscal sign omitted");
        AssertEqual(created.QrData, "", "fake QR data omitted");

        Execute("UPDATE Products SET Name='Changed name' WHERE Id=@id", ("@id", productId));
        Execute("UPDATE SaleItems SET SellPriceUZS=99999 WHERE SaleId=@id", ("@id", saleId));
        new FiscalSettingsService().Save(new FiscalSettings
        {
            BusinessName = "New name",
            StorePhone = "New phone"
        }, Admin);
        SaleReceipt? reopened = new ReceiptService().GetBySaleId(saleId);
        AssertTrue(reopened != null, "receipt can be reopened");
        AssertEqual(reopened!.BusinessName, "Test do'kon", "reopened receipt store snapshot");
        AssertEqual(reopened.StorePhone, "+998 90 123 45 67", "reopened receipt phone snapshot");
        AssertEqual(reopened.ThankYouMessage, "Xaridingiz uchun rahmat", "reopened receipt thank-you snapshot");
        AssertEqual(reopened.SellerName, Admin.Username, "reopened receipt seller snapshot");
        AssertTrue(reopened.LogoPng.Length > 0, "reopened receipt logo snapshot");
        AssertEqual(reopened.Items[0].ProductName, "Original name", "reopened receipt product-name snapshot");
        AssertClose(reopened.Items[0].UnitPriceUZS, 15000, "reopened receipt price snapshot");
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

    private static void HomeDashboardShowsRoleAppropriateMetrics()
    {
        int productId = InsertProduct("Dashboard item", purchaseUzs: 10000, quantity: 3);
        _ = new SaleService().CreateSale(new Sale
        {
            Date = DateTime.Today.AddHours(10),
            SubtotalUZS = 30000,
            TotalUZS = 30000,
            Items = new List<SaleItem>
            {
                new SaleItem { ProductId = productId, Quantity = 2, SellPriceUZS = 15000 }
            }
        }, Admin);
        new ExpenseService().Add(new Expense
        {
            Date = DateTime.Today.AddHours(11),
            Type = "Test",
            Description = "Dashboard expense",
            AmountUZS = 2000
        }, Admin);

        HomeDashboardSnapshot adminSnapshot = new HomeDashboardService().GetSnapshot(Admin, DateTime.Today);
        AssertClose(adminSnapshot.TodaySalesUZS, 30000, "dashboard today sales");
        AssertClose(adminSnapshot.TodayProfitUZS, 10000, "dashboard today profit");
        AssertClose(adminSnapshot.TodayExpensesUZS, 2000, "dashboard today expenses");
        AssertClose(adminSnapshot.TodayNetUZS, 8000, "dashboard today net");
        AssertEqual(adminSnapshot.ProductCount, 1, "dashboard product count");
        AssertEqual(adminSnapshot.LowStockCount, 1, "dashboard low stock count");
        AssertEqual(adminSnapshot.Week.Count, 7, "dashboard week length");
        HomeDashboardDay todayMetric = adminSnapshot.Week.Find(day => day.Date == DateTime.Today)!;
        AssertClose(todayMetric.SalesUZS, 30000, "dashboard daily chart sales");

        var seller = new AppUser { Id = 2, Username = "test-seller", Role = DatabaseHelper.RoleSeller };
        HomeDashboardSnapshot sellerSnapshot = new HomeDashboardService().GetSnapshot(seller, DateTime.Today);
        AssertTrue(!sellerSnapshot.IsAdministrator, "seller dashboard role");
        AssertClose(sellerSnapshot.TodaySalesUZS, 0, "seller dashboard hides unscoped sales");
        AssertEqual(sellerSnapshot.Week.Count, 0, "seller dashboard hides business chart");
    }

    private static void ReturnSettlementSeparatesDebtAndCash()
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
        int saleItemId = ScalarInt("SELECT Id FROM SaleItems WHERE SaleId=@saleId", ("@saleId", saleId));

        ReturnService.ReturnApplyResult result = new ReturnService().ApplyReturn(
            saleId,
            new List<(int saleItemId, double quantity)> { (saleItemId, 1) },
            "test",
            Admin);

        AssertClose(result.DebtReducedUZS, 15000, "return clears remaining debt");
        AssertClose(result.CashRefundUZS, 5000, "return records paid amount as cash refund");
        ReturnReceipt? receipt = new ReturnService().GetReturnReceipt(result.ReturnId);
        AssertTrue(receipt != null, "settlement appears on return receipt");
        AssertClose(receipt!.DebtReducedUZS, 15000, "receipt debt settlement");
        AssertClose(receipt.CashRefundUZS, 5000, "receipt cash refund");
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
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
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

    private static void EncryptedCloudBackupRoundTripsAndRejectsTampering()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] archive = Encoding.UTF8.GetBytes("test backup archive payload");
        byte[] encrypted = CloudBackupCryptoService.EncryptPayload(archive, key);

        AssertEqual(Encoding.ASCII.GetString(encrypted, 0, 8), "OSONBK01", "encrypted backup header");
        byte[] decrypted = CloudBackupCryptoService.DecryptPayload(encrypted, key);
        AssertTrue(decrypted.AsSpan().SequenceEqual(archive), "encrypted backup round trip");

        encrypted[^1] ^= 0x01;
        bool rejected = false;
        try
        {
            _ = CloudBackupCryptoService.DecryptPayload(encrypted, key);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }

        AssertTrue(rejected, "tampered encrypted backup must be rejected");
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(decrypted);
        CryptographicOperations.ZeroMemory(archive);
    }

    private static void HomeDashboardStatusVisualsMatchState()
    {
        using var form = new MainForm(Admin);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(MainForm);
        var status = (Label)(type.GetField("_lblHomeStatus", flags)?.GetValue(form)
            ?? throw new InvalidOperationException("Status label not found."));
        var date = (Label)(type.GetField("_lblHomeDate", flags)?.GetValue(form)
            ?? throw new InvalidOperationException("Date label not found."));
        var icon = (PictureBox)(type.GetField("_picHomeStatusCheck", flags)?.GetValue(form)
            ?? throw new InvalidOperationException("Status icon not found."));
        var saleButton = (Button)(type.GetField("_btnNewSale", flags)?.GetValue(form)
            ?? throw new InvalidOperationException("Sale button not found."));
        var error = type.GetField("_homeDashboardErrorMessage", flags)
            ?? throw new InvalidOperationException("Status error field not found.");
        var update = type.GetMethod("UpdateHomeStatus", flags)
            ?? throw new InvalidOperationException("Status update method not found.");

        void Check(Color expected, bool healthy)
        {
            update.Invoke(form, null);
            AssertEqual(status.BackColor.ToArgb(), expected.ToArgb(), $"status text background ({status.Text})");
            AssertEqual(date.BackColor.ToArgb(), expected.ToArgb(), "status date background");
            AssertEqual(icon.BackColor.ToArgb(), expected.ToArgb(), "status icon background");
            AssertEqual(icon.Image != null, healthy, "status icon state");
        }

        Check(Color.FromArgb(250, 243, 224), false);
        AssertEqual(saleButton.BackColor.ToArgb(), Color.FromArgb(93, 95, 239).ToArgb(), "sale button colour");
        AssertEqual(saleButton.FlatAppearance.MouseOverBackColor.ToArgb(), Color.FromArgb(77, 77, 220).ToArgb(), "sale button hover colour");
        new DatabaseHelper().SaveCurrencyRate(new CurrencyRate { Rate = 12000, Date = DateTime.Now });
        new BackupService().CreateBackup();
        Check(Color.FromArgb(233, 244, 239), true);
        error.SetValue(form, "Sinov xatosi");
        Check(Color.FromArgb(253, 235, 234), false);
        UiTheme.ToggleTheme();
        try
        {
            var applyTheme = type.GetMethod("ApplyThemeStyles", flags)
                ?? throw new InvalidOperationException("Theme update method not found.");
            applyTheme.Invoke(form, null);
            AssertEqual(saleButton.BackColor.ToArgb(), Color.FromArgb(93, 95, 239).ToArgb(), "dark sale button colour");
            AssertEqual(saleButton.ForeColor.ToArgb(), Color.White.ToArgb(), "dark sale button text");
            var cards = (IList)(type.GetField("_dashboardCards", flags)?.GetValue(form)
                ?? throw new InvalidOperationException("Dashboard cards not found."));
            string[] darkIcons = { "pdf-metric-sales-dark.png", "pdf-metric-profit-dark.png", "pdf-metric-expense-dark.png", "pdf-metric-debt-dark.png" };
            AssertEqual(cards.Count, darkIcons.Length, "dashboard card count");
            for (int i = 0; i < cards.Count; i++)
            {
                var card = (ITuple)(cards[i] ?? throw new InvalidOperationException("Dashboard card missing."));
                var cardIcon = (PictureBox)(card[4] ?? throw new InvalidOperationException("Dashboard icon missing."));
                AssertTrue(ReferenceEquals(cardIcon.Image, BrandingAssets.TryLoadAssetImage(darkIcons[i])), $"dark icon {darkIcons[i]}");
            }
            Check(Color.FromArgb(57, 34, 34), false);
            error.SetValue(form, null);
            Check(Color.FromArgb(30, 52, 45), true);
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM CurrencyRates";
                command.ExecuteNonQuery();
            }
            Check(Color.FromArgb(54, 46, 31), false);
        }
        finally
        {
            UiTheme.ToggleTheme();
        }
    }

    private static void DashboardImagesAreNotClipped()
    {
        string[] names =
        {
            "pdf-footer-info.png", "pdf-button-plus.png", "pdf-button-refresh.png", "pdf-status-check.png",
            "pdf-header-location.png", "pdf-header-bell.png", "pdf-header-chevron.png",
            "pdf-metric-sales.png", "pdf-metric-profit.png", "pdf-metric-expense.png", "pdf-metric-debt.png",
            "pdf-quick-products.png", "pdf-quick-customers.png", "pdf-quick-expenses.png", "pdf-quick-arrow.png",
            "pdf-summary-products.png", "pdf-summary-debts.png",
            "pdf-footer-info-dark.png", "pdf-button-refresh-dark.png", "pdf-status-check-dark.png",
            "pdf-header-location-dark.png", "pdf-header-bell-dark.png", "pdf-header-chevron-dark.png",
            "pdf-metric-sales-dark.png", "pdf-metric-profit-dark.png", "pdf-metric-expense-dark.png", "pdf-metric-debt-dark.png",
            "pdf-quick-products-dark.png", "pdf-quick-customers-dark.png", "pdf-quick-expenses-dark.png", "pdf-quick-arrow-dark.png",
            "pdf-summary-products-dark.png", "pdf-summary-debts-dark.png"
        };
        foreach (string name in names)
        {
            Image image = BrandingAssets.TryLoadAssetImage(name)
                ?? throw new InvalidOperationException($"Missing image: {name}");
            using var bitmap = new Bitmap(image);
            for (int x = 0; x < bitmap.Width; x++)
            {
                AssertTrue(bitmap.GetPixel(x, 0).A == 0 && bitmap.GetPixel(x, bitmap.Height - 1).A == 0, $"{name}: vertical boundary");
            }
            for (int y = 0; y < bitmap.Height; y++)
            {
                AssertTrue(bitmap.GetPixel(0, y).A == 0 && bitmap.GetPixel(bitmap.Width - 1, y).A == 0, $"{name}: horizontal boundary");
            }
            bool hasVisiblePixel = false;
            for (int y = 1; y < bitmap.Height - 1 && !hasVisiblePixel; y++)
            {
                for (int x = 1; x < bitmap.Width - 1 && !hasVisiblePixel; x++)
                {
                    hasVisiblePixel = bitmap.GetPixel(x, y).A > 0;
                }
            }
            AssertTrue(hasVisiblePixel, $"{name}: image is empty");
        }
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
