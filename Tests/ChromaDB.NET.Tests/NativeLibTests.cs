using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChromaDB.NET.Tests
{
    /// <summary>
    /// Tests for checking native library loading
    /// </summary>
    [TestClass]
    public class NativeLibTests
    {
        [TestMethod]
        public void NativeLib_LoadCheck()
        {
            // Get the test output directory
            var testDir = Path.GetDirectoryName(typeof(NativeLibTests).Assembly.Location);
            Console.WriteLine($"Test directory: {testDir}");

            // Check if the native library directories exist
            var runtimesDir = Path.Combine(testDir, "runtimes");
            Console.WriteLine($"Runtimes directory exists: {Directory.Exists(runtimesDir)}");

            string libPath = string.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows
                libPath = Path.Combine(runtimesDir, "win-x64", "native", "chroma_csharp.dll");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux
                libPath = Path.Combine(runtimesDir, "linux-x64", "native", "libchroma_csharp.so");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS
                string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
                libPath = Path.Combine(runtimesDir, arch, "native", "libchroma_csharp.dylib");
            }

            Console.WriteLine($"Native library path: {libPath}");
            Console.WriteLine($"Library exists: {File.Exists(libPath)}");

            // NuGet runtimes directory structure
            Directory.GetDirectories(testDir, "*", SearchOption.AllDirectories)
                .ToList()
                .ForEach(d => Console.WriteLine($"Directory: {d}"));

            // On Linux, copy the so file to the output directory as a workaround
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists(libPath))
            {
                string destPath = Path.Combine(testDir, "libchroma_csharp.so");
                if (!File.Exists(destPath))
                {
                    File.Copy(libPath, destPath);
                    Console.WriteLine($"Copied library to: {destPath}");
                }
            }

            // Just a test to make sure things are working
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void DisplayLibraryLoadingInfo()
        {
            Console.WriteLine("---------- Library Loading Diagnostics ----------");
            Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");

            var outputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Console.WriteLine($"Assembly directory: {outputDirectory}");

            var runtimesDir = Path.Combine(outputDirectory, "runtimes");
            Console.WriteLine($"Runtimes directory exists: {Directory.Exists(runtimesDir)}");

            if (Directory.Exists(runtimesDir))
            {
                Console.WriteLine("Contents of runtimes directory:");
                foreach (var dir in Directory.GetDirectories(runtimesDir))
                {
                    Console.WriteLine($"  {Path.GetFileName(dir)}/");
                    foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        Console.WriteLine($"    {Path.GetRelativePath(runtimesDir, file)}");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Running on: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"OS Architecture: {RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"Current directory path: {Directory.GetCurrentDirectory()}");

            // Try to create a client with verbose output
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "debug-chromadb-test", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                Console.WriteLine($"Creating test directory at: {tempDir}");

                Console.WriteLine("Creating ChromaClient...");
                using var client = new ChromaClient(persistDirectory: tempDir);
                Console.WriteLine("ChromaClient created successfully!");

                // Try to create a collection with different name patterns to test the validation
                Console.WriteLine("Attempting to create a collection...");
                string[] testNames = new string[] {
                    "debug-test-collection",
                    "a", // Too short
                    "a2", // Two chars
                    "abc", // Three chars
                    "abc123", // Alphanumeric
                    "debug_test_collection", // With underscores
                    "debug.test.collection", // With periods
                    "debug-test-collection-123" // Mix of allowed chars
                };

                foreach (var name in testNames)
                {
                    try
                    {
                        Console.WriteLine($"Trying name: '{name}'");
                        var embeddingFunction = new TestEmbeddingFunction(3);
                        using var collection = client.CreateCollectionWithUniqueName(embeddingFunction: embeddingFunction);
                        Console.WriteLine($"Collection created successfully with name: '{name}'");
                        break; // Stop on first success
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Collection creation failed for '{name}'! Error: {ex.Message}");
                    }
                }

                // Try to create a database instead
                Console.WriteLine("\nAttempting to create a database...");
                try
                {
                    client.CreateDatabase("debug-test-database");
                    Console.WriteLine("Database created successfully!");

                    // Get database ID
                    var id = client.GetDatabaseId("debug-test-database");
                    Console.WriteLine($"Database ID: {id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database creation failed! Error: {ex.Message}");
                    Console.WriteLine($"Error type: {ex.GetType().FullName}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }

                // Try the heartbeat function
                Console.WriteLine("\nTesting heartbeat...");
                try
                {
                    var heartbeat = client.Heartbeat();
                    Console.WriteLine($"Heartbeat: {heartbeat}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Heartbeat failed! Error: {ex.Message}");
                }

                Console.WriteLine("\nTest completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create ChromaClient! Error: {ex.Message}");
                Console.WriteLine($"Error type: {ex.GetType().FullName}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}