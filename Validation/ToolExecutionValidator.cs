using FluentValidation;
using OAI.Core.DTOs.Tools;

namespace OptimalyAI.Validation
{
    /// <summary>
    /// Validator for tool execution requests
    /// </summary>
    public class ToolExecutionValidator : SimpleBaseValidator<CreateToolExecutionDto>
    {
        public ToolExecutionValidator()
        {
            RuleFor(x => x.ToolId)
                .NotEmpty()
                .WithMessage("Tool ID is required")
                .Length(1, 100)
                .WithMessage("Tool ID must be between 1 and 100 characters")
                .Matches("^[a-zA-Z0-9_.-]+$")
                .WithMessage("Tool ID can only contain letters, numbers, underscores, dots, and hyphens");

            RuleFor(x => x.Parameters)
                .NotNull()
                .WithMessage("Parameters dictionary is required")
                .Must(p => p.Count <= 50)
                .WithMessage("Maximum 50 parameters allowed");

            RuleForEach(x => x.Parameters)
                .Must(kvp => !string.IsNullOrEmpty(kvp.Key))
                .WithMessage("Parameter names cannot be empty")
                .Must(kvp => kvp.Key.Length <= 100)
                .WithMessage("Parameter names must be 100 characters or less");

            RuleFor(x => x.UserId)
                .Length(0, 100)
                .WithMessage("User ID must be 100 characters or less")
                .When(x => !string.IsNullOrEmpty(x.UserId));

            RuleFor(x => x.SessionId)
                .Length(0, 100)
                .WithMessage("Session ID must be 100 characters or less")
                .When(x => !string.IsNullOrEmpty(x.SessionId));

            RuleFor(x => x.ConversationId)
                .Length(0, 100)
                .WithMessage("Conversation ID must be 100 characters or less")
                .When(x => !string.IsNullOrEmpty(x.ConversationId));

            RuleFor(x => x.ExecutionTimeout)
                .Must(timeout => timeout == null || (timeout.Value.TotalSeconds >= 1 && timeout.Value.TotalSeconds <= 3600))
                .WithMessage("Execution timeout must be between 1 second and 1 hour")
                .When(x => x.ExecutionTimeout.HasValue);

            // Validate context if provided
            RuleFor(x => x.Context)
                .SetValidator(new ToolExecutionContextValidator()!)
                .When(x => x.Context != null);
        }
    }

    /// <summary>
    /// Validator for tool execution context
    /// </summary>
    public class ToolExecutionContextValidator : AbstractValidator<ToolExecutionContextDto>
    {
        public ToolExecutionContextValidator()
        {
            RuleFor(x => x.UserId)
                .Length(0, 100)
                .WithMessage("User ID must be 100 characters or less")
                .When(x => !string.IsNullOrEmpty(x.UserId));

            RuleFor(x => x.SessionId)
                .Length(0, 100)
                .WithMessage("Session ID must be 100 characters or less")
                .When(x => !string.IsNullOrEmpty(x.SessionId));

            RuleFor(x => x.ConversationId)
                .Length(0, 100)
                .WithMessage("Conversation ID must be 100 characters or less")
                .When(x => !string.IsNullOrEmpty(x.ConversationId));

            RuleFor(x => x.UserPermissions)
                .NotNull()
                .WithMessage("User permissions dictionary is required")
                .Must(p => p.Count <= 20)
                .WithMessage("Maximum 20 user permissions allowed");

            RuleFor(x => x.CustomContext)
                .NotNull()
                .WithMessage("Custom context dictionary is required")
                .Must(p => p.Count <= 10)
                .WithMessage("Maximum 10 custom context items allowed");

            RuleFor(x => x.ExecutionTimeout)
                .Must(timeout => timeout == null || (timeout.Value.TotalSeconds >= 1 && timeout.Value.TotalSeconds <= 3600))
                .WithMessage("Execution timeout must be between 1 second and 1 hour")
                .When(x => x.ExecutionTimeout.HasValue);
        }
    }

    /// <summary>
    /// Validator for batch tool execution requests
    /// </summary>
    public class BatchToolExecutionValidator : SimpleBaseValidator<CreateBatchToolExecutionDto>
    {
        public BatchToolExecutionValidator()
        {
            RuleFor(x => x.Executions)
                .NotEmpty()
                .WithMessage("At least one execution is required")
                .Must(executions => executions.Count <= 10)
                .WithMessage("Maximum 10 executions allowed in a batch");

            RuleForEach(x => x.Executions)
                .SetValidator(new BatchToolExecutionItemValidator());

            RuleFor(x => x.ExecutionMode)
                .NotEmpty()
                .WithMessage("Execution mode is required")
                .Must(mode => mode == "Sequential" || mode == "Parallel")
                .WithMessage("Execution mode must be 'Sequential' or 'Parallel'");

            RuleFor(x => x.MaxConcurrency)
                .GreaterThan(0)
                .WithMessage("Max concurrency must be greater than 0")
                .LessThanOrEqualTo(5)
                .WithMessage("Max concurrency cannot exceed 5")
                .When(x => x.MaxConcurrency.HasValue && x.ExecutionMode == "Parallel");

            RuleFor(x => x.Context)
                .SetValidator(new ToolExecutionContextValidator()!)
                .When(x => x.Context != null);
        }
    }

    /// <summary>
    /// Validator for individual batch execution items
    /// </summary>
    public class BatchToolExecutionItemValidator : AbstractValidator<BatchToolExecutionItemDto>
    {
        public BatchToolExecutionItemValidator()
        {
            RuleFor(x => x.ToolId)
                .NotEmpty()
                .WithMessage("Tool ID is required")
                .Length(1, 100)
                .WithMessage("Tool ID must be between 1 and 100 characters");

            RuleFor(x => x.Parameters)
                .NotNull()
                .WithMessage("Parameters dictionary is required")
                .Must(p => p.Count <= 50)
                .WithMessage("Maximum 50 parameters allowed");

            RuleFor(x => x.ExecutionId)
                .Length(0, 100)
                .WithMessage("Execution ID must be 100 characters or less")
                .When(x => !string.IsNullOrEmpty(x.ExecutionId));

            RuleFor(x => x.Order)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Order must be 0 or greater")
                .When(x => x.Order.HasValue);
        }
    }

    /// <summary>
    /// Validator for tool validation requests
    /// </summary>
    public class ToolValidationRequestValidator : SimpleBaseValidator<ValidateToolExecutionDto>
    {
        public ToolValidationRequestValidator()
        {
            RuleFor(x => x.ToolId)
                .NotEmpty()
                .WithMessage("Tool ID is required")
                .Length(1, 100)
                .WithMessage("Tool ID must be between 1 and 100 characters");

            RuleFor(x => x.Parameters)
                .NotNull()
                .WithMessage("Parameters dictionary is required")
                .Must(p => p.Count <= 50)
                .WithMessage("Maximum 50 parameters allowed");

            RuleFor(x => x.Context)
                .SetValidator(new ToolExecutionContextValidator()!)
                .When(x => x.Context != null);
        }
    }
}