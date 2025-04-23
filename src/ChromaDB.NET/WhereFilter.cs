using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ChromaDB.NET;

/// <summary>
/// A builder class for filters in ChromaDB
/// </summary>
public class WhereFilter
{
    private readonly Dictionary<string, object> _filter = new Dictionary<string, object>();

    /// <summary>
    /// Creates a new filter
    /// </summary>
    public WhereFilter() { }

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
        _filter[field] = new Dictionary<string, object>
        {
            ["$in"] = values.ToList()
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
        _filter[field] = new Dictionary<string, object>
        {
            ["$nin"] = values.ToList()
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
    /// Converts this filter to a dictionary
    /// </summary>
    public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>(_filter);

    /// <summary>
    /// Implicitly converts a WhereFilter to a Dictionary
    /// </summary>
    public static implicit operator Dictionary<string, object>(WhereFilter filter) => filter.ToDictionary();
}

