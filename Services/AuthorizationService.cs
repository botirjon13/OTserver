using System;
using SantexnikaSRM.Data;
using SantexnikaSRM.Models;

namespace SantexnikaSRM.Services
{
    public static class AuthorizationService
    {
        public static bool IsAdmin(AppUser user)
        {
            return string.Equals(user.Role, DatabaseHelper.RoleAdmin, StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanViewProducts(AppUser user) => IsAdmin(user);

        public static bool CanManageProducts(AppUser user) => IsAdmin(user);

        public static bool CanCreateSales(AppUser user) => IsAdmin(user) || IsSeller(user);

        public static bool CanManageExpenses(AppUser user) => IsAdmin(user) || IsSeller(user);

        public static bool CanManageDebts(AppUser user) => IsAdmin(user) || IsSeller(user);

        public static bool CanViewReports(AppUser user) => IsAdmin(user);

        public static bool CanManageUsers(AppUser user) => IsAdmin(user);

        public static bool CanManageBackups(AppUser user) => IsAdmin(user);

        public static bool CanManagePricing(AppUser user) => IsAdmin(user);

        public static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new UnauthorizedAccessException(message);
            }
        }

        private static bool IsSeller(AppUser user)
        {
            return string.Equals(user.Role, DatabaseHelper.RoleSeller, StringComparison.OrdinalIgnoreCase);
        }
    }
}
