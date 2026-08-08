using Acr.UserDialogs;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LoginPage : ContentPage
    {
        public static string Name { get; set; }
        public static string ValidUserMail { get; set; }
        public static string Passwords { get; set; }

        public static string Pin { get; set; }

        public static string Super_Agent { get; set; }

        public static string Token { get; set; }

        public static string tradingstatus { get; set; }

        public static string accountbalance { get; set; }

        public static string Banks { get; set; }

        public static string accountnumbers { get; set; }
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            string MyEmail = EmailEntry.Text;
            string MyPassword = PasswordEntry.Text;

            using (UserDialogs.Instance.Loading("Connecting to Service, Please Wait...", null, null, true, MaskType.Gradient))
            {
                await Task.Delay(1000);



                string url = "https://borno.osoftpay.net/api/taskpayers/SagentLogin?UserName=" + MyEmail.Trim() + "&Password=" + MyPassword.Trim();


                using (HttpClient client = new HttpClient())
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (HttpResponseMessage response = client.GetAsync(url).Result)
                    {
                        using (
                            HttpContent content = response.Content)
                        {
                            var json = content.ReadAsStringAsync().Result;

                            LoginResponse result = JsonConvert.DeserializeObject<LoginResponse>
                                (json);

                            if (result.responseCode == "00")
                            {
                                ValidUserMail = MyEmail.Trim();
                                Passwords = result.detail.password;
                                Name = result.detail.name;
                                Token = result.detail.token;
                                Pin = result.detail.pin;
                                Super_Agent = result.detail.SuperAgent;
                                accountbalance = result.detail.account_Balance;
                                tradingstatus = result.detail.tradingStatus;
                                accountnumbers = result.detail.accountNumber;
                                Banks = result.detail.bank;

                                App.IsUserLoggedIn = true;

                                Application.Current.MainPage = new NavigationPage(new Views.Dashboard());


                            }
                            else
                            {

                                await Application.Current.MainPage.DisplayAlert("NOTIFICATION", "Can't Validate Your Details, Contact Support: 08027229331 or 08165932680", "TRY AGAIN");

                            }

                        }
                    }
                }


            }

        }

        private async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
        {
            await Application.Current.MainPage.DisplayAlert("NOTIFICATION", "Hello you can reach out to support with these numbers : 08144993882 or 08030523208", "TRY AGAIN");

        }
    }



    internal class LoginResponse
    {


        public string responseCode { get; set; }
        public string message { get; set; }
        public Detail detail { get; set; }
    }

    internal class Detail
    {
        public string name { get; set; }
        public string password { get; set; }
        public string email { get; set; }
        public string token { get; set; }
        public string tradingStatus { get; set; }
        public string account_Balance { get; set; }
        public string pin { get; set; }
        public string SuperAgent { get; set; }

        public string bank { get; set; }
        public string accountNumber { get; set; }
    }



}