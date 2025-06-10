using System;

namespace OAI.Core.Exceptions
{
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message) { }
        public BusinessException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    public class NotFoundException : Exception
    {
        public string EntityName { get; }
        public object Id { get; }
        
        public NotFoundException(string entityName, object id) 
            : base($"{entityName} with id {id} was not found")
        {
            EntityName = entityName;
            Id = id;
        }
        
        public NotFoundException(string message) : base(message) { }
    }
    
    public class ValidationException : Exception
    {
        public string[] Errors { get; }
        
        public ValidationException(string message) : base(message) 
        {
            Errors = new[] { message };
        }
        
        public ValidationException(string[] errors) : base("Validation failed")
        {
            Errors = errors;
        }
    }
}