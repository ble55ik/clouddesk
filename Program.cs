using System.Text;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=clouddesk;Username=clouddesk;Password=clouddesk";

var db = NpgsqlDataSource.Create(connString);

var app = builder.Build();

app.MapGet("/", () => Results.Json(new
{
    name = "CloudDesk API",
    routes = new[] { "/health", "/api/tickets", "/api/stats" }
}));

app.MapGet("/health", async () =>
{
    try
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync();
        return Results.Ok(new { status = "ok" });
    }
    catch
    {
        return Results.Json(new { status = "database unavailable" }, statusCode: 503);
    }
});

app.MapGet("/api/tickets", async (string? status, int? priority) =>
{
    if (status is not null && !StatusIsValid(status))
        return Results.BadRequest(new { error = "Статус должен быть одним из: new, in_progress, done" });
    if (priority is not null && (priority < 1 || priority > 3))
        return Results.BadRequest(new { error = "Приоритет может быть только 1, 2 или 3" });

    var sql = new StringBuilder("SELECT id, title, description, priority, status, created_at FROM tickets");
    var where = new List<string>();
    if (status is not null) where.Add("status = @status");
    if (priority is not null) where.Add("priority = @priority");
    if (where.Count > 0) sql.Append(" WHERE ").Append(string.Join(" AND ", where));
    sql.Append(" ORDER BY id");

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(sql.ToString(), conn);
    if (status is not null) cmd.Parameters.AddWithValue("status", status);
    if (priority is not null) cmd.Parameters.AddWithValue("priority", priority.Value);

    var result = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add(new
        {
            id = reader.GetInt32(0),
            title = reader.GetString(1),
            description = reader.GetString(2),
            priority = reader.GetInt32(3),
            status = reader.GetString(4),
            createdAt = reader.GetDateTime(5)
        });
    }

    return Results.Ok(result);
});

app.MapGet("/api/tickets/{id:int}", async (int id) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, title, description, priority, status, created_at FROM tickets WHERE id = @id", conn);
    cmd.Parameters.AddWithValue("id", id);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.NotFound(new { error = "Заявка не найдена" });

    return Results.Ok(new
    {
        id = reader.GetInt32(0),
        title = reader.GetString(1),
        description = reader.GetString(2),
        priority = reader.GetInt32(3),
        status = reader.GetString(4),
        createdAt = reader.GetDateTime(5)
    });
});

app.MapPost("/api/tickets", async (TicketRequest request) =>
{
    var problem = CheckInput(request.Title, request.Description, request.Priority);
    if (problem is not null) return Results.BadRequest(new { error = problem });

    var status = string.IsNullOrEmpty(request.Status) ? "new" : request.Status;
    if (!StatusIsValid(status))
        return Results.BadRequest(new { error = "Статус должен быть одним из: new, in_progress, done" });

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "INSERT INTO tickets (title, description, priority, status) " +
        "VALUES (@title, @description, @priority, @status) RETURNING id, created_at", conn);
    cmd.Parameters.AddWithValue("title", request.Title!);
    cmd.Parameters.AddWithValue("description", request.Description!);
    cmd.Parameters.AddWithValue("priority", request.Priority);
    cmd.Parameters.AddWithValue("status", status);

    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();
    var createdId = reader.GetInt32(0);

    return Results.Created($"/api/tickets/{createdId}", new
    {
        id = createdId,
        title = request.Title,
        description = request.Description,
        priority = request.Priority,
        status,
        createdAt = reader.GetDateTime(1)
    });
});

app.MapPut("/api/tickets/{id:int}", async (int id, TicketRequest request) =>
{
    var problem = CheckInput(request.Title, request.Description, request.Priority);
    if (problem is not null) return Results.BadRequest(new { error = problem });

    if (string.IsNullOrEmpty(request.Status) || !StatusIsValid(request.Status))
        return Results.BadRequest(new { error = "Статус должен быть одним из: new, in_progress, done" });

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "UPDATE tickets SET title = @title, description = @description, priority = @priority, status = @status " +
        "WHERE id = @id RETURNING created_at", conn);
    cmd.Parameters.AddWithValue("id", id);
    cmd.Parameters.AddWithValue("title", request.Title!);
    cmd.Parameters.AddWithValue("description", request.Description!);
    cmd.Parameters.AddWithValue("priority", request.Priority);
    cmd.Parameters.AddWithValue("status", request.Status);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.NotFound(new { error = "Заявка не найдена" });

    return Results.Ok(new
    {
        id,
        title = request.Title,
        description = request.Description,
        priority = request.Priority,
        status = request.Status,
        createdAt = reader.GetDateTime(0)
    });
});

app.MapDelete("/api/tickets/{id:int}", async (int id) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM tickets WHERE id = @id", conn);
    cmd.Parameters.AddWithValue("id", id);

    var deleted = await cmd.ExecuteNonQueryAsync();
    if (deleted == 0) return Results.NotFound(new { error = "Заявка не найдена" });
    return Results.NoContent();
});

app.MapGet("/api/stats", async () =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT count(*) FILTER (WHERE true), " +
        "count(*) FILTER (WHERE status = 'new'), " +
        "count(*) FILTER (WHERE status = 'in_progress'), " +
        "count(*) FILTER (WHERE status = 'done') FROM tickets", conn);

    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();

    return Results.Ok(new
    {
        total = reader.GetInt64(0),
        @new = reader.GetInt64(1),
        in_progress = reader.GetInt64(2),
        done = reader.GetInt64(3)
    });
});

app.Run();

static bool StatusIsValid(string status) => status is "new" or "in_progress" or "done";

static string? CheckInput(string? title, string? description, int priority)
{
    if (string.IsNullOrWhiteSpace(title) || title.Length < 3 || title.Length > 120)
        return "Название должно быть длиной от 3 до 120 символов";
    if (string.IsNullOrWhiteSpace(description))
        return "Описание не может быть пустым";
    if (priority < 1 || priority > 3)
        return "Приоритет может быть только 1, 2 или 3";
    return null;
}

public sealed record TicketRequest(string? Title, string? Description, int Priority, string? Status);
