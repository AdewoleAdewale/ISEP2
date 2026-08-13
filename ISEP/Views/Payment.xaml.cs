using Acr.UserDialogs;
using ISEP.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Payment : ContentPage
    {
        // ── UI Control States ────────────────────────────────────────────────
        private bool _isAnimating = false;
        private bool _isVerifying = false;
        private bool _isDragging = false;
        private bool _isPrinting = false;
        private double _initialTranslationY = 0;
        private double _dragStartY = 0;

        // ── Form Constants ───────────────────────────────────────────────────
        private const double DRAG_THRESHOLD = 100;
        private const double AUTO_CLOSE_TIMEOUT = 5 * 60 * 1000; // 5 Minutes

        // ── Timers & Data Persistence ────────────────────────────────────────
        private System.Timers.Timer _autoCloseTimer;
        private bool _isSheetClosed = false;
        private ReceiptData _lastReceiptData = null;

        public Payment()
        {
            try
            {
                InitializeComponent();
                InitializeSheet();
                SetupAutoCloseTimer();
                PopulateVerifiedData();
            }
            catch (Exception ex) { HandleException(ex, "Constructor"); }
        }

        private void PopulateVerifiedData()
        {
            try
            {
                lblRefrencenum.Text = !string.IsNullOrWhiteSpace(Verify.paymentRefs) ? Verify.paymentRefs : "N/A";
                lblTaxName.Text = !string.IsNullOrWhiteSpace(Verify.taxNames) ? Verify.taxNames : "N/A";
                lblActualAmount.Text = FormatCurrency(Verify.actualAmts);
                lblBalanceToPay.Text = FormatCurrency(Verify.amtLefts);

                if (!string.IsNullOrEmpty(Verify.actualAmts))
                {
                    amount.Text = Verify.actualAmts;
                }
            }
            catch (Exception ex) { HandleException(ex, "PopulateVerifiedData"); }
        }

        private string FormatCurrency(string input)
        {
            if (decimal.TryParse(input, out decimal val))
                return $"₦{val:N2}";
            return "₦0.00";
        }

        private void SetupAutoCloseTimer()
        {
            try
            {
                _autoCloseTimer = new System.Timers.Timer(AUTO_CLOSE_TIMEOUT);
                _autoCloseTimer.Elapsed += OnAutoCloseTimerElapsed;
                _autoCloseTimer.AutoReset = false;
                _autoCloseTimer.Start();
            }
            catch (Exception ex) { HandleException(ex, "SetupAutoCloseTimer"); }
        }

        private async void OnAutoCloseTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (!_isSheetClosed)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        UserDialogs.Instance.Toast("Session expired due to inactivity.");
                        await DismissSheet();
                    });
                }
            }
            catch (Exception ex) { HandleException(ex, "OnAutoCloseTimerElapsed"); }
        }

        private void StopAutoCloseTimer()
        {
            try { _autoCloseTimer?.Stop(); _autoCloseTimer?.Dispose(); _autoCloseTimer = null; }
            catch (Exception ex) { HandleException(ex, "StopAutoCloseTimer"); }
        }

        private void ResetAutoCloseTimer()
        {
            try
            {
                if (_autoCloseTimer != null && !_isSheetClosed)
                {
                    _autoCloseTimer.Stop();
                    _autoCloseTimer.Start();
                }
            }
            catch (Exception ex) { HandleException(ex, "ResetAutoCloseTimer"); }
        }

        private async void InitializeSheet()
        {
            try
            {
                this.Opacity = 0;
                await this.FadeTo(1, 250, Easing.CubicOut);
                await Task.Delay(50);
                await AnimateSheetIn();
                SetupDragGesture();
                await Task.Delay(200);
                PIN.Focus();
            }
            catch (Exception ex) { HandleException(ex, "InitializeSheet"); }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                _isSheetClosed = false;
                SessionService.EnsureSessionRestored();
                PopulateVerifiedData();
            }
            catch (Exception ex) { HandleException(ex, "OnAppearing"); }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            try
            {
                _isSheetClosed = true;
                StopAutoCloseTimer();
            }
            catch (Exception ex) { HandleException(ex, "OnDisappearing"); }
        }

        private async Task AnimateSheetIn()
        {
            try
            {
                if (_isAnimating) return;
                _isAnimating = true;
                await SheetFrame.TranslateTo(0, 0, 350, Easing.SpringOut);
                _isAnimating = false;
            }
            catch (Exception ex) { _isAnimating = false; HandleException(ex, "AnimateSheetIn"); }
        }

        private async Task AnimateSheetOut()
        {
            try
            {
                if (_isAnimating) return;
                _isAnimating = true;
                await SheetFrame.TranslateTo(0, 400, 200, Easing.CubicIn);
                _isAnimating = false;
            }
            catch (Exception ex) { _isAnimating = false; HandleException(ex, "AnimateSheetOut"); }
        }

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            try { if (!_isDragging && !_isAnimating) await DismissSheet(); }
            catch (Exception ex) { HandleException(ex, "OnBackgroundTapped"); }
        }

        private void OnSheetTapped(object sender, EventArgs e) => ResetAutoCloseTimer();

        private void OnPinTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                bool hasPin = !string.IsNullOrWhiteSpace(PIN.Text) && PIN.Text.Trim().Length == 4;
                VerifyButton.IsEnabled = hasPin && !_isVerifying;

                HideMessage();
                ResetAutoCloseTimer();
            }
            catch (Exception ex) { HandleException(ex, "OnPinTextChanged"); }
        }

        private void OnPinCompleted(object sender, EventArgs e)
        {
            try
            {
                if (VerifyButton.IsEnabled) OnVerifyTokenClicked(sender, e);
                ResetAutoCloseTimer();
            }
            catch (Exception ex) { HandleException(ex, "OnPinCompleted"); }
        }

        private async void OnVerifyTokenClicked(object sender, EventArgs e)
        {
            ResetAutoCloseTimer();
            await ProcessPayment();
        }

        private async Task ProcessPayment()
        {
            try
            {
                if (_isVerifying) return;

                string enterAmt = amount.Text?.Trim();
                string enterPin = PIN.Text?.Trim();

                if (string.IsNullOrWhiteSpace(enterAmt) || !decimal.TryParse(enterAmt, out decimal amtParsed) || amtParsed <= 0)
                {
                    ShowWebResponseMessage("Validation Error", "Please enter a valid amount to pay.", false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(enterPin) || enterPin.Length < 4)
                {
                    ShowWebResponseMessage("Validation Error", "Please enter your 4-digit Agent PIN.", false);
                    return;
                }

                if (enterPin != LoginPage.Pin)
                {
                    ShowWebResponseMessage("Security Error", "Invalid Agent PIN. Please check and try again.", false);
                    return;
                }

                _isVerifying = true;
                SetLoadingState(true);

                await PostPaymentRequestAsync();
            }
            catch (Exception ex) { HandleException(ex, "ProcessPayment"); }
            finally
            {
                _isVerifying = false;
                SetLoadingState(false);
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e) => await DismissSheet();

        private void SetLoadingState(bool isLoading)
        {
            try
            {
                LoadingIndicator.IsVisible = isLoading;
                LoadingIndicator.IsRunning = isLoading;
                LoadingContainer.IsVisible = isLoading;

                bool hasPin = !string.IsNullOrWhiteSpace(PIN.Text) && PIN.Text.Trim().Length == 4;

                VerifyButton.IsEnabled = !isLoading && hasPin;
                CancelButton.IsEnabled = !isLoading;
                amount.IsEnabled = !isLoading;
                PIN.IsEnabled = !isLoading;
                VerifyButton.Text = isLoading ? "PROCESSING..." : "PROCESS PAYMENT";
            }
            catch (Exception ex) { HandleException(ex, "SetLoadingState"); }
        }

        private async Task PostPaymentRequestAsync()
        {
            try
            {
                StopAutoCloseTimer();
                SessionService.EnsureSessionRestored();

                if (string.IsNullOrEmpty(LoginPage.ValidUserMail))
                {
                    ShowWebResponseMessage("Session Expired", "User session invalid. Please log in again.", false);
                    return;
                }

                // Ensure global SSL configuration is applied before network call
                ApiClient.ConfigureSSL();

                string url = $"{BrandConfig.ApiBaseUrl}/api/GPayments/v2/Payment";

                // Form-UrlEncoded payload matching Postman setup
                var nvc = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("RefNo", Verify.paymentRefs ?? lblRefrencenum.Text),
                    new KeyValuePair<string, string>("Email", LoginPage.ValidUserMail),
                    new KeyValuePair<string, string>("TaxName", Verify.taxNames ?? lblTaxName.Text),
                    new KeyValuePair<string, string>("AmountPaid", amount.Text?.Trim()),
                    new KeyValuePair<string, string>("Pin", PIN.Text?.Trim())
                };

                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Content = new FormUrlEncodedContent(nvc);

                    // Execute request through the shared, SSL-bypassing ApiClient.Instance handler
                    using (var response = await ApiClient.Instance.SendAsync(request))
                    {
                        string resultString = await response.Content.ReadAsStringAsync();
                        var paymentResponse = JsonConvert.DeserializeObject<PaymentResponseObject>(resultString);

                        if (paymentResponse != null && (paymentResponse.statusCode == "00" || paymentResponse.statusCode == "200"))
                        {
                            ShowWebResponseMessage("Transaction Successful", $"Status: {paymentResponse.status ?? "Part Payment"}\nAmount Paid: ₦{paymentResponse.amountPaid}\nBalance Unpaid: ₦{paymentResponse.amountLeft}", true);

                            var receipt = BuildReceiptData(paymentResponse, Verify.paymentRefs ?? lblRefrencenum.Text);
                            _lastReceiptData = receipt;

                            await AttemptPrintAsync(receipt, isReprint: false);

                            await Task.Delay(3000);
                            await RedirectToLandingPage();
                        }
                        else
                        {
                            string webStatusMsg = paymentResponse?.status ?? paymentResponse?.message ?? "Transaction processing failed on server.";
                            ShowWebResponseMessage("Web Server Response", webStatusMsg, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Displays user-friendly exception string or SSL failure diagnosis
                ShowWebResponseMessage("Network/SSL Error", ApiClient.FriendlyMessage(ex), false);
                HandleException(ex, "PostPaymentRequestAsync");
            }
        }

        private ReceiptData BuildReceiptData(PaymentResponseObject resp, string invoiceToken, bool isReprint = false)
        {
            decimal amtPaid = decimal.TryParse(resp.amountPaid, out decimal p) ? p : (decimal.TryParse(amount.Text, out decimal a) ? a : 0m);
            decimal amtLeft = decimal.TryParse(resp.amountLeft, out decimal l) ? l : 0m;

            var items = new List<ReceiptItem>
            {
                new ReceiptItem
                {
                    Description = !string.IsNullOrEmpty(resp.taxName) ? resp.taxName : (lblTaxName.Text ?? "Revenue Payment"),
                    Amount = amtPaid,
                    SubText = $"Payer ID: {resp.payerId}"
                }
            };

            return new ReceiptData
            {
                StoreName = BrandConfig.ReceiptStoreName,
                StoreAddress = resp.address ?? resp.street ?? BrandConfig.ReceiptAddress,
                StorePhone = resp.phone ?? BrandConfig.ReceiptPhone,
                ReceiptNumber = resp.refNo ?? invoiceToken,
                AgentName = resp.agent ?? LoginPage.ValidUserMail ?? "N/A",
                CollectionPoint = resp.lga ?? "BOIRS Mobile Terminal",
                PrintDate = DateTime.Now,
                Items = items,
                TotalAmount = decimal.TryParse(resp.actualAmt, out decimal act) ? act : (amtPaid + amtLeft),
                AmountPaid = amtPaid,
                AmountLeft = amtLeft,
                BarcodeLabel = $"{BrandConfig.VerifyReceiptUrl}{resp.refNo ?? invoiceToken}",
                FooterLine1 = isReprint ? "*** REPRINTED RECEIPT ***" : BrandConfig.ReceiptFooterLine1,
                FooterLine2 = isReprint ? $"Reprinted: {DateTime.Now:dd MMM yyyy HH:mm} | {BrandConfig.ReceiptFooterLine2}" : BrandConfig.ReceiptFooterLine2
            };
        }

        private async Task AttemptPrintAsync(ReceiptData receipt, bool isReprint)
        {
            if (_isPrinting) return;
            _isPrinting = true;

            try
            {
                bool granted = await BluetoothPermissionHelper.RequestAsync();
                if (!granted)
                {
                    UserDialogs.Instance.Toast("Bluetooth permission required to print receipt.");
                    ShowReprintButton();
                    return;
                }

                var job = await App.PrintJobManager.EnqueueAsync(receipt, logoAssetName: "Logo.png");

                var progress = new Progress<PrintProgress>(p =>
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        switch (p.Status)
                        {
                            case PrintProgressStatus.ChunkStarted:
                                UserDialogs.Instance.Toast($"Printing {p.ChunkName}…");
                                break;
                            case PrintProgressStatus.SessionCompleted:
                                HideReprintButton();
                                UserDialogs.Instance.Toast(isReprint ? "Receipt reprinted!" : "Receipt printed successfully.");
                                break;
                            case PrintProgressStatus.ChunkFailed:
                                ShowReprintButton();
                                break;
                        }
                    }));

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                {
                    await App.PrintJobManager.ExecuteAsync(job.JobId, progress, cts.Token);
                    await App.PrintJobManager.DeleteJobAsync(job.JobId);
                    HideReprintButton();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Payment Print Error]: {ex.Message}");
                UserDialogs.Instance.Toast("Printer issue. Tap Reprint to try again.");
                ShowReprintButton();
            }
            finally
            {
                _isPrinting = false;
            }
        }

        private async void OnReprintReceiptClicked(object sender, EventArgs e)
        {
            if (_lastReceiptData == null)
            {
                UserDialogs.Instance.Toast("No receipt available for reprint.");
                return;
            }

            ReprintButton.IsEnabled = false;
            ReprintButton.Text = "🖨️ REPRINTING...";

            try
            {
                _lastReceiptData.FooterLine1 = "*** REPRINTED RECEIPT ***";
                _lastReceiptData.FooterLine2 = $"Reprinted: {DateTime.Now:dd MMM yyyy HH:mm} | {BrandConfig.ReceiptFooterLine2}";

                await AttemptPrintAsync(_lastReceiptData, isReprint: true);
            }
            finally
            {
                ReprintButton.IsEnabled = true;
                ReprintButton.Text = "🖨️ REPRINT RECEIPT";
            }
        }

        private void ShowReprintButton()
        {
            try
            {
                ReprintButton.IsVisible = true;
                ReprintButton.Opacity = 0;
                ReprintButton.FadeTo(1, 200, Easing.CubicOut);
            }
            catch { }
        }

        private void HideReprintButton()
        {
            try { ReprintButton.IsVisible = false; } catch { }
        }

        private void ShowWebResponseMessage(string title, string message, bool isSuccess)
        {
            try
            {
                MessageContainer.IsVisible = true;
                MessageTitle.Text = title;
                MessageLabel.Text = message;

                if (isSuccess)
                {
                    MessageFrame.BackgroundColor = Color.FromHex("#D1FAE5");
                    MessageIcon.Text = "✅";
                    MessageTitle.TextColor = Color.FromHex("#065F46");
                    MessageLabel.TextColor = Color.FromHex("#065F46");
                }
                else
                {
                    MessageFrame.BackgroundColor = Color.FromHex("#FEE2E2");
                    MessageIcon.Text = "⚠️";
                    MessageTitle.TextColor = Color.FromHex("#991B1B");
                    MessageLabel.TextColor = Color.FromHex("#991B1B");
                }
            }
            catch { }
        }

        private void HideMessage() => MessageContainer.IsVisible = false;

        private async Task RedirectToLandingPage() => await DismissSheet();

        private async Task DismissSheet()
        {
            try
            {
                if (_isAnimating || _isSheetClosed) return;
                _isSheetClosed = true;
                StopAutoCloseTimer();

                await AnimateSheetOut();
                await this.FadeTo(0, 150, Easing.CubicIn);

                if (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync();
                else
                    await Navigation.PopAsync();
            }
            catch (Exception ex) { HandleException(ex, "DismissSheet"); }
        }

        protected override bool OnBackButtonPressed()
        {
            MainThread.BeginInvokeOnMainThread(async () => await DismissSheet());
            return true;
        }

        private async void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            try
            {
                if (_isAnimating || _isVerifying) return;

                switch (e.StatusType)
                {
                    case GestureStatus.Started:
                        _isDragging = true;
                        _initialTranslationY = SheetFrame.TranslationY;
                        _dragStartY = e.TotalY;
                        break;

                    case GestureStatus.Running:
                        if (_isDragging)
                        {
                            var newY = _initialTranslationY + (e.TotalY - _dragStartY);
                            if (newY >= 0)
                            {
                                SheetFrame.TranslationY = newY;
                                this.Opacity = Math.Max(0.2, 1 - (newY / 400));
                            }
                        }
                        break;

                    case GestureStatus.Completed:
                    case GestureStatus.Canceled:
                        if (_isDragging)
                        {
                            _isDragging = false;
                            if (SheetFrame.TranslationY > DRAG_THRESHOLD)
                                await DismissSheet();
                            else
                                await Task.WhenAll(
                                    SheetFrame.TranslateTo(0, 0, 250, Easing.SpringOut),
                                    this.FadeTo(1, 150, Easing.CubicOut));
                        }
                        break;
                }
            }
            catch (Exception ex) { HandleException(ex, "OnPanUpdated"); }
        }

        private void SetupDragGesture()
        {
            try
            {
                var panGesture = new PanGestureRecognizer();
                panGesture.PanUpdated += OnPanUpdated;
                SheetFrame.GestureRecognizers.Add(panGesture);
                DragHandleArea.GestureRecognizers.Add(panGesture);
            }
            catch { }
        }

        private void HandleException(Exception ex, string context)
        {
            System.Diagnostics.Debug.WriteLine($"[Payment Error in {context}]: {ex.Message}");
        }
    }

    internal class PaymentObject
    {
        public string RefNo { get; set; }
        public string Email { get; set; }
        public string AmountPaid { get; set; }
        public string TaxName { get; set; }
        public string Pin { get; set; }
    }

    internal class PaymentResponseObject
    {
        public string refNo { get; set; }
        public string email { get; set; }
        public string amountPaid { get; set; }
        public string amountLeft { get; set; }
        public string actualAmt { get; set; }
        public string totalPaid { get; set; }
        public string taxName { get; set; }
        public string status { get; set; }
        public string payerId { get; set; }
        public string payerName { get; set; }
        public string lga { get; set; }
        public string street { get; set; }
        public string phone { get; set; }
        public string statusCode { get; set; }
        public string address { get; set; }
        public string agent { get; set; }
        public string message { get; set; }
    }
}