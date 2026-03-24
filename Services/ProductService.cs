using Microsoft.Data.Sqlite;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SantexnikaSRM.Services
{
    public class ProductService
    {
        public void Add(Product product, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageProducts(currentUser),
                "Mahsulot qo'shish huquqi mavjud emas.");

            using var connection = Database.GetConnection();
            connection.Open();

            string currency = NormalizeCurrency(product.PurchaseCurrency);
            double rate = GetLatestRate(connection);
            (double purchasePrice, double purchasePriceUzs, double purchasePriceUsd) = NormalizePriceTuple(currency, product.PurchasePrice, rate);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Products (Name, PurchaseCurrency, PurchasePrice, PurchasePriceUZS, PurchasePriceUSD, QuantityUSD)
                VALUES (@name, @currency, @purchasePrice, @purchasePriceUzs, @purchasePriceUsd, @qty)";

            cmd.Parameters.AddWithValue("@name", product.Name);
            cmd.Parameters.AddWithValue("@currency", currency);
            cmd.Parameters.AddWithValue("@purchasePrice", purchasePrice);
            cmd.Parameters.AddWithValue("@purchasePriceUzs", purchasePriceUzs);
            cmd.Parameters.AddWithValue("@purchasePriceUsd", purchasePriceUsd);
            cmd.Parameters.AddWithValue("@qty", product.QuantityUSD);

            cmd.ExecuteNonQuery();
        }

        public List<Product> GetAll()
        {
            var list = new List<Product>();

            using var connection = Database.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Products";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(MapProduct(reader));
            }

            return list;
        }

        public void UpdateProduct(int productId, string? newName, double? newPurchasePrice, double? newQuantity, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageProducts(currentUser),
                "Mahsulotni tahrirlash huquqi mavjud emas.");

            if (productId <= 0)
            {
                throw new Exception("Noto'g'ri mahsulot identifikatori.");
            }

            if (newName == null && newPurchasePrice == null && newQuantity == null)
            {
                throw new Exception("Kamida bitta maydon tanlanishi kerak.");
            }

            using var connection = Database.GetConnection();
            connection.Open();

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Name, PurchaseCurrency, PurchasePrice, PurchasePriceUZS, PurchasePriceUSD, QuantityUSD FROM Products WHERE Id=@id";
            selectCmd.Parameters.AddWithValue("@id", productId);

            string currentName;
            string currentCurrency;
            double currentPurchasePrice;
            double currentQty;

            using (var reader = selectCmd.ExecuteReader())
            {
                if (!reader.Read())
                {
                    throw new Exception("Mahsulot topilmadi.");
                }

                currentName = reader.GetString(0);
                currentCurrency = NormalizeCurrency(reader.GetString(1));
                currentPurchasePrice = reader.GetDouble(2);
                _ = reader.GetDouble(3);
                _ = reader.GetDouble(4);
                currentQty = reader.GetDouble(5);
            }

            string nameToSave = newName == null ? currentName : newName.Trim();
            double enteredPriceToSave = newPurchasePrice ?? currentPurchasePrice;
            double qtyToSave = newQuantity ?? currentQty;
            double rate = GetLatestRate(connection);
            (double purchasePriceToSave, double purchasePriceUzsToSave, double purchasePriceUsdToSave) =
                NormalizePriceTuple(currentCurrency, enteredPriceToSave, rate);

            if (string.IsNullOrWhiteSpace(nameToSave))
            {
                throw new Exception("Mahsulot nomi bo'sh bo'lmasligi kerak.");
            }

            if (purchasePriceToSave <= 0 || purchasePriceUzsToSave <= 0 || purchasePriceUsdToSave <= 0)
            {
                throw new Exception("Mahsulot narxi musbat son bo'lishi kerak.");
            }

            if (qtyToSave < 0)
            {
                throw new Exception("Mahsulot soni manfiy bo'lmasligi kerak.");
            }

            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Products
                SET Name=@name,
                    PurchasePrice=@purchasePrice,
                    PurchasePriceUZS=@purchasePriceUzs,
                    PurchasePriceUSD=@purchasePriceUsd,
                    QuantityUSD=@qty
                WHERE Id=@id";
            updateCmd.Parameters.AddWithValue("@name", nameToSave);
            updateCmd.Parameters.AddWithValue("@purchasePrice", purchasePriceToSave);
            updateCmd.Parameters.AddWithValue("@purchasePriceUzs", purchasePriceUzsToSave);
            updateCmd.Parameters.AddWithValue("@purchasePriceUsd", purchasePriceUsdToSave);
            updateCmd.Parameters.AddWithValue("@qty", qtyToSave);
            updateCmd.Parameters.AddWithValue("@id", productId);
            updateCmd.ExecuteNonQuery();
        }

        public void UpdateQuantity(int productId, double newQuantity)
        {
            using var connection = Database.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Products SET QuantityUSD=@qty WHERE Id=@id";
            cmd.Parameters.AddWithValue("@qty", newQuantity);
            cmd.Parameters.AddWithValue("@id", productId);

            cmd.ExecuteNonQuery();
        }

        public void RemoveFromList(int productId, AppUser currentUser)
        {
            AuthorizationService.Require(
                AuthorizationService.CanManageProducts(currentUser),
                "Mahsulotni ro'yxatdan chiqarish huquqi mavjud emas.");

            if (productId <= 0)
            {
                throw new Exception("Noto'g'ri mahsulot identifikatori.");
            }

            using var connection = Database.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Products SET QuantityUSD = 0 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", productId);

            int affected = cmd.ExecuteNonQuery();
            if (affected <= 0)
            {
                throw new Exception("Mahsulot topilmadi.");
            }
        }

        public Product? GetById(int id)
        {
            using var connection = Database.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Products WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return MapProduct(reader);
            }

            return null;
        }

        private static Product MapProduct(SqliteDataReader reader)
        {
            int idIndex = reader.GetOrdinal("Id");
            int nameIndex = reader.GetOrdinal("Name");
            int qtyIndex = reader.GetOrdinal("QuantityUSD");
            int currencyIndex = reader.GetOrdinal("PurchaseCurrency");
            int purchasePriceIndex = reader.GetOrdinal("PurchasePrice");
            int purchasePriceUzsIndex = reader.GetOrdinal("PurchasePriceUZS");
            int purchasePriceUsdIndex = reader.GetOrdinal("PurchasePriceUSD");

            return new Product
            {
                Id = reader.GetInt32(idIndex),
                Name = reader.GetString(nameIndex),
                PurchaseCurrency = NormalizeCurrency(reader.IsDBNull(currencyIndex) ? "USD" : reader.GetString(currencyIndex)),
                PurchasePrice = reader.IsDBNull(purchasePriceIndex) ? 0 : reader.GetDouble(purchasePriceIndex),
                PurchasePriceUZS = reader.IsDBNull(purchasePriceUzsIndex) ? 0 : reader.GetDouble(purchasePriceUzsIndex),
                PurchasePriceUSD = reader.IsDBNull(purchasePriceUsdIndex) ? 0 : reader.GetDouble(purchasePriceUsdIndex),
                QuantityUSD = reader.GetDouble(qtyIndex)
            };
        }

        private static string NormalizeCurrency(string? currency)
        {
            return string.Equals(currency?.Trim(), "UZS", StringComparison.OrdinalIgnoreCase) ? "UZS" : "USD";
        }

        private static double GetLatestRate(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Rate FROM CurrencyRates ORDER BY Date DESC LIMIT 1";
            object? result = cmd.ExecuteScalar();

            if (result != null && result != DBNull.Value)
            {
                double parsed = Convert.ToDouble(result, CultureInfo.InvariantCulture);
                if (parsed > 0)
                {
                    return parsed;
                }
            }

            return 12500;
        }

        private static (double PurchasePrice, double PurchasePriceUzs, double PurchasePriceUsd) NormalizePriceTuple(
            string currency,
            double enteredPrice,
            double rate)
        {
            if (enteredPrice <= 0)
            {
                throw new Exception("Mahsulot narxi musbat son bo'lishi kerak.");
            }

            double safeRate = rate > 0 ? rate : 12500;
            if (currency == "UZS")
            {
                double uzs = enteredPrice;
                double usd = uzs / safeRate;
                return (enteredPrice, uzs, usd);
            }

            double usdPrice = enteredPrice;
            double uzsPrice = usdPrice * safeRate;
            return (enteredPrice, uzsPrice, usdPrice);
        }
    }
}
