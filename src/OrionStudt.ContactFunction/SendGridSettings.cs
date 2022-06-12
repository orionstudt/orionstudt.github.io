namespace OrionStudt.ContactFunction
{
    public class SendGridSettings
    {
        public string ApiKey { get; set; }

        public string Domain { get; set; }

        public string FromAddress { get; set; }

        public string ToAddress { get; set; }

        public string SubjectFormat { get; set; }

        public string Category { get; set; }
    }
}
