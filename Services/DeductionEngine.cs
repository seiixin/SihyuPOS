#nullable enable
using Microsoft.Data.Sqlite;
using SihyuPOSPayroll.Data;
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
            SqliteConnection conn,
            SqliteTransaction tx,
            IEnumerable<OrderItemModel> items,
            OrderType orderType,
            Action<LowStockAlert> onLowStock);
    }

    // =========================================================================
    // StoreDeductionStrategy
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
            SqliteConnection conn,
            SqliteTransaction tx,
            IEnumerable<OrderItemModel> items,
            OrderType orderType,
            Action<LowStockAlert> onLowStock)
        {
            foreach (var item in items)
            {
                int productId  = item.ProductId;
                int orderedQty = item.Quantity;

                // ── Deduct ──────────────────────────────────────────────────
                // BUG 25 FIX: add AND Quantity >= @orderedQty so stock cannot go
                // negative. rowsAffected == 0 means either no such product OR
                // insufficient stock — both are treated as a low-stock skip.
                int rowsAffected;
                using (var updateCmd = conn.CreateCommand())
                {
                    updateCmd.Transaction = tx;
                    updateCmd.CommandText =
                        "UPDATE inventory_items SET Quantity = Quantity - @orderedQty WHERE Id = @productId AND Quantity >= @orderedQty;";
                    updateCmd.Parameters.AddWithValue("@orderedQty", orderedQty);
                    updateCmd.Parameters.AddWithValue("@productId",  productId);
                    rowsAffected = updateCmd.ExecuteNonQuery();
                }

                if (rowsAffected == 0)
                {
                    Debug.WriteLine($"[StoreDeduction] Missing inventory for product {productId}");
                    continue;
                }

                // ── Low-stock check ─────────────────────────────────────────
                // BUG 3 FIX: column is 'ProductName', not 'Name'
                using var selectCmd = conn.CreateCommand();
                selectCmd.Transaction = tx;
                selectCmd.CommandText =
                    "SELECT Id, ProductName, Quantity FROM inventory_items WHERE Id = @productId LIMIT 1;";
                selectCmd.Parameters.AddWithValue("@productId", productId);

                using var rdr = selectCmd.ExecuteReader();
                if (rdr.Read())
                {
                    int    remaining = rdr.GetInt32(rdr.GetOrdinal("Quantity"));
                    string name      = rdr.IsDBNull(rdr.GetOrdinal("ProductName"))
                                           ? $"Product {productId}"
                                           : rdr.GetString(rdr.GetOrdinal("ProductName"));

                    if (remaining < LowStockThreshold)
                        onLowStock(new LowStockAlert(productId, name, remaining));
                }
            }
        }
    }

    // =========================================================================
    // RestaurantDeductionStrategy
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
            SqliteConnection conn,
            SqliteTransaction tx,
            IEnumerable<OrderItemModel> items,
            OrderType orderType,
            Action<LowStockAlert> onLowStock)
        {
            foreach (var item in items)
            {
                int menuItemId = item.ProductId;
                int orderedQty = item.Quantity;

                // ── 1. Fetch recipe ingredients for this menu item ──────────
                var recipes = new List<(int IngredientId, decimal QuantityPerServing)>();

                using (var recipeCmd = conn.CreateCommand())
                {
                    recipeCmd.Transaction = tx;
                    recipeCmd.CommandText = @"
                        SELECT ir.IngredientId, ir.QuantityPerServing
                        FROM   IngredientRecipes ir
                        WHERE  ir.MenuItemId = @menuItemId;";
                    recipeCmd.Parameters.AddWithValue("@menuItemId", menuItemId);

                    using var recipeRdr = recipeCmd.ExecuteReader();
                    while (recipeRdr.Read())
                    {
                        int     ingId = recipeRdr.GetInt32(recipeRdr.GetOrdinal("IngredientId"));
                        decimal qps   = recipeRdr.GetDecimal(recipeRdr.GetOrdinal("QuantityPerServing"));
                        recipes.Add((ingId, qps));
                    }
                }

                if (recipes.Count == 0)
                {
                    Debug.WriteLine($"[RestaurantDeduction] Missing recipe for menuItem {menuItemId}");
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
                        using (var batchCmd = conn.CreateCommand())
                        {
                            batchCmd.Transaction = tx;
                            batchCmd.CommandText = @"
                                SELECT Id, YieldPerBatch
                                FROM   IngredientBatches
                                WHERE  IngredientId = @ingredientId
                                ORDER  BY CreatedAt ASC;";
                            batchCmd.Parameters.AddWithValue("@ingredientId", ingredientId);

                            using var batchRdr = batchCmd.ExecuteReader();
                            while (batchRdr.Read())
                            {
                                batches.Add((
                                    batchRdr.GetInt32(batchRdr.GetOrdinal("Id")),
                                    batchRdr.GetDecimal(batchRdr.GetOrdinal("YieldPerBatch"))));
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

                                using var deleteCmd = conn.CreateCommand();
                                deleteCmd.Transaction = tx;
                                deleteCmd.CommandText =
                                    "DELETE FROM IngredientBatches WHERE Id = @batchId;";
                                deleteCmd.Parameters.AddWithValue("@batchId", batchId);
                                deleteCmd.ExecuteNonQuery();
                            }
                            else
                            {
                                // Partial deduction
                                using var updateBatchCmd = conn.CreateCommand();
                                updateBatchCmd.Transaction = tx;
                                updateBatchCmd.CommandText = @"
                                    UPDATE IngredientBatches
                                    SET    YieldPerBatch = YieldPerBatch - @consumed
                                    WHERE  Id = @batchId;";
                                updateBatchCmd.Parameters.AddWithValue("@consumed", remaining);
                                updateBatchCmd.Parameters.AddWithValue("@batchId",  batchId);
                                updateBatchCmd.ExecuteNonQuery();
                                remaining = 0m;
                            }
                        }

                        // ── 3. Low-stock check: sum remaining yield for this ingredient ──
                        decimal totalRemainingYield;
                        using (var sumCmd = conn.CreateCommand())
                        {
                            sumCmd.Transaction = tx;
                            sumCmd.CommandText = @"
                                SELECT COALESCE(SUM(YieldPerBatch), 0)
                                FROM   IngredientBatches
                                WHERE  IngredientId = @ingredientId;";
                            sumCmd.Parameters.AddWithValue("@ingredientId", ingredientId);
                            totalRemainingYield = Convert.ToDecimal(sumCmd.ExecuteScalar());
                        }

                        if (totalRemainingYield < LowStockThreshold)
                        {
                            // BUG 3 FIX: column is 'ProductName', not 'Name'
                            string ingName = $"Ingredient {ingredientId}";
                            using (var nameCmd = conn.CreateCommand())
                            {
                                nameCmd.Transaction = tx;
                                nameCmd.CommandText =
                                    "SELECT ProductName FROM inventory_items WHERE Id = @id LIMIT 1;";
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

                    using (var pkgCmd = conn.CreateCommand())
                    {
                        pkgCmd.Transaction = tx;
                        pkgCmd.CommandText = @"
                            SELECT InventoryItemId, QuantityPerOrder
                            FROM   PackagingMaterials
                            WHERE  MenuItemId = @menuItemId;";
                        pkgCmd.Parameters.AddWithValue("@menuItemId", menuItemId);

                        using var pkgRdr = pkgCmd.ExecuteReader();
                        while (pkgRdr.Read())
                        {
                            packagingMaterials.Add((
                                pkgRdr.GetInt32(pkgRdr.GetOrdinal("InventoryItemId")),
                                pkgRdr.GetInt32(pkgRdr.GetOrdinal("QuantityPerOrder"))));
                        }
                    }

                    foreach (var (inventoryItemId, quantityPerOrder) in packagingMaterials)
                    {
                        int rowsAffected;
                        using (var pkgUpdateCmd = conn.CreateCommand())
                        {
                            pkgUpdateCmd.Transaction = tx;
                            pkgUpdateCmd.CommandText = @"
                                UPDATE inventory_items
                                SET    Quantity = Quantity - @quantityPerOrder
                                WHERE  Id = @inventoryItemId;";
                            pkgUpdateCmd.Parameters.AddWithValue("@quantityPerOrder", quantityPerOrder);
                            pkgUpdateCmd.Parameters.AddWithValue("@inventoryItemId",  inventoryItemId);
                            rowsAffected = pkgUpdateCmd.ExecuteNonQuery();
                        }

                        if (rowsAffected == 0)
                        {
                            Debug.WriteLine($"[RestaurantDeduction] Missing packaging for menuItem {menuItemId}");
                            continue;
                        }

                        // BUG 3 FIX: column is 'ProductName', not 'Name'
                        using var pkgSelectCmd = conn.CreateCommand();
                        pkgSelectCmd.Transaction = tx;
                        pkgSelectCmd.CommandText =
                            "SELECT Id, ProductName, Quantity FROM inventory_items WHERE Id = @inventoryItemId LIMIT 1;";
                        pkgSelectCmd.Parameters.AddWithValue("@inventoryItemId", inventoryItemId);

                        using var pkgQtyRdr = pkgSelectCmd.ExecuteReader();
                        if (pkgQtyRdr.Read())
                        {
                            int    remaining = pkgQtyRdr.GetInt32(pkgQtyRdr.GetOrdinal("Quantity"));
                            string pkgName   = pkgQtyRdr.IsDBNull(pkgQtyRdr.GetOrdinal("ProductName"))
                                                   ? $"PackagingItem {inventoryItemId}"
                                                   : pkgQtyRdr.GetString(pkgQtyRdr.GetOrdinal("ProductName"));

                            if (remaining < LowStockThreshold)
                                onLowStock(new LowStockAlert(inventoryItemId, pkgName, remaining));
                        }
                    }
                }
            }
        }
    }

    // =========================================================================
    // DeductionEngine
    // =========================================================================

    /// <summary>
    /// Orchestrates inventory deduction when an order is confirmed as paid.
    /// Selects the appropriate <see cref="IDeductionStrategy"/> based on the
    /// current <see cref="SettingsService.CurrentMode"/>, runs it inside a
    /// single SQLite transaction, and raises <see cref="LowStockDetected"/>
    /// for every low-stock alert produced during the deduction.
    /// </summary>
    public class DeductionEngine
    {
        /// <summary>
        /// Raised after a successful deduction commit for each item whose
        /// remaining quantity (or batch yield in RestaurantMode) has fallen
        /// below the configured low-stock threshold.
        /// </summary>
        public event Action<LowStockAlert>? LowStockDetected;

        /// <summary>
        /// Opens a SQLite connection, selects the strategy for the current
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
        public void Apply(
            int orderId,
            IEnumerable<OrderItemModel> items,
            OrderType orderType = OrderType.NotApplicable)
        {
            IDeductionStrategy strategy =
                SettingsService.Instance.CurrentMode == SystemMode.StoreMode
                    ? new StoreDeductionStrategy()
                    : (IDeductionStrategy)new RestaurantDeductionStrategy();

            var alerts = new List<LowStockAlert>();

            using var conn = SqliteConnectionFactory.CreateOpenConnection();
            using var tx   = conn.BeginTransaction();

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
