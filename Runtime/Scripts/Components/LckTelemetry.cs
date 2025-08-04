using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Text;
using Liv.Lck.Settings;
using System.Net;
using System.IO;
using System.Net.Http.Headers;
using UnityEngine.Rendering;

namespace Liv.Lck.Telemetry
{
    public class EnumSnakeCaseConverter : StringEnumConverter
    {
        public EnumSnakeCaseConverter()
        {
            NamingStrategy = new SnakeCaseNamingStrategy { ProcessDictionaryKeys = true };
        }
    }

    [JsonConverter(typeof(EnumSnakeCaseConverter))]
    public enum TelemetryEventType
    {
        GameInitialized,

        ServiceCreated,
        ServiceDisposed,

        CameraEnabled,
        CameraDisabled,

        RecordingStarted,
        RecordingStopped,

        PhotoCaptured,
        
        RecorderError,
        PhotoCaptureError,
        SdkError,

        Performance,

    }

    public class TelemetryEvent
    {
        public TelemetryEventType EventType;
        public Dictionary<string, object> Context;

        public TelemetryEvent(TelemetryEventType eventType)
        {
            EventType = eventType;
        }

        public TelemetryEvent(TelemetryEventType eventType, Dictionary<string, object> context)
        {
            EventType = eventType;
            Context = context;
        }
    }

    public static class LckTelemetry
    {
        private const string TelemetryEndpoint = "https://errors.liv.tv/ingest/qck";
        private const string ACTIVATION_ID_KEY = "LCK_ACTIVATION_ID";

        private static Func<string> _getUserId = null;

        private static HttpClient _client;

        private static string _runId;
        private static string _deviceModel;
        private static string _activationId;
        private static GeoLocation _geolocation;
        private static string _rawDeviceModel;
        private static string _deviceId;
        private static string _renderPipelineType;
        private static string _graphicsAPI;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            if (!LckService.VerifyGraphicsApi())
            {
                return;
            }

            _ = InitializeAsync();
#if !UNITY_EDITOR
            LckLog.Log($"LCK version is v{LckSettings.Version}#{LckSettings.Build}");
#endif
        }

        private static async Task InitializeAsync()
        {
            _runId = Guid.NewGuid().ToString();


            if (LckSettings.Instance.AllowLocationTelemetry)
            {
                try { _geolocation = await GetGeoLocation(); } catch {}
            }
            else
            {
                _geolocation = new GeoLocation
                {
                    Status = null,
                    Region = null,
                    RegionName = null,
                    City = null
                };
            }

            if (LckSettings.Instance.AllowDeviceTelemetry)
            {
                _deviceId = SystemInfo.deviceUniqueIdentifier;
                _rawDeviceModel = SystemInfo.deviceModel;

#if UNITY_ANDROID && !UNITY_EDITOR
                    var buildinfo = new AndroidJavaClass("android.os.Build");
                    var device = buildinfo.GetStatic<string>("DEVICE");
                    _rawDeviceModel = device;

                    switch (device)
                    {
                        case "panther":
                            _deviceModel = "Quest 3S";
                            break;
                        case "eureka":
                            _deviceModel = "Quest 3";
                            break;
                        case "hollywood":
                            _deviceModel = "Quest 2";
                            break;
                        default:
                            _deviceModel = "Unknown";
                            break;
                    }
#endif
            }
            else
            {
                _deviceId = null;
                _rawDeviceModel = null;
                _deviceModel = null;
            }

            _graphicsAPI = SystemInfo.graphicsDeviceType.ToString();
            _renderPipelineType = GetRenderPipelineType();
            
            LoadOrCreateDeviceId();

            InitializeHttpClient();

            SendTelemetry(new TelemetryEvent(TelemetryEventType.GameInitialized));
        }

            private static void InitializeHttpClient()
            {
                _client = new HttpClient();
                _client.Timeout = TimeSpan.FromSeconds(1);
                _client.DefaultRequestHeaders.UserAgent.Clear();
                _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "liv-qck");
                _client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );
                _client.DefaultRequestHeaders.Add("x-liv-version", LckSettings.Version);
                _client.DefaultRequestHeaders.Add("x-liv-uid", SystemInfo.deviceUniqueIdentifier);
            }

            private static void LoadOrCreateDeviceId()
            {
                var activationId = PlayerPrefs.GetString(ACTIVATION_ID_KEY);
                if (string.IsNullOrEmpty(activationId))
                {
                    _activationId = System.Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(ACTIVATION_ID_KEY, _activationId);
                }
                else
                {
                    _activationId = activationId;
                }
            }

            private static string GetRenderPipelineType()
            {
                if (GraphicsSettings.defaultRenderPipeline)
                {
                    if (GraphicsSettings.defaultRenderPipeline.GetType().ToString().Contains("HDRenderPipelineAsset"))
                    {
                        return "High Definition render pipeline";
                    }
                    else if (GraphicsSettings.defaultRenderPipeline.GetType().ToString().Contains("UniversalRenderPipelineAsset"))
                    {
                        return "Universal render pipeline";
                    }
                    else
                    {
                        return "Custom render pipeline";
                    }
                }
                else
                {
                    return "Built-in render pipeline";
                }
            }

            public static void SetUserIdProvider(Func<string> getUserId)
            {
                _getUserId = getUserId;
            }

            public static void SendTelemetry(TelemetryEvent eventData)
            {
                _ = SendTelemetryAsync(eventData);
            }

#pragma warning disable CS1998 // Gets rid of warning because the async send is disabled in editor only
            private static async Task SendTelemetryAsync(TelemetryEvent eventData)
            {
                if (_client == null)
                {
                    InitializeHttpClient();
                };

                using (var request = new HttpRequestMessage(HttpMethod.Post, TelemetryEndpoint))
                {
                    request.Content = SerializeTelemetryEvent(eventData);
#if UNITY_EDITOR
                    LckLog.LogTrace($"Telemetry event sent in editor: {eventData.EventType}. Request: {request.Content}");
#else
                    try
                    {
                        using var response = await _client
                            .SendAsync(
                                request,
                                HttpCompletionOption.ResponseHeadersRead
                            );

                        if (response.IsSuccessStatusCode)
                        {
                            LckLog.LogTrace($"Telemetry event sent successfully: {eventData.EventType}. Response: {response.StatusCode} - {response.ReasonPhrase}, Request: {request.Content}");
                        }
                        else
                        {
                            LckLog.LogTrace($"Failed to send telemetry event: {eventData.EventType}. Response: {response.StatusCode} - {response.ReasonPhrase}, Request: {request.Content}");
                        }
                    }
                    catch (HttpRequestException ex) {
                        LckLog.LogTrace($"Failed to send telemetry event: {eventData.EventType}. Exception: {ex.ToString()}:{ex.Message}");
                    }
                    catch (Exception ex) {
                        LckLog.LogError($"Failed to send telemetry event: {eventData.EventType}. Exception: {ex.ToString()}:{ex.Message}");
                    }
#endif
                }
            }
#pragma warning restore CS1998 // Gets rid of warning because the async send is disabled in editor only

            private static HttpContent SerializeTelemetryEvent(TelemetryEvent eventData)
            {
                string userId = null;
                if (_getUserId != null)
                {
                    userId = _getUserId();
                    return null;
                }

                var meta = MetaData.Collect(_runId, _geolocation, userId);
                var data = new TelemetryData(eventData, meta);
                var payload = new TelemetryPayload(data);
                var payloadJson = JsonConvert.SerializeObject(payload);

                return new StringContent(payloadJson, Encoding.UTF8, "application/json");
            }

        private struct MetaData
        {
            [JsonProperty("hmd", NullValueHandling = NullValueHandling.Ignore)]
            public string Hmd;

            [JsonProperty("rawDeviceModel", NullValueHandling = NullValueHandling.Ignore)]
            public string RawDeviceModel;

            [JsonProperty("platform")]
            public string Platform;

            [JsonProperty("deviceId")]
            public string DeviceId;

            [JsonProperty("userId")]
            public string UserId;

            [JsonProperty("runId")]
            public string RunId;
            
            [JsonProperty("projectName")]
            public string UnityProjectName;
            
            [JsonProperty("gameName")]
            public string GameName;
            
            [JsonProperty("lckBuild")]
            public string LckBuild;

            [JsonProperty("gameVersion")]
            public string GameVersion;

            [JsonProperty("companyName")]
            public string CompanyName;

            [JsonProperty("trackingId")]
            public string TrackingId;

            [JsonProperty("region")]
            public string Region;

            [JsonProperty("regionName")]
            public string RegionName;

            [JsonProperty("city")]
            public string City;

            [JsonProperty("unityVersion")]
            public string UnityVersion;

            [JsonProperty("renderPipelineType")]
            public string RenderPipelineType;

            [JsonProperty("graphicsAPI", NullValueHandling = NullValueHandling.Ignore)]
            public string GraphicsAPI;

            public static MetaData Collect(string runId, GeoLocation geoLocation, string userId)
            {
                return new MetaData()
                {
                    Hmd = _deviceModel,
                    RawDeviceModel = _rawDeviceModel,
                    Platform = Application.platform.ToString(),
                    DeviceId = _deviceId,
                    UnityVersion = Application.unityVersion,
                    RunId = runId,
                    UserId = userId,
                    UnityProjectName = Application.productName,
                    GameName = LckSettings.Instance.GameName,
                    GameVersion = Application.version,
                    CompanyName = Application.companyName,
                    Region = geoLocation.Region,
                    RegionName = geoLocation.RegionName,
                    City = geoLocation.City,
                    TrackingId = LckSettings.Instance.TrackingId,
                    LckBuild = LckSettings.Build.ToString(),
                    RenderPipelineType = _renderPipelineType,
                    GraphicsAPI = _graphicsAPI,
                };
            }
        }

        private struct TelemetryData
        {
            [JsonProperty("type")]
            public readonly TelemetryEventType EventType;

            [JsonProperty("context", NullValueHandling = NullValueHandling.Ignore)]
            public readonly Dictionary<string, object> Context;

            [JsonProperty("meta")]
            public readonly MetaData Meta;

            public TelemetryData(TelemetryEvent eventData, MetaData meta)
                : this()
            {
                Meta = meta;
                Context = eventData.Context;
                EventType = eventData.EventType;
            }
        }

        private readonly struct TelemetryPayload
        {
            [JsonProperty("stats")]
            public readonly List<TelemetryData> Stats;

            public TelemetryPayload(TelemetryData data)
            {
                Stats = new List<TelemetryData>() { data };
            }

            public TelemetryPayload(List<TelemetryData> data)
            {
                Stats = data;
            }
        }

        private struct GeoLocation
        {
            [JsonProperty("status", Required = Required.Always)]
            public string Status;
            [JsonProperty("region", Required = Required.Always)]
            public string Region;
            [JsonProperty("regionName", Required = Required.Always)]
            public string RegionName;
            [JsonProperty("city")]
            public string City;
        }

        private static async Task<GeoLocation> GetGeoLocation()
        {
            string endpoint = "http://ip-api.com/json/?fields=status,region,regionName,city";

            HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

            using (var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead))
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer serializer = new JsonSerializer();
                var geolocation = serializer.Deserialize<GeoLocation>(jsonReader);

                return geolocation;
            }
        }
    }
}
