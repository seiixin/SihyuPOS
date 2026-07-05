#nullable enable
using System;
using System.Windows.Input;

namespace SihyuPOSPayroll.Helpers
{
    // -------- Non-generic --------
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    // -------- Generic --------
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            var (ok, value) = TryCast(parameter);
            return ok && (_canExecute?.Invoke(value) ?? true);
        }

        public void Execute(object? parameter)
        {
            var (_, value) = TryCast(parameter);
            _execute(value);
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();

        private static (bool ok, T? value) TryCast(object? parameter)
        {
            if (parameter is T t) return (true, t);

            if (parameter is null)
            {
                var isNonNullableValueType =
                    typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null;
                return isNonNullableValueType ? (false, default) : (true, default);
            }

            try
            {
                var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                if (parameter is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
                {
                    return (true, (T?)Convert.ChangeType(parameter, target));
                }
            }
            catch { /* ignore */ }

            return (false, default);
        }
    }
}
