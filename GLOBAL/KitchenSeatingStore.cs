using LealInfoPDV.Network;
using Microsoft.Data.Sqlite;
namespace LealInfoPDV;

internal sealed record KitchenReservation(int Number, bool Reserved, long Revision, string Note, string Operator);
internal sealed record KitchenSeatingSnapshot(int Count, long Revision, KitchenReservation[] Reservations);

internal sealed partial class KitchenStore
{
    internal string? LastSeatingBackup { get; private set; }
    internal void EnsureSeatingReady()
    {
        lock (schemaGate)
        {
            using var db = open();
            _ = Identity(db);
            using var present = Command(db, null, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('kitchen_tables_v2','kitchen_table_settings_v2')");
            int count = Convert.ToInt32(present.ExecuteScalar());
            if (count == 2) { _ = ReadSeating(db, null); return; }
            if (count != 0) throw new InvalidDataException("Estrutura de mesas incompleta. Dados preservados; restaure o backup validado.");
            if (!canMigrate) throw new InvalidOperationException("Abra a cozinha no servidor para preparar as reservas com backup.");
            using (var check = Command(db, null, "PRAGMA quick_check"))
                if (Convert.ToString(check.ExecuteScalar()) != "ok") throw new InvalidDataException("Integridade não confirmada; mesas não preparadas.");
            Directory.CreateDirectory(Database.BackupFolder);
            string backup = Path.Combine(Database.BackupFolder, "ANTES_MESAS_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N") + ".db");
            using (var copy = new SqliteConnection("Data Source=" + backup + ";Pooling=False"))
            {
                copy.Open(); db.BackupDatabase(copy);
                using var check = copy.CreateCommand(); check.CommandText = "PRAGMA quick_check";
                if (Convert.ToString(check.ExecuteScalar()) != "ok") throw new InvalidDataException("Backup de mesas não validado.");
            }
            LastSeatingBackup = backup;
            using var tx = db.BeginTransaction();
            int initialCount = Math.Max(40, OccupiedTables(db, tx).DefaultIfEmpty().Max());
            using var create = Command(db, tx, """
                CREATE TABLE kitchen_table_settings_v2(id INTEGER PRIMARY KEY CHECK(id=1),table_count INTEGER NOT NULL CHECK(table_count BETWEEN 10 AND 1000),revision INTEGER NOT NULL);
                CREATE TABLE kitchen_tables_v2(number INTEGER PRIMARY KEY CHECK(number BETWEEN 1 AND 1000),reserved INTEGER NOT NULL CHECK(reserved IN (0,1)),revision INTEGER NOT NULL,note TEXT NOT NULL,operator TEXT NOT NULL,updated_utc TEXT NOT NULL);
                INSERT INTO kitchen_table_settings_v2(id,table_count,revision) VALUES(1,$initial,1);
                """, ("$initial", initialCount));
            create.ExecuteNonQuery(); tx.Commit();
        }
    }
    private static KitchenSeatingSnapshot ReadSeating(PdvConnection db, PdvTransaction? tx)
    {
        int count; long revision;
        using (var command = Command(db, tx, "SELECT table_count,revision FROM kitchen_table_settings_v2 WHERE id=1"))
        using (var r = command.ExecuteReader())
        {
            if (!r.Read()) throw new InvalidDataException("Configuração de mesas ausente.");
            count = r.GetInt32(0); revision = r.GetInt64(1);
            if (count < 10 || count > 1000 || revision < 1) throw new InvalidDataException("Configuração de mesas inválida.");
        }
        var reservations = new List<KitchenReservation>();
        using (var command = Command(db, tx, "SELECT number,reserved,revision,note,operator FROM kitchen_tables_v2 ORDER BY number"))
        using (var r = command.ExecuteReader())
            while (r.Read()) reservations.Add(new(r.GetInt32(0), r.GetInt64(1) == 1, r.GetInt64(2), r.GetString(3), r.GetString(4)));
        return new(count, revision, reservations.ToArray());
    }
    internal KitchenSeatingSnapshot Seating() { using var db = open(); return ReadSeating(db, null); }
    private static int[] OccupiedTables(PdvConnection db, PdvTransaction tx)
    {
        using var command = Command(db, tx, "SELECT DISTINCT o.reference FROM kitchen_orders_v1 o LEFT JOIN kitchen_paid_orders_v1 p ON p.order_id=o.id WHERE o.status!='CANCELADO' AND p.order_id IS NULL");
        using var r = command.ExecuteReader(); var tables = new List<int>();
        while (r.Read()) if (KitchenTablesView.TableNumber(r.GetString(0)) is int number) tables.Add(number);
        return tables.ToArray();
    }
    internal KitchenSeatingSnapshot ChangeTableCount(int count, long expectedRevision)
    {
        if (count < 10 || count > 1000) throw new InvalidDataException("Escolha entre 10 e 1000 mesas.");
        using var db = open(); using var tx = db.BeginTransaction();
        var prior = ReadSeating(db, tx);
        int highest = OccupiedTables(db, tx).Concat(prior.Reservations.Where(r => r.Reserved).Select(r => r.Number)).DefaultIfEmpty().Max();
        if (count < highest) throw new InvalidOperationException("Há pedido ou reserva em uma mesa acima da quantidade escolhida.");
        using var change = Command(db, tx, "UPDATE kitchen_table_settings_v2 SET table_count=$count,revision=revision+1 WHERE id=1 AND revision=$revision", ("$count", count), ("$revision", expectedRevision));
        if (change.ExecuteNonQuery() != 1) throw new InvalidOperationException("As mesas mudaram em outro terminal. Atualize e tente novamente.");
        var result = ReadSeating(db, tx); tx.Commit(); return result;
    }
    internal KitchenSeatingSnapshot ReserveTable(int number, long expectedRevision, bool reserved, string note, string actor)
    {
        if (number < 1 || number > 1000 || note.Length > 160 || string.IsNullOrWhiteSpace(actor) || actor.Length > 160) throw new InvalidDataException("Reserva inválida.");
        using var db = open(); using var tx = db.BeginTransaction();
        var prior = ReadSeating(db, tx);
        if (number > prior.Count) throw new InvalidOperationException("Mesa fora da quantidade cadastrada.");
        var existing = prior.Reservations.SingleOrDefault(r => r.Number == number);
        if ((existing?.Revision ?? 0) != expectedRevision) throw new InvalidOperationException("A reserva mudou em outro terminal. Atualize antes de continuar.");
        if (reserved && OccupiedTables(db, tx).Contains(number)) throw new InvalidOperationException("A mesa tem pedido ativo. Conclua o atendimento antes de reservar.");
        using var change = Command(db, tx, """
            INSERT INTO kitchen_tables_v2(number,reserved,revision,note,operator,updated_utc)
            VALUES($number,$reserved,1,$note,$actor,$now)
            ON CONFLICT(number) DO UPDATE SET reserved=excluded.reserved,revision=kitchen_tables_v2.revision+1,note=excluded.note,operator=excluded.operator,updated_utc=excluded.updated_utc
            """, ("$number", number), ("$reserved", reserved ? 1 : 0), ("$note", reserved ? note.Trim() : ""), ("$actor", actor), ("$now", Stamp(DateTimeOffset.UtcNow)));
        change.ExecuteNonQuery(); var result = ReadSeating(db, tx); tx.Commit(); return result;
    }
}
