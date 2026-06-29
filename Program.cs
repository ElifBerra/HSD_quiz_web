using Dapper;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string connStr = "Host=aws-1-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.fclorbthrtwonsqrehvt;Password=hsd-quiz-web-2026!?";

// 1. Baðlantý Testi
app.MapGet("/api/test-db", async () => {
    using var conn = new NpgsqlConnection(connStr);
    return await conn.QueryAsync("SELECT title FROM quizzes");
});

// 2. Tüm iliþkileri (Soru + Þýk) tek seferde getir
app.MapGet("/api/quiz-data/{quizId}", async (Guid quizId) => {
    using var conn = new NpgsqlConnection(connStr);
    var sql = @"
        SELECT q.id as QuestionId, q.questiontext, o.id as OptionId, o.optiontext, o.score 
        FROM questions q
        LEFT JOIN options o ON q.id = o.questionid
        WHERE q.quizid = @quizId
        ORDER BY q.ordernum";
    var result = await conn.QueryAsync(sql, new { quizId });
    return Results.Ok(result);
});

// 3. Sonucu puana göre getir
app.MapGet("/api/result-by-score", async (Guid quizId, int score) => {
    using var conn = new NpgsqlConnection(connStr);
    var result = await conn.QueryFirstOrDefaultAsync(
        "SELECT title, description, imageurl FROM results WHERE quizid = @quizId AND @score BETWEEN minscore AND maxscore",
        new { quizId, score });
    if (result == null) return Results.NotFound();
    return Results.Ok(result);
});

// 4. Tüm quizleri listele (Admin)
app.MapGet("/api/admin/quizzes", async () => {
    using var conn = new NpgsqlConnection(connStr);
    return await conn.QueryAsync("SELECT id, title FROM quizzes");
});

// 5. Soru ekle (Admin)
app.MapPost("/api/admin/add-question", async (QuestionRequest req) => {
    using var conn = new NpgsqlConnection(connStr);
    var id = await conn.QuerySingleAsync<Guid>(
        "INSERT INTO questions (quizid, questiontext, ordernum) VALUES (@QuizId, @Text, @OrderNum) RETURNING id",
        req);
    return Results.Ok(new { id });
});

// 6. Þýk ekle (Admin)
app.MapPost("/api/admin/add-option", async (OptionRequest req) => {
    using var conn = new NpgsqlConnection(connStr);
    await conn.ExecuteAsync(
        "INSERT INTO options (questionid, optiontext, score) VALUES (@Questionid, @Text, @Score)",
        new { Questionid = req.Questionid, Text = req.Text, Score = req.Score });
    return Results.Ok();
});

// 6b. Þýk güncelle (Admin)
app.MapPut("/api/admin/update-option", async ([Microsoft.AspNetCore.Mvc.FromBody] OptionUpdate req) => {
    try
    {
        using var conn = new NpgsqlConnection(connStr);
        await conn.ExecuteAsync(
            "UPDATE options SET optiontext = @Text, score = @Score WHERE id = @Id",
            new { Text = req.Text, Score = req.Score, Id = req.Id });
        return Results.Ok();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Option Update Hatasý: " + ex.Message);
        return Results.Problem(ex.Message);
    }
});

// 6c. Þýk sil (Admin)
app.MapDelete("/api/admin/delete-option/{id}", async (Guid id) => {
    using var conn = new NpgsqlConnection(connStr);
    await conn.ExecuteAsync("DELETE FROM options WHERE id = @id", new { id });
    return Results.Ok();
});

// 7. Sonuç kriteri ekle (Admin)
app.MapPost("/api/admin/add-result", async (ResultRequest req) => {
    using var conn = new NpgsqlConnection(connStr);
    var id = await conn.QuerySingleAsync<Guid>(
        @"INSERT INTO results (quizid, minscore, maxscore, title, description, imageurl) 
          VALUES (@QuizId, @MinScore, @MaxScore, @Title, @Description, @ImageUrl) 
          RETURNING id",
        req);
    return Results.Ok(new { id });
});

// 7b. Sonuç kriteri güncelle (Admin)
app.MapPut("/api/admin/update-result", async ([Microsoft.AspNetCore.Mvc.FromBody] ResultUpdate req) => {
    try
    {
        using var conn = new NpgsqlConnection(connStr);
        await conn.ExecuteAsync(
            @"UPDATE results SET minscore=@MinScore, maxscore=@MaxScore, title=@Title, 
              description=@Description, imageurl=@ImageUrl WHERE id=@Id",
            req);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Result Update Hatasý: " + ex.Message);
        return Results.Problem(ex.Message);
    }
});

// 7c. Sonuç kriteri sil (Admin)
app.MapDelete("/api/admin/delete-result/{id}", async (Guid id) => {
    using var conn = new NpgsqlConnection(connStr);
    await conn.ExecuteAsync("DELETE FROM results WHERE id = @id", new { id });
    return Results.Ok();
});

// 8. Bir quiz'in sonuç kriterlerini listele (Admin)
app.MapGet("/api/admin/results/{quizId}", async (Guid quizId) => {
    using var conn = new NpgsqlConnection(connStr);
    return await conn.QueryAsync(
        "SELECT id, minscore, maxscore, title, description, imageurl FROM results WHERE quizid = @quizId ORDER BY minscore",
        new { quizId });
});

// 9. Soru sil (Admin)
app.MapDelete("/api/admin/delete-question/{id}", async (Guid id) => {
    using var conn = new NpgsqlConnection(connStr);
    await conn.ExecuteAsync("DELETE FROM options WHERE questionid = @id", new { id });
    await conn.ExecuteAsync("DELETE FROM questions WHERE id = @id", new { id });
    return Results.Ok();
});

// 10. Soru ve Þýklarý güncelle (Admin)
app.MapPut("/api/admin/update-question", async (QuestionUpdate req) => {
    try
    {
        using var conn = new NpgsqlConnection(connStr);
        // 1. Sorunun metnini güncelle
        await conn.ExecuteAsync(
            "UPDATE questions SET questiontext = @Text WHERE id = @Id",
            new { Text = req.Text, Id = req.Id });

        // 2. Bu soruya ait eski þýklarý tamamen temizle
        await conn.ExecuteAsync("DELETE FROM options WHERE questionid = @Id", new { Id = req.Id });

        // 3. Formdan gelen yeni/güncellenmiþ þýklarý ekle
        if (req.Options != null)
        {
            foreach (var opt in req.Options)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO options (questionid, optiontext, score) VALUES (@Id, @Text, @Score)",
                    new { Id = req.Id, Text = opt.Text, Score = opt.Score });
            }
        }
        return Results.Ok();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Güncelleme Hatasý: " + ex.Message);
        return Results.Problem(ex.Message);
    }
});


// Yeni Test Ekle (Admin)
app.MapPost("/api/admin/add-quiz", async (QuizRequest req) => {
    try
    {
        using var conn = new NpgsqlConnection(connStr);
        var id = await conn.QuerySingleAsync<Guid>(
            "INSERT INTO quizzes (title) VALUES (@Title) RETURNING id",
            req);
        return Results.Ok(new { id });
    }
    catch (Exception ex)
    {
        Console.WriteLine("Quiz Ekleme Hatasý: " + ex.Message);
        return Results.Problem(ex.Message);
    }
});

// Test (Quiz) Sil (Admin)
app.MapDelete("/api/admin/delete-quiz/{id}", async (Guid id) => {
    try
    {
        using var conn = new NpgsqlConnection(connStr);
        // 1. Önce bu teste ait sorularýn þýklarýný sil
        await conn.ExecuteAsync("DELETE FROM options WHERE questionid IN (SELECT id FROM questions WHERE quizid = @id)", new { id });
        // 2. Sorularý sil
        await conn.ExecuteAsync("DELETE FROM questions WHERE quizid = @id", new { id });
        // 3. Sonuç kriterlerini sil
        await conn.ExecuteAsync("DELETE FROM results WHERE quizid = @id", new { id });
        // 4. En son testi (quiz) sil
        await conn.ExecuteAsync("DELETE FROM quizzes WHERE id = @id", new { id });

        return Results.Ok();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Quiz Silme Hatasý: " + ex.Message);
        return Results.Problem(ex.Message);
    }
});

app.UseStaticFiles();
app.MapFallbackToFile("index.html");
app.Run();

// Modeller (Records)
public record QuestionRequest(Guid QuizId, string Text, int OrderNum);
public record OptionRequest(Guid Questionid, string Text, int Score);
public record ResultRequest(Guid QuizId, int MinScore, int MaxScore, string Title, string Description, string ImageUrl);
public record OptionUpdate(Guid Id, string Text, int Score);

// YENÝDEN DÜZENLENEN KISIM:
public record QuestionUpdate(Guid Id, string Text, List<OptionItem> Options);
public record OptionItem(string Text, int Score);

public record ResultUpdate(Guid Id, int MinScore, int MaxScore, string Title, string Description, string ImageUrl);
public record QuizRequest(string Title);