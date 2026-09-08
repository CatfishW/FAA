using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FAA.Customization.Tests
{
    public class XPlaneApiRecoveryTests
    {
        private static Type PolicyType => Type.GetType("FAA.XPlaneIntegration.Core.XPlaneApiRetryPolicy, Assembly-CSharp", true);
        private static Type BridgeType => Type.GetType("FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge, Assembly-CSharp", true);

        [TestCase(0L, false)]
        [TestCase(200L, false)]
        [TestCase(401L, false)]
        [TestCase(403L, false)]
        [TestCase(404L, true)]
        [TestCase(405L, true)]
        [TestCase(429L, false)]
        [TestCase(500L, false)]
        [TestCase(503L, false)]
        public void OnlyMissingEndpoints_EnableLegacyFallback(long status, bool expected)
        {
            Assert.That(PolicyType.GetMethod("AllowsCompatibilityFallback").Invoke(null, new object[] { status }), Is.EqualTo(expected));
        }

        [Test]
        public void RepeatedFailures_BackOffAndSuccessfulRecoveryResetsTheBudget()
        {
            object policy = Activator.CreateInstance(PolicyType);
            double now = 100d;
            foreach (double delay in new[] { 1d, 2d, 4d, 8d, 10d, 10d })
            {
                PolicyType.GetMethod("RecordFailure").Invoke(policy, new object[] { now });
                Assert.That(PolicyType.GetMethod("SecondsRemaining").Invoke(policy, new object[] { now }), Is.EqualTo(delay));
                Assert.That(PolicyType.GetMethod("CanRequest").Invoke(policy, new object[] { now + delay - 0.01d }), Is.False);
                now += delay;
                Assert.That(PolicyType.GetMethod("CanRequest").Invoke(policy, new object[] { now }), Is.True);
            }

            PolicyType.GetMethod("Reset").Invoke(policy, null);
            Assert.That(PolicyType.GetProperty("ConsecutiveFailures").GetValue(policy), Is.EqualTo(0));
            Assert.That(PolicyType.GetMethod("CanRequest").Invoke(policy, new object[] { now }), Is.True);
            PolicyType.GetMethod("RecordFailure").Invoke(policy, new object[] { now });
            Assert.That(PolicyType.GetMethod("SecondsRemaining").Invoke(policy, new object[] { now }), Is.EqualTo(1d));
        }

        [UnityTest]
        public IEnumerator ServerFailure_DoesNotFanOutOrRetryImmediately()
        {
            using (var server = new SnapshotTestServer(_ => (503, "{}")))
            using (var bridge = new TestBridge(server.BaseUrl))
            {
                yield return bridge.PollOnce();
                yield return bridge.PollOnce();
                Assert.That(server.Paths.ToArray(), Is.EqualTo(new[] { "/v1/snapshot" }));
                Assert.That(bridge.Get("ConsecutiveHttpFailures"), Is.EqualTo(1));
                Assert.That(bridge.Get("IsFeedHealthy"), Is.False);
                StringAssert.Contains("Retrying", (string)bridge.Get("LastError"));
            }
        }

        [UnityTest]
        public IEnumerator ValidEmptySnapshot_WaitsForSimulatorInsteadOfProbingLegacyEndpoints()
        {
            const string waiting = "{\"health\":{\"status\":\"waiting\",\"last_packet_age_sec\":99},\"raw\":{}}";
            using (var server = new SnapshotTestServer(_ => (200, waiting)))
            using (var bridge = new TestBridge(server.BaseUrl))
            {
                yield return bridge.PollOnce();
                Assert.That(server.Paths.ToArray(), Is.EqualTo(new[] { "/v1/snapshot" }));
                Assert.That(bridge.Get("ConsecutiveHttpFailures"), Is.EqualTo(0));
                Assert.That(bridge.Get("IsFeedHealthy"), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator MissingSnapshot_StillSupportsLegacyCategoryApi()
        {
            using (var server = new SnapshotTestServer(path => path == "/v1/snapshot"
                       ? (404, "{}")
                       : (200, "{\"status\":\"waiting\",\"values\":{}}")))
            using (var bridge = new TestBridge(server.BaseUrl))
            {
                yield return bridge.PollOnce();
                Assert.That(server.Paths.ToArray(), Is.EqualTo(new[]
                {
                    "/v1/snapshot", "/api/health", "/api/data?category=aircraft",
                    "/api/data?category=weather", "/api/data?category=systems", "/api/data?category=traffic"
                }));
                Assert.That(bridge.Get("ConsecutiveHttpFailures"), Is.EqualTo(0));
            }
        }

        [UnityTest]
        public IEnumerator MissingHealth_StopsBeforeRequestingCategories()
        {
            using (var server = new SnapshotTestServer(_ => (404, "{}")))
            using (var bridge = new TestBridge(server.BaseUrl))
            {
                yield return bridge.PollOnce();
                Assert.That(server.Paths.ToArray(), Is.EqualTo(new[] { "/v1/snapshot", "/api/health", "/health" }));
                Assert.That(bridge.Get("ConsecutiveHttpFailures"), Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator InvalidJson_IsAFailureNotAnApiVersionProbe()
        {
            using (var server = new SnapshotTestServer(_ => (200, "not json")))
            using (var bridge = new TestBridge(server.BaseUrl))
            {
                yield return bridge.PollOnce();
                Assert.That(server.Paths.ToArray(), Is.EqualTo(new[] { "/v1/snapshot" }));
                Assert.That(bridge.Get("ConsecutiveHttpFailures"), Is.EqualTo(1));
                Assert.That(bridge.Get("IsFeedHealthy"), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulRetry_ResumesPollingAndClearsFailureState()
        {
            int count = 0;
            const string waiting = "{\"health\":{\"status\":\"waiting\",\"last_error\":\"\",\"last_packet_age_sec\":99},\"raw\":{}}";
            using (var server = new SnapshotTestServer(_ => ++count == 1 ? (503, "{}") : (200, waiting)))
            using (var bridge = new TestBridge(server.BaseUrl))
            {
                yield return bridge.PollOnce();
                // Use realtime, just like the runtime. Time.timeScale must not
                // prevent an API from reconnecting while the simulator is paused.
                double retryAt = Time.realtimeSinceStartupAsDouble + (double)bridge.Get("HttpRetrySecondsRemaining");
                while (Time.realtimeSinceStartupAsDouble < retryAt + 0.02d)
                {
                    yield return null;
                }
                yield return bridge.PollOnce();
                Assert.That(server.Paths.Count, Is.EqualTo(2));
                Assert.That(bridge.Get("ConsecutiveHttpFailures"), Is.EqualTo(0));
                Assert.That(bridge.Get("LastError"), Is.EqualTo(string.Empty));
            }
        }

        private sealed class TestBridge : IDisposable
        {
            private readonly GameObject root;
            private readonly Component component;

            public TestBridge(string baseUrl)
            {
                root = new GameObject("API Recovery Test");
                root.SetActive(false); // Do not bind to or alter the user's scene.
                component = root.AddComponent(BridgeType);
                BridgeType.GetField("baseUrl", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, baseUrl);
            }

            public IEnumerator PollOnce() => Drive((IEnumerator)BridgeType.GetMethod("PollOnce", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null));
            public object Get(string name) => BridgeType.GetProperty(name).GetValue(component);
            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);

            private static IEnumerator Drive(IEnumerator routine)
            {
                // EditMode's test iterator does not wait for every runtime yield
                // instruction. Explicitly await UnityWebRequestAsyncOperation so
                // assertions exercise completed responses, never InProgress.
                try
                {
                    while (routine.MoveNext())
                    {
                        if (routine.Current is IEnumerator nested)
                        {
                            yield return Drive(nested);
                        }
                        else if (routine.Current is AsyncOperation operation)
                        {
                            while (!operation.isDone) yield return null;
                        }
                        else
                        {
                            yield return routine.Current;
                        }
                    }
                }
                finally
                {
                    (routine as IDisposable)?.Dispose();
                }
            }
        }

        /// <summary>A loopback-only fixture; no SSH or running simulator needed.</summary>
        private sealed class SnapshotTestServer : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly Func<string, (int status, string body)> respond;
            private readonly Task worker;
            private volatile bool disposed;
            public readonly ConcurrentQueue<string> Paths = new ConcurrentQueue<string>();
            public string BaseUrl { get; }

            public SnapshotTestServer(Func<string, (int status, string body)> respond)
            {
                this.respond = respond;
                listener.Start();
                BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
                worker = Task.Run(Serve);
            }

            private void Serve()
            {
                try
                {
                    while (!disposed)
                    {
                        using (TcpClient client = listener.AcceptTcpClient())
                        {
                            client.ReceiveTimeout = 2000;
                            client.SendTimeout = 2000;
                            using (NetworkStream stream = client.GetStream())
                            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                            {
                                string line = reader.ReadLine();
                                if (line == null) continue;
                                string path = line.Split(' ')[1];
                                while (!string.IsNullOrEmpty(reader.ReadLine())) { }
                                Paths.Enqueue(path);
                                var response = respond(path);
                                byte[] body = Encoding.UTF8.GetBytes(response.body);
                                byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 {response.status} Test\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                                stream.Write(header, 0, header.Length);
                                stream.Write(body, 0, body.Length);
                            }
                        }
                    }
                }
                catch (SocketException) when (disposed) { }
                catch (ObjectDisposedException) when (disposed) { }
            }

            public void Dispose()
            {
                disposed = true;
                listener.Stop();
                Assert.That(worker.Wait(TimeSpan.FromSeconds(3)), Is.True, "The test server must shut down cleanly.");
            }
        }
    }
}
