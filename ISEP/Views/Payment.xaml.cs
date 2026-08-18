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
        private bool _isAnimating = false;
        private bool _isVerifying = false;
        private bool _isDragging = false;
        private double _initialTranslationY = 0;
        private double _dragStartY = 0;
        private bool _isPrinting = false;
        private ReceiptData _lastReceiptData = null;

        private const double DRAG_THRESHOLD = 120;
        private const double AUTO_CLOSE_TIMEOUT = 5 * 60 * 1000;

        private System.Timers.Timer _autoCloseTimer;
        private bool _isSheetClosed = false;

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

                decimal.TryParse(Verify.actualAmts, out decimal actualAmt);
                decimal.TryParse(Verify.amtLefts, out decimal amtLeft);

                bool hasPreviousPartPayment = (actualAmt > 0 && amtLeft > 0 && amtLeft < actualAmt);

                if (hasPreviousPartPayment)
                {
                    amount.Text = amtLeft.ToString("0.##");
                    amount.IsReadOnly = false;
                    lblPartPaymentNote.IsVisible = false;
                }
                else
                {
                    amount.Text = actualAmt.ToString("0.##");
                    amount.IsReadOnly = true;
                    lblPartPaymentNote.IsVisible = true;
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
                _autoCloseTimer.Elapsed += async (s, e) =>
                {
                    if (!_isSheetClosed)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            UserDialogs.Instance.Toast("Session expired due to inactivity.");
                            await DismissSheet();
                        });
                    }
                };
                _autoCloseTimer.AutoReset = false;
                _autoCloseTimer.Start();
            }
            catch (Exception ex) { HandleException(ex, "SetupAutoCloseTimer"); }
        }

        private void StopAutoCloseTimer()
        {
            try { _autoCloseTimer?.Stop(); _autoCloseTimer?.Dispose(); _autoCloseTimer = null; }
            catch { }
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
            catch { }
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
            catch { }
        }

        private async Task AnimateSheetIn()
        {
            try
            {
                if (_isAnimating) return;
                _isAnimating = true;
                await SheetFrame.TranslateTo(0, 0, 300, Easing.SpringOut);
                _isAnimating = false;
            }
            catch { _isAnimating = false; }
        }

        private async Task AnimateSheetOut()
        {
            try
            {
                if (_isAnimating) return;
                _isAnimating = true;
                await SheetFrame.TranslateTo(0, 500, 200, Easing.CubicIn);
                _isAnimating = false;
            }
            catch { _isAnimating = false; }
        }

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            try
            {
                if (!_isDragging && !_isAnimating)
                    await DismissSheet();
            }
            catch { }
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
            catch { }
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

                decimal.TryParse(Verify.actualAmts, out decimal actualAmt);
                decimal.TryParse(Verify.amtLefts, out decimal amtLeft);
                bool hasPreviousPartPayment = (actualAmt > 0 && amtLeft > 0 && amtLeft < actualAmt);

                if (!hasPreviousPartPayment && amtParsed < actualAmt)
                {
                    ShowWebResponseMessage("Part-Payment Restricted", $"This notice requires the full payment of ₦{actualAmt:N2}.", false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(enterPin) || enterPin.Length < 4)
                {
                    ShowWebResponseMessage("Validation Error", "Please enter your 4-digit Agent PIN.", false);
                    return;
                }

                if (enterPin != LoginPage.Pin)
                {
                    ShowWebResponseMessage("Security Error", "Invalid Agent PIN. Please verify and retry.", false);
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
            catch { }
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

                ApiClient.ConfigureSSL();

                string url = $"{BrandConfig.ApiBaseUrl}/api/GPayments/v2/Payment";

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

                    using (var response = await ApiClient.Instance.SendAsync(request))
                    {
                        string resultString = await response.Content.ReadAsStringAsync();
                        var paymentResponse = JsonConvert.DeserializeObject<PaymentResponseObject>(resultString);

                        if (paymentResponse != null && (paymentResponse.statusCode == "00" || paymentResponse.statusCode == "200"))
                        {
                            var receipt = BuildReceiptData(paymentResponse, Verify.paymentRefs ?? lblRefrencenum.Text);
                            _lastReceiptData = receipt;

                            DisplaySuccessBottomSheet(paymentResponse);

                            await TriggerReceiptPrintAsync(receipt);
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
                ShowWebResponseMessage("Network/SSL Error", ApiClient.FriendlyMessage(ex), false);
                HandleException(ex, "PostPaymentRequestAsync");
            }
        }

        private void DisplaySuccessBottomSheet(PaymentResponseObject resp)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                PaymentFormView.IsVisible = false;

                lblSuccessRef.Text = resp.refNo ?? lblRefrencenum.Text;
                lblSuccessPayer.Text = resp.payerName ?? "N/A";
                lblSuccessTax.Text = resp.taxName ?? lblTaxName.Text;
                lblSuccessAmountPaid.Text = $"₦{resp.amountPaid}";
                lblSuccessAmountLeft.Text = $"₦{resp.amountLeft ?? "0.00"}";

                PaymentSuccessView.IsVisible = true;
            });
        }

      
        private async void OnNewTransactionClicked(object sender, EventArgs e)
        {
            await DismissSheet();
        }


        // ─────────────────────────────────────────────────────────────────────
        //  RECEIPT BUILDER (Matching RegisterPropertyWHT / Borno Standard)
        // ─────────────────────────────────────────────────────────────────────

        private ReceiptData BuildReceiptData(PaymentResponseObject resp, string invoiceToken)
        {
            decimal amtPaid = decimal.TryParse(resp.amountPaid, out decimal p)
                ? p
                : (decimal.TryParse(amount.Text, out decimal a) ? a : 0m);

            decimal amtLeft = decimal.TryParse(resp.amountLeft, out decimal l) ? l : 0m;
            decimal actualAmt = decimal.TryParse(resp.actualAmt, out decimal act) ? act : (amtPaid + amtLeft);

            var receipt = ReceiptPrinter.CreateBrandedReceipt();
            receipt.ReceiptNumber = resp.refNo ?? invoiceToken;
            receipt.AgentName = LoginPage.Name ?? LoginPage.ValidUserMail ?? "N/A";
            receipt.CollectionPoint =  "BOIRS Collection Point";
            receipt.TotalAmount = actualAmt;
            receipt.AmountPaid = amtPaid;
            receipt.AmountLeft = amtLeft;
            receipt.BarcodeLabel = $"{BrandConfig.VerifyReceiptUrl}{resp.refNo ?? invoiceToken}";

            // Line items formatted like RegisterPropertyWHT
            receipt.Items.Add(new ReceiptItem
            {
                Description = !string.IsNullOrEmpty(resp.taxName) ? resp.taxName : (lblTaxName.Text ?? "Revenue Payment"),
                Amount = amtPaid
            });

            if (!string.IsNullOrWhiteSpace(resp.payerName))
            {
                receipt.Items.Add(new ReceiptItem { Description = "Payer Name", Amount = 0m, SubText = resp.payerName });
            }

            if (!string.IsNullOrWhiteSpace(resp.payerId))
            {
                receipt.Items.Add(new ReceiptItem { Description = "Payer ID", Amount = 0m, SubText = resp.payerId });
            }

            if (!string.IsNullOrWhiteSpace(resp.lga))
            {
                receipt.Items.Add(new ReceiptItem { Description = "LGA", Amount = 0m, SubText = resp.lga });
            }

            receipt.Items.Add(new ReceiptItem
            {
                Description = "Date",
                Amount = 0m,
                SubText = DateTime.Now.ToString("dd MMM yyyy HH:mm")
            });

            return receipt;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TRIGGER PRINT (Post-Payment & Reprint Handlers)
        // ─────────────────────────────────────────────────────────────────────

        private async Task TriggerReceiptPrintAsync(ReceiptData receipt)
        {
            if (receipt == null) return;
            await ReceiptPrinter.PrintAsync(receipt);
        }

        private async void OnPrintReceiptClicked(object sender, EventArgs e)
        {
            if (_lastReceiptData == null)
            {
                UserDialogs.Instance.Toast("No receipt data available.");
                return;
            }

            PrintReceiptButton.IsEnabled = false;
            try
            {
                _lastReceiptData.FooterLine1 = "*** REPRINTED RECEIPT ***";
                _lastReceiptData.FooterLine2 = $"Reprinted: {DateTime.Now:dd MMM yyyy HH:mm} | POWERED BY OSOFTPAY";
                await TriggerReceiptPrintAsync(_lastReceiptData);
            }
            finally
            {
                PrintReceiptButton.IsEnabled = true;
            }
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
                                this.Opacity = Math.Max(0.2, 1 - (newY / 500));
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