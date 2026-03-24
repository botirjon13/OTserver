using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Utils
{
    public static class ExcelExportHelper
    {
        public static void ExportInventory(List<Product> products, double usdRate, string filePath)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Ombor qoldiqlari");

            sheet.Cell(1, 1).Value = "Mahsulot nomi";
            sheet.Cell(1, 2).Value = "Mahsulot soni";
            sheet.Cell(1, 3).Value = "Kiritilgan narx";
            sheet.Cell(1, 4).Value = "Valyuta";
            sheet.Cell(1, 5).Value = "Mahsulot narxi UZS";

            double totalQty = 0;
            double totalUzsValue = 0;
            for (int i = 0; i < products.Count; i++)
            {
                Product p = products[i];
                int row = i + 2;
                double uzsPrice = p.PurchasePriceUZS;

                sheet.Cell(row, 1).Value = p.Name;
                sheet.Cell(row, 2).Value = p.QuantityUSD;
                sheet.Cell(row, 3).Value = p.PurchasePrice;
                sheet.Cell(row, 4).Value = p.PurchaseCurrency;
                sheet.Cell(row, 5).Value = uzsPrice;

                totalQty += p.QuantityUSD;
                totalUzsValue += uzsPrice * p.QuantityUSD;
            }

            int totalRow = products.Count + 3;
            sheet.Cell(totalRow, 1).Value = "JAMI";
            sheet.Cell(totalRow, 2).Value = totalQty;
            sheet.Cell(totalRow, 5).Value = totalUzsValue;
            sheet.Range(totalRow, 1, totalRow, 5).Style.Font.Bold = true;

            sheet.Column(2).Style.NumberFormat.Format = "#,##0.##";
            sheet.Column(3).Style.NumberFormat.Format = "#,##0.00";
            sheet.Column(5).Style.NumberFormat.Format = "#,##0";

            var range = sheet.Range(1, 1, Math.Max(totalRow, 2), 5);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            sheet.Range(1, 1, 1, 5).Style.Font.Bold = true;
            sheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        }

        public static void ExportReport(
            List<SaleReportRow> sales,
            List<Expense> expenses,
            double usdRate,
            string filePath)
        {
            using var workbook = new XLWorkbook();

            var salesSheet = workbook.Worksheets.Add("Savdo");
            salesSheet.Cell(1, 1).Value = "Mahsulot nomi";
            salesSheet.Cell(1, 2).Value = "Mahsulot soni";
            salesSheet.Cell(1, 3).Value = "Sotilgan narxi USD";
            salesSheet.Cell(1, 4).Value = "Sotilgan narxi UZS";
            salesSheet.Cell(1, 5).Value = "Qilingan foyda USD";
            salesSheet.Cell(1, 6).Value = "Qilingan foyda UZS";
            salesSheet.Range(1, 1, 1, 6).Style.Font.Bold = true;

            double totalQty = 0;
            double totalSoldUsd = 0;
            double totalSoldUzs = 0;
            double totalProfitUsd = 0;
            double totalProfitUzs = 0;
            for (int i = 0; i < sales.Count; i++)
            {
                SaleReportRow s = sales[i];
                int row = i + 2;
                salesSheet.Cell(row, 1).Value = s.ProductName;
                salesSheet.Cell(row, 2).Value = s.Quantity;
                salesSheet.Cell(row, 3).Value = s.SoldAmountUSD;
                salesSheet.Cell(row, 4).Value = s.SoldAmountUZS;
                salesSheet.Cell(row, 5).Value = s.ProfitUSD;
                salesSheet.Cell(row, 6).Value = s.ProfitUZS;

                totalQty += s.Quantity;
                totalSoldUsd += s.SoldAmountUSD;
                totalSoldUzs += s.SoldAmountUZS;
                totalProfitUsd += s.ProfitUSD;
                totalProfitUzs += s.ProfitUZS;
            }

            int salesTotalRow = sales.Count + 3;
            salesSheet.Cell(salesTotalRow, 1).Value = "JAMI";
            salesSheet.Cell(salesTotalRow, 2).Value = totalQty;
            salesSheet.Cell(salesTotalRow, 3).Value = totalSoldUsd;
            salesSheet.Cell(salesTotalRow, 4).Value = totalSoldUzs;
            salesSheet.Cell(salesTotalRow, 5).Value = totalProfitUsd;
            salesSheet.Cell(salesTotalRow, 6).Value = totalProfitUzs;
            salesSheet.Range(salesTotalRow, 1, salesTotalRow, 6).Style.Font.Bold = true;

            salesSheet.Column(2).Style.NumberFormat.Format = "#,##0.##";
            salesSheet.Column(3).Style.NumberFormat.Format = "#,##0.00";
            salesSheet.Column(4).Style.NumberFormat.Format = "#,##0";
            salesSheet.Column(5).Style.NumberFormat.Format = "#,##0.00";
            salesSheet.Column(6).Style.NumberFormat.Format = "#,##0";
            salesSheet.Columns().AdjustToContents();

            var expenseSheet = workbook.Worksheets.Add("Rasxodlar");
            expenseSheet.Cell(1, 1).Value = "Sana";
            expenseSheet.Cell(1, 2).Value = "Turi";
            expenseSheet.Cell(1, 3).Value = "Izohi";
            expenseSheet.Cell(1, 4).Value = "Summasi UZS";
            expenseSheet.Cell(1, 5).Value = "Summasi USD";
            expenseSheet.Range(1, 1, 1, 5).Style.Font.Bold = true;

            double totalExpenseUzs = 0;
            double totalExpenseUsd = 0;
            for (int i = 0; i < expenses.Count; i++)
            {
                Expense x = expenses[i];
                int row = i + 2;
                double amountUsd = usdRate > 0 ? x.AmountUZS / usdRate : 0;
                expenseSheet.Cell(row, 1).Value = x.Date.ToString("yyyy-MM-dd HH:mm:ss");
                expenseSheet.Cell(row, 2).Value = x.Type;
                expenseSheet.Cell(row, 3).Value = x.Description;
                expenseSheet.Cell(row, 4).Value = x.AmountUZS;
                expenseSheet.Cell(row, 5).Value = amountUsd;

                totalExpenseUzs += x.AmountUZS;
                totalExpenseUsd += amountUsd;
            }

            int expenseTotalRow = expenses.Count + 3;
            expenseSheet.Cell(expenseTotalRow, 1).Value = "JAMI";
            expenseSheet.Cell(expenseTotalRow, 4).Value = totalExpenseUzs;
            expenseSheet.Cell(expenseTotalRow, 5).Value = totalExpenseUsd;
            expenseSheet.Range(expenseTotalRow, 1, expenseTotalRow, 5).Style.Font.Bold = true;

            expenseSheet.Column(4).Style.NumberFormat.Format = "#,##0";
            expenseSheet.Column(5).Style.NumberFormat.Format = "#,##0.00";
            expenseSheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        }
    }
}
