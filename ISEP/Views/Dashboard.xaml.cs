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
        private bool _balanceVisible = true;

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
            SessionService.EnsureSessionRestored();
            PopulateIdentity();
            PopulateWallet();
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
                // The API pads these values ("TESTING  WALLET ") — collapse
                // the whitespace or the layout shows a ragged trailing gap.
                string agent = Clean(LoginPage.Name);
                AgentNameLabel.Text = string.IsNullOrWhiteSpace(agent) ? Clean(LoginPage.ValidUserMail) : agent;
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

        // ════════════════════════════════════════════════════════
        //  WALLET / ACCOUNT DETAILS
        //  Source: the `detail` object from SagentLogin, parked on the
        //  LoginPage statics. Nothing here re-hits the network.
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Collapses the double/trailing spaces the API sends back
        /// ("TESTING  WALLET ") into single-spaced, trimmed text.
        /// </summary>
        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"\s+", " ");
        }

        /// <summary>
        /// The balance arrives pre-formatted ("73,176.30"), so it is NOT
        /// re-parsed — parsing it with the device locale would read the
        /// comma as a decimal separator and show ₦73.18.
        /// </summary>
        private static string FormatBalance(string raw)
        {
            string v = Clean(raw);
            if (string.IsNullOrEmpty(v)) return "₦0.00";
            return v.StartsWith("₦") ? v : "₦" + v;
        }

        private void PopulateWallet()
        {
            try
            {
                AccountNameLabel.Text = string.IsNullOrEmpty(Clean(LoginPage.Super_Agent))
                    ? Clean(LoginPage.Name)
                    : Clean(LoginPage.Super_Agent);

                BankLabel.Text = string.IsNullOrEmpty(Clean(LoginPage.Banks))
                    ? "—"
                    : Clean(LoginPage.Banks);

                AccountNumberLabel.Text = string.IsNullOrEmpty(Clean(LoginPage.accountnumbers))
                    ? "—"
                    : Clean(LoginPage.accountnumbers);

                _balanceVisible = true;
                BalanceLabel.Text = FormatBalance(LoginPage.accountbalance);
                BalanceToggle.Text = "🙈";

                ApplyTradingStatus(Clean(LoginPage.tradingstatus));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] PopulateWallet: {ex}");
            }
        }

        private void ApplyTradingStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) status = "UNKNOWN";

            TradingStatusLabel.Text = status.ToUpperInvariant();

            bool active = status.Equals("Active", StringComparison.OrdinalIgnoreCase);

            // Amber, not red — an inactive wallet is a state to notice,
            // not an error the officer caused.
            StatusBadge.BackgroundColor = active ? Color.FromHex("#DCFCE7") : Color.FromHex("#FEF3C7");
            TradingStatusLabel.TextColor = active ? Color.FromHex("#059669") : Color.FromHex("#B45309");
        }

        private void OnToggleBalanceTapped(object sender, EventArgs e)
        {
            try
            {
                _balanceVisible = !_balanceVisible;
                BalanceLabel.Text = _balanceVisible ? FormatBalance(LoginPage.accountbalance) : "₦ ••••••";
                BalanceToggle.Text = _balanceVisible ? "🙈" : "👁️";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] ToggleBalance: {ex.Message}");
            }
        }

        // ── Clipboard ───────────────────────────────────────────────

        private async void OnCopyAccountNumberTapped(object sender, EventArgs e)
        {
            string acct = Clean(LoginPage.accountnumbers);

            if (string.IsNullOrEmpty(acct))
            {
                UserDialogs.Instance.Toast("No account number available.");
                return;
            }

            await CopyAsync(acct, $"Account number {acct} copied");
        }

        private async void OnCopyDetailsTapped(object sender, EventArgs e)
        {
            string acct = Clean(LoginPage.accountnumbers);

            if (string.IsNullOrEmpty(acct))
            {
                UserDialogs.Instance.Toast("No account details available.");
                return;
            }

            // Bank / number / name, in the order a Nigerian transfer form
            // asks for them — so the whole block can be pasted into chat.
            string block =
                $"Bank: {Clean(LoginPage.Banks)}\n" +
                $"Account Number: {acct}\n" +
                $"Account Name: {(string.IsNullOrEmpty(Clean(LoginPage.Super_Agent)) ? Clean(LoginPage.Name) : Clean(LoginPage.Super_Agent))}";

            await CopyAsync(block, "Account details copied");
        }

        /// <summary>
        /// Clipboard access is a platform call and throws on some OEM
        /// builds. It is never worth crashing the dashboard over a copy.
        /// </summary>
        private async Task CopyAsync(string text, string confirmation)
        {
            try
            {
                await Xamarin.Essentials.Clipboard.SetTextAsync(text);
                UserDialogs.Instance.Toast(confirmation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Clipboard: {ex}");
                UserDialogs.Instance.Toast("Could not copy to clipboard.");
            }
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

        private async void OnCashoutTapped(object sender, EventArgs e) => await Navigation.PushAsync(new Views.CashoutPage());
        private async void OnChangePinTapped(object sender, EventArgs e) => await Navigation.PushAsync(new Views.ChangePin());
        private async void OnChangePasswordTapped(object sender, EventArgs e) => await Navigation.PushAsync(new Views.ChangePassword());

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