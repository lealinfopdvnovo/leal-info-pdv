using System.Net.Http;
using System.Text.Json;
namespace LealInfoPDV;

internal static class DeliveryAddress
{
    private static readonly HttpClient Client=new(){Timeout=TimeSpan.FromSeconds(12)};
    internal sealed record CepAddress(string Street,string District,string City,string Uf);
    internal static string NormalizeCep(string value)
    {
        var text=value.Trim().Replace("-","");
        return text.Length==8&&text.All(c=>c>='0'&&c<='9')?text:"";
    }
    internal static CepAddress Parse(string json)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        if(root.TryGetProperty("erro",out var error)&&(error.ValueKind==JsonValueKind.True||error.ToString()=="true"))
            throw new ArgumentException("CEP não encontrado. Confira o CEP ou preencha o endereço manualmente.");
        string Read(string key)=>root.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
        var result=new CepAddress(Read("logradouro"),Read("bairro"),Read("localidade"),Read("uf"));
        if(string.IsNullOrWhiteSpace(result.City)||result.Uf.Length!=2)throw new ArgumentException("Resposta de CEP inválida.");
        return result;
    }
    internal static async Task<CepAddress> LookupAsync(string cep,CancellationToken token)
    {
        var digits=NormalizeCep(cep);
        if(digits.Length!=8)throw new ArgumentException("Informe um CEP válido com 8 dígitos.");
        return Parse(await Client.GetStringAsync("https://viacep.com.br/ws/"+digits+"/json/",token));
    }
    internal static string Compose(string cep,string street,string number,string district,string city,string uf)
    {
        var digits=NormalizeCep(cep);uf=uf.Trim().ToUpperInvariant();
        if(digits.Length!=8||new[]{street,number,district,city}.Any(string.IsNullOrWhiteSpace)||uf.Length!=2||!uf.All(c=>c>='A'&&c<='Z'))
            throw new ArgumentException("Informe CEP válido, Rua/Logradouro, Número (ou S/N), Bairro, Cidade e UF.");
        return $"{street.Trim()}, {number.Trim()}, {district.Trim()}, {city.Trim()} - {uf}, CEP {digits[..5]}-{digits[5..]}";
    }
    internal static string Additional(string complement,string reference)=>string.Join(" | ",new[]{
        string.IsNullOrWhiteSpace(complement)?"":"Complemento: "+complement.Trim(),
        string.IsNullOrWhiteSpace(reference)?"":"Referência: "+reference.Trim()}.Where(s=>s.Length>0));
}
