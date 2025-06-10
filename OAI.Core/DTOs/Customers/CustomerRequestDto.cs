using System;
using OAI.Core.Entities.Customers;

namespace OAI.Core.DTOs.Customers
{
    public class CustomerRequestDto : BaseGuidDto
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public RequestType Type { get; set; }
        public RequestPriority Priority { get; set; }
        public RequestStatus Status { get; set; }
        public string? Category { get; set; }
        public decimal? EstimatedBudget { get; set; }
        public DateTime? RequestedDeadline { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string? ResolvedBy { get; set; }
        public string? Resolution { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public RequestSource Source { get; set; }
        public string? InternalNotes { get; set; }
        public string? Attachments { get; set; }
    }

    public class CustomerRequestListDto : BaseGuidDto
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Title { get; set; }
        public RequestType Type { get; set; }
        public RequestPriority Priority { get; set; }
        public RequestStatus Status { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime? RequestedDeadline { get; set; }
        public Guid? ProjectId { get; set; }
    }

    public class CreateCustomerRequestDto : CreateDtoBase
    {
        public Guid CustomerId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public RequestType Type { get; set; } = RequestType.NewProject;
        public RequestPriority Priority { get; set; } = RequestPriority.Medium;
        public string? Category { get; set; }
        public decimal? EstimatedBudget { get; set; }
        public DateTime? RequestedDeadline { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public RequestSource Source { get; set; } = RequestSource.Email;
        public string? InternalNotes { get; set; }
        public string? Attachments { get; set; }
    }

    public class UpdateCustomerRequestDto : UpdateDtoBase
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public RequestType Type { get; set; }
        public RequestPriority Priority { get; set; }
        public RequestStatus Status { get; set; }
        public string? Category { get; set; }
        public decimal? EstimatedBudget { get; set; }
        public DateTime? RequestedDeadline { get; set; }
        public string? Resolution { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? InternalNotes { get; set; }
    }

    public class ConvertRequestToProjectDto
    {
        public Guid RequestId { get; set; }
        public string ProjectName { get; set; }
        public string? ProjectDescription { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? HourlyRate { get; set; }
        public string ProjectType { get; set; } = "General";
    }
}