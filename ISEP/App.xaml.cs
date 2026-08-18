using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using ISEP.Views;
using ISEP.Services;

namespace ISEP
{
    public partial class App : Application
    {
        public static bool IsUserLoggedIn { get; set; }
        public static string RevenueServiceName { get; set; } = BrandConfig.OrganisationName;
        public static string PrinterFooter { get; set; } = BrandConfig.ReceiptFooterLine1;
        public static string ThankYouMessage { get; set; } = BrandConfig.ReceiptFooterLine2;

        // Print Job Manager instance accessible globally
        public static PrintJobManager PrintJobManager { get; private set; }
        public static IPrinterService Printer { get; private set; }

        public App()
        {
            InitializeComponent();

            // Configure API SSL Settings globally
            ApiClient.ConfigureSSL();

            // Initialize Printer Service Pipeline
            Printer = new MockPrinterService(); // Replace with your native BluetoothPrinterService instance in Android project
            PrintJobManager = new PrintJobManager(Printer);

            // Check for Auto-Login Session
            if (SessionService.TryAutoLogin())
            {
                IsUserLoggedIn = true;
                MainPage = new NavigationPage(new Dashboard());
            }
            else
            {
                IsUserLoggedIn = false;
                MainPage = new NavigationPage(new LoginPage());
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            // Register platform instances
            if (Printer == null)
            {
                // On Android head: DependencyService or platform provider assigns BluetoothPrinterService
                Printer = DependencyService.Get<IPrinterService>() ?? new MockPrinterService();
            }

            if (PrintJobManager == null)
            {
                PrintJobManager = new PrintJobManager(Printer);
            }

            // Clean up jobs older than 48 hours on startup
            Task.Run(async () => await PrintJobManager.PruneOldJobsAsync());
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await ProcessPendingPrintJobsAsync();
        }

        private async Task ProcessPendingPrintJobsAsync()
        {
            try
            {
                // Retry any receipts queued during network or printer connection loss
                await ReceiptPrinter.RetryPendingAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing pending print jobs: {ex.Message}");
            }
        }

        protected override void OnSleep()
        {
            base.OnSleep();
        }
    }
}