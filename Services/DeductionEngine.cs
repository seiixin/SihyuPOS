#nullable enable
using MySql.Data.MySqlClient;
using SihyuPOSPayroll.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SihyuPOSPayroll.Services
{
    // =========================================================================
    // IDeductionStrategy
    // =========================================================================

    /// <summary>
    /// Strategy interface for inventory deduction. Implementations differ by
    /// system mode: <see cref="StoreDeductionStrategy"/> for StoreMode and
    /// <see cref="RestaurantDeductionStrategy"/> for RestaurantMode.
    /// </summary>
    public interface IDeductionStrategy
    {
        /// <summary>
        /// Applies inventory deductions for the given order items within the
        /// supplied open connection and active transaction. Low-stock alerts are
        /// surfaced by invoking <paramref name="onLowStock"/>.
        /// </summary>
        void Apply(
            MySqlConnection conn,
            MySqlTransaction tx,
            IEnumerable<OrderItemModel> items,
            OrderType orderType,
            Action<LowStockAlert> onLowStock);
    }

    // =========================================================================
    // StoreDeductionStrategy  (Task 9.2)
    // =========================================================================

    /// <summary>
    /// Deducts inventory by decrementing <c>inventory_items.Quantity</c> by the
    /// ordered quantity for each product. Used when <see cref="SystemMode"/> is
    /// <see cref="SystemMode.StoreMode"/>.
    /// </summary>
    public class StoreDeductionStrategy : IDeductionStrategy
    {
        private const int LowStockThreshold = 10;

        public void Apply(
            MySqlConnection conn,
            MySqlTransaction tx,
            IEnumerable<OrderItemModel> items,
            OrderType orderType,
            Action<LowStockAlert> onLowStock)
        {
            foreach (var item in items)
            {
                int productId = item.ProductId;
                int orderedQty = item.Quantity;

                // ── Deduct ──────────────────────────────────────────────────
                int rowsAffected;
                using (var updateCmd = new MySqlCommand(
                    "UPDATE inventory_items SET Quantity = Quantity - @orderedQty WHERE Id = @productId;",
                    conn, tx))
                {
                    updateCmd.Parameters.AddWithValue("@orderedQty", orderedQty);
                    updateCmd.Parameters.AddWithValue("@productId", productId);
                    rowsAffected = updateCmd.ExecuteNonQuery();
                }

                if (rowsAffected == 0)
                {
                    Debug.WriteLine($"[StoreDeduction] Missing inventory for product {productId}");
                    continue;
                }

                // ── Low-stock check ─────────────────────────────────────────
                using var selectCmd = new MySqlCommand(
                    "SELECT Id, Name, Quantity FROM inventory_items WHERE Id = @productId LIMIT 1;",
                    conn, tx);
                selectCmd.Parameters.AddWithValue("@productId", productId);

                using var rdr = selectCmd.ExecuteReader();
                if (rdr.Read())
                {
                    int remaining = rdr.GetInt32("Quantity");
                    string name = rdr.IsDBNull(rdr.GetOrdinal("Name")) ? $"Product {productId}" : rdr.GetString("Name");

                    if (remaining < LowStockThreshold)
                        onLowStock(new LowStockAlert(productId, name, remaining));
                }
            }
        }
    }

    // =========================================================================
    // RestaurantDeductionStrategy  (Tasks 10.1 + 10.4)
    // =========================================================================

    /// <summary>
    /// Deducts raw ingredient batches from <c>IngredientBatches</c> using FIFO
    /// batch ordering. For Take-Out orders also deducts packaging materials from
    /// <c>inventory_items</c>. Used when <see cref="SystemMode"/> is
    /// <see cref="SystemMode.RestaurantMode"/>.
    /// </summary>
    public class RestaurantDeductionStrategy : IDeductionStrategy
    {
        private const int LowStockThreshold = 10;

        public void Apply(
            MySqlConnection conn,
            MySqlTransaction tx,
            IEnumerable<OrderItemModel> items,
            OrderType orderType,
            Action<LowStockAlert> onLowStock)
        {
            foreach (var item in items)
            {
                int menuItemId = item.ProductId;   // ProductId maps to menu item id
                int orderedQty = item.Quantity;

                // ── 1. Fetch recipe ingredients for this menu item ──────────
                var recipes = new List<(int IngredientId, decimal QuantityPerServing)>();

                using (var recipeCmd = new MySqlCommand(
                    @"SELECT ir.IngredientId, ir.QuantityPerServing
                      FROM IngredientRecipes ir
                      WHERE ir.MenuItemId = @menuItemId;",
                    conn, tx))
                {
                    recipeCmd.Parameters.AddWithValue("@menuItemId", menuItemId);
                    using var recipeRdr = recipeCmd.ExecuteReader();
                    while (recipeRdr.Read())
                    {
                        int ingId = recipeRdr.GetInt32("IngredientId");
                        decimal qps = recipeRdr.GetDecimal("QuantityPerServing");
                        recipes.Add((ingId, qps));
                    }
                }

                if (recipes.Count == 0)
                {
                    Debug.WriteLine($"[RestaurantDeduction] Missing recipe for menuItem {menuItemId}");
                    // Raise sentinel alert so callers know recipe data is missing
                    onLowStock(new LowStockAlert(menuItemId, $"MenuItem {menuItemId}", -1));
                    // Still attempt packaging deduction for Take-Out below
                }
                else
                {
                    // ── 2. FIFO batch deduction for each ingredient ─────────
                    foreach (var (ingredientId, quantityPerServing) in recipes)
                    {
                        decimal consumption = quantityPerServing * orderedQty;

                        // Fetch FIFO-ordered batches
                        var batches = new List<(int BatchId, decimal YieldPerBatch)>();
                        using (var batchCmd = new MySqlCommand(
                            @"SELECT Id, YieldPerBatch
                              FROM IngredientBatches
                              WHERE IngredientId = @ingredientId
                              ORDER BY CreatedAt ASC;",
                            conn, tx))
                        {
                            batchCmd.Parameters.AddWithValue("@ingredientId", ingredientId);
                            using var batchRdr = batchCmd.ExecuteReader();
                            while (batchRdr.Read())
                            {
                                batches.Add((batchRdr.GetInt32("Id"), batchRdr.GetDecimal("YieldPerBatch")));
                            }
                        }

                        // Deduct consumption across batches (FIFO)
                        decimal remaining = consumption;
                        foreach (var (batchId, yield) in batches)
                        {
                            if (remaining <= 0m) break;

                            if (yield <= remaining)
                            {
                                // Exhaust entire batch → delete it
                                remaining -= yield;

                                using var deleteCmd = new MySqlCommand(
                                    "DELETE FROM IngredientBatches WHERE Id = @batchId;",
                                    conn, tx);
                                deleteCmd.Parameters.AddWithValue("@batchId", batchId);
                                deleteCmd.ExecuteNonQuery();
                            }
                            else
                            {
                                // Partial deduction
                                using var updateBatchCmd = new MySqlCommand(
                                    "UPDATE IngredientBatches SET YieldPerBatch = YieldPerBatch - @consumed WHERE Id = @batchId;",
                                    conn, tx);
                                updateBatchCmd.Parameters.AddWithValue("@consumed", remaining);
                                updateBatchCmd.Parameters.AddWithValue("@batchId", batchId);
                                updateBatchCmd.ExecuteNonQuery();
                                remaining = 0m;
                            }
                        }

                        // ── 3. Low-stock check: sum remaining yield for this ingredient ──
                        decimal totalRemainingYield;
                        using (var sumCmd = new MySqlCommand(
                            @"SELECT COALESCE(SUM(YieldPerBatch), 0)
                              FROM IngredientBatches
                              WHERE IngredientId = @ingredientId;",
                            conn, tx))
                        {
                            sumCmd.Parameters.AddWithValue("@ingredientId", ingredientId);
                            totalRemainingYield = Convert.ToDecimal(sumCmd.ExecuteScalar());
                        }

                        if (totalRemainingYield < LowStockThreshold)
                        {
                            // Fetch ingredient name from inventory_items
                            string ingName = $"Ingredient {ingredientId}";
                            using (var nameCmd = new MySqlCommand(
                                "SELECT Name FROM inventory_items WHERE Id = @id LIMIT 1;",
                                conn, tx))
                            {
                                nameCmd.Parameters.AddWithValue("@id", ingredientId);
                                var nameVal = nameCmd.ExecuteScalar();
                                if (nameVal != null && nameVal != DBNull.Value)
                                    ingName = nameVal.ToString()!;
                            }

                            onLowStock(new LowStockAlert(ingredientId, ingName, (int)totalRemainingYield));
                        }
                    }
                }

                // ── 4. Packaging material deduction (Take-Out only) ─────────
                if (orderType == OrderType.TakeOut)
                {
                    var packagingMaterials = new List<(int InventoryItemId, int QuantityPerOrder)>();

                    using (var pkgCmd = new MySqlCommand(
                        @"SELECT InventoryItemId, QuantityPerOrder
                          FROM PackagingMaterials
                          WHERE MenuItemId = @menuItemId;",
                        conn, tx))
                    {
                        pkgCmd.Parameters.AddWithValue("@menuItemId", menuItemId);
                        using var pkgRdr = pkgCmd.ExecuteReader();
                        while (pkgRdr.Read())
                        {
                            packagingMaterials.Add((
                                pkgRdr.GetInt32("InventoryItemId"),
                                pkgRdr.GetInt32("QuantityPerOrder")));
                        }
                    }

                    foreach (var (inventoryItemId, quantityPerOrder) in packagingMaterials)
                    {
                        int rowsAffected;
                        using (var pkgUpdateCmd = new MySqlCommand(
                            "UPDATE inventory_items SET Quantity = Quantity - @quantityPerOrder WHERE Id = @inventoryItemId;",
                            conn, tx))
                        {
                            pkgUpdateCmd.Parameters.AddWithValue("@quantityPerOrder", quantityPerOrder);
                            pkgUpdateCmd.Parameters.AddWithValue("@inventoryItemId", inventoryItemId);
                            rowsAffected = pkgUpdateCmd.ExecuteNonQuery();
                        }

                        if (rowsAffected == 0)
                        {
                            Debug.WriteLine($"[RestaurantDeduction] Missing packaging for menuItem {menuItemId}");
                            continue;
                        }

                        // Low-stock check for packaging material
                        using var pkgSelectCmd = new MySqlCommand(
                            "SELECT Id, Name, Quantity FROM inventory_items WHERE Id = @inventoryItemId LIMIT 1;",
                            conn, tx);
                        pkgSelectCmd.Parameters.AddWithValue("@inventoryItemId", inventoryItemId);

                        using var pkgQtyRdr = pkgSelectCmd.ExecuteReader();
                        if (pkgQtyRdr.Read())
                        {
                            int remaining = pkgQtyRdr.GetInt32("Quantity");
                            string pkgName = pkgQtyRdr.IsDBNull(pkgQtyRdr.GetOrdinal("Name"))
                                ? $"PackagingItem {inventoryItemId}"
                                : pkgQtyRdr.GetString("Name");

                            if (remaining < LowStockThreshold)
                                onLowStock(new LowStockAlert(inventoryItemId, pkgName, remaining));
                        }
                    }
                }
            }
        }
    }

    // =========================================================================
    // DeductionEngine  (Task 9.1)
    // =========================================================================

    /// <summary>
    /// Orchestrates inventory deduction when an order is confirmed as paid.
    /// Selects the appropriate <see cref="IDeductionStrategy"/> based on the
    /// current <see cref="SettingsService.CurrentMode"/>, runs it inside a
    /// single database transaction, and raises <see cref="LowStockDetected"/>
    /// for every low-stock alert produced during the deduction.
    /// </summary>
    public class DeductionEngine
    {
        private const string DefaultCs =
            "server=localhost;user=root;password=;database=sihyu_pos;";

        /// <summary>
        /// Raised after a successful deduction commit for each item whose
        /// remaining quantity (or batch yield in RestaurantMode) has fallen
        /// below the configured low-stock threshold.
        /// </summary>
        public event Action<LowStockAlert>? LowStockDetected;

        /// <summary>
        /// Opens a connection, selects the strategy for the current
        /// <see cref="SystemMode"/>, runs it in a transaction, commits, then
        /// raises <see cref="LowStockDetected"/> for each collected alert.
        /// </summary>
        /// <param name="orderId">The ID of the order being confirmed (reserved for future audit logging).</param>
        /// <param name="items">The line items belonging to the order.</param>
        /// <param name="orderType">
        /// The order classification. Must be set to the appropriate value when
        /// <see cref="SystemMode"/> is <see cref="SystemMode.RestaurantMode"/>;
        /// defaults to <see cref="OrderType.NotApplicable"/> for StoreMode.
        /// </param>
        /// <param name="cs">Optional override for the MySQL connection string.</param>
        public void Apply(
            int orderId,
            IEnumerable<OrderItemModel> items,
            OrderType orderType = OrderType.NotApplicable,
            string cs = DefaultCs)
        {
            IDeductionStrategy strategy =
                SettingsService.Instance.CurrentMode == SystemMode.StoreMode
                    ? new StoreDeductionStrategy()
                    : new RestaurantDeductionStrategy();

            var alerts = new List<LowStockAlert>();

            using var conn = new MySqlConnection(cs);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                strategy.Apply(conn, tx, items, orderType, alert => alerts.Add(alert));
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            // Raise alerts after successful commit
            foreach (var alert in alerts)
                LowStockDetected?.Invoke(alert);
        }
    }
}
