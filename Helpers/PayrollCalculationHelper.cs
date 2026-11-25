using System;
using System.Collections.Generic;
using System.Linq;
using SihyuPOSPayroll.Models;

namespace SihyuPOSPayroll.Helpers
{
    public static class PayrollCalculationHelper
    {
        public static int CalculateDaysWorked(DateTime startDate, DateTime endDate, List<AttendanceModel> attendanceRecords)
        {
            // BUG 5 FIX: only count days where BOTH time_in AND time_out are recorded.
            // "Clocked In" means the employee clocked in but never clocked out — an
            // incomplete shift. Counting it overpays the employee.
            return attendanceRecords
                .Where(a => a.Date >= startDate
                         && a.Date <= endDate
                         && a.TimeIn.HasValue
                         && a.TimeOut.HasValue)
                .Count();
        }

        public static int GetDaysWorked(DateTime startDate, DateTime endDate, List<AttendanceModel> attendanceRecords)
        {
            return CalculateDaysWorked(startDate, endDate, attendanceRecords);
        }

        public static decimal CalculateGrossSalary(int daysWorked, decimal dailyRate)
        {
            return daysWorked * dailyRate;
        }

        public static decimal CalculateTotalDeductions(decimal sss, decimal philhealth, decimal pagibig, decimal other)
        {
            return sss + philhealth + pagibig + other;
        }

        public static decimal CalculateNetSalary(decimal grossSalary, decimal totalDeductions, decimal bonus = 0)
        {
            return grossSalary - totalDeductions + bonus;
        }
    }
}
