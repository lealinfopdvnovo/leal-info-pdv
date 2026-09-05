using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace LealInfoPDV;

public static class Database
{
    public static string AppFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LealInfoPDV");

    public static string DbPath => Path.Combine(AppFolder, "lealinfo.db");
    public static string BackupFolder => Path.Combine(AppFolder, "Backups");
    public static string ConnectionString => $"Data Source={DbPath};Foreign Keys=True";

    public static void Initialize()
    {
        Directory.CreateDirectory(AppFolder);
        Directory.CreateDirectory(BackupFolder);

        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
        PRAGMA journal_mode=WAL;

        CREATE TABLE IF NOT EXISTS products(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            barcode TEXT,
            name TEXT NOT NULL,
            category TEXT,
            cost REAL NOT NULL DEFAULT 0,
            price REAL NOT NULL DEFAULT 0,
            stock REAL NOT NULL DEFAULT 0,
            min_stock REAL NOT NULL DEFAULT 0,
            active INTEGER NOT NULL DEFAULT 1,
            created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS customers(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            document TEXT,
            phone TEXT,
            email TEXT,
            address TEXT,
            created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS suppliers(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            document TEXT,
            phone TEXT,
            email TEXT,
            address TEXT,
            created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS services(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            price REAL NOT NULL DEFAULT 0,
            description TEXT,
            created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS sales(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            sold_at TEXT NOT NULL,
            customer_id INTEGER,
            payment TEXT NOT NULL,
            subtotal REAL NOT NULL,
            discount REAL NOT NULL DEFAULT 0,
            total REAL NOT NULL,
            operator TEXT NOT NULL DEFAULT 'ADMIN',
            FOREIGN KEY(customer_id) REFERENCES customers(id)
        );

        CREATE TABLE IF NOT EXISTS sale_items(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            sale_id INTEGER NOT NULL,
            product_id INTEGER NOT NULL,
            description TEXT NOT NULL,
            qty REAL NOT NULL,
            unit_price REAL NOT NULL,
            total REAL NOT NULL,
            FOREIGN KEY(sale_id) REFERENCES sales(id) ON DELETE CASCADE,
            FOREIGN KEY(product_id) REFERENCES products(id)
        );

        CREATE TABLE IF NOT EXISTS cash_movements(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            occurred_at TEXT NOT NULL,
            type TEXT NOT NULL,
            description TEXT NOT NULL,
            amount REAL NOT NULL,
            sale_id INTEGER
        );

        CREATE TABLE IF NOT EXISTS sale_payments(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            sale_id INTEGER NOT NULL,
            method TEXT NOT NULL,
            amount REAL NOT NULL,
            FOREIGN KEY(sale_id) REFERENCES sales(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS service_orders(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            opened_at TEXT NOT NULL,
            customer_id INTEGER,
            customer_name TEXT NOT NULL,
            equipment TEXT,
            defect TEXT,
            service_done TEXT,
            status TEXT NOT NULL DEFAULT 'ABERTA',
            amount REAL NOT NULL DEFAULT 0,
            notes TEXT,
            FOREIGN KEY(customer_id) REFERENCES customers(id)
        );

        CREATE TABLE IF NOT EXISTS quotes(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            created_at TEXT NOT NULL,
            customer_id INTEGER,
            customer_name TEXT NOT NULL,
            description TEXT NOT NULL,
            amount REAL NOT NULL DEFAULT 0,
            status TEXT NOT NULL DEFAULT 'PENDENTE',
            FOREIGN KEY(customer_id) REFERENCES customers(id)
        );

        CREATE TABLE IF NOT EXISTS users(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            full_name TEXT NOT NULL,
            username TEXT NOT NULL UNIQUE COLLATE NOCASE,
            password_hash TEXT NOT NULL,
            password_salt TEXT NOT NULL,
            role TEXT NOT NULL DEFAULT 'OPERADOR',
            email TEXT,
            phone TEXT,
            active INTEGER NOT NULL DEFAULT 1,
            can_discount INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS password_reset_codes(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER NOT NULL,
            code_hash TEXT NOT NULL,
            expires_at TEXT NOT NULL,
            used INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS emergency_recovery_codes(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER NOT NULL,
            code_hash TEXT NOT NULL,
            used INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS settings(
            key TEXT PRIMARY KEY,
            value TEXT
        );

        INSERT OR IGNORE INTO settings(key,value) VALUES('company','LEAL INFO CONECTADO');
        INSERT OR IGNORE INTO settings(key,value) VALUES('operator','ADMIN');
        INSERT OR IGNORE INTO settings(key,value) VALUES('company_registered','0');
        INSERT OR IGNORE INTO settings(key,value) VALUES('company_name','');
        INSERT OR IGNORE INTO settings(key,value) VALUES('company_trade_name','');
        INSERT OR IGNORE INTO settings(key,value) VALUES('company_document','');
        INSERT OR IGNORE INTO settings(key,value) VALUES('company_phone','');
        INSERT OR IGNORE INTO settings(key,value) VALUES('company_address','');
        INSERT OR IGNORE INTO settings(key,value) VALUES('company_city_state','');
        INSERT OR IGNORE INTO settings(key,value) VALUES('company_footer','Obrigado pela preferência!');
        INSERT OR IGNORE INTO settings(key,value) VALUES('smtp_host','');
        INSERT OR IGNORE INTO settings(key,value) VALUES('smtp_port','587');
        INSERT OR IGNORE INTO settings(key,value) VALUES('smtp_user','');
        INSERT OR IGNORE INTO settings(key,value) VALUES('smtp_password','');
        INSERT OR IGNORE INTO settings(key,value) VALUES('smtp_ssl','1');
        INSERT OR IGNORE INTO settings(key,value) VALUES('smtp_from_name','LEAL INFO PDV');
        INSERT OR IGNORE INTO settings(key,value) VALUES('security_setup_completed','0');
        """;
        cmd.ExecuteNonQuery();

        // Migração compatível com bancos já existentes.
        try
        {
            using var alter = cn.CreateCommand();
            alter.CommandText = "ALTER TABLE products ADD COLUMN photo_path TEXT";
            alter.ExecuteNonQuery();
        }
        catch
        {
            // Coluna já existe.
        }
    }

    public static SqliteConnection Open()
    {
        var cn = new SqliteConnection(ConnectionString);
        cn.Open();
        return cn;
    }

    public static string DeviceSerial()
    {
        string source;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            source = key?.GetValue("MachineGuid")?.ToString() ?? Environment.MachineName;
        }
        catch { source = Environment.MachineName; }

        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes("LEALINFO|" + source)));
        return $"LI-{hash[..4]}-{hash[4..8]}-{hash[8..12]}-{hash[12..16]}";
    }

    public static void Backup()
    {
        Directory.CreateDirectory(BackupFolder);
        var name = $"LEAL_INFO_PDV_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        File.Copy(DbPath, Path.Combine(BackupFolder, name), true);
    }
}
