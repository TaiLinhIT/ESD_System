using ESD.Core;
using MySqlConnector;

namespace ESD.Data;

public sealed class MySqlEventRepository : IEventRepository
{
    private readonly string _connectionString;
    public MySqlEventRepository(string connectionString) => _connectionString = connectionString;

    public async Task InsertAsync(DbEvent item, CancellationToken ct)
    {
        await using var cn = new MySqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        const string sql = """
        INSERT INTO esd_events
        (event_time, device_name, employee_id, event_type, status, raw_data, message)
        VALUES (@time, @device, @employee, @type, @status, @raw, @message)
        """;

        await using var cmd = new MySqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@time", item.Timestamp);
        cmd.Parameters.AddWithValue("@device", item.Device);
        cmd.Parameters.AddWithValue("@employee", item.EmployeeId);
        cmd.Parameters.AddWithValue("@type", item.EventType);
        cmd.Parameters.AddWithValue("@status", item.Status);
        cmd.Parameters.AddWithValue("@raw", item.RawData);
        cmd.Parameters.AddWithValue("@message", item.Message);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
