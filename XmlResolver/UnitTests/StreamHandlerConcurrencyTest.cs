using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Org.XmlResolver.Utils;

namespace UnitTests {
    /// <summary>
    /// Regression test for the lost-update race on the global <c>UriUtils.streamHandlers</c> field.
    /// </summary>
    /// <para>RegisterStreamHandler/Dispose perform an unsynchronized read-modify-write of a shared
    /// static field. Under concurrent registration/disposal this drops handlers, after which
    /// <see cref="UriUtils.GetStream(string)"/> falls through to the default handlers. Because the
    /// custom URI scheme used here matches no default handler, a lost registration surfaces as an
    /// <c>ArgumentException("Unexpected URI scheme")</c> (or a content mismatch). With the mutation
    /// synchronized, every registered handler stays resolvable and the test passes.</para>
    public class StreamHandlerConcurrencyTest {
        [Test]
        public void ConcurrentRegisterAndResolve() {
            const int taskCount = 16;
            const int iterations = 500;
            ConcurrentBag<string> failures = [];

            Parallel.For(0, taskCount, t => {
                for (int i = 0; i < iterations; i++) {
                    string uri = "komsa-test:handler/" + t + "/" + i;
                    try {
                        using (UriUtils.RegisterStreamHandler(
                                   u => u == uri,
                                   (u, asm) => new MemoryStream(Encoding.UTF8.GetBytes(u)))) {
                            // Widen the window so a concurrent register/dispose can interfere.
                            Thread.Yield();
                            using Stream stream = UriUtils.GetStream(uri);
                            using MemoryStream buffer = new();
                            stream.CopyTo(buffer);
                            string actual = Encoding.UTF8.GetString(buffer.ToArray());
                            if (actual != uri) {
                                failures.Add("content mismatch for " + uri + " (got '" + actual + "')");
                            }
                        }
                    }
                    catch (Exception ex) {
                        failures.Add(uri + ": " + ex.GetType().Name + ": " + ex.Message);
                    }
                }
            });

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }
    }
}
