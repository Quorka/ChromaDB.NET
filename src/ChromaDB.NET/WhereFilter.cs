using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromaDB.NET;

/// <summary>
/// A builder class for filters in ChromaDB
/// </summary>
[JsonConverter(typeof(WhereFilterConverter))]
public class WhereFilter
{
    private readonly Dictionary<string, object> _filter = new Dictionary<string, object>();
    private bool _combineWithOr = false; // Flag to indicate OR combination

    // Internal accessor for the converter
    internal bool CombineWithOr => _combineWithOr;
    internal IReadOnlyDictionary<string, object> FilterDictionary => _filter;

    /// <summary>
    /// Creates a new filter
    /// </summary>
    public WhereFilter() { }

    /// <summary>
    /// Specifies that the conditions added to this filter instance should be combined using OR logic.
    /// If not called, conditions are combined using AND logic (default).
    /// Note: This applies only when multiple conditions are added directly to this WhereFilter instance.
    /// </summary>
    /// <returns>The current WhereFilter instance for chaining.</returns>
    public WhereFilter Or()
    {
        _combineWithOr = true;
        return this;
    }

    /// <summary>
    /// Adds an equals condition to the filter
    /// </summary>
    /// <param name="field">Field name</param>
    /// <param name="value">Value to match</param>
    /// <returns>This filter instance for chaining</returns>
    public WhereFilter Equals(string field, object value)
    {
        _filter[field] = value;
        return this;
    }

    /// <summary>
    /// Adds an $in condition to the filter
    /// </summary>
    /// <param name="field">Field name</param>
    /// <param name="values">Values to match</param>
    /// <returns>This filter instance for chaining</returns>
    public WhereFilter In(string field, IEnumerable<object> values)
    {
        // Ensure values is materialized if it's a deferred execution LINQ query
        var valueList = values.ToList();
        if (!valueList.Any())
        {
            throw new ArgumentException("IN operator requires a non-empty list of values.", nameof(values));
        }
        _filter[field] = new Dictionary<string, object>
        {
            ["$in"] = valueList
        };
        return this;
    }

    /// <summary>
    /// Adds a $nin (not in) condition to the filter
    /// </summary>
    /// <param name="field">Field name</param>
    /// <param name="values">Values to exclude</param>
    /// <returns>This filter instance for chaining</returns>
    public WhereFilter NotIn(string field, IEnumerable<object> values)
    {
        // Ensure values is materialized if it's a deferred execution LINQ query
        var valueList = values.ToList();
        if (!valueList.Any())
        {
            throw new ArgumentException("NIN operator requires a non-empty list of values.", nameof(values));
        }
        _filter[field] = new Dictionary<string, object>
        {
            ["$nin"] = valueList
        };
        return this;
    }

    /// <summary>
    /// Adds a $gt (greater than) condition to the filter
    /// </summary>
    /// <param name="field">Field name</param>
    /// <param name="value">Value to compare against</param>
    /// <returns>This filter instance for chaining</returns>
    public WhereFilter GreaterThan(string field, object value)
    {
        _filter[field] = new Dictionary<string, object>
        {
            ["$gt"] = value
        };
        return this;
    }

    /// <summary>
    /// Adds a $gte (greater than or equal) condition to the filter
    /// </summary>
    /// <param name="field">Field name</param>
    /// <param name="value">Value to compare against</param>
    /// <returns>This filter instance for chaining</returns>
    public WhereFilter GreaterThanOrEqual(string field, object value)
    {
        _filter[field] = new Dictionary<string, object>
        {
            ["$gte"] = value
        };
        return this;
    }

    /// <summary>
    /// Adds a $lt (less than) condition to the filter
    /// </summary>
    /// <param name="field">Field name</param>
    /// <param name="value">Value to compare against</param>
    /// <returns>This filter instance for chaining</returns>
    public WhereFilter LessThan(string field, object value)
    {
        _filter[field] = new Dictionary<string, object>
        {
            ["$lt"] = value
        };
        return this;
    }

    /// <summary>
    /// Adds a $lte (less than or equal) condition to the filter
    /// </summary>
    /// <param name="field">Field name</param>
    /// <param name="value">Value to compare against</param>
    /// <returns>This filter instance for chaining</returns>
    public WhereFilter LessThanOrEqual(string field, object value)
    {
        _filter[field] = new Dictionary<string, object>
        {
            ["$lte"] = value
        };
        return this;
    }

    /// <summary>
    /// Explicitly combines multiple filters with an AND operator
    /// </summary>
    /// <param name="filters">The filters to combine</param>
    /// <returns>A new filter representing the AND combination</returns>
    public WhereFilter And(params WhereFilter[] filters)
    {
        _filter["$and"] = filters.Select(f => f.ToDictionary()).ToList();
        return this;
    }

    /// <summary>
    /// Explicitly combines multiple filters with an OR operator
    /// </summary>
    /// <param name="filters">The filters to combine</param>
    /// <returns>A new filter representing the OR combination</returns>
    public WhereFilter Or(params WhereFilter[] filters)
    {
        _filter["$or"] = filters.Select(f => f.ToDictionary()).ToList();
        return this;
    }

    /// <summary>
    /// Converts this filter to a dictionary
    /// </summary>
    public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>(_filter);

    /// <summary>
    /// Implicitly converts a WhereFilter to a Dictionary
    /// </summary>
    public static implicit operator Dictionary<string, object>(WhereFilter filter) => filter.ToDictionary();
}

