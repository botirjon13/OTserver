using Microsoft.Data.Sqlite;

namespace SantexnikaSRM.Data
{
    public static class DbInitializer
    {
        public static void Initialize()
        {
            using var connection = Database.GetConnection();
            connection.Open();

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                pragma.ExecuteNonQuery();
            }

            var command = connection.CreateCommand();

            command.CommandText = @"

            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                PurchaseCurrency TEXT NOT NULL DEFAULT 'USD' CHECK(PurchaseCurrency IN ('USD', 'UZS')),
                PurchasePrice REAL NOT NULL CHECK(PurchasePrice > 0),
                PurchasePriceUZS REAL NOT NULL CHECK(PurchasePriceUZS > 0),
                PurchasePriceUSD REAL NOT NULL CHECK(PurchasePriceUSD > 0),
                QuantityUSD REAL NOT NULL CHECK(QuantityUSD >= 0),
                ImagePath TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Sales (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT NOT NULL,
                TotalUZS REAL NOT NULL CHECK(TotalUZS >= 0),
                SubtotalUZS REAL NOT NULL DEFAULT 0 CHECK(SubtotalUZS >= 0),
                DiscountType TEXT NOT NULL DEFAULT 'None',
                DiscountValue REAL NOT NULL DEFAULT 0 CHECK(DiscountValue >= 0),
                DiscountUZS REAL NOT NULL DEFAULT 0 CHECK(DiscountUZS >= 0),
                ProfitUZS REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS SaleItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SaleId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                Quantity REAL NOT NULL CHECK(Quantity > 0),
                SellPriceUZS REAL NOT NULL CHECK(SellPriceUZS > 0),
                DiscountUZS REAL NOT NULL DEFAULT 0 CHECK(DiscountUZS >= 0),
                FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE,
                FOREIGN KEY (ProductId) REFERENCES Products(Id)
            );

            CREATE TABLE IF NOT EXISTS Expenses (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT NOT NULL,
                Type TEXT NOT NULL,
                Description TEXT,
                AmountUZS REAL NOT NULL CHECK(AmountUZS > 0)
            );

            CREATE TABLE IF NOT EXISTS CurrencyRates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Rate REAL NOT NULL CHECK(Rate > 0),
                Date TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS FiscalSettings (
                Id INTEGER PRIMARY KEY CHECK(Id = 1),
                BusinessName TEXT NOT NULL DEFAULT '',
                TIN TEXT NOT NULL DEFAULT '',
                StoreAddress TEXT NOT NULL DEFAULT '',
                KkmNumber TEXT NOT NULL DEFAULT '',
                IsVatPayer INTEGER NOT NULL DEFAULT 0,
                VatRatePercent REAL NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS SaleReceipts (
                SaleId INTEGER PRIMARY KEY,
                ReceiptNumber TEXT NOT NULL,
                IssuedAt TEXT NOT NULL,
                PaymentType TEXT NOT NULL,
                FiscalSign TEXT NOT NULL,
                QrData TEXT NOT NULL,
                BusinessName TEXT NOT NULL,
                TIN TEXT NOT NULL,
                StoreAddress TEXT NOT NULL,
                KkmNumber TEXT NOT NULL,
                IsVatPayer INTEGER NOT NULL,
                VatRatePercent REAL NOT NULL,
                VatAmount REAL NOT NULL,
                TotalUZS REAL NOT NULL,
                FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NOT NULL,
                Phone TEXT NOT NULL DEFAULT '',
                Note TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Debts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SaleId INTEGER NOT NULL UNIQUE,
                CustomerId INTEGER NOT NULL,
                TotalAmountUZS REAL NOT NULL CHECK(TotalAmountUZS > 0),
                PaidAmountUZS REAL NOT NULL CHECK(PaidAmountUZS >= 0),
                RemainingAmountUZS REAL NOT NULL CHECK(RemainingAmountUZS >= 0),
                DueDate TEXT NOT NULL,
                Status TEXT NOT NULL CHECK(Status IN ('Open', 'Closed', 'Overdue')),
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE,
                FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
            );

            CREATE TABLE IF NOT EXISTS DebtPayments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DebtId INTEGER NOT NULL,
                AmountUZS REAL NOT NULL CHECK(AmountUZS > 0),
                PaymentType TEXT NOT NULL,
                Comment TEXT NOT NULL DEFAULT '',
                PaymentDate TEXT NOT NULL,
                FOREIGN KEY (DebtId) REFERENCES Debts(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS SaleCustomers (
                SaleId INTEGER PRIMARY KEY,
                CustomerId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE,
                FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
            );

            CREATE TABLE IF NOT EXISTS PricingSettings (
                Id INTEGER PRIMARY KEY CHECK(Id = 1),
                SuggestedMarkupPercent REAL NOT NULL DEFAULT 20,
                AutoFillSuggestedPrice INTEGER NOT NULL DEFAULT 1,
                QuickDiscountEnabled INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS Returns (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SaleId INTEGER NOT NULL,
                ReturnDate TEXT NOT NULL,
                Reason TEXT NOT NULL DEFAULT '',
                SubtotalUZS REAL NOT NULL CHECK(SubtotalUZS >= 0),
                DiscountUZS REAL NOT NULL CHECK(DiscountUZS >= 0),
                TotalUZS REAL NOT NULL CHECK(TotalUZS >= 0),
                ProfitReductionUZS REAL NOT NULL DEFAULT 0,
                CreatedByUser TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ReturnItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ReturnId INTEGER NOT NULL,
                SaleItemId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                Quantity REAL NOT NULL CHECK(Quantity > 0),
                UnitPriceUZS REAL NOT NULL CHECK(UnitPriceUZS >= 0),
                DiscountUZS REAL NOT NULL CHECK(DiscountUZS >= 0),
                LineTotalUZS REAL NOT NULL CHECK(LineTotalUZS >= 0),
                FOREIGN KEY (ReturnId) REFERENCES Returns(Id) ON DELETE CASCADE,
                FOREIGN KEY (SaleItemId) REFERENCES SaleItems(Id),
                FOREIGN KEY (ProductId) REFERENCES Products(Id)
            );

            ";

            command.ExecuteNonQuery();
            EnsureIndexesAndLegacyGuards(connection);
        }

        private static void EnsureIndexesAndLegacyGuards(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE INDEX IF NOT EXISTS IX_Products_Name ON Products(Name);
                CREATE INDEX IF NOT EXISTS IX_Sales_Date ON Sales(Date);
                CREATE INDEX IF NOT EXISTS IX_Expenses_Date ON Expenses(Date);
                CREATE INDEX IF NOT EXISTS IX_SaleItems_SaleId ON SaleItems(SaleId);
                CREATE INDEX IF NOT EXISTS IX_SaleItems_ProductId ON SaleItems(ProductId);
                CREATE INDEX IF NOT EXISTS IX_SaleReceipts_IssuedAt ON SaleReceipts(IssuedAt);
                CREATE INDEX IF NOT EXISTS IX_Customers_FullName ON Customers(FullName);
                CREATE INDEX IF NOT EXISTS IX_Customers_Phone ON Customers(Phone);
                CREATE INDEX IF NOT EXISTS IX_Debts_Status ON Debts(Status);
                CREATE INDEX IF NOT EXISTS IX_Debts_DueDate ON Debts(DueDate);
                CREATE INDEX IF NOT EXISTS IX_Debts_CustomerId ON Debts(CustomerId);
                CREATE INDEX IF NOT EXISTS IX_DebtPayments_DebtId ON DebtPayments(DebtId);
                CREATE INDEX IF NOT EXISTS IX_SaleCustomers_CustomerId ON SaleCustomers(CustomerId);
                CREATE INDEX IF NOT EXISTS IX_Returns_SaleId ON Returns(SaleId);
                CREATE INDEX IF NOT EXISTS IX_Returns_ReturnDate ON Returns(ReturnDate);
                CREATE INDEX IF NOT EXISTS IX_ReturnItems_ReturnId ON ReturnItems(ReturnId);
                CREATE INDEX IF NOT EXISTS IX_ReturnItems_SaleItemId ON ReturnItems(SaleItemId);
            ";
            cmd.ExecuteNonQuery();

            var seedSettings = connection.CreateCommand();
            seedSettings.CommandText = @"
                INSERT INTO FiscalSettings (Id, BusinessName, TIN, StoreAddress, KkmNumber, IsVatPayer, VatRatePercent)
                SELECT 1, '', '', '', '', 0, 0
                WHERE NOT EXISTS (SELECT 1 FROM FiscalSettings WHERE Id = 1);";
            seedSettings.ExecuteNonQuery();

            ExecuteIgnoreErrors(connection, "ALTER TABLE PricingSettings ADD COLUMN QuickDiscountEnabled INTEGER NOT NULL DEFAULT 1;");

            var seedPricing = connection.CreateCommand();
            seedPricing.CommandText = @"
                INSERT INTO PricingSettings (Id, SuggestedMarkupPercent, AutoFillSuggestedPrice, QuickDiscountEnabled)
                SELECT 1, 20, 1, 1
                WHERE NOT EXISTS (SELECT 1 FROM PricingSettings WHERE Id = 1);";
            seedPricing.ExecuteNonQuery();

            // Legacy bazalar uchun tezkor chegirma maydonlarini qo'shamiz.
            ExecuteIgnoreErrors(connection, "ALTER TABLE Sales ADD COLUMN SubtotalUZS REAL NOT NULL DEFAULT 0;");
            ExecuteIgnoreErrors(connection, "ALTER TABLE Sales ADD COLUMN DiscountType TEXT NOT NULL DEFAULT 'None';");
            ExecuteIgnoreErrors(connection, "ALTER TABLE Sales ADD COLUMN DiscountValue REAL NOT NULL DEFAULT 0;");
            ExecuteIgnoreErrors(connection, "ALTER TABLE Sales ADD COLUMN DiscountUZS REAL NOT NULL DEFAULT 0;");
            ExecuteIgnoreErrors(connection, "ALTER TABLE SaleItems ADD COLUMN DiscountUZS REAL NOT NULL DEFAULT 0;");
            ExecuteIgnoreErrors(connection, @"
                CREATE TABLE IF NOT EXISTS Returns (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SaleId INTEGER NOT NULL,
                    ReturnDate TEXT NOT NULL,
                    Reason TEXT NOT NULL DEFAULT '',
                    SubtotalUZS REAL NOT NULL CHECK(SubtotalUZS >= 0),
                    DiscountUZS REAL NOT NULL CHECK(DiscountUZS >= 0),
                    TotalUZS REAL NOT NULL CHECK(TotalUZS >= 0),
                    ProfitReductionUZS REAL NOT NULL DEFAULT 0,
                    CreatedByUser TEXT NOT NULL DEFAULT '',
                    FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE
                );");
            ExecuteIgnoreErrors(connection, @"
                CREATE TABLE IF NOT EXISTS ReturnItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ReturnId INTEGER NOT NULL,
                    SaleItemId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    Quantity REAL NOT NULL CHECK(Quantity > 0),
                    UnitPriceUZS REAL NOT NULL CHECK(UnitPriceUZS >= 0),
                    DiscountUZS REAL NOT NULL CHECK(DiscountUZS >= 0),
                    LineTotalUZS REAL NOT NULL CHECK(LineTotalUZS >= 0),
                    FOREIGN KEY (ReturnId) REFERENCES Returns(Id) ON DELETE CASCADE,
                    FOREIGN KEY (SaleItemId) REFERENCES SaleItems(Id),
                    FOREIGN KEY (ProductId) REFERENCES Products(Id)
                );");
            ExecuteIgnoreErrors(connection, "CREATE INDEX IF NOT EXISTS IX_Returns_SaleId ON Returns(SaleId);");
            ExecuteIgnoreErrors(connection, "CREATE INDEX IF NOT EXISTS IX_Returns_ReturnDate ON Returns(ReturnDate);");
            ExecuteIgnoreErrors(connection, "CREATE INDEX IF NOT EXISTS IX_ReturnItems_ReturnId ON ReturnItems(ReturnId);");
            ExecuteIgnoreErrors(connection, "CREATE INDEX IF NOT EXISTS IX_ReturnItems_SaleItemId ON ReturnItems(SaleItemId);");
            ExecuteIgnoreErrors(connection, "ALTER TABLE Products ADD COLUMN PurchaseCurrency TEXT NOT NULL DEFAULT 'USD';");
            ExecuteIgnoreErrors(connection, "ALTER TABLE Products ADD COLUMN PurchasePrice REAL NOT NULL DEFAULT 0;");
            ExecuteIgnoreErrors(connection, "ALTER TABLE Products ADD COLUMN PurchasePriceUZS REAL NOT NULL DEFAULT 0;");
            ExecuteIgnoreErrors(connection, "ALTER TABLE Products ADD COLUMN ImagePath TEXT NOT NULL DEFAULT '';");
            ExecuteIgnoreErrors(connection, "UPDATE Products SET PurchaseCurrency='USD' WHERE PurchaseCurrency IS NULL OR TRIM(PurchaseCurrency) = '';");
            ExecuteIgnoreErrors(connection, "UPDATE Products SET PurchasePrice=PurchasePriceUSD WHERE PurchasePrice <= 0;");
            ExecuteIgnoreErrors(connection, @"
                UPDATE Products
                SET PurchasePriceUZS = PurchasePriceUSD * COALESCE((SELECT Rate FROM CurrencyRates ORDER BY Date DESC LIMIT 1), 12500)
                WHERE PurchasePriceUZS <= 0;");

            var fillSubtotal = connection.CreateCommand();
            fillSubtotal.CommandText = "UPDATE Sales SET SubtotalUZS = TotalUZS WHERE SubtotalUZS <= 0;";
            fillSubtotal.ExecuteNonQuery();

            // Legacy jadvallar oldindan yaratilgan bo'lsa ham minimal integrity saqlanishi uchun triggerlar.
            ExecuteIgnoreErrors(connection, @"
                CREATE TRIGGER IF NOT EXISTS trg_products_check_insert
                BEFORE INSERT ON Products
                FOR EACH ROW
                WHEN NEW.PurchasePriceUSD <= 0 OR NEW.PurchasePrice <= 0 OR NEW.PurchasePriceUZS <= 0
                    OR NEW.QuantityUSD < 0
                    OR NEW.PurchaseCurrency NOT IN ('USD', 'UZS')
                BEGIN
                    SELECT RAISE(ABORT, 'Products uchun noto''g''ri qiymat');
                END;");

            ExecuteIgnoreErrors(connection, @"
                CREATE TRIGGER IF NOT EXISTS trg_products_check_update
                BEFORE UPDATE ON Products
                FOR EACH ROW
                WHEN NEW.PurchasePriceUSD <= 0 OR NEW.PurchasePrice <= 0 OR NEW.PurchasePriceUZS <= 0
                    OR NEW.QuantityUSD < 0
                    OR NEW.PurchaseCurrency NOT IN ('USD', 'UZS')
                BEGIN
                    SELECT RAISE(ABORT, 'Products uchun noto''g''ri qiymat');
                END;");

            ExecuteIgnoreErrors(connection, @"
                CREATE TRIGGER IF NOT EXISTS trg_saleitems_refs_insert
                BEFORE INSERT ON SaleItems
                FOR EACH ROW
                WHEN NEW.Quantity <= 0 OR NEW.SellPriceUZS <= 0
                    OR (SELECT COUNT(*) FROM Sales WHERE Id = NEW.SaleId) = 0
                    OR (SELECT COUNT(*) FROM Products WHERE Id = NEW.ProductId) = 0
                BEGIN
                    SELECT RAISE(ABORT, 'SaleItems ma''lumotlari yaroqsiz');
                END;");

            ExecuteIgnoreErrors(connection, @"
                CREATE TRIGGER IF NOT EXISTS trg_expenses_check_insert
                BEFORE INSERT ON Expenses
                FOR EACH ROW
                WHEN NEW.AmountUZS <= 0
                BEGIN
                    SELECT RAISE(ABORT, 'Expenses summasi musbat bo''lishi kerak');
                END;");

            ExecuteIgnoreErrors(connection, @"
                CREATE TRIGGER IF NOT EXISTS trg_expenses_check_update
                BEFORE UPDATE ON Expenses
                FOR EACH ROW
                WHEN NEW.AmountUZS <= 0
                BEGIN
                    SELECT RAISE(ABORT, 'Expenses summasi musbat bo''lishi kerak');
                END;");
        }

        private static void ExecuteIgnoreErrors(SqliteConnection connection, string sql)
        {
            try
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // Legacy bazada ayrim triggerlar allaqachon bo'lishi yoki sxema farqi bo'lishi mumkin.
            }
        }
    }
}
