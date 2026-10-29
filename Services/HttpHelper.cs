using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace QuickLook.Plugin.PluginManager.Services
{
    internal static class HttpHelper
    {
        public const string UserAgent = "QuickLook.Plugin.PluginManager/1.0";

        static HttpHelper()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
            }

            try
            {
                ServicePointManager.Expect100Continue = false;
            }
            catch
            {
            }
        }

        public static HttpClient CreateClient(TimeSpan timeout)
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            var client = new HttpClient(handler) { Timeout = timeout };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            return client;
        }

        public static string Describe(Exception ex)
        {
            if (ex == null) return "";
            var parts = new List<string>();
            for (var e = ex; e != null; e = e.InnerException)
            {
                var message = e.Message;
                if (string.IsNullOrWhiteSpace(message)) continue;
                message = message.Trim();
                var socket = e as SocketException;
                if (socket != null)
                    message += " [SocketError " + socket.ErrorCode + "]";
                if (parts.Contains(message, StringComparer.OrdinalIgnoreCase)) continue;
                parts.Add(message);
            }
            return parts.Count == 0 ? ex.GetType().Name : string.Join(" -> ", parts);
        }
    }
}
