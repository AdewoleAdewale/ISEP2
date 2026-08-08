using Acr.UserDialogs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ISEP.Services;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Dashboard : ContentPage
    {
        private CancellationTokenSource _cts;
        private bool _isBusy = false;

        public static string superAgent { get; set; }
        public static string agent { get; set; }
        public static string cashoutBalance { get; set; }

        public Dashboard()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            PopulateIdentity();
            _ = LoadTransactionsAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _cts?.Cancel();
        }

        private void PopulateIdentity()
        {
            try
            {
                AgentNameLabel.Text = string.IsNullOrWhiteSpace(LoginPage.Name) ? LoginPage.ValidUserMail : LoginPage.Name;
                MdaLabel.Text = BrandConfig.OrganisationName;
                WelcomeLabel.Text = GetGreeting();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] PopulateIdentity: {ex.Message}");
            }
        }

        private static string GetGreeting()
        {
            int h = DateTime.Now.Hour;
            if (h < 12) return "Good morning 🌅";
            if (h < 17) return "Good afternoon ☀️";
            return "Good evening 🌙";
        }

        private async Task LoadTransactionsAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            SetTxnState(loading: true, empty: false, error: false, list: false);

            try
            {
                string email = LoginPage.ValidUserMail?.Trim();
                if (string.IsNullOrEmpty(email))
                {
                    SetTxnError("User email not found. Please log in again.");
                    return;
                }

                string url = $"{BrandConfig.ApiBaseUrl}/api/GPayments/gettransaction?Email={Uri.EscapeDataString(email)}&SearchFrom={DateTime.Now.AddDays(-30):MM/dd/yyyy}&SearchTo={DateTime.Now:MM/dd/yyyy}";
                var all = await ApiClient.GetAsync<List<InvoiceRecord>>(url);

                if (all == null || all.Count == 0)
                {
                    SetTxnState(loading: false, empty: true, error: false, list: false);
                    StatInvoices.Text = "0";
                    StatTotalAmount.Text = "₦0";
                    StatPending.Text = "0";
                    return;
                }

                int totalCount = all.Count;
                double totalAmount = (double)all.Sum(i => i.amount);

                Device.BeginInvokeOnMainThread(() =>
                {
                    StatInvoices.Text = totalCount.ToString();
                    StatTotalAmount.Text = FormatAmount(totalAmount);
                    StatPending.Text = "0";

                    TransactionListView.HeightRequest = Math.Min(all.Count * 75, 400);
                    TransactionListView.ItemsSource = all.Take(5).ToList();
                    SetTxnState(loading: false, empty: false, error: false, list: true);
                });
            }
            catch (Exception ex)
            {
                SetTxnError("Could not update collection metrics.");
                System.Diagnostics.Debug.WriteLine($"[Dashboard] LoadTxn: {ex.Message}");
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void SetTxnState(bool loading, bool empty, bool error, bool list)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                TxnLoadingCard.IsVisible = loading;
                TxnEmptyCard.IsVisible = empty;
                TxnErrorCard.IsVisible = error;
                TransactionListView.IsVisible = list;
            });
        }

        private void SetTxnError(string message)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                TxnErrorLabel.Text = message;
                SetTxnState(loading: false, empty: false, error: true, list: false);
            });
        }

        private static string FormatAmount(double amount)
        {
            if (amount >= 1_000_000) return $"₦{amount / 1_000_000:F1}M";
            if (amount >= 1_000) return $"₦{amount / 1_000:F1}K";
            return $"₦{amount:N0}";
        }

        private async void OnDirectPaymentTapped(object sender, EventArgs e) => await Navigation.PushAsync(new Views.Payment());
        private async void OnVerifyPaymentTapped(object sender, EventArgs e) => await Navigation.PushAsync(new Views.Verify());
        private async void OnTaxReportTapped(object sender, EventArgs e) => await Navigation.PushAsync(new Views.History());
        private async void OnInvoiceHistoryTapped(object sender, EventArgs e) => await Navigation.PushAsync(new Views.History());
        private async void OnTxnRetryTapped(object sender, EventArgs e) => await LoadTransactionsAsync();

        private async void TestPrinter_Clicked(object sender, EventArgs e)
        {
            using (UserDialogs.Instance.Loading("Running Printer Diagnostics..."))
            {
                try
                {
                    await App.Printer.PrintTestPageAsync();
                    UserDialogs.Instance.Toast("Test receipt sent to printer.");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Printer Diagnostics", ex.Message, "OK");
                }
            }
        }

        private async void OnLogoutTapped(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Sign Out", "Are you sure you want to log out?", "Logout", "Cancel");
            if (confirm)
            {
                SessionService.ClearSession();
                App.IsUserLoggedIn = false;
                Application.Current.MainPage = new NavigationPage(new LoginPage());
            }
        }
    }

    public class InvoiceRecord
    {
        [JsonProperty("payerName")]
        public string payer_Name { get; set; }

        [JsonProperty("amount")]
        public decimal amount { get; set; }

        [JsonProperty("serviceName")]
        public string service_Name { get; set; }

        [JsonProperty("status")]
        public string status { get; set; }
    }
}