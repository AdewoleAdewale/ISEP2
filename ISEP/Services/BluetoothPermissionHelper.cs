using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ISEP.Services
{
    public static class BluetoothPermissionHelper
    {
        private static Func<Task<bool>> _provider;

        public static void SetProvider(Func<Task<bool>> provider)
            => _provider = provider;

        public static Task<bool> RequestAsync()
            => _provider != null ? _provider() : Task.FromResult(true);
    }
}
