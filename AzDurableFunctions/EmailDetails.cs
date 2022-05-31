namespace AzDurableFunctions
{
    public class EmailDetails
    {
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }

    }
}