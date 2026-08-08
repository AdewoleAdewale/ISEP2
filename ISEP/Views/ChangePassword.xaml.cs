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
    public partial class ChangePassword : ContentPage
    {
        public ChangePassword()
        {
            InitializeComponent();
        }
        protected override bool OnBackButtonPressed()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                using (UserDialogs.Instance.Loading("Connecting to Service, Please Wait...", null, null, true))
                {
                    await Task.Delay(10);

                    await Navigation.PushAsync(new Views.Dashboard());
                }

            });
            return true;
        }


        private async void TapGestureRecognizer_Tapped_1(object sender, EventArgs e)
        {
            //change password
            using (UserDialogs.Instance.Loading("Connecting to Service, Please Wait...", null, null, true))
            {
                await Task.Delay(2000);

                if (OldPasswordEntry.Text == null || ConfirmPassword.Text == null)
                {

                    await DisplayAlert("NOTIFICATION", "Kindly fill in all details before you proceed", "TRY AGAIN");
                    await Navigation.PushModalAsync(new Views.ChangePassword());
                }

                else if (OldPasswordEntry.Text != null && LoginPage.Passwords == OldPasswordEntry.Text)
                {
                    //Connect to cloud and retrieve email and password combination
                    string url = "https://borno.osoftpay.net/api/taskpayers/SAChangePassword?UserName=" + LoginPage.ValidUserMail + "&NewPassword=" + ConfirmPassword.Text;

                    try
                    {

                        using (HttpClient client = new HttpClient())
                        {
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            using (HttpResponseMessage response = client.GetAsync(url).Result)
                            {
                                using (HttpContent content = response.Content)
                                {
                                    var json = content.ReadAsStringAsync().Result;
                                    InterfacePass result = JsonConvert.DeserializeObject<InterfacePass>
                                        (json);

                                    if (result != null)
                                    {
                                        if (result.status == "00")
                                        {
                                            App.IsUserLoggedIn = false;
                                            await DisplayAlert("NOTIFICATION", "Password Change Successful. Please Login Again!", "OKAY");
                                            Application.Current.MainPage = new NavigationPage(new LoginPage());
                                        }
                                        else
                                        {
                                            await DisplayAlert("NOTIFICATION", "Error, Password was not changed!", "OKAY");

                                        }
                                    }
                                    else
                                    {
                                        await DisplayAlert("NOTIFICATION", "Connection Failed", "OKAY");
                                    }
                                }
                            }
                        }

                    }
                    catch (Exception exe)
                    {
                        await DisplayAlert("NOTIFICATION", "Check your Internet", "TRY AGAIN");
                        exe.ToString();
                    }

                }
            }

        }
    }

    internal class InterfacePass
    {
        public string MerchantSubUser { get; set; }

        public string status { get; set; }

        public string Password { get; set; }

        public string PhoneNumber { get; set; }

        public string FullName { get; set; }
    }
}