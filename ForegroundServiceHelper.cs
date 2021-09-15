using System;
using System.Linq;
using System.Threading.Tasks;
using Android.Gms.Common;
using Android.Gms.Location;
using Android.Locations;
using Android.Util;
using Android.Widget;
using OTG.Contracts;
using OTG.Droid.Helper;
using OTG.Droid.Services.LocationServices;
using OTG.Helpers;
using OTG.Models;

[assembly: Xamarin.Forms.Dependency(typeof(ForegroundServiceHelper))]
namespace OTG.Droid.Services.LocationServices
{    
    public class ForegroundServiceHelper: IForegroundServiceHelper
    {
        private static FusedLocationProviderClient fusedLocationProviderClient;
        private static LocationRequest locationRequest;
        private static FusedLocationProviderCallback locationCallback;
        private MainActivity androidActivity;
        private static DriverLocationConfiguration _configuration;
        private static bool _isLocationUpdateStarted;
        
        public ForegroundServiceHelper()
        {
            androidActivity = Android.App.Application.Context as MainActivity;
            if (androidActivity == null)
            {
                androidActivity = AndroidAppActivity.Current as MainActivity;
            }
        }

        public async Task RemoveLocationUpdates()
        {
            await fusedLocationProviderClient.RemoveLocationUpdatesAsync(locationCallback);
        }

        public async Task RequestLocationUpdates()
        {
            if (_isLocationUpdateStarted)
                return;

            if (fusedLocationProviderClient == null)
            {
                InitializeFusedLocationRequest(androidActivity);
            }

            await fusedLocationProviderClient.RequestLocationUpdatesAsync(locationRequest, locationCallback,
                                                Android.OS.Looper.MainLooper);
            _isLocationUpdateStarted = true;
        }

        public async Task StartService(DriverLocationConfiguration config)
        {
            if (IsPlayServicesAvailable())
            {
                _configuration=config;                

                InitializeFusedLocationRequest(androidActivity);
                GeolocationHelper.Current.LocationServiceConnected += OnLocationServiceConnected;

                await GeolocationHelper.StartLocationService();
            }            
        }

        private void OnLocationServiceConnected(object sender, ServiceConnectedEventArgs e)
        {
            
        }

        public void StopService()
        {
            GeolocationHelper.StopLocationService();
        }

        public void InitializeFusedLocationRequest(MainActivity mainActivity)
        {
            var manufacturer = Xamarin.Essentials.DeviceInfo.Manufacturer;
            var platform = Xamarin.Essentials.DeviceInfo.Platform.ToString();
            var model = Xamarin.Essentials.DeviceInfo.Model;
            var oSVersion = Xamarin.Essentials.DeviceInfo.VersionString;
            App.LoggerService.Info($"DEVICEINFO==> Manufacturer: {manufacturer}; Model: {model}; OSVersion: {oSVersion}; Platform: {platform}");

            int accuracy;
            if(mainActivity == null)
            {
                mainActivity = AndroidAppActivity.Current as MainActivity;
            }
            
            switch (_configuration.Priority)
            {
                case "HIGH":
                    accuracy = LocationRequest.PriorityHighAccuracy;
                    break;
                case "MEDIUM":
                    accuracy = LocationRequest.PriorityBalancedPowerAccuracy;
                    break;
                case "LOW":
                    accuracy = LocationRequest.PriorityLowPower;
                    break;
                case "NOPOWER":
                    accuracy = LocationRequest.PriorityNoPower;
                    break;
                default:
                    accuracy = LocationRequest.PriorityHighAccuracy;
                    break;
            }

            if (locationRequest == null)
            {
                /*
                    SuperAggressive	    20	15
                    Aggressive config	50	30
                    Moderate	        100	60
                    Defesive	        200	60
                */
                //Defensive
                _configuration.SmallestDisplacement = 0;
                _configuration.MaxWaitTime = 0;
                _configuration.Interval = 20;
                _configuration.FastestInterval = 10;
                
                
                locationRequest = new LocationRequest().SetPriority(accuracy);  //configure Accuracy
                App.LoggerService.Info($"Config set by USER ==>  Priority: {accuracy}");

                if (_configuration.SmallestDisplacement > 0)
                {
                    locationRequest.SetSmallestDisplacement(_configuration.SmallestDisplacement);//get from config
                    App.LoggerService.Info($"Config set by USER ==>  SetSmallestDisplacement: {_configuration.SmallestDisplacement}");
                }

                if (_configuration.MaxWaitTime > 0)
                {
                    locationRequest.SetMaxWaitTime(_configuration.MaxWaitTime * 1000); //configure batch updates req for background 30
                    App.LoggerService.Info($"Config set by USER ==>  MaxWaitTime: {(_configuration.MaxWaitTime * 1000)}");
                }

                if(_configuration.Interval > 0)
                {
                    locationRequest.SetInterval(_configuration.Interval * 1000); //configure 30
                    App.LoggerService.Info($"Config set by USER ==>  Interval: {(_configuration.Interval * 1000)}");
                }

                if (_configuration.FastestInterval > 0)
                {
                    locationRequest.SetFastestInterval(_configuration.FastestInterval * 1000);//configure 15
                    App.LoggerService.Info($"Config set by USER ==>  FastestInterval: {(_configuration.FastestInterval * 1000)}");
                }

                App.LoggerService.Info($"=======================================================================");

                App.LoggerService.Info($"Config set by SYSTEM ==> Priority: {locationRequest.Priority}; SetSmallestDisplacement: {locationRequest.SmallestDisplacement}; " +
                    $"MaxWaitTime: {locationRequest.MaxWaitTime}; Interval: {locationRequest.Interval}; " +
                    $"FastestInterval: {locationRequest.FastestInterval}");
            }

            if (fusedLocationProviderClient == null)
            {
                fusedLocationProviderClient = Android.Gms.Location.LocationServices.GetFusedLocationProviderClient(mainActivity);
            }

            if (locationCallback == null)
            {
                locationCallback = new FusedLocationProviderCallback(mainActivity, fusedLocationProviderClient);
            }

        }

        public bool IsPlayServicesAvailable()
        {
            var queryResult = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(androidActivity.ApplicationContext);
            if (queryResult == ConnectionResult.Success)
            {
                Log.Info("MainActivity", "Google Play Services is installed on this device.");
                //Toast.MakeText(androidContext, "Google Play Services is installed on this device.", ToastLength.Long);
                return true;
            }

            if (GoogleApiAvailability.Instance.IsUserResolvableError(queryResult))
            {
                // Check if there is a way the user can resolve the issue
                var errorString = GoogleApiAvailability.Instance.GetErrorString(queryResult);
                Log.Error("MainActivity", "There is a problem with Google Play Services on this device: {0} - {1}",
                          queryResult, errorString);
                //Toast.MakeText(androidContext, $"There is a problem with Google Play Services on this device: {queryResult} - {errorString}",
                    //ToastLength.Long);
                // Alternately, display the error to the user.
            }

            return false;
        }
    }


    public class FusedLocationProviderCallback : LocationCallback
    {
        private long? lastDateTime = null;
        private Location lastLocation;
        readonly MainActivity activity;
        readonly FusedLocationProviderClient fusedLocationProviderClient;

        public FusedLocationProviderCallback(MainActivity activity, FusedLocationProviderClient flpClient)
        {
            this.activity = activity;
            fusedLocationProviderClient = flpClient;
        }

        public override void OnLocationAvailability(LocationAvailability locationAvailability)
        {
            Log.Debug("FusedLocationProviderSample", "IsLocationAvailable: {0}", locationAvailability.IsLocationAvailable);

            Toast.MakeText(activity, $"IsLocationAvailable: {locationAvailability.IsLocationAvailable}",
                        ToastLength.Long);
        }

        public override async void OnLocationResult(LocationResult result)
        {
            try
            {
                if (result.Locations.Any())
                {
                    //deviceLocation = result.Locations.First();
                    App.LoggerService.Info($"Location Array Length: {result.Locations.Count}");


                    foreach (var item in result.Locations)
                    {
                        float calcDistance = 0;
                        long elapsedTime = 0;

                        if (lastDateTime != null)
                            elapsedTime = (item.Time - lastDateTime.Value) / 1000;                       

                        if (lastLocation != null)
                            calcDistance = item.DistanceTo(lastLocation);

                        lastDateTime = item.Time;
                        lastLocation = item;

                        var epochDate = new DateTime(1970, 01, 01);
                        var reportedTime = epochDate.AddMilliseconds(lastDateTime.Value).ToLocalTime();

                        string msg = $"LOCATIONSERVICE==>{DateTime.Now}=> Distance travelled : {calcDistance}m ; Accuracy: {item.Accuracy}; " +
                           $"BatteryLevel: {Plugin.Battery.CrossBattery.Current.RemainingChargePercent};" +
                           $"ElapsedTime: {elapsedTime}; ; LastDateTime: {reportedTime};" +
                           $"The location is : {item.Latitude} - {item.Longitude};";

                        App.LoggerService.Info(msg);

                        await App.PushDriverLocation(item.Latitude, item.Longitude);
                    }
                }
                else
                {
                    App.LoggerService.Info($"LOCATIONSERVICE==>LastLocation to work with.");                    
                }                            
            }
            catch (Exception ex)
            {
                App.LoggerService.Exception("Error", ex);
            }
        }
    }
}
