using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using ISEP.Views;
using ISEP.Services;

namespace ISEP
{
    public partial class App : Application
    {
        // ── Legacy compatibility surface ─────────────────────────
        // Forwards to BrandConfig and SessionService so there is a
        // single source of truth across the app.
        public static bool IsUserLoggedIn { get; set; }

        public static string RevenueServiceName => BrandConfig.OrganisationName + " (" + BrandConfig.OrganisationAbbr + ") ";
        public static string PrinterFooter => BrandConfig.ReceiptFooterLine2;
        public static string ThankYouMessage => "CONTACT US : " + BrandConfig.SupportPhone1 + ", " + BrandConfig.SupportPhone2;

        /// <summary>Shared printer service. Prefer using <see cref="ReceiptPrinter"/> over calling this directly.</summary>
        public static IPrinterService Printer { get; private set; }

        /// <summary>Durable print job queue. Managed automatically by <see cref="ReceiptPrinter"/>.</summary>
        public static PrintJobManager PrintJobManager { get; private set; }

        /// <summary>
        /// Called by the platform head (MainActivity on Android) BEFORE
        /// LoadApplication to inject the platform printer driver.
        /// </summary>
        public static void InitializePrinting(IPrinterService printer)
        {
            Printer = printer ?? new MockPrinterService();
            PrintJobManager = new PrintJobManager(Printer);
        }

        public App()
        {
            InitializeComponent();

            // Safety fallback for previewers/tests if platform head did not inject a printer
            if (Printer == null)
            {
                InitializePrinting(DependencyService.Get<IPrinterService>() ?? new MockPrinterService());
            }

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

        protected override async void OnStart()
        {
            base.OnStart();

            try
            {
                // 1. Session restoration check
                SessionService.EnsureSessionRestored();

                // 2. Clean up jobs older than 48 hours
                if (PrintJobManager != null)
                {
                    await PrintJobManager.PruneOldJobsAsync();
                }

                // 3. Durable print recovery (retries any receipts interrupted earlier)
                await ReceiptPrinter.RetryPendingAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App OnStart Error]: {ex.Message}");
            }
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            _sleptAtUtc = DateTime.UtcNow;
        }

        protected override async void OnResume()
        {
            base.OnResume();

            try
            {
                // Inactivity timeout enforcement (10 mins by default from BrandConfig)
                if (IsUserLoggedIn && _sleptAtUtc.HasValue)
                {
                    var away = DateTime.UtcNow - _sleptAtUtc.Value;
                    if (away > TimeSpan.FromMinutes(BrandConfig.SessionInactivityTimeoutMinutes))
                    {
                        SessionService.ClearSession();
                        IsUserLoggedIn = false;
                        MainPage = new NavigationPage(new LoginPage());
                        return;
                    }
                }

                // Ensure static session state is restored if process was recycled
                SessionService.EnsureSessionRestored();

                // Retry any pending print jobs upon resuming Bluetooth connectivity
                await ReceiptPrinter.RetryPendingAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App OnResume Error]: {ex.Message}");
            }
        }

        private DateTime? _sleptAtUtc;
    }
}