using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV.Network;
var root = Path.Combine(Path.GetTempPath(), "license-recovery-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
using var seller = RSA.Create(2048);
var pub = seller.ExportSubjectPublicKeyInfoPem();
var terms = new LicenseTerms("QA", 2, "unico", null, "", 1, "qa", "pro", "001");
var bytes = JsonSerializer.SerializeToUtf8Bytes(terms);
var signed = JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(bytes),
    Convert.ToBase64String(seller.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))));
try {
    foreach (var damage in new[] { Array.Empty<byte>(), new byte[] { 1, 2, 3 }, RandomNumberGenerator.GetBytes(128) }) {
        var path = Path.Combine(root, Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, damage);
        var license = new NetworkLicense(path, "QA", pub);
        Check(license.Terms.Revision == 0, "Corruption must not activate PDV");
        var preserved = Directory.GetFiles(root, Path.GetFileName(path) + ".corrupt-*");
        Check(preserved.Length == 1 && File.ReadAllBytes(preserved[0]).SequenceEqual(damage), "Original not preserved");
        try { license.Activate("{}"); throw new Exception("Invalid activation accepted"); }
        catch (Exception e) when (e is ArgumentException or JsonException or CryptographicException or FormatException) {}
        license.Activate(signed);
        Check(new NetworkLicense(path, "QA", pub).Terms == terms, "Signed reactivation did not persist");
    }
    var normal = Path.Combine(root, "normal");
    var active = new NetworkLicense(normal, "QA", pub); active.Activate(signed);
    active.Register("T", "K", "Terminal");
    var reopened = new NetworkLicense(normal, "QA", pub);
    Check(reopened.RegisteredCount == 2 && reopened.Terms == terms, "Normal license or devices changed");
    Check(Directory.GetFiles(root, "normal.corrupt-*").Length == 0, "Healthy file quarantined");
    var locked = Path.Combine(root, "locked"); File.WriteAllBytes(locked, new byte[] { 1 });
    using (var handle = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
        try { _ = new NetworkLicense(locked, "QA", pub); throw new Exception("Locked file accepted"); }
        catch (IOException) {}
    }
    Check(File.ReadAllBytes(locked).SequenceEqual(new byte[] { 1 }), "Locked file modified");
    Console.WriteLine("PASS: truncated/empty/corrupt preservation, signed reactivation, healthy license/devices, locked file.");
} finally { Directory.Delete(root, true); }
namespace LealInfoPDV.Network { internal static class SellerPublicKey { internal const string Pem = ""; } }
