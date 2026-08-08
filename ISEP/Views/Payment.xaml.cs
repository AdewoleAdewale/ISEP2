using Acr.UserDialogs;
using Android.Bluetooth;
using Java.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Payment : ContentPage
    {
        public Payment()
        {
            InitializeComponent();
            sheetBehavior.IsOpen = false;
            Refrencenum.Text = Verify.paymentRefs;
            taxname.Text = Verify.taxNames;
            amountpaid.Text = Verify.actualAmts;
            balancetopay.Text = Verify.amtLefts;




        }

        private void sheetBehavior_ActionClicked(object sender, EventArgs e)
        {
            sheetBehavior.Opened += sheetBehavior_ActionClicked;
        }


        protected override bool OnBackButtonPressed()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {

                using (UserDialogs.Instance.Loading("Connecting to Service, Please Wait...", null, null, true))
                {
                    await Task.Delay(10);


                    await Navigation.PushAsync(new Views.Verify());


                }

            });
            return true;
        }

        private async void TapGestureRecognizer_Tapped(object sender, EventArgs e)
        {

            using (IProgressDialog progress = UserDialogs.Instance.Progress("Connecting to Service, Please Wait...", null, null, true, MaskType.Gradient))
            {
                for (int i = 0; i < 100; i++)
                {
                    progress.PercentComplete = i;
                    await Task.Delay(60);
                }

                if (amount.Text == null || PIN.Text == null)
                {

                    await DisplayAlert("NOTIFICATION", "Kindly fill in all details before you proceed", "TRY AGAIN");

                }

                else if (amount.Text != null && PIN.Text == LoginPage.Pin)
                {


                    string url = "https://borno.osoftpay.net/api/GPayments/Payment";

                    try
                    {
                        PaymentObject PaymentObjectss = new PaymentObject()
                        {
                            RefNo = Verify.paymentRefs,
                            Pin = PIN.Text,
                            Email = LoginPage.ValidUserMail,
                            TaxName = Verify.taxNames,
                            AmountPaid = amount.Text,

                        };

                        var httpClientHandler = new HttpClientHandler();

                        var nvc = new List<KeyValuePair<string, string>>();
                        nvc.Add(new KeyValuePair<string, string>("RefNo", PaymentObjectss.RefNo));
                        nvc.Add(new KeyValuePair<string, string>("Email", PaymentObjectss.Email));
                        nvc.Add(new KeyValuePair<string, string>("TaxName", PaymentObjectss.TaxName));
                        nvc.Add(new KeyValuePair<string, string>("Pin", PaymentObjectss.Pin));
                        nvc.Add(new KeyValuePair<string, string>("AmountPaid", PaymentObjectss.AmountPaid));


                        var client = new HttpClient(httpClientHandler);
                        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(nvc) };
                        var res = await client.SendAsync(req);

                        var resultString = await res.Content.ReadAsStringAsync();
                        var PaymentResponseObjectClass = JsonConvert.DeserializeObject<PaymentResponseObject>(resultString);
                        if (PaymentResponseObjectClass.statusCode == "00")
                        {
                            sheetBehavior.IsOpen = true;

                            String PrintText;

                            PrintText = "\n" + App.RevenueServiceName + "\n";
                            PrintText = PrintText + "--------------------------------";
                            PrintText = PrintText + "\n\n";
                            PrintText = PrintText + "DATE:" + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "\n";
                            PrintText = PrintText + "REFERENCE:" + PaymentResponseObjectClass.refNo + "\n";
                            PrintText = PrintText + "AGENT:" + LoginPage.ValidUserMail + "\n";
                            PrintText = PrintText + "BUSINESS NAME:" + PaymentResponseObjectClass.payerName + "\n";
                            PrintText = PrintText + "BUSINESS ID:" + PaymentResponseObjectClass.payerId + "\n";
                            PrintText = PrintText + "TAX NAME:" + PaymentResponseObjectClass.taxName + "\n";
                            PrintText = PrintText + "ACTUAL AMOUNT: N" + Verify.actualAmts + "\n";
                            PrintText = PrintText + "AMOUNT PAID: N" + amount.Text + "\n";
                            PrintText = PrintText + "BAL. UNPAID: N" + PaymentResponseObjectClass.amountLeft + "\n";
                            PrintText = PrintText + "LGA: " + Verify.lgas + "\n";
                            PrintText = PrintText + "ADDRESS: " + Verify.addresss + "\n";
                            PrintText = PrintText + "\n\n";
                            //PrintText = PrintText + baos + "\n";
                            PrintText = PrintText + "--------------------------------";
                            //PrintText = PrintText + "\n\n";
                            PrintText = PrintText + App.PrinterFooter + "\n";
                            PrintText = PrintText + "--------------------------------";
                            PrintText = PrintText + App.ThankYouMessage + "\n";
                            PrintText = PrintText + "\n\n";

                            CallPrinter(PrintText);

                            RedirecttoLandingPage();

                        }
                        else
                        {
                            sheetBehavior.IsOpen = false;
                            await DisplayActionSheet("NOTIFICATION", PaymentResponseObjectClass.status, "TRYAGAIN");
                            RedirecttoLandingPage();
                        }
                    }
                    catch (Exception exe)
                    {
                        sheetBehavior.IsOpen = false;
                        UserDialogs.Instance.Toast(" Can't Process  Due To Bad Network Now Try Again Later ", TimeSpan.FromSeconds(10));

                        exe.ToString();
                    }


                }
            }

        }
        private async void CallPrinter(string input)
        {

#pragma warning disable CS0618 // Type or member is obsolete
            using (BluetoothAdapter bluetoothAdapter = BluetoothAdapter.DefaultAdapter)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                if (bluetoothAdapter == null)
                {
                    throw new Exception("No default adapter");
                    //return;
                }

                if (!bluetoothAdapter.IsEnabled)
                {
                    throw new Exception("Bluetooth not enabled");
                    //Intent enableIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
                    //StartActivityForResult(enableIntent, REQUEST_ENABLE_BT);
                    // Otherwise, setup the chat session
                }

                string printer1 = "MPT-II";
                string printer2 = "printer001";
                string printer3 = "RPP02N";
                string printer4 = "RPP210";
                string printer5 = "InnerPrinter";
                string printer6 = "b906";
                string printer7 = "ANDROID BT";
                string printer8 = "FP8800";
                string printer9 = "IposPrinter";
                string printer10 = "CS10";
                string printer11 = "Q2i";
                string printer12 = "Internal Bluetooth Printer";

                BluetoothDevice device = (from bd in bluetoothAdapter.BondedDevices
                                          where (bd.Name == printer1) || (bd.Name == printer2) || (bd.Name == printer3) || (bd.Name == printer4) || (bd.Name == printer5) || (bd.Name == printer6) || (bd.Name == printer7) || (bd.Name == printer8) || (bd.Name == printer9) || (bd.Name == printer10) || (bd.Name == printer11) || (bd.Name == printer12)
                                          select bd).FirstOrDefault();
                if (device == null)
                    sheetBehavior.IsOpen = true;

                await DisplayAlert("NOTIFICATION", "Bluethooth Not Connected To Designated Printer", "TRY AGAIN");


                try
                {
                    using (BluetoothSocket _socket = device.CreateRfcommSocketToServiceRecord(UUID.FromString("00001101-0000-1000-8000-00805f9b34fb")))
                    {

                        await _socket.ConnectAsync();

                        if (_socket.IsConnected)
                        {
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(input);
                            await Task.Delay(3000);
                            // Write data to the device
                            await _socket.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                            _socket.Close();
                        }
                        else
                        {
                            await DisplayAlert("1st Warning", "Check your bluetooth printer before clicking ok", "Ok");
                            if (_socket.IsConnected)
                            {
                                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(input);
                                await Task.Delay(3000);
                                // Write data to the device
                                await _socket.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                _socket.Close();
                            }
                            else
                            {
                                await DisplayAlert("2nd Warning", "Check your bluetooth printer before clicking ok", "Ok");
                                if (_socket.IsConnected)
                                {
                                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(input);
                                    await Task.Delay(3000);
                                    // Write data to the device
                                    await _socket.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                    _socket.Close();
                                }
                                else
                                {
                                    await DisplayAlert("Last Warning", "Check your bluetooth printer before clicking ok", "Ok");
                                    if (_socket.IsConnected)
                                    {
                                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(input);
                                        await Task.Delay(3000);
                                        // Write data to the device
                                        await _socket.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                        _socket.Close();
                                    }
                                }

                            }
                        }

                    }
                }
                catch (Exception exp)
                {
                    await DisplayAlert("Info", "Printer not connected", "Ok");
                    exp.ToString();
                }


            }


        }


        private async void RedirecttoLandingPage()
        {

            await Navigation.PushAsync(new Views.Verify());

        }

        private void Button_Clicked_1(object sender, EventArgs e)
        {
            sheetBehavior.Opened += sheetBehavior_ActionClicked;
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