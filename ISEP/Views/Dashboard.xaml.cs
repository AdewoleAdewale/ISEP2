using Acr.UserDialogs;
using Android.Bluetooth;
using Java.Util;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Dashboard : ContentPage
    {


        public static string superAgent { get; set; }
        public static string agent { get; set; }
        public static string cashoutBalance { get; set; }

        public Dashboard()
        {
            InitializeComponent();

            if (LoginPage.Name == " ")
            {
                Username.Text = LoginPage.ValidUserMail;
                Balance.Text = LoginPage.accountbalance;
                bankname.Text = LoginPage.Banks;
                Accountnumber.Text = LoginPage.accountnumbers;
            }
            else if (LoginPage.Name != " ")
            {
                Username.Text = LoginPage.Name;
                Balance.Text = LoginPage.accountbalance;
                bankname.Text = LoginPage.Banks;
                Accountnumber.Text = LoginPage.accountnumbers;
            }


        }

        private async void Button_Clicked(object sender, System.EventArgs e)
        {
            using (UserDialogs.Instance.Loading("Connecting to ISEP, Please Wait...", null, null, true, MaskType.Gradient))
            {
                await Task.Delay(1000);


                await Navigation.PushAsync(new Views.Verify());
            }
        }

        private async void Button_Clicked_2(object sender, System.EventArgs e)
        {
            using (UserDialogs.Instance.Loading("Connecting to ISEP, Please Wait...", null, null, true, MaskType.Gradient))
            {
                await Task.Delay(1000);


                await Navigation.PushAsync(new Views.History());
            }
        }

        private void Button_Clicked_3(object sender, System.EventArgs e)
        {

            Device.BeginInvokeOnMainThread(async () =>
            {
                string action = "";
                action = await DisplayActionSheet("HI, WHAT DO YOU WANT TO DO?", "CANCEL", null, "CHANGE PASSWORD", "CHANGE PIN", "TEST PRINTER");

                if (action == "CHANGE PASSWORD")
                {

                    try
                    {

                        await Navigation.PushAsync(new Views.ChangePassword());
                    }
                    catch (Exception ex)
                    {

                        ex.ToString();
                    }
                }

                else if (action == "CHANGE PIN")
                {

                    try
                    {
                        await Navigation.PushAsync(new ChangePin());
                    }
                    catch (Exception ex)
                    {

                        ex.ToString();
                    }
                }




                else if (action == "TEST PRINTER")
                {

                    try
                    {
                        CallPrinter();
                    }
                    catch (Exception ex)
                    {

                        ex.ToString();
                    }
                }

            });

        }

        private void TapGestureRecognizer_Tapped(object sender, System.EventArgs e)
        {
            App.IsUserLoggedIn = false;
            System.Diagnostics.Process.GetCurrentProcess().CloseMainWindow();

        }



        private async void CallPrinter()
        {
            String PrintText;

            PrintText = "\nTest Test Test\n";
            PrintText = PrintText + "--------------------------------";
            PrintText = PrintText + "\n\n";
            PrintText = PrintText + "Status: Printer Connected\n";
            PrintText = PrintText + "--------------------------------";
            PrintText = PrintText + "\n\n";


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
                    await DisplayAlert("NOTIFICATION", "Bluethooth Not Connected To Designated Printer", "TRY AGAIN");


                try
                {
                    using (BluetoothSocket _socket = device.CreateRfcommSocketToServiceRecord(UUID.FromString("00001101-0000-1000-8000-00805f9b34fb")))
                    {

                        await _socket.ConnectAsync();

                        if (_socket.IsConnected)
                        {
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(PrintText);
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
                                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(PrintText);
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
                                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(PrintText);
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
                                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(PrintText);
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
                    await DisplayAlert("NOTIFICATION", "Printer not connected", "OKAY");
                    exp.ToString();
                }


            }

        }

        internal class BalanceResponse
        {
            public string superAgent { get; set; }
            public string agent { get; set; }
            public string cashoutBalance { get; set; }


        }

        private async void TapGestureRecognizer_Tapped_1(object sender, EventArgs e)
        {
            using (UserDialogs.Instance.Loading("Connecting to Service, Please Wait...", null, null, true))
            {
                await Task.Delay(500);


                string url = "https://collection.osoftpay.net/api/Caccounts";
                try
                {

                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Agent", LoginPage.ValidUserMail);
                        client.DefaultRequestHeaders.Add("TradingPin", LoginPage.Pin);
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        using (HttpResponseMessage response = client.GetAsync(url).Result)
                        {
                            using (HttpContent content = response.Content)
                            {
                                var json = content.ReadAsStringAsync().Result;
                                BalanceResponse result = JsonConvert.DeserializeObject<BalanceResponse>
                                    (json);
                                if (result != null)
                                {
                                    agent = result.agent;
                                    superAgent = result.superAgent;
                                    cashoutBalance = result.cashoutBalance;
                                    await Navigation.PushAsync(new CashoutPage());

                                }
                            }
                        }
                    }

                }
                catch (System.Exception exe)
                {

                    exe.ToString();
                }

            }
        }
    }
}