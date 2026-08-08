using Xamarin.Forms;

namespace ISEP
{
    public partial class App : Application
    {
        public static bool IsUserLoggedIn { get; set; }
        public static string PrinterFooter { get; set; }
        public static string RevenueServiceName { get; set; }
        public static string CentralPortalURL { get; set; }
        public static string CentralPortalURLkeke { get; set; }
        public static string ThankYouMessage { get; set; }
        public App()
        {
            InitializeComponent();

            CentralPortalURL = "https://borno.osoftpay.net/api/SingleCollections/PostCollect/NewCollect";
            RevenueServiceName = "BORNO STATE INTERNAL REVENUE SERVICE(BOIRS) ";
            PrinterFooter = "POWERED BY OSOFTPAY";
            ThankYouMessage = "CONTACT US : 08144993882,###########";

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
