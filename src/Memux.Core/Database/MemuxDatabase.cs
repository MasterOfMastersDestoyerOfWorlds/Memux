using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Memux.Core.Database;

/// <summary>
/// SQLite database for skill storage following the Everything CLI schema:
/// - Column 1: Python/C# code that makes up the skill
/// - Column 2: IDs of other skills called by this skill
/// - Column 3: ELO ranking
/// - Column 4: List of words that describe the skill (tags)
/// - Column 5: Location in the codebase where the code exists
/// </summary>
public class MemuxDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;
    
    public MemuxDatabase(string dbPath)
    {
        _dbPath = dbPath;
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeSchema();
    }
    
    private void InitializeSchema()
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS skills (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                code TEXT NOT NULL,
                dependencies TEXT NOT NULL,  -- JSON array of skill IDs
                elo_rating REAL NOT NULL DEFAULT 1000.0,
                tags TEXT NOT NULL,          -- JSON array of tag strings
                code_location TEXT,
                usage_count INTEGER NOT NULL DEFAULT 0,
                success_count INTEGER NOT NULL DEFAULT 0,
                failure_count INTEGER NOT NULL DEFAULT 0,
                last_used TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            
            CREATE TABLE IF NOT EXISTS skill_usage_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                skill_id TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                success INTEGER NOT NULL,
                execution_time_ms INTEGER,
                context TEXT,                -- JSON representation of context
                error_message TEXT,
                FOREIGN KEY (skill_id) REFERENCES skills(id)
            );
            
            CREATE TABLE IF NOT EXISTS goals (
                id TEXT PRIMARY KEY,
                description TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                started_at TEXT,
                completed_at TEXT,
                last_evaluated_at TEXT,
                progress REAL NOT NULL DEFAULT 0.0,
                sub_goals TEXT NOT NULL,     -- JSON array
                attempted_skills TEXT NOT NULL, -- JSON array
                metadata TEXT NOT NULL        -- JSON object
            );
            
            CREATE TABLE IF NOT EXISTS action_sequences (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                actions TEXT NOT NULL,       -- JSON array of actions
                context TEXT,                -- JSON representation of perception state
                resulting_skill_id TEXT,     -- If this sequence was converted to a skill
                frequency INTEGER NOT NULL DEFAULT 1
            );
            
            CREATE INDEX IF NOT EXISTS idx_skills_elo ON skills(elo_rating DESC);
            CREATE INDEX IF NOT EXISTS idx_skills_usage ON skills(usage_count DESC);
            CREATE INDEX IF NOT EXISTS idx_skill_usage_history_skill ON skill_usage_history(skill_id);
            CREATE INDEX IF NOT EXISTS idx_goals_status ON goals(status);
            CREATE INDEX IF NOT EXISTS idx_action_sequences_timestamp ON action_sequences(timestamp);
        ";
        command.ExecuteNonQuery();
    }
    
    public void InsertSkill(
        string id,
        string name,
        string code,
        List<string> dependencies,
        List<string> tags,
        string? codeLocation = null,
        double eloRating = 1000.0)
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO skills (id, name, code, dependencies, elo_rating, tags, code_location, created_at, updated_at)
            VALUES ($id, $name, $code, $dependencies, $elo_rating, $tags, $code_location, $created_at, $updated_at)
        ";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$code", code);
        command.Parameters.AddWithValue("$dependencies", JsonSerializer.Serialize(dependencies));
        command.Parameters.AddWithValue("$elo_rating", eloRating);
        command.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(tags));
        command.Parameters.AddWithValue("$code_location", codeLocation ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
    
    public void UpdateSkillElo(string skillId, double newElo)
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            UPDATE skills 
            SET elo_rating = $elo_rating, updated_at = $updated_at
            WHERE id = $id
        ";
        command.Parameters.AddWithValue("$id", skillId);
        command.Parameters.AddWithValue("$elo_rating", newElo);
        command.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
    
    public void RecordSkillUsage(string skillId, bool success, int executionTimeMs, string? context = null, string? errorMessage = null)
    {
        using var transaction = _connection.BeginTransaction();
        
        // Update skill stats
        var updateCommand = _connection.CreateCommand();
        updateCommand.CommandText = @"
            UPDATE skills 
            SET usage_count = usage_count + 1,
                success_count = success_count + $success,
                failure_count = failure_count + $failure,
                last_used = $last_used,
                updated_at = $updated_at
            WHERE id = $id
        ";
        updateCommand.Parameters.AddWithValue("$id", skillId);
        updateCommand.Parameters.AddWithValue("$success", success ? 1 : 0);
        updateCommand.Parameters.AddWithValue("$failure", success ? 0 : 1);
        updateCommand.Parameters.AddWithValue("$last_used", DateTime.UtcNow.ToString("O"));
        updateCommand.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("O"));
        updateCommand.ExecuteNonQuery();
        
        // Insert usage history
        var insertCommand = _connection.CreateCommand();
        insertCommand.CommandText = @"
            INSERT INTO skill_usage_history (skill_id, timestamp, success, execution_time_ms, context, error_message)
            VALUES ($skill_id, $timestamp, $success, $execution_time_ms, $context, $error_message)
        ";
        insertCommand.Parameters.AddWithValue("$skill_id", skillId);
        insertCommand.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));
        insertCommand.Parameters.AddWithValue("$success", success ? 1 : 0);
        insertCommand.Parameters.AddWithValue("$execution_time_ms", executionTimeMs);
        insertCommand.Parameters.AddWithValue("$context", context ?? (object)DBNull.Value);
        insertCommand.Parameters.AddWithValue("$error_message", errorMessage ?? (object)DBNull.Value);
        insertCommand.ExecuteNonQuery();
        
        transaction.Commit();
    }
    
    public List<SkillRecord> GetTopSkillsByElo(int limit = 10)
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, code, dependencies, elo_rating, tags, code_location, usage_count
            FROM skills
            ORDER BY elo_rating DESC
            LIMIT $limit
        ";
        command.Parameters.AddWithValue("$limit", limit);
        
        var skills = new List<SkillRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            skills.Add(ReadSkillRecord(reader));
        }
        return skills;
    }
    
    public List<SkillRecord> SearchSkillsByTags(List<string> tags, int limit = 10)
    {
        // For simplicity, we'll do a LIKE search for now
        // In production, consider using a proper FTS table or vector embeddings
        var command = _connection.CreateCommand();
        var tagConditions = string.Join(" OR ", tags.Select((_, i) => $"tags LIKE $tag{i}"));
        command.CommandText = $@"
            SELECT id, name, code, dependencies, elo_rating, tags, code_location, usage_count
            FROM skills
            WHERE {tagConditions}
            ORDER BY elo_rating DESC
            LIMIT $limit
        ";
        
        for (int i = 0; i < tags.Count; i++)
        {
            command.Parameters.AddWithValue($"$tag{i}", $"%{tags[i]}%");
        }
        command.Parameters.AddWithValue("$limit", limit);
        
        var skills = new List<SkillRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            skills.Add(ReadSkillRecord(reader));
        }
        return skills;
    }
    
    public SkillRecord? GetSkillById(string id)
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, code, dependencies, elo_rating, tags, code_location, usage_count
            FROM skills
            WHERE id = $id
        ";
        command.Parameters.AddWithValue("$id", id);
        
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return ReadSkillRecord(reader);
        }
        return null;
    }
    
    public void RecordActionSequence(string actions, string? context = null)
    {
        var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO action_sequences (timestamp, actions, context)
            VALUES ($timestamp, $actions, $context)
        ";
        command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$actions", actions);
        command.Parameters.AddWithValue("$context", context ?? (object)DBNull.Value);
        command.ExecuteNonQuery();
    }
    
    private SkillRecord ReadSkillRecord(SqliteDataReader reader)
    {
        return new SkillRecord
        {
            Id = reader.GetString(0),
            Name = reader.GetString(1),
            Code = reader.GetString(2),
            Dependencies = JsonSerializer.Deserialize<List<string>>(reader.GetString(3)) ?? new(),
            EloRating = reader.GetDouble(4),
            Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? new(),
            CodeLocation = reader.IsDBNull(6) ? null : reader.GetString(6),
            UsageCount = reader.GetInt32(7)
        };
    }
    
    public void Dispose()
    {
        _connection.Dispose();
    }
}

public class SkillRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
    public double EloRating { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? CodeLocation { get; set; }
    public int UsageCount { get; set; }
}

