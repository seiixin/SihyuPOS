using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SihyuPOSPayroll.Models
{
    public class OrderItemModel : INotifyPropertyChanged
    {
        public int Id { get; set; }       // order_items.id
        public int OrderId { get; set; }  // order_items.order_id
        public int ProductId { get; set; }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subtotal));
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (_unitPrice == value) return;
                _unitPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subtotal));
            }
        }

        public decimal Subtotal => UnitPrice * Quantity;

        private string? _productName;
        public string? ProductName
        {
            get => _productName;
            set { _productName = value; OnPropertyChanged(); }
        }

        private string? _category;
        public string? Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
