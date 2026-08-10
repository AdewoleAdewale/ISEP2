using Acr.UserDialogs;
using ISEP.Services;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LoginPage : ContentPage
    {
        // ─────────────────────────────────────────────────────────────
        //  Session statics consumed by Dashboard, Payment, CashoutPage,
        //  ChangePassword and ChangePin. Names unchanged.
        // ─────────────────────────────────────────────────────────────
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

        private const string LoginEndpoint =
            "https://borno.osoftpay.net/api/taskpayers/SagentLogin";

        private const string SupportLine =
            "Contact Support: 08027229331 or 08165932680";

        private bool _isAuthenticating;

        public LoginPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            RestoreSavedCredentials();
        }

        /// <summary>
        /// Pre-fills the form from SecureStorage/Preferences. Wrapped because
        /// storage access can throw on devices with a broken keystore, and a
        /// throw here would kill the app before the page ever renders.
        /// </summary>
        private void RestoreSavedCredentials()
        {
            try
            {
                if (RememberMeCheck != null)
                    RememberMeCheck.IsChecked = SessionService.IsRememberMe;

                if (RememberPasswordCheck != null)
                    RememberPasswordCheck.IsChecked = SessionService.IsRememberPassword;

                if (SessionService.IsRememberMe && EmailEntry != null)
                    EmailEntry.Text = SessionService.SavedEmail;

                if (SessionService.IsRememberPassword && PasswordEntry != null)
                    PasswordEntry.Text = SessionService.SavedPassword;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Login] RestoreSavedCredentials: {ex}");
            }
        }

        // ═════════════════════════════════════════════════════════════
        //  SIGN IN
        //
        //  This is an `async void` event handler. Any exception that
        //  escapes it is NOT catchable by the caller — Android tears the
        //  process down immediately. That is the crash. Everything below
        //  the outer try/catch exists so nothing ever escapes.
        // ═════════════════════════════════════════════════════════════
        private async void Button_Clicked(object sender, EventArgs e)
        {
            if (_isAuthenticating) return;          // block double-tap
            _isAuthenticating = true;

            var signInButton = sender as Button;
            if (signInButton != null) signInButton.IsEnabled = false;

            IDisposable loading = null;
            LoginResponse result = null;

            try
            {
                // ── 1. Read + validate input ────────────────────────
                // Entry.Text is null until the user types. The old code
                // called .Trim() on it directly -> NullReferenceException
                // on an empty form, before any network call happened.
                string email = EmailEntry?.Text?.Trim();
                string password = PasswordEntry?.Text?.Trim();

                if (string.IsNullOrWhiteSpace(email))
                {
                    await ShowAlertAsync("NOTIFICATION", "Please enter your email address.", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    await ShowAlertAsync("NOTIFICATION", "Please enter your password.", "OK");
                    return;
                }

                // ── 2. Fail fast when there is no connectivity ──────
                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                {
                    await ShowAlertAsync(
                        "NO CONNECTION",
                        "You are not connected to the internet. Turn on mobile data or Wi-Fi and try again.",
                        "OK");
                    return;
                }

                loading = UserDialogs.Instance.Loading(
                    "Connecting to Service, Please Wait...", null, null, true, MaskType.Gradient);

                // ── 3. Build the request ────────────────────────────
                // Credentials MUST be URL-encoded. An unencoded '&', '+',
                // '#' or '%' in a password silently truncates the query
                // string and the server sees the wrong credentials.
                string url = LoginEndpoint
                           + "?UserName=" + Uri.EscapeDataString(email)
                           + "&Password=" + Uri.EscapeDataString(password);

                // ── 4. Call the API ─────────────────────────────────
                // Goes through the central ApiClient, so login uses the
                // same TLS policy, timeout and retry as every other page.
                // await, never .Result — the old `.Result` blocked the UI
                // thread inside a Xamarin SynchronizationContext, which
                // deadlocks and ANRs, and rethrows as AggregateException.
                string json = await ApiClient.GetStringAsync(url);

                // ── 5. Parse ────────────────────────────────────────
                // A gateway timeout or WAF block returns an HTML page, not
                // JSON. Deserializing that throws JsonReaderException.
                if (string.IsNullOrWhiteSpace(json))
                {
                    loading?.Dispose();
                    loading = null;

                    await ShowAlertAsync(
                        "NOTIFICATION",
                        $"The server returned an empty response. Please try again.\n\n{SupportLine}",
                        "TRY AGAIN");
                    return;
                }

                try
                {
                    result = JsonConvert.DeserializeObject<LoginResponse>(json);
                }
                catch (JsonException jex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Login] Bad payload: {jex.Message}");
                    System.Diagnostics.Debug.WriteLine(
                        $"[Login] Raw: {json.Substring(0, Math.Min(300, json.Length))}");

                    loading?.Dispose();
                    loading = null;

                    await ShowAlertAsync(
                        "NOTIFICATION",
                        $"The server sent an unexpected response. Please try again.\n\n{SupportLine}",
                        "TRY AGAIN");
                    return;
                }

                // Deserialize returns null for the literal "null" body.
                if (result == null)
                {
                    loading?.Dispose();
                    loading = null;

                    await ShowAlertAsync(
                        "NOTIFICATION",
                        $"Could not read the login response.\n\n{SupportLine}",
                        "TRY AGAIN");
                    return;
                }

                // ── 6. Handle a rejected login ──────────────────────
                if (result.responseCode != "00")
                {
                    loading?.Dispose();
                    loading = null;

                    string reason = string.IsNullOrWhiteSpace(result.message)
                        ? "Can't Validate Your Details"
                        : result.message;

                    await ShowAlertAsync("NOTIFICATION", $"{reason}.\n\n{SupportLine}", "TRY AGAIN");
                    return;
                }

                // ── 7. responseCode == "00" but detail missing ──────
                // The old code went straight to result.detail.password
                // here. If the API ever omits `detail`, that is an
                // unhandled NullReferenceException = instant crash.
                if (result.detail == null)
                {
                    loading?.Dispose();
                    loading = null;

                    await ShowAlertAsync(
                        "NOTIFICATION",
                        $"Login succeeded but your profile could not be loaded.\n\n{SupportLine}",
                        "TRY AGAIN");
                    return;
                }

                // ── 8. Populate session ─────────────────────────────
                Detail d = result.detail;

                ValidUserMail = email;
                Passwords = d.password;
                Name = d.name;
                Token = d.token;
                Pin = d.pin;
                Super_Agent = d.SuperAgent;
                accountbalance = d.account_Balance;
                tradingstatus = d.tradingStatus;
                accountnumbers = d.accountNumber;
                Banks = d.bank;

                App.IsUserLoggedIn = true;

                PersistSession(email, password, d.token, json);

                // ── 9. Dismiss the dialog BEFORE navigating ─────────
                // Disposing a UserDialogs loader after MainPage has been
                // swapped throws (its host activity/window is gone).
                loading?.Dispose();
                loading = null;

                await NavigateToDashboardAsync();
            }
            catch (ApiException aex)
            {
                // Non-success status or unreadable body, already retried
                // by ApiClient. StatusCode/ResponseBody are available for
                // logging without re-parsing strings.
                System.Diagnostics.Debug.WriteLine($"[Login] ApiException {aex.StatusCode}: {aex.Message}");

                loading?.Dispose();
                loading = null;

                await ShowAlertAsync(
                    "SIGN IN FAILED",
                    $"{ApiClient.FriendlyMessage(aex)}\n\n{SupportLine}",
                    "TRY AGAIN");
            }
            catch (OperationCanceledException)
            {
                // Covers TaskCanceledException from the per-request
                // timeout inside ApiClient.
                System.Diagnostics.Debug.WriteLine("[Login] Request timed out.");

                loading?.Dispose();
                loading = null;

                await ShowAlertAsync(
                    "TIMEOUT",
                    "The server took too long to respond. Check your connection and try again.",
                    "TRY AGAIN");
            }
            catch (HttpRequestException hex)
            {
                // DNS failure, connection refused, TLS handshake failure.
                // The real reason is usually in InnerException.
                System.Diagnostics.Debug.WriteLine($"[Login] HttpRequestException: {hex}");
                System.Diagnostics.Debug.WriteLine($"[Login] Inner: {hex.InnerException?.Message}");

                loading?.Dispose();
                loading = null;

                await ShowAlertAsync(
                    "CONNECTION FAILED",
                    $"Could not reach the payment service. Please check your internet " +
                    $"connection and try again.\n\n{SupportLine}",
                    "TRY AGAIN");
            }
            catch (Exception ex)
            {
                // Final net. Nothing gets past this, so nothing kills
                // the process from inside this handler.
                System.Diagnostics.Debug.WriteLine($"[Login] Unhandled: {ex}");

                loading?.Dispose();
                loading = null;

                await ShowAlertAsync(
                    "UNEXPECTED ERROR",
                    $"Something went wrong while signing you in. Please try again.\n\n" +
                    $"{SupportLine}",
                    "TRY AGAIN");
            }
            finally
            {
                // Belt and braces: if any path above returned early
                // without disposing, the loader dies here rather than
                // hanging over the UI forever.
                try { loading?.Dispose(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Login] Dialog dispose: {ex.Message}"); }

                _isAuthenticating = false;
                if (signInButton != null) signInButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Writes credentials to storage. Isolated so a keystore failure
        /// on one device model cannot block an otherwise valid login.
        /// </summary>
        private void PersistSession(string email, string password, string token, string userDataJson)
        {
            try
            {
                SessionService.IsRememberMe = RememberMeCheck?.IsChecked ?? false;
                SessionService.IsRememberPassword = RememberPasswordCheck?.IsChecked ?? false;
                SessionService.SaveSession(email, password, token, userDataJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Login] PersistSession: {ex}");
            }
        }

        /// <summary>
        /// Page construction and OnAppearing run user code that can throw.
        /// If Dashboard throws here the user sees a message instead of the
        /// app vanishing — which looks identical to a login crash.
        /// </summary>
        private async Task NavigateToDashboardAsync()
        {
            try
            {
                var dashboard = new Dashboard();

                if (Device.IsInvokeRequired)
                    Device.BeginInvokeOnMainThread(() =>
                        Application.Current.MainPage = new NavigationPage(dashboard));
                else
                    Application.Current.MainPage = new NavigationPage(dashboard);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Login] Navigation failed: {ex}");

                await ShowAlertAsync(
                    "NOTIFICATION",
                    $"Signed in, but the dashboard could not be opened. Please restart " +
                    $"the app.\n\n{SupportLine}",
                    "OK");
            }
        }

        /// <summary>
        /// DisplayAlert throws if the page is detached or the call is off
        /// the main thread. Never let an error message become the crash.
        /// </summary>
        private async Task ShowAlertAsync(string title, string message, string cancel)
        {
            try
            {
                var page = Application.Current?.MainPage;
                if (page == null) return;

                await page.DisplayAlert(title, message, cancel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Login] ShowAlert: {ex.Message}");
            }
        }

        private async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
        {
            await ShowAlertAsync(
                "NOTIFICATION",
                "Hello, you can reach out to support with these numbers: 08144993882 or 08030523208",
                "OK");
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