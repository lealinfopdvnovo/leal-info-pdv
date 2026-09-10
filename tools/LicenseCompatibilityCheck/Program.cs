using LealInfoLicenseGenerator;
using LealInfoPDV;

var deviceId = LicenseManager.DeviceId();
var expires = DateTime.UtcNow.AddYears(1);

foreach (var plan in new[] { "STANDARD", "PLUS", "PRO" })
{
    var serial = LicenseSigner.CreateLicense(plan, "TESTE AUTOMATICO", deviceId, expires);
    if (!LicenseManager.TryParse(serial, out var state) || !state.IsValid)
        throw new Exception($"Falha de compatibilidade no plano {plan}: {state.Error}");

    if (!state.Edition.ToString().Equals(plan, StringComparison.OrdinalIgnoreCase))
        throw new Exception($"Plano validado incorretamente. Esperado {plan}, obtido {state.Edition}.");
}

var anySerial = LicenseSigner.CreateLicense("PRO", "TESTE ANY", "ANY", expires);
if (!LicenseManager.TryParse(anySerial, out var anyState) || !anyState.IsValid)
    throw new Exception($"Falha de compatibilidade no serial ANY: {anyState.Error}");

Console.WriteLine("COMPATIBILIDADE OK: o gerador mestre e o PDV usam a mesma identidade criptografica.");
