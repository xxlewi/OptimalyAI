namespace OAI.Core.Interfaces.Tools
{
    /// <summary>
    /// Defines a dependency between parameters
    /// </summary>
    public class ParameterDependency
    {
        /// <summary>
        /// Name of the parameter this depends on
        /// </summary>
        public string DependsOn { get; set; }
        
        /// <summary>
        /// Value the dependent parameter must have
        /// </summary>
        public object WhenValue { get; set; }
        
        /// <summary>
        /// Whether this parameter is visible when the dependency is met
        /// </summary>
        public bool IsVisible { get; set; } = true;
        
        /// <summary>
        /// Whether this parameter is required when the dependency is met
        /// </summary>
        public bool IsRequired { get; set; } = false;
    }
}