using Acr.UserDialogs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ISEP.Services;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class History : ContentPage
    {
        class HistoryData
        {
            public string payerName { get; set; }
            public string payerId { get; set; }
            public string serviceName { get; set; }
            public string payRef { get; set; }
            public decimal amount { get; set; }
            public string dateRecorded { get; set; }
        }

        class HistoryDataHeaderFooter
        {
            public List<HistoryData> HD { get; set; }
            public string Intro => $"Performed {HD?.Count ?? 0} transactions within search range";
        }

        public History()
        {
            InitializeComponent();
        }

        private async void Search_Clicked(object sender, EventArgs e)
        {
            using (UserDialogs.Instance.Loading("Fetching History...", null, null, true))
            {
                string searchFrom = startDatePicker.Date.ToString("MM/dd/yyyy");
                string searchTo = endDatePicker.Date.ToString("MM/dd/yyyy");

                string url = $"{BrandConfig.ApiBaseUrl}/api/GPayments/gettransaction?Email={LoginPage.ValidUserMail}&SearchFrom={searchFrom}&SearchTo={searchTo}";

                try
                {
                    var items = await ApiClient.GetAsync<List<HistoryData>>(url);
                    BindingContext = new HistoryDataHeaderFooter { HD = items };
                }
                catch (Exception ex)
                {
                    UserDialogs.Instance.Toast("Could not load history. Please try again later.");
                    System.Diagnostics.Debug.WriteLine($"History fetch error: {ex.Message}");
                }
            }
        }
    }
}