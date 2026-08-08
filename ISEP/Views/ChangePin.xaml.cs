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
    public partial class ChangePin : ContentPage
    {
        public ChangePin()
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

        private async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
        {
            using (UserDialogs.Instance.Loading("Connecting to Service, Please Wait...", null, null, true))
            {
                await Task.Delay(2000);

                if (OldPINEntry.Text == LoginPage.Pin)
                {
                    //Connect to cloud and retrieve email and password combination
                    string url = "https://borno.osoftpay.net/api/taskpayers/SAChangePin?UserName=" + LoginPage.ValidUserMail + "&NewPin=" + ConfirmPIN.Text;

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
                                            await DisplayAlert("NOTIFICATION", "Pin Change Successful. Please Login Again!", "OKAY");
                                            Application.Current.MainPage = new NavigationPage(new LoginPage());
                                        }
                                        else
                                        {
                                            await DisplayAlert("NOTIFICATION", "Error, PIN was not changed!", "OKAY");

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
                else
                {
                    await DisplayAlert("NOTIFICATION", "Can't Confirm Your Old Pin Please Try Again", "OKAY");

                }
            }
        }
    }
}