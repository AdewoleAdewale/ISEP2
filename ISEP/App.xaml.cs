using ISEP.Services;
using Xamarin.Forms;

namespace ISEP
{
    public partial class App : Application
    {
        public static bool IsUserLoggedIn { get; set; }
   

        public static string PrinterFooter => BrandConfig.ReceiptFooterLine2;
        public static string RevenueServiceName => BrandConfig.OrganisationName + " (" + BrandConfig.OrganisationAbbr + ") ";
        public static string CentralPortalURL => BrandConfig.CentralCollectUrl;
        public static string CentralPortalURLkeke { get; set; }
        public static string ThankYouMessage => "CONTACT US : " + BrandConfig.SupportPhone1 + "," + BrandConfig.SupportPhone2;

        /// <summary>Shared printer service. Prefer <see cref="ReceiptPrinter"/> over calling this directly.</summary>
        public static IPrinterService Printer { get; private set; }

        /// <summary>Durable print job queue. Prefer <see cref="ReceiptPrinter"/>.</summary>
        public static PrintJobManager PrintJobManager { get; private set; }

        /// <summary>
        /// Called by each platform head (MainActivity on Android) BEFORE
        /// LoadApplication to inject the platform printer implementation.
        /// The shared project no longer references Android assemblies.
        /// </summary>
        public static void InitializePrinting(IPrinterService printer)
        {
            Printer = printer ?? new MockPrinterService();
            PrintJobManager = new PrintJobManager(Printer);
        }
        public App()
        {
            InitializeComponent();
            if (Printer == null)
                InitializePrinting(new MockPrinterService());

            MainPage = new Views.LoginPage();
            //if (!Properties.TryGetValue("first_time", out object value))
            //{
            //    Properties.Add("first_time", true);
            //    Current.SavePropertiesAsync();
            //    MainPage = new NavigationPage(new MainPage());
            //}

            //else if (!Properties.TryGetValue("not_first", out object values))
            //{
            //    Properties.Add("not_first", true);
            //    Current.SavePropertiesAsync();
            //    MainPage = new Views.LoginPage();


            //}
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
