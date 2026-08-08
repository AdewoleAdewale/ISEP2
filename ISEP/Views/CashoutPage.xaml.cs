using Acr.UserDialogs;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ISEP.Services;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class CashoutPage : ContentPage
    {
        public CashoutPage()
        {
            InitializeComponent();
            AgentSupervisor.Text = Dashboard.superAgent;
            Agentname.Text = string.IsNullOrWhiteSpace(LoginPage.Name) ? LoginPage.ValidUserMail : LoginPage.Name;
            CashoutBalance.Text = "₦" + Dashboard.cashoutBalance;
        }

        private async void ProcessCashout_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Password.Text) || Password.Text.Length < 4)
            {
                await DisplayAlert("NOTIFICATION", "Please enter a valid 4-digit PIN.", "OK");
                return;
            }

            using (UserDialogs.Instance.Loading("Processing Cashout...", null, null, true))
            {
                try
                {
                    var payload = new { Agent = LoginPage.ValidUserMail };
                    string url = "https://collection.osoftpay.net/api/S_CashOutCall";

                    var client = ApiClient.CreateClient();
                    client.DefaultRequestHeaders.Add("Super_Agent", LoginPage.Token);
                    client.DefaultRequestHeaders.Add("TradingPin", Password.Text);

                    var response = await ApiClient.PostAsync<CashOutResponse>(url, payload);

                    if (response != null && response.status == "00")
                    {
                        await DisplayAlert("NOTIFICATION", $"{response.message} Amount: ₦{response.details?.amountReceived}", "THANK YOU");
                        await Navigation.PopAsync();
                    }
                    else
                    {
                        await DisplayAlert("NOTIFICATION", response?.message ?? "Cashout failed.", "TRY AGAIN");
                    }
                }
                catch (Exception ex)
                {
                    UserDialogs.Instance.Toast("Could not process request. Please try again.");
                    System.Diagnostics.Debug.WriteLine($"Cashout error: {ex.Message}");
                }
            }
        }

        internal class CashOutResponse
        {
            public string status { get; set; }
            public string message { get; set; }
            public Details details { get; set; }
        }

        internal class Details
        {
            public string superAgent { get; set; }
            public string amountReceived { get; set; }
            public string agent { get; set; }
        }
    }
}