using Acr.UserDialogs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ISEP.Services;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Payment : ContentPage
    {
        // ── UI Animation & Control States ────────────────────────────────────
        private bool _isAnimating = false;
        private bool _isVerifying = false;
        private bool _isDragging = false;
        private bool _isPrinting = false;
        private double _initialTranslationY = 0;
        private double _dragStartY = 0;

        // ── Form Constants ───────────────────────────────────────────────────
        private const int TOKEN_MIN_LENGTH = 6;
        private const int TOKEN_MAX_LENGTH = 30;
        private const string TOKEN_PATTERN = @"^[a-zA-Z0-9\-_]{6,30}$";
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
                InitializePaymentMethods();

                // Auto-fill from Verify screen if navigated directly
                if (!string.IsNullOrEmpty(Verify.paymentRefs))
                {
                    inputtoken.Text = Verify.paymentRefs;
                }
            }
            catch (Exception ex) { HandleException(ex, "Constructor"); }
        }

        private void InitializePaymentMethods()
        {
            try { paymentmethod.SelectedIndex = 0; }
            catch (Exception ex) { HandleException(ex, "InitializePaymentMethods"); }
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
                if (!_isSheetClosed && string.IsNullOrWhiteSpace(inputtoken.Text))
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
                { _autoCloseTimer.Stop(); _autoCloseTimer.Start(); }
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
                inputtoken.Focus();
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

                // Ensure token auto-fills if navigated from Verify screen
                if (string.IsNullOrEmpty(inputtoken.Text) && !string.IsNullOrEmpty(Verify.paymentRefs))
                {
                    inputtoken.Text = Verify.paymentRefs;
                }
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

        private void OnTokenTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var token = e.NewTextValue ?? string.Empty;
                bool isValid = ValidateTokenFormat(token);
                bool hasPM = paymentmethod.SelectedIndex >= 0;

                VerifyButton.IsEnabled = isValid && hasPM && !_isVerifying;
                UpdateInputFieldStyle(isValid, token.Length > 0);

                if (token.Length > 0) { HideMessage(); ResetAutoCloseTimer(); }
            }
            catch (Exception ex) { HandleException(ex, "OnTokenTextChanged"); }
        }

        private void OnPaymentMethodChanged(object sender, EventArgs e)
        {
            try
            {
                var token = inputtoken.Text ?? string.Empty;
                bool isValid = ValidateTokenFormat(token);
                bool hasPM = paymentmethod.SelectedIndex >= 0;
                VerifyButton.IsEnabled = isValid && hasPM && !_isVerifying;
                ResetAutoCloseTimer();
            }
            catch (Exception ex) { HandleException(ex, "OnPaymentMethodChanged"); }
        }

        private void OnTokenCompleted(object sender, EventArgs e)
        {
            try
            {
                if (VerifyButton.IsEnabled) OnVerifyTokenClicked(sender, e);
                ResetAutoCloseTimer();
            }
            catch (Exception ex) { HandleException(ex, "OnTokenCompleted"); }
        }

        private bool ValidateTokenFormat(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token)) return false;
                if (token.Length < TOKEN_MIN_LENGTH || token.Length > TOKEN_MAX_LENGTH) return false;
                return Regex.IsMatch(token, TOKEN_PATTERN);
            }
            catch (Exception ex) { HandleException(ex, "ValidateTokenFormat"); return false; }
        }

        private void UpdateInputFieldStyle(bool isValid, bool hasContent)
        {
            try
            {
                if (!hasContent)
                    TokenInputFrame.BorderColor = Color.FromHex("#CBD5E1");
                else if (isValid)
                    TokenInputFrame.BorderColor = Color.FromHex("#059669");
                else
                    TokenInputFrame.BorderColor = Color.FromHex("#DC2626");
            }
            catch (Exception ex) { HandleException(ex, "UpdateInputFieldStyle"); }
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

                var token = inputtoken.Text?.Trim();
                var selectedPaymentMethod = paymentmethod.SelectedItem?.ToString();

                if (string.IsNullOrWhiteSpace(token))
                {
                    ShowErrorMessage("Please enter an Invoice / RRR number");
                    return;
                }
                if (!ValidateTokenFormat(token))
                {
                    ShowErrorMessage("Invalid Invoice format. Please check and try again.");
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

                var token = inputtoken.Text ?? string.Empty;
                bool hasPM = paymentmethod.SelectedIndex >= 0;

                VerifyButton.IsEnabled = !isLoading && ValidateTokenFormat(token) && hasPM;
                CancelButton.IsEnabled = !isLoading;
                inputtoken.IsEnabled = !isLoading;
                paymentmethod.IsEnabled = !isLoading;
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

                string invoiceToken = inputtoken.Text?.Trim() ?? "";
                string selectedPaymentMethod = paymentmethod.SelectedItem?.ToString() ?? "Cash";

                if (string.IsNullOrEmpty(LoginPage.ValidUserMail))
                {
                    ShowErrorMessage("User session invalid. Please log in again.");
                    return;
                }

                var payload = new
                {
                    RefNo = invoiceToken,
                    Email = LoginPage.ValidUserMail,
                    TaxName = Verify.taxNames ?? "Revenue Collection",
                    AmountPaid = Verify.actualAmts ?? "0",
                    PaymentMethod = selectedPaymentMethod
                };

                // Use central ApiClient for SSL safety
                string url = $"{BrandConfig.ApiBaseUrl}/api/GPayments/v2/Payment";
                var paymentResponse = await ApiClient.PostAsync<PaymentResponseObject>(url, payload);

                if (paymentResponse != null && (paymentResponse.statusCode == "00" || paymentResponse.statusCode == "200"))
                {
                    ShowSuccessMessage($"Payment successful for Ref: {invoiceToken}");

                    var receipt = BuildReceiptData(paymentResponse, invoiceToken);
                    _lastReceiptData = receipt;

                    await AttemptPrintAsync(receipt, isReprint: false);

                    await Task.Delay(3000);
                    await DismissSheet();
                }
                else
                {
                    string msg = paymentResponse?.status ?? "Transaction processing failed.";
                    ShowErrorMessage(msg);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Payment error: {ex.Message}");
                HandleException(ex, "PostPaymentRequestAsync");
            }
        }

        private ReceiptData BuildReceiptData(PaymentResponseObject resp, string invoiceToken, bool isReprint = false)
        {
            decimal amtPaid = decimal.TryParse(resp.amountPaid, out decimal p) ? p : 0m;
            decimal amtLeft = decimal.TryParse(resp.amountLeft, out decimal l) ? l : 0m;

            var items = new List<ReceiptItem>
            {
                new ReceiptItem
                {
                    Description = !string.IsNullOrEmpty(resp.taxName) ? resp.taxName : "Revenue Payment",
                    Amount = amtPaid,
                    SubText = $"Payer ID: {resp.payerId}"
                }
            };

            return new ReceiptData
            {
                StoreName = BrandConfig.ReceiptStoreName,
                StoreAddress = BrandConfig.ReceiptAddress,
                StorePhone = BrandConfig.ReceiptPhone,
                ReceiptNumber = resp.refNo ?? invoiceToken,
                AgentName = LoginPage.ValidUserMail ?? "N/A",
                CollectionPoint = "BOIRS Mobile Terminal",
                PrintDate = DateTime.Now,
                Items = items,
                TotalAmount = amtPaid + amtLeft,
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

        private void ShowErrorMessage(string message) => ShowMessage(message, "❌", Color.FromHex("#FEE2E2"), Color.FromHex("#991B1B"));

        private void ShowSuccessMessage(string message) => ShowMessage(message, "✅", Color.FromHex("#D1FAE5"), Color.FromHex("#065F46"));

        private void ShowMessage(string message, string icon, Color backgroundColor, Color textColor)
        {
            try
            {
                MessageContainer.IsVisible = true;
                MessageFrame.BackgroundColor = backgroundColor;
                MessageIcon.Text = icon;
                MessageIcon.TextColor = textColor;
                MessageLabel.Text = message;
                MessageLabel.TextColor = textColor;
            }
            catch { }
        }

        private void HideMessage() => MessageContainer.IsVisible = false;

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
    

internal class PaymentObject
        {
            public string RefNo { get; set; }
            public string Email { get; set; }
            public string AmountPaid { get; set; }
            public string TaxName { get; set; }
            public string Pin { get; set; }



        }
    }

    internal class PaymentResponseObject
    {
        public string statusCode { get; set; }
        public string amountLeft { get; set; }
        public string refNo { get; set; }
        public string status { get; set; }
        public string phone { get; set; }
        public string street { get; set; }
        public string payerName { get; set; }
        public string payerId { get; set; }
        public string taxName { get; set; }
        public string amountPaid { get; set; }

    }


}