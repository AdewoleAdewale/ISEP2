using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ISEP.Services
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════════
    ///  THE SINGLE NETWORK ENTRY POINT FOR THE WHOLE APP.
    ///
    ///  Every page — Login, Dashboard, History, Verify, Payment,
    ///  Cashout, ChangePin, ChangePassword — goes through here. There is
    ///  exactly one HttpClient, one TLS policy, one timeout policy and
    ///  one retry policy in the process.
    ///
    ///  WHY AN EXPLICIT HttpClientHandler:
    ///  ISEP.Android.csproj sets AndroidHttpClientHandlerType to
    ///  Xamarin.Android.Net.AndroidClientHandler. That only affects the
    ///  PARAMETERLESS `new HttpClient()`. Passing an explicit handler
    ///  below selects Mono's managed handler instead, which is the only
    ///  one that honours ServerCertificateCustomValidationCallback and
    ///  ServicePointManager.SecurityProtocol. AndroidClientHandler
    ///  silently ignores both — which is why the old per-page
    ///  `ServicePointManager.SecurityProtocol = Tls12` lines did nothing.
    ///
    ///  This also matters for minSdkVersion 21: several Android 5.x
    ///  builds do not negotiate TLS 1.2 by default. The managed BTLS
    ///  stack does, regardless of OS version.
    /// ══════════════════════════════════════════════════════════════════
    /// </summary>
    public static class ApiClient
    {
        // ── Policy knobs ────────────────────────────────────────────
        public const int DefaultTimeoutSeconds = 45;
        private const int MaxRetries = 2;              // 3 attempts total
        private const int RetryBaseDelayMs = 800;      // 800ms, then 1600ms

        static ApiClient()
        {
            // Runs before _client is ever realised, so ServicePointManager
            // is configured before the first handler is constructed. The
            // original bug was setting SecurityProtocol AFTER the client
            // existed, at which point it is inert.
            ConfigureSSL();
        }

        // ════════════════════════════════════════════════════════════
        //  TLS / TRANSPORT CONFIGURATION
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// App-wide transport configuration. Called from App.xaml.cs and
        /// from the static constructor. Safe to call more than once.
        /// </summary>
        public static void ConfigureSSL()
        {
            // TLS 1.2 is the floor. TLS 1.3 (12288) is not in the
            // netstandard2.0 enum, so it is added by cast and only if the
            // running platform accepts it.
            var protocols = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

            try
            {
                ServicePointManager.SecurityProtocol = protocols | (SecurityProtocolType)12288;
            }
            catch (NotSupportedException)
            {
                ServicePointManager.SecurityProtocol = protocols;
            }

            ServicePointManager.ServerCertificateValidationCallback =
                new RemoteCertificateValidationCallback(ValidateCertificate);

            ServicePointManager.DefaultConnectionLimit = 10;

            // Expect: 100-continue adds a full round trip before the body
            // is sent and several gateways mishandle it on POST. The old
            // value of `true` was costing a round trip on every payment.
            ServicePointManager.Expect100Continue = false;

            // Re-resolve DNS every 60s so a failover on the API host is
            // picked up without an app restart.
            ServicePointManager.DnsRefreshTimeout = 60000;
        }

        /// <summary>
        /// The ONE certificate-validation policy for the whole app.
        /// Valid chains always pass. On any SSL error the outcome is
        /// governed by the single switch in BrandConfig.
        ///
        /// SET BrandConfig.AllowUntrustedServerCertificate = false ONCE
        /// THE SERVER'S INTERMEDIATE CHAIN IS FIXED. Leaving it true in
        /// production means this payments app will accept a
        /// man-in-the-middle certificate.
        /// </summary>
        public static bool ValidateCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            System.Diagnostics.Debug.WriteLine($"[ApiClient] SSL error: {sslPolicyErrors}");
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Subject: {certificate?.Subject}");
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Issuer:  {certificate?.Issuer}");

            return BrandConfig.AllowUntrustedServerCertificate;
        }

        // ════════════════════════════════════════════════════════════
        //  THE SHARED CLIENT
        // ════════════════════════════════════════════════════════════

        private static readonly Lazy<HttpClient> _client = new Lazy<HttpClient>(() =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    (message, cert, chain, errors) => ValidateCertificate(message, cert, chain, errors),

                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler)
            {
                // Infinite here on purpose. Timeouts are enforced per
                // request by a CancellationTokenSource so that a retry
                // gets a fresh budget instead of inheriting a
                // half-consumed one.
                Timeout = Timeout.InfiniteTimeSpan
            };

            client.DefaultRequestHeaders.Add("User-Agent", "ISEP/2.0 (Android)");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            return client;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// The shared client. NEVER dispose this — it is process-wide.
        /// Prefer the Get/Post helpers below over touching it directly.
        /// </summary>
        public static HttpClient Instance => _client.Value;

        // ════════════════════════════════════════════════════════════
        //  CORE SEND — every call in the app funnels through this
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Sends a request with the shared TLS policy, a per-request
        /// timeout and transient-failure retry. Throws
        /// <see cref="ApiException"/> on any non-success outcome.
        /// </summary>
        private static async Task<string> SendAsync(
            HttpMethod method,
            string url,
            HttpContent content,
            IDictionary<string, string> headers,
            int timeoutSeconds,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Request URL is empty.", nameof(url));

            Exception lastError = null;

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                // An HttpRequestMessage cannot be reused across attempts,
                // so it is rebuilt each time.
                using (var request = new HttpRequestMessage(method, url))
                {
                    // Per-request headers. DefaultRequestHeaders would be
                    // shared across every concurrent call in the app —
                    // that is how Cashout's Super_Agent / TradingPin
                    // headers would leak onto unrelated requests.
                    if (headers != null)
                    {
                        foreach (var kv in headers)
                        {
                            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                            request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                        }
                    }

                    if (content != null)
                        request.Content = content;

                    try
                    {
                        using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
                        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct))
                        using (var response = await Instance.SendAsync(
                                   request, HttpCompletionOption.ResponseContentRead, linked.Token))
                        {
                            string body = response.Content == null
                                ? string.Empty
                                : await response.Content.ReadAsStringAsync();

                            if (response.IsSuccessStatusCode)
                                return body;

                            int code = (int)response.StatusCode;

                            System.Diagnostics.Debug.WriteLine(
                                $"[ApiClient] {method} {url} -> HTTP {code}");

                            // 4xx is the caller's fault. Retrying will not
                            // help, and on a payment endpoint it risks a
                            // duplicate charge. Fail immediately.
                            if (code < 500 && code != 408 && code != 429)
                                throw new ApiException(code, body, response.ReasonPhrase);

                            lastError = new ApiException(code, body, response.ReasonPhrase);
                        }
                    }
                    catch (ApiException)
                    {
                        throw; // already final, do not retry
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        // Caller cancelled (page closed) — honour it.
                        throw;
                    }
                    catch (OperationCanceledException)
                    {
                        lastError = new ApiException(0, null, "Request timed out.");
                        System.Diagnostics.Debug.WriteLine($"[ApiClient] Timeout on {url}");
                    }
                    catch (HttpRequestException hex)
                    {
                        lastError = hex;
                        System.Diagnostics.Debug.WriteLine($"[ApiClient] Transport error: {hex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[ApiClient] Inner: {hex.InnerException?.Message}");
                    }
                }

                if (attempt < MaxRetries)
                {
                    // Exponential backoff: 800ms, then 1600ms.
                    await Task.Delay(RetryBaseDelayMs * (int)Math.Pow(2, attempt), ct);
                }
            }

            throw lastError ?? new ApiException(0, null, "The request failed.");
        }

        private static T Deserialize<T>(string json, string url)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default(T);

            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonException jex)
            {
                // A WAF block or gateway timeout returns an HTML page.
                // Deserializing that throws — and until now it threw
                // straight into an `async void` handler.
                System.Diagnostics.Debug.WriteLine($"[ApiClient] Bad payload from {url}: {jex.Message}");
                System.Diagnostics.Debug.WriteLine(
                    $"[ApiClient] Raw: {json.Substring(0, Math.Min(300, json.Length))}");

                throw new ApiException(0, json, "The server sent an unexpected response.");
            }
        }

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API — signatures match the existing call sites
        // ════════════════════════════════════════════════════════════

        /// <summary>GET and deserialise. Used by History, Verify, Dashboard, ChangePin, ChangePassword.</summary>
        public static async Task<T> GetAsync<T>(
            string url,
            IDictionary<string, string> headers = null,
            int timeoutSeconds = DefaultTimeoutSeconds,
            CancellationToken ct = default(CancellationToken))
        {
            string json = await SendAsync(HttpMethod.Get, url, null, headers, timeoutSeconds, ct);
            return Deserialize<T>(json, url);
        }

        /// <summary>POST a JSON body and deserialise the reply. Used by Payment and Cashout.</summary>
        public static async Task<TResponse> PostAsync<TResponse>(
            string url,
            object payload,
            IDictionary<string, string> headers = null,
            int timeoutSeconds = DefaultTimeoutSeconds,
            CancellationToken ct = default(CancellationToken))
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            string json = await SendAsync(HttpMethod.Post, url, content, headers, timeoutSeconds, ct);
            return Deserialize<TResponse>(json, url);
        }

        /// <summary>GET the raw response body.</summary>
        public static Task<string> GetStringAsync(
            string url,
            IDictionary<string, string> headers = null,
            int timeoutSeconds = DefaultTimeoutSeconds,
            CancellationToken ct = default(CancellationToken))
        {
            return SendAsync(HttpMethod.Get, url, null, headers, timeoutSeconds, ct);
        }

        // ── Back-compat aliases so nothing else has to change ───────
        public static Task<T> GetJsonAsync<T>(string url, CancellationToken ct = default(CancellationToken))
            => GetAsync<T>(url, null, DefaultTimeoutSeconds, ct);

        public static Task<TResponse> PostJsonAsync<TResponse>(
            string url, object payload, CancellationToken ct = default(CancellationToken))
            => PostAsync<TResponse>(url, payload, null, DefaultTimeoutSeconds, ct);

        /// <summary>
        /// Kept only so older call sites still compile. Do not use — it
        /// creates an unpooled client outside the shared retry/timeout
        /// policy. Pass a headers dictionary to GetAsync/PostAsync
        /// instead.
        /// </summary>
        [Obsolete("Use ApiClient.GetAsync/PostAsync with the headers parameter.")]
        public static HttpClient CreateClient(int timeoutSeconds = DefaultTimeoutSeconds)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    (message, cert, chain, errors) => ValidateCertificate(message, cert, chain, errors)
            };

            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        }

        // ════════════════════════════════════════════════════════════
        //  ONE PLACE THAT TURNS AN EXCEPTION INTO USER-FACING TEXT
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Every page shows the same wording for the same failure.
        /// Call this from a catch block instead of ex.Message, which
        /// leaks stack detail to end users.
        /// </summary>
        public static string FriendlyMessage(Exception ex)
        {
            var api = ex as ApiException;
            if (api != null)
            {
                if (api.StatusCode == 0)
                    return "The server took too long or sent an unexpected response. Please try again.";
                if (api.StatusCode == 401 || api.StatusCode == 403)
                    return "Your session has expired. Please sign in again.";
                if (api.StatusCode == 404)
                    return "That record could not be found.";
                if (api.StatusCode == 429)
                    return "Too many requests. Please wait a moment and try again.";
                if (api.StatusCode >= 500)
                    return "The service is temporarily unavailable. Please try again shortly.";

                return "The request could not be completed. Please check the details and try again.";
            }

            if (ex is OperationCanceledException)
                return "The request timed out. Check your connection and try again.";

            if (ex is HttpRequestException)
                return "Could not reach the service. Please check your internet connection.";

            return "Something went wrong. Please try again.";
        }
    }

    /// <summary>
    /// Carries the HTTP status and raw body so a catch block can decide
    /// what to do without re-parsing strings.
    /// </summary>
    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }

        public ApiException(int statusCode, string responseBody, string reason)
            : base($"HTTP {statusCode}: {reason}")
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}