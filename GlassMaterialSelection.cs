using System.ComponentModel;

namespace LealInfoPDV;

public sealed record GlassMaterialLink(long ProductId, string Name, string Barcode, string Category, decimal Price);

internal sealed class GlassMaterialSelection : IDisposable
{
    private readonly BindingList<GlassMaterialLink> links = new();
    private readonly TextBox query = new() { Name = "MaterialQuery" };
    private readonly DataGridView results = Grid("MaterialResults");
    private readonly DataGridView selected = Grid("ProjectMaterials");
    internal IReadOnlyList<GlassMaterialLink> Materials => links.ToArray();
    internal void Mount(TableLayoutPanel fields)
    {
        GlassWorkshopForm.AddField(fields, "PESQUISAR NO CADASTRO DE PRODUTOS", query);
        var search = new SalesVisualButton { Name = "FindMaterial", Text = "PROCURAR MATERIAL" };
        search.Click += (_, _) => Search();
        GlassWorkshopForm.AddField(fields, "", search);
        GlassWorkshopForm.AddField(fields, "RESULTADOS (até 100; refine a pesquisa)", results);
        var link = new SalesVisualButton { Name = "LinkMaterial", Text = "VINCULAR MATERIAL SELECIONADO" };
        link.Click += (_, _) => { if (results.CurrentRow?.DataBoundItem is GlassMaterialLink item && !links.Any(x => x.ProductId == item.ProductId)) links.Add(item); };
        GlassWorkshopForm.AddField(fields, "", link);
        selected.DataSource = links;
        GlassWorkshopForm.AddField(fields, "MATERIAIS VINCULADOS AO PROJETO", selected);
        var remove = new SalesVisualButton { Name = "RemoveMaterialLink", Text = "REMOVER VÍNCULO DO PROJETO" };
        remove.Click += (_, _) => { if (selected.CurrentRow?.DataBoundItem is GlassMaterialLink item) links.Remove(item); };
        GlassWorkshopForm.AddField(fields, "", remove);
        GlassWorkshopForm.AddField(fields, "", new Label { AutoSize = true, Text = "Vínculo informativo incluído ao SALVAR PROJETO. Não altera preço do orçamento, estoque ou cadastro. Plano geométrico disponível abaixo. Fabricação e estoque de sobras continuam para próxima etapa, com regras profissionais validadas." });
    }
    private void Search()
    {
        try
        {
            var items = new List<GlassMaterialLink>();
            using var connection = Database.Open(); using var command = connection.CreateCommand();
            command.CommandText = "SELECT id,name,COALESCE(barcode,''),COALESCE(category,''),price FROM products WHERE id>0 AND active=1 AND (instr(lower(name),lower($q))>0 OR instr(lower(COALESCE(barcode,'')),lower($q))>0) ORDER BY name,id LIMIT 100";
            command.Parameters.AddWithValue("$q", query.Text.Trim());
            using var reader = command.ExecuteReader();
            while (reader.Read()) items.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4)));
            results.DataSource = new BindingList<GlassMaterialLink>(items);
        }
        catch (Exception) { MessageBox.Show(query.FindForm(), "Não foi possível consultar os produtos. Nenhum dado foi alterado.", "Materiais"); }
    }
    private static DataGridView Grid(string name)
    {
        var grid = new DataGridView { Name = name, AutoGenerateColumns = false, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, Height = 170, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        foreach (var column in new[] { ("ProductId", "ID"), ("Name", "Material"), ("Barcode", "Código"), ("Category", "Categoria"), ("Price", "Preço cadastrado") })
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = column.Item1, Name = column.Item1, HeaderText = column.Item2 });
        return grid;
    }
    internal void RestoreSaved(IReadOnlyList<GlassMaterialLink> saved)
    { links.Clear(); foreach(var item in saved) links.Add(item); }
    public void Dispose() { links.Clear(); }
}
