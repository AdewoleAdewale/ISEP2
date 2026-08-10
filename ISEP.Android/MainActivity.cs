using Acr.UserDialogs;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using System;
using System.Threading.Tasks;

namespace ISEP.Droid
{
    [Activity(Label = "ISEP", Icon = "@drawable/Borno", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            // Registered FIRST so it covers failures during Forms.Init
            // and LoadApplication as well as everything afterwards.
            RegisterGlobalExceptionHandlers();

            base.OnCreate(savedInstanceState);

            Xamarin.Forms.Forms.SetFlags(new string[] { "CarouselView_Experimental", "SwipeView_Experimental", "IndicatorView_Experimental" });
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);
            UserDialogs.Init(this);

            LoadApplication(new App());
        }

        /// <summary>
        /// Catches anything that escapes a handler anywhere in the app and
        /// writes the full stack trace to logcat before Android kills the
        /// process. Without this a crash shows up as a bare
        /// "Unhandled Exception" with no usable trace.
        ///
        /// View it with:  adb logcat -s ISEP-CRASH:V mono-stdout:V
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
            {
                LogFatal("AndroidEnvironment", args.Exception);
                args.Handled = false; // let Android finish the teardown
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                LogFatal("AppDomain", args.ExceptionObject as Exception);
            };

            // Fires when a faulted Task is garbage-collected without its
            // exception ever being observed — e.g. a fire-and-forget
            // `_ = SomethingAsync();` that threw.
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogFatal("UnobservedTask", args.Exception);
                args.SetObserved();
            };
        }

        private static void LogFatal(string source, Exception ex)
        {
            try
            {
                Android.Util.Log.Error("ISEP-CRASH", $"[{source}] {ex}");

                var inner = ex?.InnerException;
                int depth = 0;
                while (inner != null && depth < 5)
                {
                    Android.Util.Log.Error("ISEP-CRASH", $"[{source}] Inner({depth}): {inner}");
                    inner = inner.InnerException;
                    depth++;
                }
            }
            catch
            {
                // Never throw from inside a crash handler.
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}