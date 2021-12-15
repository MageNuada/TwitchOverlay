using Avalonia;
using Avalonia.ReactiveUI;
using ChatOverlay.Core;
using LogMagic;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace ChatOverlay;

class Program
{
    private const string tempName = "temp_ChatOverlay.Core.dll";
    private const string localName = "ChatOverlay.Core.dll";

    private static ILog Log { get; set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    public static void Main(string[] args)
    {
        Log = L.G("client");
        L.Config.WriteTo.File($".{Path.DirectorySeparatorChar}logs{Path.DirectorySeparatorChar}log.txt").EnrichWith.ThreadId();
        //UpdateClient();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void UpdateClient()
    {
        bool result = false;
        try
        {
            if (!Debugger.IsAttached)
            {
                using SHA256 sha256Hash = SHA256.Create();
                using HttpClient client = new();
                var bytes = client.GetByteArrayAsync(new Uri(@"https://drive.google.com/uc?export=download&id=14H9vs7Smv8wvyZYy0LP4CMjAKzJ9vQHU")).Result;
                var netSource = Encoding.UTF8.GetString(bytes);

                var localSource = File.ReadAllText("ChatOverlay.Core.dll");
                string localHash = GetHash(sha256Hash, localSource);
                string netHash = GetHash(sha256Hash, netSource);

                // Create a StringComparer an compare the hashes.
                StringComparer comparer = StringComparer.OrdinalIgnoreCase;

                if (comparer.Compare(localHash, netHash) != 0)
                {
                    if (File.Exists(tempName))
                        File.Delete(tempName);
                    File.WriteAllBytes(tempName, bytes);
                    result = true;
                }
            }
            if (result)
            {
                File.Delete(localName);
                File.Move(tempName, localName);
            }
        }
        catch (Exception e)
        {
            Log.Trace($"Error while updating library: ", e);
        }
    }

    private static string GetHash(HashAlgorithm hashAlgorithm, string input)
    {
        // Convert the input string to a byte array and compute the hash.
        byte[] data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));

        // Create a new Stringbuilder to collect the bytes
        // and create a string.
        var sBuilder = new StringBuilder();

        // Loop through each byte of the hashed data
        // and format each one as a hexadecimal string.
        for (int i = 0; i < data.Length; i++)
            sBuilder.Append(data[i].ToString("x2"));

        // Return the hexadecimal string.
        return sBuilder.ToString();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .LogToTrace();
}
