using System;
using System.Collections.Generic;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.ServiceLayer.Services.Orchestration.Base
{
    /// <summary>
    /// Specialized exception for orchestrator errors
    /// </summary>
    public class OrchestratorException : Exception
    {
        public string Code { get; set; }
        public string Details { get; set; }
        public OrchestratorErrorType ErrorType { get; set; }
        public IDictionary<string, object> ErrorData { get; set; }
        public IList<string> ValidationErrors { get; set; }

        public OrchestratorException(string message) : base(message)
        {
            ErrorType = OrchestratorErrorType.UnknownError;
            Code = "ORCHESTRATOR_ERROR";
            ErrorData = new Dictionary<string, object>();
        }

        public OrchestratorException(string message, Exception innerException) 
            : base(message, innerException)
        {
            ErrorType = OrchestratorErrorType.UnknownError;
            Code = "ORCHESTRATOR_ERROR";
            ErrorData = new Dictionary<string, object>();
        }

        public OrchestratorException(string message, OrchestratorErrorType errorType) 
            : base(message)
        {
            ErrorType = errorType;
            Code = errorType.ToString().ToUpper();
            ErrorData = new Dictionary<string, object>();
        }

        public OrchestratorException(string message, OrchestratorErrorType errorType, IList<string> validationErrors) 
            : base(message)
        {
            ErrorType = errorType;
            Code = errorType.ToString().ToUpper();
            ValidationErrors = validationErrors;
            ErrorData = new Dictionary<string, object>
            {
                ["validationErrors"] = validationErrors
            };
        }

        public OrchestratorException(string message, string code, string details = null) 
            : base(message)
        {
            Code = code;
            Details = details;
            ErrorType = OrchestratorErrorType.ExecutionError;
            ErrorData = new Dictionary<string, object>();
        }

        /// <summary>
        /// Create a validation error exception
        /// </summary>
        public static OrchestratorException ValidationError(string message, IList<string> errors)
        {
            return new OrchestratorException(message, OrchestratorErrorType.ValidationError, errors);
        }

        /// <summary>
        /// Create a timeout error exception
        /// </summary>
        public static OrchestratorException TimeoutError(string message, TimeSpan timeout)
        {
            return new OrchestratorException(message, OrchestratorErrorType.TimeoutError)
            {
                ErrorData = new Dictionary<string, object>
                {
                    ["timeout"] = timeout.TotalSeconds
                }
            };
        }

        /// <summary>
        /// Create a tool error exception
        /// </summary>
        public static OrchestratorException ToolError(string message, string toolId, Exception innerException = null)
        {
            var ex = innerException != null 
                ? new OrchestratorException(message, innerException)
                : new OrchestratorException(message);
            
            ex.ErrorType = OrchestratorErrorType.ToolError;
            ex.ErrorData["toolId"] = toolId;
            
            return ex;
        }

        /// <summary>
        /// Create a model error exception
        /// </summary>
        public static OrchestratorException ModelError(string message, string modelId, Exception innerException = null)
        {
            var ex = innerException != null 
                ? new OrchestratorException(message, innerException)
                : new OrchestratorException(message);
            
            ex.ErrorType = OrchestratorErrorType.ModelError;
            ex.ErrorData["modelId"] = modelId;
            
            return ex;
        }

        /// <summary>
        /// Create a configuration error exception
        /// </summary>
        public static OrchestratorException ConfigurationError(string message, string configKey = null)
        {
            var ex = new OrchestratorException(message, OrchestratorErrorType.ConfigurationError);
            
            if (!string.IsNullOrEmpty(configKey))
            {
                ex.ErrorData["configKey"] = configKey;
            }
            
            return ex;
        }
    }
}