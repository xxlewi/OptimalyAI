using System;
using System.Collections.Generic;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Adapters.Base
{
    /// <summary>
    /// Simple implementation of parameter validation
    /// </summary>
    public class SimpleParameterValidation : IParameterValidation
    {
        public object MinValue { get; set; }
        public object MaxValue { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string Pattern { get; set; }
        public IReadOnlyList<object> AllowedValues { get; set; }
        public Func<object, ParameterValidationResult> CustomValidator { get; set; }
        public IReadOnlyList<string> AllowedFileExtensions { get; set; }
        public long? MaxFileSizeBytes { get; set; }
        public IReadOnlyDictionary<string, object> CustomRules { get; set; }
    }
}