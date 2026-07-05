#nullable enable
using System.Windows.Input;

namespace SihyuPOSPayroll.Helpers
{
    public static class CommandExtensions
    {
        /// <summary>
        /// Universal helper: forces WPF to requery CanExecute for any ICommand.
        /// </summary>
        public static void RaiseCanExecuteChanged(this ICommand _)
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
