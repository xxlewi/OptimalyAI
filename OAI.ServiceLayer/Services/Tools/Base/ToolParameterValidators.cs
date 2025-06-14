using System;
using System.Text.RegularExpressions;

namespace OAI.ServiceLayer.Services.Tools.Base
{
    /// <summary>
    /// Common validation methods for tool parameters
    /// </summary>
    public static class ToolParameterValidators
    {
        /// <summary>
        /// Validates that a URL is well-formed and uses HTTP or HTTPS
        /// </summary>
        public static bool ValidateUrl(string url, out string error)
        {
            error = null;
            
            if (string.IsNullOrWhiteSpace(url))
            {
                error = "URL cannot be empty";
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                error = "URL is not a valid absolute URI";
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                error = "URL must use HTTP or HTTPS protocol";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that a required string parameter is not null or empty
        /// </summary>
        public static bool ValidateRequiredString(string value, string paramName, out string error)
        {
            error = null;
            
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"Parameter '{paramName}' is required and cannot be empty";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that a string parameter has a minimum length
        /// </summary>
        public static bool ValidateMinLength(string value, string paramName, int minLength, out string error)
        {
            error = null;
            
            if (string.IsNullOrEmpty(value))
            {
                error = $"Parameter '{paramName}' cannot be empty";
                return false;
            }

            if (value.Length < minLength)
            {
                error = $"Parameter '{paramName}' must be at least {minLength} characters long";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that a string parameter does not exceed maximum length
        /// </summary>
        public static bool ValidateMaxLength(string value, string paramName, int maxLength, out string error)
        {
            error = null;
            
            if (!string.IsNullOrEmpty(value) && value.Length > maxLength)
            {
                error = $"Parameter '{paramName}' cannot exceed {maxLength} characters";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that an integer parameter is within a specified range
        /// </summary>
        public static bool ValidateIntegerRange(int value, string paramName, int min, int max, out string error)
        {
            error = null;
            
            if (value < min || value > max)
            {
                error = $"Parameter '{paramName}' must be between {min} and {max}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates an email address format
        /// </summary>
        public static bool ValidateEmail(string email, out string error)
        {
            error = null;
            
            if (string.IsNullOrWhiteSpace(email))
            {
                error = "Email address cannot be empty";
                return false;
            }

            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
            if (!emailRegex.IsMatch(email))
            {
                error = "Email address format is invalid";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that a string contains only allowed characters
        /// </summary>
        public static bool ValidateAllowedCharacters(string value, string paramName, string allowedPattern, out string error)
        {
            error = null;
            
            if (string.IsNullOrEmpty(value))
            {
                return true; // Empty values are handled by required validation
            }

            var regex = new Regex(allowedPattern);
            if (!regex.IsMatch(value))
            {
                error = $"Parameter '{paramName}' contains invalid characters";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that a value is one of the allowed options
        /// </summary>
        public static bool ValidateAllowedValues(string value, string paramName, string[] allowedValues, out string error)
        {
            error = null;
            
            if (string.IsNullOrEmpty(value))
            {
                return true; // Empty values are handled by required validation
            }

            if (Array.IndexOf(allowedValues, value) == -1)
            {
                error = $"Parameter '{paramName}' must be one of: {string.Join(", ", allowedValues)}";
                return false;
            }

            return true;
        }
    }
}