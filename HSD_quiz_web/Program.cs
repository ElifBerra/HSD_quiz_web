using Dapper;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Connection string'ini Supabase > Settings > Database kýsmýndan al
string connStr = "Host=...;Port=5432;Database=postgres;User Id=postgres;Password=...";

// 1. Tüm testleri listele
app.MapGet("/api/quizzes", async () => {
    using var conn = new NpgsqlConnection(connStr);
    return await conn.QueryAsync("SELECT * FROM quizzes");
});

// 2. Bir testin tüm sorularýný ve þýklarýný tek seferde al
app.MapGet("/api/quiz-data/{id}", async (Guid id) => {
    using var conn = new NpgsqlConnection(connStr);
    var sql = @"
        SELECT q.id as QuestionId, q.questiontext, o.id as OptionId, o.optiontext, o.score, o.result_type 
        FROM questions q
        JOIN options o ON q.id = o.questionid
        WHERE q.quizid = @id
        ORDER BY q.ordernum";
    return await conn.QueryAsync(sql, new { id });
});

app.UseStaticFiles(); // wwwroot içindeki index.html'i sunmak için
app.MapFallbackToFile("index.html");
app.Run();