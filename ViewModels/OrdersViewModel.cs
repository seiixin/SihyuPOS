using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;              // MessageBox
using System.Windows.Input;        // ICommand
using SihyuPOSPayroll.Helpers; // RelayCommand
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll.ViewModels
{
    public class OrdersViewModel : BaseViewModel
    {
        private readonly OrderService _orderService = new OrderService();

        // ====== Sources ======
        public ObservableCollection<OrderModel> Orders { get; set; } = new();
        public ObservableCollection<MenuModel> MenuProducts { get; set; } = new();
        public ObservableCollection<OrderService.TableOption> TablesForPicker { get; set; } = new();

        // NEW: enum lists for editor dropdowns
        public Array PaymentStatuses => Enum.GetValues(typeof(PaymentStatus));
        public Array OrderStatuses => Enum.GetValues(typeof(OrderStatus));

        // ----- Inline editor state -----
        public OrderModel? EditingOrder { get; set; }
        public ObservableCollection<OrderItemModel> EditingItems { get; set; } = new();

        // ----- READ-ONLY info (??) -----
        public OrderModel? InfoOrder { get; set; }
        public ObservableCollection<OrderItemModel> InfoItems { get; set; } = new();

        // OPTIONAL: quick actions (bind to buttons if you like)
        public ICommand MarkPaidNowCommand { get; }
        public ICommand MarkUnpaidNowCommand { get; }

        public OrdersViewModel()
        {
            try { LoadMenu(); } catch { /* ignore */ }
            try { LoadOrders(); } catch { /* ignore */ }

            MarkPaidNowCommand = new RelayCommand(o =>
                RequestPaymentStatusChange((o as OrderModel) ?? EditingOrder, PaymentStatus.Paid));

            MarkUnpaidNowCommand = new RelayCommand(o =>
                RequestPaymentStatusChange((o as OrderModel) ?? EditingOrder, PaymentStatus.Unpaid));
        }

        // ===== Loaders =====
        public void LoadOrders()
        {
            try
            {
                var list = _orderService.GetAllOrders();
                Orders = new ObservableCollection<OrderModel>(list);
                OnPropertyChanged(nameof(Orders));
            }
            catch
            {
                Orders = new ObservableCollection<OrderModel>();
                OnPropertyChanged(nameof(Orders));
            }
        }

        public void LoadMenu()
        {
            MenuProducts.Clear();
            var list = _orderService.GetAllMenu();
            foreach (var p in list) MenuProducts.Add(p);
            OnPropertyChanged(nameof(MenuProducts));
        }

        private void LoadTablesForPicker(int? currentOrderId, bool includeOccupied)
        {
            TablesForPicker.Clear();

            var raw = _orderService.GetTablesForPicker(currentOrderId);

            IEnumerable<OrderService.TableOption> filtered = raw;
            if (!includeOccupied)
                filtered = raw.Where(t => t.Status == "Available");

            foreach (var t in filtered)
                TablesForPicker.Add(t);

            OnPropertyChanged(nameof(TablesForPicker));

            if (EditingOrder != null &&
                !string.IsNullOrWhiteSpace(EditingOrder.TableNumber) &&
                !TablesForPicker.Any(t => t.TableNumber == EditingOrder.TableNumber))
            {
                EditingOrder.TableNumber = null;
                OnPropertyChanged(nameof(EditingOrder));
            }
        }

        // ===== Editor lifecycle =====
        public void BeginAdd()
        {
            EditingOrder = new OrderModel
            {
                CreatedAt = DateTime.Now,
                PaymentStatus = PaymentStatus.Unpaid,
                OrderStatus = OrderStatus.Pending,
                TableNumber = null
            };

            EditingItems = new ObservableCollection<OrderItemModel>();
            LoadTablesForPicker(currentOrderId: null, includeOccupied: false);

            OnPropertyChanged(nameof(EditingOrder));
            OnPropertyChanged(nameof(EditingItems));
        }

        public void BeginEdit(OrderModel source)
        {
            if (source == null) return;

            var items = _orderService.GetOrderItems(source.Id);

            EditingOrder = new OrderModel
            {
                Id = source.Id,
                CustomerId = source.CustomerId,
                TableNumber = source.TableNumber,
                TotalAmount = source.TotalAmount,
                PaymentStatus = source.PaymentStatus,
                CreatedAt = source.CreatedAt,
                CashRegisterId = source.CashRegisterId,
                OrderStatus = source.OrderStatus,
                OrderedByUserId = source.OrderedByUserId
            };

            EditingItems = new ObservableCollection<OrderItemModel>(
                (items ?? new List<OrderItemModel>()).Select(it => new OrderItemModel
                {
                    Id = it.Id,
                    OrderId = it.OrderId,
                    ProductId = it.ProductId,
                    Quantity = it.Quantity,
                    UnitPrice = it.UnitPrice,
                    ProductName = it.ProductName,
                    Category = it.Category
                })
            );

            LoadTablesForPicker(currentOrderId: source.Id, includeOccupied: true);

            OnPropertyChanged(nameof(EditingOrder));
            OnPropertyChanged(nameof(EditingItems));
        }

        // ===== Info (read-only) =====
        public void BeginInfo(OrderModel source)
        {
            if (source == null) return;

            var items = _orderService.GetOrderItems(source.Id);

            InfoOrder = source;
            InfoItems = new ObservableCollection<OrderItemModel>(items ?? new List<OrderItemModel>());

            OnPropertyChanged(nameof(InfoOrder));
            OnPropertyChanged(nameof(InfoItems));
        }

        // ===== Item row ops =====
        public void AddLine()
        {
            EditingItems.Add(new OrderItemModel { Quantity = 1 });
            OnPropertyChanged(nameof(EditingItems));
            RecalcEditingTotal();
        }

        public void RemoveLine(OrderItemModel item)
        {
            if (item == null) return;
            EditingItems.Remove(item);
            OnPropertyChanged(nameof(EditingItems));
            RecalcEditingTotal();
        }

        public void RecalcEditingTotal()
        {
            if (EditingOrder == null) return;
            EditingOrder.Items = new List<OrderItemModel>(EditingItems);
            EditingOrder.RecalculateTotal();
            OnPropertyChanged(nameof(EditingOrder));
        }

        // ===== Persist header+items =====
        public void SaveEditing()
        {
            if (EditingOrder == null) return;

            try
            {
                EditingOrder.Items = new List<OrderItemModel>(EditingItems);
                EditingOrder.RecalculateTotal();

                if (EditingOrder.Id == 0)
                    _orderService.AddOrder(EditingOrder);     // throws if table occupied
                else
                    _orderService.UpdateOrder(EditingOrder);  // throws if new table occupied

                LoadOrders();

                var orderId = EditingOrder.Id == 0 ? (int?)null : EditingOrder.Id;
                LoadTablesForPicker(orderId, includeOccupied: orderId.HasValue);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Orders", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save order.\n{ex.Message}", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void DeleteOrder(int id)
        {
            try
            {
                _orderService.DeleteOrder(id);
                LoadOrders();
                LoadTablesForPicker(currentOrderId: null, includeOccupied: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete order.\n{ex.Message}", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Payment status change helper =====
        /// <summary>
        /// Ask for confirmation and update the order's payment status immediately.
        /// If <paramref name="target"/> is null, it tries to use EditingOrder.
        /// </summary>
        public void RequestPaymentStatusChange(OrderModel? target, PaymentStatus newStatus)
        {
            var order = target ?? EditingOrder;
            if (order == null)
            {
                MessageBox.Show("No order selected.", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (order.PaymentStatus == newStatus)
                return; // nothing to do

            var msg = newStatus == PaymentStatus.Paid
                ? $"Mark order #{order.Id} as PAID? The table will become available."
                : $"Mark order #{order.Id} as UNPAID?";

            var confirm = MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                // ensure items are present before update
                order.Items = _orderService.GetOrderItems(order.Id) ?? new List<OrderItemModel>();
                order.PaymentStatus = newStatus;

                _orderService.UpdateOrder(order);

                LoadOrders();
                // Refresh add-mode picker to reflect availability changes
                LoadTablesForPicker(currentOrderId: null, includeOccupied: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update payment status.\n{ex.Message}", "Orders",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
