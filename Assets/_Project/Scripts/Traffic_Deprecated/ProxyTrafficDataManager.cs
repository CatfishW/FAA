using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Net.Security;
using UnityEngine;
using UnityEngine.Networking;
using System.Security.Cryptography.X509Certificates;
/// <summary>
/// Extends TrafficDataManager to add support for random proxy usage
/// </summary>
public class ProxyTrafficDataManager : TrafficDataManager
{
    private List<string> proxyList = new List<string>();
    private string selectedProxy;
    private string proxyHost;
    private int proxyPort;
    private bool usingSocks4 = false;

    private void Start()
    {
        LoadProxies();
        SelectRandomProxy();
        ParseSelectedProxy();
        
        // Test the proxy connection
        //StartCoroutine(TestProxyConnection());
    }

    /// <summary>
    /// Loads proxy IPs from the proxy.csv file.
    /// </summary>
    private void LoadProxies()
    {
        string filePath = Path.Combine(Application.dataPath, "Resources/proxy.csv");
        if (File.Exists(filePath))
        {
            string[] lines = File.ReadAllLines(filePath);
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line) && line.Trim().StartsWith("socks4://", StringComparison.OrdinalIgnoreCase))
                {
                    proxyList.Add(line.Trim());
                }
            }
            Debug.Log($"Loaded {proxyList.Count} SOCKS4 proxies from proxy.csv");
        }
        else
        {
            Debug.LogError("Proxy file not found at: " + filePath);
        }
    }

    /// <summary>
    /// Selects a random proxy from the loaded list.
    /// </summary>
    private void SelectRandomProxy()
    {
        if (proxyList.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, proxyList.Count);
            selectedProxy = proxyList[randomIndex];
            Debug.Log($"Selected Proxy: {selectedProxy}");
        }
        else
        {
            Debug.LogWarning("No proxies available to select.");
        }
    }

    /// <summary>
    /// Parses the selected proxy string into host and port components
    /// </summary>
    private void ParseSelectedProxy()
    {
        if (string.IsNullOrEmpty(selectedProxy))
            return;

        try
        {
            // Ensure the proxy starts with "socks4://"
            if (!selectedProxy.StartsWith("socks4://", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"Ignoring non-SOCKS4 proxy: {selectedProxy}");
                return;
            }

            usingSocks4 = true;
            string proxyUrl = selectedProxy.Substring(9).Trim(); // Remove the "socks4://" prefix

            // Split by comma if there's additional info like country code
            if (proxyUrl.Contains(","))
            {
                string[] proxyParts = proxyUrl.Split(',');
                proxyUrl = proxyParts[0].Trim();
            }

            // Parse as IP:Port format
            string[] addressParts = proxyUrl.Split(':');
            if (addressParts.Length == 2)
            {
                proxyHost = addressParts[0];
                if (!int.TryParse(addressParts[1], out proxyPort))
                {
                    proxyPort = 1080; // Default SOCKS4 port
                    Debug.LogWarning($"Invalid proxy port, using default 1080: {addressParts[1]}");
                }
            }
            else
            {
                Debug.LogError($"Invalid SOCKS4 proxy format: {proxyUrl}. Expected format: socks4://IP:Port");
                return;
            }

            Debug.Log($"Parsed SOCKS4 proxy: {proxyHost}:{proxyPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing SOCKS4 proxy: {ex.Message}");
        }
    }

    /// <summary>
    /// Overrides the CreateApiRequest method to use the selected proxy.
    /// </summary>
    protected override UnityWebRequest CreateApiRequest(string url)
    {
        UnityWebRequest request = new UnityWebRequest(url, "GET");
        request.downloadHandler = new DownloadHandlerBuffer();

        // Add the same headers as in the base class
        request.SetRequestHeader("Accept", "application/json");

        // Attach a custom certificate handler to bypass SSL verification
        request.certificateHandler = new BypassCertificateHandler();

        // Use the proxy to send the request
        StartCoroutine(SendWebRequestWithProxy(request, (response) =>
        {
            if (response.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Request successful! Response: {response.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"Request failed! Error: {response.error}");
            }
        }));

        return request;
    }

    /// <summary>
    /// Sends a request through a SOCKS4 proxy
    /// </summary>
    public IEnumerator SendWebRequestWithProxy(UnityWebRequest request, Action<UnityWebRequest> onComplete)
    {
        if (usingSocks4 && !string.IsNullOrEmpty(proxyHost) && proxyPort > 0)
        {
            Debug.Log($"Sending request through SOCKS4 proxy: {proxyHost}:{proxyPort}");
            
            // Parse the URL to get host and path
            Uri uri = new Uri(request.url);
            string targetHost = uri.Host;
            int targetPort = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);
            bool isHttps = uri.Scheme == "https";
            
            // Create a custom download handler that will use our SOCKS4 proxy
            byte[] result = null;
            string error = null;
            
            // Run the proxy connection in a background thread
            yield return new WaitForThreadedTask(() => 
            {
                try
                {
                    result = SendThroughSocks4(targetHost, targetPort, uri.PathAndQuery, isHttps);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            });
            
            if (error != null)
            {
                Debug.LogError($"Proxy error: {error}");
                request.Abort();
                request.SetRequestHeader("X-Proxy-Error", error);
                onComplete?.Invoke(request);
                yield break;
            }
            
            // Create a mock response
            if (result != null)
            {
                Debug.Log($"Received {result.Length} bytes from proxy");
                // Set the result to the request's download handler
                request.downloadHandler = new DownloadHandlerBuffer();
                request.downloadHandler = new CustomDownloadHandler(result);
                onComplete?.Invoke(request);
                //yield return request; // Return the request to indicate completion through proxy
            }
            else
            {
                Debug.LogError("Failed to get response through proxy");
                request.Abort();
                onComplete?.Invoke(request);
            }
        }
        else
        {
            // Fall back to standard request if not using SOCKS4
            yield return request.SendWebRequest();
            onComplete?.Invoke(request);
        }
    }
    
    private byte[] SendThroughSocks4(string targetHost, int targetPort, string pathAndQuery, bool isHttps)
    {
        try
        {
            // Resolve the target hostname to an IP address
            IPAddress targetIp;
            if (!IPAddress.TryParse(targetHost, out targetIp))
            {
                IPAddress[] addresses = Dns.GetHostAddresses(targetHost);
                if (addresses.Length == 0)
                {
                    throw new Exception($"Could not resolve hostname: {targetHost}");
                }
                targetIp = addresses[0];
            }

            // Connect to the SOCKS4 proxy server
            using (TcpClient client = new TcpClient())
            {
                client.Connect(proxyHost, proxyPort);
                client.ReceiveTimeout = 10000; // Set timeout to 10 seconds
                client.SendTimeout = 10000;

                using (NetworkStream stream = client.GetStream())
                {
                    // SOCKS4 handshake
                    byte[] handshake = new byte[9];
                    handshake[0] = 4; // SOCKS version 4
                    handshake[1] = 1; // CONNECT command
                    handshake[2] = (byte)(targetPort >> 8); // Port high byte
                    handshake[3] = (byte)(targetPort & 0xff); // Port low byte

                    // Copy IP (4 bytes)
                    byte[] ipBytes = targetIp.GetAddressBytes();
                    Array.Copy(ipBytes, 0, handshake, 4, 4);

                    // NULL termination
                    handshake[8] = 0;

                    // Send handshake
                    stream.Write(handshake, 0, handshake.Length);

                    // Read response (should be 8 bytes)
                    byte[] response = new byte[8];
                    int bytesRead = stream.Read(response, 0, response.Length);

                    if (bytesRead < 2 || response[1] != 90)
                    {
                        throw new Exception($"SOCKS connection failed with code: {response[1]}");
                    }

                    Debug.Log("SOCKS4 connection established successfully!");

                    // Handle HTTPS (if required)
                    if (isHttps)
                    {
                        using (var sslStream = new SslStream(stream, false, new RemoteCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true)))
                        {
                            sslStream.AuthenticateAsClient(targetHost, null, System.Security.Authentication.SslProtocols.Tls12, false);
                            return SendHttpRequest(sslStream, targetHost, pathAndQuery);
                        }
                    }
                    else
                    {
                        return SendHttpRequest(stream, targetHost, pathAndQuery);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"SOCKS4 proxy error: {ex.Message}");
            throw;
        }
    }

    private byte[] SendHttpRequest(Stream stream, string targetHost, string pathAndQuery)
    {
        // Build HTTP request
        string httpRequest = $"GET {pathAndQuery} HTTP/1.1\r\n" +
                            $"Host: {targetHost}\r\n" +
                            "Accept: application/json\r\n" +
                            "Connection: close\r\n\r\n";

        byte[] requestBytes = System.Text.Encoding.ASCII.GetBytes(httpRequest);
        stream.Write(requestBytes, 0, requestBytes.Length);

        // Read the response
        using (MemoryStream ms = new MemoryStream())
        {
            byte[] buffer = new byte[4096];
            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;
                ms.Write(buffer, 0, read);
            }

            return ms.ToArray();
        }
    }
    
    // Helper class to run tasks on a background thread
    private class WaitForThreadedTask : CustomYieldInstruction
    {
        private bool isDone = false;
        
        public WaitForThreadedTask(Action action)
        {
            Task.Run(() => 
            {
                try
                {
                    action();
                }
                finally
                {
                    isDone = true;
                }
            });
        }
        
        public override bool keepWaiting => !isDone;
    }

    // Custom DownloadHandler to handle data
    private class CustomDownloadHandler : DownloadHandlerScript
    {
        public CustomDownloadHandler(byte[] data) : base(data) { }
    }
    // Custom CertificateHandler to bypass SSL verification
    private class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            // Always return true to bypass SSL certificate validation
            return true;
        }
    }
}