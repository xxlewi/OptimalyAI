using System.Collections.Generic;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Describes the structure of data handled by an adapter
    /// </summary>
    public interface IAdapterSchema
    {
        /// <summary>
        /// Schema identifier
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Schema name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Schema description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// JSON Schema definition
        /// </summary>
        string JsonSchema { get; }

        /// <summary>
        /// Example data conforming to this schema
        /// </summary>
        object ExampleData { get; }

        /// <summary>
        /// Fields available in this schema
        /// </summary>
        IReadOnlyList<SchemaField> Fields { get; }
    }

    /// <summary>
    /// Individual field in a schema
    /// </summary>
    public class SchemaField
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
        public string Format { get; set; }
    }
}