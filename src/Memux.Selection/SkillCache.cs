using Memux.Skills;
using Memux.Core.Models;

namespace Memux.Selection;

/// <summary>
/// Caches skill selection results to avoid redundant LLM calls
/// Improves selection performance significantly for repeated contexts
/// </summary>
public class SkillCache
{
    private readonly Dictionary<string, CachedSkill> _cache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(5); // Short TTL for dynamic environments
    private readonly int _maxCacheSize = 1000;
    private readonly bool _enabled;
    
    public SkillCache(bool cacheEnabled = true)
    {
        _enabled = cacheEnabled;
    }
    
    /// <summary>
    /// Generate cache key from context and goal
    /// </summary>
    public string GenerateCacheKey(ContextInfo context, Goal? goal)
    {
        // Create key from context tags and goal
        var tags = string.Join(",", context.Tags.OrderBy(t => t));
        var goalDesc = goal?.Description ?? "no-goal";
        
        // Include combat/menu/dialog state for more specific caching
        var state = $"{context.IsInCombat}|{context.IsInMenu}|{context.IsInDialog}|{context.HasNearbyObstacle}";
        
        return $"{tags}|{state}|{goalDesc}";
    }
    
    /// <summary>
    /// Try to get cached skill if available and not expired
    /// </summary>
    public bool TryGetCachedSkill(string cacheKey, out Skill? skill)
    {
        skill = null;
        
        if (!_enabled)
        {
            return false;
        }
        
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            // Check expiration
            if (DateTime.UtcNow - cached.Timestamp < _cacheExpiration)
            {
                skill = cached.Skill;
                cached.HitCount++;
                return true;
            }
            else
            {
                // Expired, remove from cache
                _cache.Remove(cacheKey);
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Cache a skill selection result
    /// </summary>
    public void CacheSkill(string cacheKey, Skill skill)
    {
        if (!_enabled)
        {
            return;
        }
        
        // Enforce max cache size (LRU eviction)
        if (_cache.Count >= _maxCacheSize)
        {
            EvictOldest();
        }
        
        _cache[cacheKey] = new CachedSkill
        {
            Skill = skill,
            Timestamp = DateTime.UtcNow,
            HitCount = 0
        };
    }
    
    /// <summary>
    /// Evict oldest entries from cache
    /// </summary>
    private void EvictOldest()
    {
        var toRemove = _cache
            .OrderBy(kvp => kvp.Value.Timestamp)
            .Take(_maxCacheSize / 4) // Remove 25% of cache
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in toRemove)
        {
            _cache.Remove(key);
        }
    }
    
    /// <summary>
    /// Clear all cached entries
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }
    
    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStats GetStats()
    {
        return new CacheStats
        {
            EntryCount = _cache.Count,
            TotalHits = _cache.Values.Sum(c => c.HitCount),
            Enabled = _enabled
        };
    }
    
    private class CachedSkill
    {
        public Skill Skill { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public int HitCount { get; set; }
    }
}

public class CacheStats
{
    public int EntryCount { get; set; }
    public int TotalHits { get; set; }
    public bool Enabled { get; set; }
    
    public override string ToString()
    {
        if (!Enabled)
        {
            return "Cache disabled";
        }
        
        return $"Cache: {EntryCount} entries, {TotalHits} hits";
    }
}

