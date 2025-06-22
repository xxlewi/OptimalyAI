namespace OAI.Core.DTOs
{
    /// <summary>
    /// Reprezentuje AI model pro zobrazení v dropdown seznamu
    /// </summary>
    public class ChatModelOptionDto
    {
        /// <summary>
        /// Název modelu (hodnota pro dropdown)
        /// </summary>
        public string Value { get; set; } = string.Empty;
        
        /// <summary>
        /// Zobrazovaný text v dropdownu (název modelu + server)
        /// </summary>
        public string Display { get; set; } = string.Empty;
        
        /// <summary>
        /// Typ serveru (Ollama, LMStudio, atd.)
        /// </summary>
        public string ServerType { get; set; } = string.Empty;
    }
}