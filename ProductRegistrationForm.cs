using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace LealInfoPDV;

internal sealed class ProductRegistrationForm : Form
{
    private readonly long? _initialId;
    private long? _id;
    private string? _photoPath;
    private readonly Dictionary<string, Control> _fields = new();
    private readonly PictureBox _photo = new();
    private readonly Label _profit = new();
    private readonly Label _status = new();
    private readonly CheckBox _searchInternet = new() { Text = "Buscar produto na Internet" };
    private readonly CheckBox _searchPhoto = new() { Text = "Buscar foto na Internet" };

    public static void Show(IWin32Window owner, long? productId)
    {
        EnsureSchema();
        using var form = new ProductRegistrationForm(productId);
        form.ShowDialog(owner);
    }

    private ProductRegistrationForm(long? productId)
    {
        _initialId = productId;
        _id = productId;
        Text = "Cadastro de Produtos • LEAL INFO PDV";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 720);
        BackColor = Color.FromArgb(3, 20, 42);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;
        BuildUi();
        LoadCombos();
        if (_id.HasValue) LoadProduct(_id.Value); else NewProduct();
        KeyDown += HandleShortcut;
    }

    private static void EnsureSchema()
    {
        using var cn = Database.Open();
        string[] columns =
        {
            "supplier_id INTEGER", "unit TEXT NOT NULL DEFAULT 'UNID'", "size TEXT", "brand TEXT",
            "product_group TEXT", "subgroup TEXT", "wholesale_price REAL NOT NULL DEFAULT 0",
            "wholesale_qty REAL NOT NULL DEFAULT 0", "promotion_price REAL NOT NULL DEFAULT 0",
            "promotion_start TEXT", "promotion_end TEXT", "commission_percent REAL NOT NULL DEFAULT 0",
            "approx_price REAL NOT NULL DEFAULT 0", "track_stock INTEGER NOT NULL DEFAULT 1",
            "use_scale INTEGER NOT NULL DEFAULT 0", "fractional_sale INTEGER NOT NULL DEFAULT 0",
            "individual_sale INTEGER NOT NULL DEFAULT 1", "sell_in_grid INTEGER NOT NULL DEFAULT 0",
            "kitchen_preparation INTEGER NOT NULL DEFAULT 0", "composition TEXT", "location TEXT",
            "notes TEXT", "expiry_date TEXT", "updated_at TEXT"
        };
        foreach (var definition in columns)
        {
            try
            {
                using var cmd = cn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE products ADD COLUMN {definition}";
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)) { }
        }

        using var movement = cn.CreateCommand();
        movement.CommandText = """
            CREATE TABLE IF NOT EXISTS product_stock_movements(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                product_id INTEGER NOT NULL,
                occurred_at TEXT NOT NULL,
                quantity REAL NOT NULL,
                reason TEXT NOT NULL,
                operator TEXT NOT NULL,
                FOREIGN KEY(product_id) REFERENCES products(id)
            );
            """;
        movement.ExecuteNonQuery();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        Controls.Add(root);

        var title = new Label { Text = "CADASTRO DE PRODUTOS", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 25, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(5, 74, 132) };
        root.Controls.Add(title, 0, 0);

        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 45, 82), Padding = new Padding(12, 7, 0, 0) };
        StyleCheck(_searchInternet); StyleCheck(_searchPhoto);
        options.Controls.AddRange(new Control[] { _searchInternet, _searchPhoto, _status });
        _status.AutoSize = true; _status.ForeColor = Color.FromArgb(100, 220, 255); _status.Padding = new Padding(24, 3, 0, 0);
        root.Controls.Add(options, 0, 1);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.FromArgb(5, 38, 72), Padding = new Padding(12) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        root.Controls.Add(content, 0, 2);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(7, 52, 92), Padding = new Padding(15) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, BackColor = Color.Transparent };
        for (int i = 0; i < 4; i++) fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        scroll.Controls.Add(fields); content.Controls.Add(scroll, 0, 0);

        int row = 0;
        AddField(fields, "barcode", "Código", new TextBox(), row, 0);
        AddField(fields, "name", "Descrição", new TextBox(), row, 1, 3); row++;
        AddField(fields, "supplier", "Fornecedor", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, row, 0, 2);
        AddField(fields, "unit", "Unidade", Combo("UNID", "KG", "LT", "MT", "CX", "PCT", "SERV"), row, 2);
        AddField(fields, "size", "Tamanho", new TextBox(), row, 3); row++;
        AddField(fields, "cost", "Preço de compra", MoneyBox(), row, 0);
        AddField(fields, "price", "Preço de venda", MoneyBox(), row, 1);
        AddField(fields, "wholesale_price", "Preço atacado", MoneyBox(), row, 2);
        AddField(fields, "wholesale_qty", "Qtd. atacado", NumberBox(), row, 3); row++;
        AddField(fields, "approx_price", "Preço aproximado", MoneyBox(), row, 0);
        AddField(fields, "commission", "Comissão (%)", MoneyBox(), row, 1);
        AddField(fields, "promotion_price", "Preço promoção", MoneyBox(), row, 2);
        AddField(fields, "promotion_start", "Início promoção", DateBox(), row, 3); row++;
        AddField(fields, "promotion_end", "Fim promoção", DateBox(), row, 0);
        AddField(fields, "stock", "Estoque atual", NumberBox(), row, 1);
        AddField(fields, "min_stock", "Estoque mínimo", NumberBox(), row, 2);
        AddField(fields, "expiry", "Validade", DateBox(true), row, 3); row++;
        AddField(fields, "brand", "Marca", EditableCombo(), row, 0);
        AddField(fields, "category", "Categoria", EditableCombo(), row, 1);
        AddField(fields, "group", "Grupo", EditableCombo(), row, 2);
        AddField(fields, "subgroup", "Subgrupo", EditableCombo(), row, 3); row++;
        AddField(fields, "composition", "Composição / ingredientes", new TextBox(), row, 0, 2);
        AddField(fields, "location", "Localização", new TextBox(), row, 2);
        AddField(fields, "notes", "Observação", new TextBox(), row, 3); row++;

        var checks = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(2, 8, 2, 5) };
        foreach (var item in new[] { ("track_stock", "Controlar estoque"), ("use_scale", "Usar balança"), ("fractional", "Venda fracionada"), ("individual", "Venda individual"), ("grid", "Inserir/editar em grades"), ("kitchen", "Preparar na cozinha") })
        {
            var cb = new CheckBox { Text = item.Item2, AutoSize = true, ForeColor = Color.White, Margin = new Padding(10, 5, 12, 5) };
            _fields[item.Item1] = cb; checks.Controls.Add(cb);
        }
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); fields.Controls.Add(checks, 0, row); fields.SetColumnSpan(checks, 4); row++;
        _profit.Text = "LUCRO: R$ 0,00  •  MARGEM: 0,00%"; _profit.Dock = DockStyle.Fill; _profit.Font = new Font("Segoe UI", 12, FontStyle.Bold); _profit.ForeColor = Color.FromArgb(80, 235, 255); _profit.TextAlign = ContentAlignment.MiddleCenter;
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); fields.Controls.Add(_profit, 0, row); fields.SetColumnSpan(_profit, 4);

        var photoPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, BackColor = Color.FromArgb(7, 52, 92), Padding = new Padding(14) };
        photoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); photoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); photoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); photoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); photoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        photoPanel.Controls.Add(new Label { Text = "FOTO DO PRODUTO", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
        _photo.Dock = DockStyle.Fill; _photo.BackColor = Color.White; _photo.SizeMode = PictureBoxSizeMode.Zoom; _photo.Margin = new Padding(8); photoPanel.Controls.Add(_photo, 0, 1);
        photoPanel.Controls.Add(ActionButton("BUSCAR FOTO NO COMPUTADOR", (_, _) => ChoosePhoto()), 0, 2);
        photoPanel.Controls.Add(ActionButton("BUSCAR FOTO NA INTERNET", (_, _) => SearchPhotoOnline()), 0, 3);
        photoPanel.Controls.Add(ActionButton("REMOVER FOTO", (_, _) => { _photoPath = null; LoadPhoto(); }), 0, 4);
        content.Controls.Add(photoPanel, 1, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 31, 60), Padding = new Padding(10), WrapContents = false };
        actions.Controls.AddRange(new Control[]
        {
            BottomButton("NOVO • F2", NewProduct, Color.FromArgb(0,145,210)), BottomButton("SALVAR • F3", SaveProduct, Color.FromArgb(0,170,120)),
            BottomButton("EDITAR • F4", SelectFromList, Color.FromArgb(25,105,180)), BottomButton("EXCLUIR • F5", DeleteProduct, Color.FromArgb(195,48,55)),
            BottomButton("LISTA • F6", SelectFromList, Color.FromArgb(25,105,180)), BottomButton("ESTOQUE • F7", AdjustStock, Color.FromArgb(0,125,180)),
            BottomButton("VALIDADE • F9", ShowExpiry, Color.FromArgb(225,125,20)), BottomButton("CLONAR", CloneProduct, Color.FromArgb(90,75,180)),
            BottomButton("EDIÇÃO EM MASSA", MassEdit, Color.FromArgb(0,120,145))
        });
        root.Controls.Add(actions, 0, 3);

        ((TextBox)_fields["barcode"]).KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && _searchInternet.Checked) { e.SuppressKeyPress = true; await LookupOnlineAsync(); } };
        ((TextBox)_fields["cost"]).TextChanged += (_, _) => UpdateProfit();
        ((TextBox)_fields["price"]).TextChanged += (_, _) => UpdateProfit();
    }

    private void AddField(TableLayoutPanel panel, string key, string label, Control input, int row, int col, int span = 1)
    {
        while (panel.RowCount <= row * 2 + 1) { panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); panel.RowCount += 2; }
        var lbl = new Label { Text = label, Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
        input.Dock = DockStyle.Fill; input.Margin = new Padding(3, 2, 8, 5); input.Font = new Font("Segoe UI", 10.5f); input.BackColor = Color.FromArgb(241, 248, 255); input.ForeColor = Color.FromArgb(4, 38, 72);
        panel.Controls.Add(lbl, col, row * 2); panel.Controls.Add(input, col, row * 2 + 1); panel.SetColumnSpan(lbl, span); panel.SetColumnSpan(input, span); _fields[key] = input;
    }

    private static TextBox MoneyBox() => new() { Text = "0,00", TextAlign = HorizontalAlignment.Right };
    private static TextBox NumberBox() => new() { Text = "0", TextAlign = HorizontalAlignment.Right };
    private static ComboBox Combo(params string[] values) { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(values); c.SelectedIndex = 0; return c; }
    private static ComboBox EditableCombo() => new() { DropDownStyle = ComboBoxStyle.DropDown };
    private static DateTimePicker DateBox(bool optional = false) => new() { Format = DateTimePickerFormat.Short, ShowCheckBox = optional || true, Checked = false };
    private static void StyleCheck(CheckBox cb) { cb.AutoSize = true; cb.ForeColor = Color.White; cb.Font = new Font("Segoe UI", 9, FontStyle.Bold); cb.Margin = new Padding(8, 0, 18, 0); }
    private Button ActionButton(string text, EventHandler action) { var b = new Button { Text = text, Dock = DockStyle.Fill, Margin = new Padding(8, 4, 8, 4), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 125, 190), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) }; b.FlatAppearance.BorderSize = 0; b.Click += action; return b; }
    private Button BottomButton(string text, Action action, Color color) { var b = new Button { Text = text, Width = text.Length > 12 ? 150 : 118, Height = 52, Margin = new Padding(4), FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = Color.White, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }; b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => action(); return b; }

    private void LoadCombos()
    {
        using var cn = Database.Open();
        var supplier = (ComboBox)_fields["supplier"]; supplier.Items.Clear(); supplier.Items.Add(new ComboItem(0, "SEM FORNECEDOR"));
        using (var cmd = cn.CreateCommand()) { cmd.CommandText = "SELECT id,name FROM suppliers ORDER BY name"; using var rd = cmd.ExecuteReader(); while (rd.Read()) supplier.Items.Add(new ComboItem(rd.GetInt64(0), rd.GetString(1))); }
        supplier.SelectedIndex = 0;
        foreach (var pair in new[] { ("brand", "brand"), ("category", "category"), ("group", "product_group"), ("subgroup", "subgroup") })
        {
            var combo = (ComboBox)_fields[pair.Item1]; using var cmd = cn.CreateCommand(); cmd.CommandText = $"SELECT DISTINCT COALESCE({pair.Item2},'') FROM products WHERE TRIM(COALESCE({pair.Item2},''))<>'' ORDER BY 1"; using var rd = cmd.ExecuteReader(); while (rd.Read()) combo.Items.Add(rd.GetString(0));
        }
    }

    private void NewProduct()
    {
        _id = null; _photoPath = null;
        foreach (var c in _fields.Values) { if (c is TextBox t) t.Text = t == _fields["unit"] ? "UNID" : ""; else if (c is CheckBox cb) cb.Checked = false; else if (c is DateTimePicker dt) dt.Checked = false; }
        Set("cost", "0,00"); Set("price", "0,00"); Set("wholesale_price", "0,00"); Set("wholesale_qty", "0"); Set("approx_price", "0,00"); Set("commission", "0,00"); Set("promotion_price", "0,00"); Set("stock", "0"); Set("min_stock", "0");
        ((ComboBox)_fields["unit"]).SelectedIndex = 0; ((ComboBox)_fields["supplier"]).SelectedIndex = 0;
        ((CheckBox)_fields["track_stock"]).Checked = true; ((CheckBox)_fields["individual"]).Checked = true;
        LoadPhoto(); _status.Text = "Novo produto"; _fields["barcode"].Focus(); UpdateProfit();
    }

    private void LoadProduct(long id)
    {
        using var cn = Database.Open(); using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(barcode,''),name,COALESCE(category,''),cost,price,stock,min_stock,COALESCE(photo_path,''),
            COALESCE(supplier_id,0),COALESCE(unit,'UNID'),COALESCE(size,''),COALESCE(brand,''),COALESCE(product_group,''),COALESCE(subgroup,''),
            wholesale_price,wholesale_qty,promotion_price,COALESCE(promotion_start,''),COALESCE(promotion_end,''),commission_percent,approx_price,
            track_stock,use_scale,fractional_sale,individual_sale,sell_in_grid,kitchen_preparation,COALESCE(composition,''),COALESCE(location,''),COALESCE(notes,''),COALESCE(expiry_date,'')
            FROM products WHERE id=$id AND active=1
            """; cmd.Parameters.AddWithValue("$id", id); using var r = cmd.ExecuteReader(); if (!r.Read()) return;
        _id = id; Set("barcode", r.GetString(0)); Set("name", r.GetString(1)); Set("category", r.GetString(2)); Set("cost", F(r.GetDouble(3))); Set("price", F(r.GetDouble(4))); Set("stock", Q(r.GetDouble(5))); Set("min_stock", Q(r.GetDouble(6))); _photoPath = r.GetString(7);
        SelectSupplier(r.GetInt64(8)); SetCombo("unit", r.GetString(9)); Set("size", r.GetString(10)); Set("brand", r.GetString(11)); Set("group", r.GetString(12)); Set("subgroup", r.GetString(13)); Set("wholesale_price", F(r.GetDouble(14))); Set("wholesale_qty", Q(r.GetDouble(15))); Set("promotion_price", F(r.GetDouble(16))); SetDate("promotion_start", r.GetString(17)); SetDate("promotion_end", r.GetString(18)); Set("commission", F(r.GetDouble(19))); Set("approx_price", F(r.GetDouble(20)));
        SetCheck("track_stock", r.GetInt32(21)); SetCheck("use_scale", r.GetInt32(22)); SetCheck("fractional", r.GetInt32(23)); SetCheck("individual", r.GetInt32(24)); SetCheck("grid", r.GetInt32(25)); SetCheck("kitchen", r.GetInt32(26)); Set("composition", r.GetString(27)); Set("location", r.GetString(28)); Set("notes", r.GetString(29)); SetDate("expiry", r.GetString(30));
        LoadPhoto(); UpdateProfit(); _status.Text = $"Editando produto #{id}";
    }

    private void SaveProduct()
    {
        if (string.IsNullOrWhiteSpace(TextOf("name"))) { MessageBox.Show(this, "Informe a descrição do produto."); _fields["name"].Focus(); return; }
        var code = TextOf("barcode");
        using var cn = Database.Open();
        using (var duplicate = cn.CreateCommand()) { duplicate.CommandText = "SELECT COUNT(*) FROM products WHERE barcode=$b AND active=1 AND id<>COALESCE($id,-1)"; duplicate.Parameters.AddWithValue("$b", code); duplicate.Parameters.AddWithValue("$id", (object?)_id ?? DBNull.Value); if (!string.IsNullOrWhiteSpace(code) && Convert.ToInt32(duplicate.ExecuteScalar()) > 0) { MessageBox.Show(this, "Já existe um produto ativo com esse código."); return; } }
        _photoPath = CopyPhoto(_photoPath, _id);
        using var cmd = cn.CreateCommand();
        string columns = "barcode=$barcode,name=$name,category=$category,cost=$cost,price=$price,stock=$stock,min_stock=$min_stock,photo_path=$photo,supplier_id=$supplier,unit=$unit,size=$size,brand=$brand,product_group=$group,subgroup=$subgroup,wholesale_price=$wholesale_price,wholesale_qty=$wholesale_qty,promotion_price=$promotion_price,promotion_start=$promotion_start,promotion_end=$promotion_end,commission_percent=$commission,approx_price=$approx_price,track_stock=$track_stock,use_scale=$use_scale,fractional_sale=$fractional,individual_sale=$individual,sell_in_grid=$grid,kitchen_preparation=$kitchen,composition=$composition,location=$location,notes=$notes,expiry_date=$expiry,updated_at=$updated";
        cmd.CommandText = _id.HasValue ? $"UPDATE products SET {columns} WHERE id=$id" : $"INSERT INTO products({string.Join(',', columns.Split(',').Select(x => x.Split('=')[0]))}) VALUES({string.Join(',', columns.Split(',').Select(x => x.Split('=')[1]))}); SELECT last_insert_rowid();";
        Add(cmd, "$id", _id); Add(cmd, "$barcode", code); Add(cmd, "$name", TextOf("name")); Add(cmd, "$category", TextOf("category")); Add(cmd, "$cost", Num("cost")); Add(cmd, "$price", Num("price")); Add(cmd, "$stock", Num("stock")); Add(cmd, "$min_stock", Num("min_stock")); Add(cmd, "$photo", _photoPath); Add(cmd, "$supplier", SupplierId() == 0 ? null : SupplierId()); Add(cmd, "$unit", TextOf("unit")); Add(cmd, "$size", TextOf("size")); Add(cmd, "$brand", TextOf("brand")); Add(cmd, "$group", TextOf("group")); Add(cmd, "$subgroup", TextOf("subgroup")); Add(cmd, "$wholesale_price", Num("wholesale_price")); Add(cmd, "$wholesale_qty", Num("wholesale_qty")); Add(cmd, "$promotion_price", Num("promotion_price")); Add(cmd, "$promotion_start", DateValue("promotion_start")); Add(cmd, "$promotion_end", DateValue("promotion_end")); Add(cmd, "$commission", Num("commission")); Add(cmd, "$approx_price", Num("approx_price")); Add(cmd, "$track_stock", Bool("track_stock")); Add(cmd, "$use_scale", Bool("use_scale")); Add(cmd, "$fractional", Bool("fractional")); Add(cmd, "$individual", Bool("individual")); Add(cmd, "$grid", Bool("grid")); Add(cmd, "$kitchen", Bool("kitchen")); Add(cmd, "$composition", TextOf("composition")); Add(cmd, "$location", TextOf("location")); Add(cmd, "$notes", TextOf("notes")); Add(cmd, "$expiry", DateValue("expiry")); Add(cmd, "$updated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        var result = cmd.ExecuteScalar(); if (!_id.HasValue) _id = Convert.ToInt64(result); _status.Text = $"Produto #{_id} salvo com sucesso"; MessageBox.Show(this, "Produto salvo com sucesso.", "LEAL INFO PDV", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task LookupOnlineAsync()
    {
        var code = new string(TextOf("barcode").Where(char.IsDigit).ToArray()); if (code.Length < 8) return;
        try { _status.Text = "Consultando código na Internet..."; using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) }; http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV/10.312"); var json = await http.GetFromJsonAsync<JsonElement>($"https://world.openfoodfacts.org/api/v2/product/{code}.json"); if (json.TryGetProperty("product", out var p)) { if (p.TryGetProperty("product_name", out var n) && string.IsNullOrWhiteSpace(TextOf("name"))) Set("name", n.GetString() ?? ""); if (p.TryGetProperty("brands", out var b)) Set("brand", b.GetString() ?? ""); if (p.TryGetProperty("categories", out var c) && string.IsNullOrWhiteSpace(TextOf("category"))) Set("category", (c.GetString() ?? "").Split(',')[0]); if (_searchPhoto.Checked && p.TryGetProperty("image_front_url", out var image) && Uri.TryCreate(image.GetString(), UriKind.Absolute, out var uri)) { var dir = Path.Combine(Database.AppFolder, "ProductImages"); Directory.CreateDirectory(dir); var path = Path.Combine(dir, $"ean_{code}.jpg"); await File.WriteAllBytesAsync(path, await http.GetByteArrayAsync(uri)); _photoPath = path; LoadPhoto(); } _status.Text = "Produto localizado na Internet"; } else _status.Text = "Código não localizado; continue o cadastro manual"; }
        catch (Exception ex) { _status.Text = "Internet indisponível; cadastro manual liberado"; Debug.WriteLine(ex); }
    }

    private void SelectFromList()
    {
        using var f = new Form { Text = "Lista de Produtos", StartPosition = FormStartPosition.CenterParent, Width = 1000, Height = 650, BackColor = Color.FromArgb(5, 38, 72) }; var search = new TextBox { Dock = DockStyle.Top, Height = 38, PlaceholderText = "Pesquisar código, descrição, marca ou categoria..." }; var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White }; f.Controls.Add(grid); f.Controls.Add(search);
        void Load(string term) { using var cn = Database.Open(); using var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT id AS ID,barcode AS Código,name AS Descrição,brand AS Marca,category AS Categoria,price AS Preço,stock AS Estoque FROM products WHERE active=1 AND id>0 AND (barcode LIKE $q OR name LIKE $q OR brand LIKE $q OR category LIKE $q) ORDER BY name"; cmd.Parameters.AddWithValue("$q", $"%{term}%"); using var rd = cmd.ExecuteReader(); var table = new System.Data.DataTable(); table.Load(rd); grid.DataSource = table; }
        search.TextChanged += (_, _) => Load(search.Text); grid.CellDoubleClick += (_, _) => { if (grid.CurrentRow != null) { var id = Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value); f.Close(); LoadProduct(id); } }; Load(""); f.ShowDialog(this);
    }

    private void DeleteProduct() { if (!_id.HasValue) return; if (MessageBox.Show(this, "Deseja desativar este produto?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; Exec("UPDATE products SET active=0 WHERE id=$id", ("$id", _id.Value)); NewProduct(); }
    private void CloneProduct() { if (!_id.HasValue) { MessageBox.Show(this, "Selecione um produto para clonar."); return; } var old = TextOf("name"); _id = null; Set("barcode", ""); Set("name", old + " - CÓPIA"); _status.Text = "Cópia criada; revise e clique em SALVAR"; }
    private void AdjustStock() { if (!_id.HasValue) { MessageBox.Show(this, "Selecione ou salve o produto primeiro."); return; } using var f = SmallDialog("Ajuste de Estoque", out var value, out var reason); if (f.ShowDialog(this) != DialogResult.OK) return; var qty = Parse(value.Text); using var cn = Database.Open(); using var tx = cn.BeginTransaction(); using var a = cn.CreateCommand(); a.Transaction = tx; a.CommandText = "UPDATE products SET stock=stock+$q WHERE id=$id"; a.Parameters.AddWithValue("$q", qty); a.Parameters.AddWithValue("$id", _id.Value); a.ExecuteNonQuery(); using var m = cn.CreateCommand(); m.Transaction = tx; m.CommandText = "INSERT INTO product_stock_movements(product_id,occurred_at,quantity,reason,operator) VALUES($id,$date,$q,$reason,$operator)"; m.Parameters.AddWithValue("$id", _id.Value); m.Parameters.AddWithValue("$date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); m.Parameters.AddWithValue("$q", qty); m.Parameters.AddWithValue("$reason", reason.Text); m.Parameters.AddWithValue("$operator", Auth.OperatorName); m.ExecuteNonQuery(); tx.Commit(); LoadProduct(_id.Value); }
    private void ShowExpiry() { using var cn = Database.Open(); using var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT name,expiry_date,stock FROM products WHERE active=1 AND expiry_date IS NOT NULL AND expiry_date<>'' ORDER BY expiry_date"; using var rd = cmd.ExecuteReader(); var lines = new List<string>(); while (rd.Read()) lines.Add($"{rd.GetString(1)}  •  {rd.GetString(0)}  •  Estoque {rd.GetDouble(2):N3}"); MessageBox.Show(this, lines.Count == 0 ? "Nenhum produto com validade cadastrada." : string.Join(Environment.NewLine, lines), "Controle de Validade"); }
    private void MassEdit() { using var f = SmallDialog("Edição em Massa • Reajuste de Preços", out var value, out var reason); reason.Text = "Categoria (vazio = todos)"; if (f.ShowDialog(this) != DialogResult.OK) return; var percent = Parse(value.Text); var category = reason.Text == "Categoria (vazio = todos)" ? "" : reason.Text.Trim(); using var cn = Database.Open(); using var cmd = cn.CreateCommand(); cmd.CommandText = "UPDATE products SET price=ROUND(price*(1+$p/100.0),2),updated_at=$date WHERE active=1 AND id>0 AND ($category='' OR category=$category)"; cmd.Parameters.AddWithValue("$p", percent); cmd.Parameters.AddWithValue("$date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); cmd.Parameters.AddWithValue("$category", category); var count = cmd.ExecuteNonQuery(); MessageBox.Show(this, $"{count} produto(s) atualizado(s)."); }

    private Form SmallDialog(string title, out TextBox value, out TextBox reason) { var f = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, Width = 480, Height = 245, BackColor = Color.FromArgb(7, 52, 92), ForeColor = Color.White, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false }; value = new TextBox { Left = 25, Top = 45, Width = 410, PlaceholderText = "Quantidade ou percentual (use negativo para saída)" }; reason = new TextBox { Left = 25, Top = 90, Width = 410, PlaceholderText = "Motivo" }; var ok = new Button { Text = "CONFIRMAR", Left = 285, Top = 140, Width = 150, Height = 40, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(0, 150, 205), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; f.Controls.AddRange(new Control[] { value, reason, ok }); f.AcceptButton = ok; return f; }
    private void ChoosePhoto() { using var d = new OpenFileDialog { Filter = "Imagens|*.jpg;*.jpeg;*.png;*.webp;*.bmp" }; if (d.ShowDialog(this) == DialogResult.OK) { _photoPath = d.FileName; LoadPhoto(); } }
    private void SearchPhotoOnline() { var term = string.IsNullOrWhiteSpace(TextOf("name")) ? TextOf("barcode") : TextOf("name"); Process.Start(new ProcessStartInfo("https://www.bing.com/images/search?q=" + Uri.EscapeDataString(term + " produto")) { UseShellExecute = true }); }
    private void LoadPhoto() { _photo.Image?.Dispose(); _photo.Image = null; var path = _photoPath; if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"); if (File.Exists(path)) { using var img = Image.FromFile(path); _photo.Image = new Bitmap(img); } }
    private static string? CopyPhoto(string? source, long? id) { if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return source; var dir = Path.Combine(Database.AppFolder, "ProductImages"); Directory.CreateDirectory(dir); var destination = Path.Combine(dir, $"produto_{id?.ToString() ?? Guid.NewGuid().ToString("N")}{Path.GetExtension(source).ToLowerInvariant()}"); if (!Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) File.Copy(source, destination, true); return destination; }
    private void UpdateProfit() { var cost = Num("cost"); var price = Num("price"); var gain = price - cost; var margin = cost > 0 ? gain / cost * 100 : (price > 0 ? 100 : 0); _profit.Text = $"LUCRO: {gain:C2}  •  MARGEM: {margin:N2}%"; }
    private void HandleShortcut(object? sender, KeyEventArgs e) { Action? action = e.KeyCode switch { Keys.F2 => NewProduct, Keys.F3 => SaveProduct, Keys.F4 => SelectFromList, Keys.F5 => DeleteProduct, Keys.F6 => SelectFromList, Keys.F7 => AdjustStock, Keys.F9 => ShowExpiry, _ => null }; if (action != null) { e.SuppressKeyPress = true; action(); } }
    private string TextOf(string key) => _fields[key].Text.Trim(); private void Set(string key, string value) => _fields[key].Text = value; private double Num(string key) => Parse(TextOf(key)); private int Bool(string key) => ((CheckBox)_fields[key]).Checked ? 1 : 0; private void SetCheck(string key, int value) => ((CheckBox)_fields[key]).Checked = value != 0;
    private static double Parse(string value) => double.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var n) ? n : double.TryParse(value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out n) ? n : 0;
    private static string F(double n) => n.ToString("N2", CultureInfo.GetCultureInfo("pt-BR")); private static string Q(double n) => n.ToString("N3", CultureInfo.GetCultureInfo("pt-BR"));
    private void SetCombo(string key, string value) { var c = (ComboBox)_fields[key]; var i = c.FindStringExact(value); if (i < 0) c.Items.Add(value); c.SelectedItem = value; }
    private void SelectSupplier(long id) { var c = (ComboBox)_fields["supplier"]; for (int i = 0; i < c.Items.Count; i++) if (((ComboItem)c.Items[i]).Id == id) { c.SelectedIndex = i; return; } c.SelectedIndex = 0; }
    private long SupplierId() => ((ComboBox)_fields["supplier"]).SelectedItem is ComboItem c ? c.Id : 0;
    private void SetDate(string key, string value) { var d = (DateTimePicker)_fields[key]; d.Checked = DateTime.TryParse(value, out var date); if (d.Checked) d.Value = date; }
    private object? DateValue(string key) { var d = (DateTimePicker)_fields[key]; return d.Checked ? d.Value.ToString("yyyy-MM-dd") : null; }
    private static void Add(SqliteCommand cmd, string name, object? value) => cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private static void Exec(string sql, params (string, object)[] args) { using var cn = Database.Open(); using var cmd = cn.CreateCommand(); cmd.CommandText = sql; foreach (var a in args) cmd.Parameters.AddWithValue(a.Item1, a.Item2); cmd.ExecuteNonQuery(); }
    private sealed record ComboItem(long Id, string Name) { public override string ToString() => Name; }
}
