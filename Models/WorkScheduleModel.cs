#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SihyuPOSPayroll.Models
{
    public class WorkScheduleModel : INotifyPropertyChanged
    {
        private int _id;
        private string _label = string.Empty;

        private bool _mon, _tue, _wed, _thu, _fri, _sat, _sun;
        private bool _isActive = true;
        private DateTime _updatedAt = DateTime.Now;

        // ---------- Identity / meta ----------
        public int Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        public string Label
        {
            get => _label;
            set { if (_label != value) { _label = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set { if (_updatedAt != value) { _updatedAt = value; OnPropertyChanged(); } }
        }

        // ---------- Day flags ----------
        public bool Mon { get => _mon; set => SetDay(ref _mon, value); }
        public bool Tue { get => _tue; set => SetDay(ref _tue, value); }
        public bool Wed { get => _wed; set => SetDay(ref _wed, value); }
        public bool Thu { get => _thu; set => SetDay(ref _thu, value); }
        public bool Fri { get => _fri; set => SetDay(ref _fri, value); }
        public bool Sat { get => _sat; set => SetDay(ref _sat, value); }
        public bool Sun { get => _sun; set => SetDay(ref _sun, value); }

        // Pretty string for display-only cells (used by XAML)
        public string DaysText => BuildDaysText();

        // Optional: bindable bit-mask (0..127) if you ever want to store flags compactly
        public byte DaysMask
        {
            get => ToDaysMask();
            set
            {
                FromDaysMask(value);
                OnPropertyChanged();                // DaysMask
                OnPropertyChanged(nameof(DaysText));// refresh display text too
            }
        }

        // ---------- Helpers ----------
        private void SetDay(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();                    // this specific day
            OnPropertyChanged(nameof(DaysText));    // recompute pretty text
            OnPropertyChanged(nameof(DaysMask));    // mask changed as well
        }

        private string BuildDaysText()
        {
            // Common presets
            if (Mon && Tue && Wed && Thu && Fri && !Sat && !Sun) return "Mon–Fri";
            if (Mon && Wed && Fri && !Tue && !Thu && !Sat && !Sun) return "MWF";
            if (!Mon && Tue && !Wed && Thu && !Fri && !Sat && !Sun) return "TTh";
            if (Mon && Tue && Wed && Thu && Fri && Sat && Sun) return "Daily";

            var parts = new[]
            {
                Mon ? "Mon" : null,
                Tue ? "Tue" : null,
                Wed ? "Wed" : null,
                Thu ? "Thu" : null,
                Fri ? "Fri" : null,
                Sat ? "Sat" : null,
                Sun ? "Sun" : null
            }.Where(s => s != null)!;

            var text = string.Join(" ", parts);
            return string.IsNullOrWhiteSpace(text) ? "(none)" : text;
        }

        public byte ToDaysMask()
        {
            byte m = 0;
            if (Mon) m |= 1 << 0;
            if (Tue) m |= 1 << 1;
            if (Wed) m |= 1 << 2;
            if (Thu) m |= 1 << 3;
            if (Fri) m |= 1 << 4;
            if (Sat) m |= 1 << 5;
            if (Sun) m |= 1 << 6;
            return m;
        }

        public void FromDaysMask(byte mask)
        {
            // Use backing fields to avoid double notifications;
            // then raise once for each property we changed.
            bool mon = (mask & (1 << 0)) != 0;
            bool tue = (mask & (1 << 1)) != 0;
            bool wed = (mask & (1 << 2)) != 0;
            bool thu = (mask & (1 << 3)) != 0;
            bool fri = (mask & (1 << 4)) != 0;
            bool sat = (mask & (1 << 5)) != 0;
            bool sun = (mask & (1 << 6)) != 0;

            // Assign via properties so DaysText/DaysMask notifications fire
            Mon = mon; Tue = tue; Wed = wed; Thu = thu; Fri = fri; Sat = sat; Sun = sun;
        }

        // ---------- INotifyPropertyChanged ----------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
