using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SihyuPOSPayroll.Helpers
{
    public static class Session
    {
        public static int CurrentUserId { get; set; }
        public static string CurrentUserRole { get; set; }
    }
}
