using System;
using System.Threading.Tasks;
using Acr.UserDialogs;
using Xamarin.Forms;

namespace ISEP.Services
{
    /// <summary>
    /// The standard Borno IGR printing facade.
    /// Handles permissions, disk queueing, discovery check, and chunked execution.
    /// </summary>
    public static class ReceiptPrinter
    {
        public static ReceiptData CreateBrandedReceipt()
        {
            return new ReceiptData
            {
                StoreName = BrandConfig.ReceiptStoreName,
                StoreAddress = BrandConfig.ReceiptAddress,
                StorePhone = BrandConfig.ReceiptPhone,
                FooterLine1 = BrandConfig.ReceiptFooterLine1,
                FooterLine2 = BrandConfig.ReceiptFooterLine2,
                BarcodeLabel = BrandConfig.VerifyReceiptUrl,
                PrintDate = DateTime.Now
            };
        }

        public static async Task<bool> PrintAsync(ReceiptData receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));

            // 1 ── Runtime Bluetooth permissions (Android 12+)
            var granted = await BluetoothPermissionHelper.RequestAsync();
            if (!granted)
            {
                await ShowAlertAsync("Bluetooth Permission",
                    "Printing needs Bluetooth permission. Please allow it in Settings and try again.");
                return false;
            }

            // 2 ── Persist the job FIRST so nothing is lost
            var job = await App.PrintJobManager.EnqueueAsync(
                receipt, BrandConfig.ReceiptLogoAsset);

            // 3 ── Availability check with friendly prompt
            var available = await App.Printer.IsPrinterAvailableAsync();
            if (!available)
            {
                var retry = await UserDialogs.Instance.ConfirmAsync(
                    "No paired printer found.\n\nTurn on your printer and Bluetooth, then tap RETRY. " +
                    "The receipt is saved and will also retry automatically later.",
                    "🖨️ Printer Not Found", "RETRY", "LATER");

                if (!retry) return false;

                available = await App.Printer.IsPrinterAvailableAsync();
                if (!available)
                {
                    UserDialogs.Instance.Toast("Receipt saved — it will print automatically once the printer is available.");
                    return false;
                }
            }

            // 4 ── Execute with progress feedback
            using (var dialog = UserDialogs.Instance.Loading("🖨️ Printing receipt…", null, null, true, MaskType.Black))
            {
                var progress = new Progress<PrintProgress>(p =>
                {
                    if (!string.IsNullOrEmpty(p.ChunkName))
                        dialog.Title = string.Format("🖨️ Printing… {0}", p.ChunkName);
                });

                try
                {
                    await App.PrintJobManager.ExecuteAsync(job.JobId, progress);
                    await App.PrintJobManager.DeleteJobAsync(job.JobId);
                    UserDialogs.Instance.Toast("✅ Receipt printed");
                    return true;
                }
                catch (Exception)
                {
                    await ShowAlertAsync("Print Interrupted",
                        "The receipt could not finish printing. It has been saved and will " +
                        "resume automatically.");
                    return false;
                }
            }
        }

        public static async Task RetryPendingAsync()
        {
            try
            {
                if (await App.Printer.IsPrinterAvailableAsync())
                    await App.PrintJobManager.ResumeUnfinishedJobsAsync();
            }
            catch
            {
                // Silent — resume is opportunistic; jobs remain persisted.
            }
        }

     
        private static Task ShowAlertAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();
            Device.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Application.Current.MainPage.DisplayAlert(title, message, "OK");
                    tcs.TrySetResult(true);
                }
                catch (Exception ex) { tcs.TrySetException(ex); }
            });
            return tcs.Task;
        }
    }
}