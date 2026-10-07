using Microsoft.Data.Sqlite;

namespace Hive.Persistence;

internal static class EmbeddedSqliteDbType
{
    public static SqliteType UniqueIdentifier => SqliteType.Text;
    public static SqliteType NVarChar => SqliteType.Text;
    public static SqliteType BigInt => SqliteType.Integer;
    public static SqliteType Int => SqliteType.Integer;
    public static SqliteType DateTime2 => SqliteType.Text;
    public static SqliteType VarBinary => SqliteType.Blob;
}
