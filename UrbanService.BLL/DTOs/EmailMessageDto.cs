namespace UrbanService.BLL.Dtos;

public class EmailMessageDto
{
    public IReadOnlyCollection<string> To { get; set; } = [];

    public IReadOnlyCollection<string> Cc { get; set; } = [];

    public IReadOnlyCollection<string> Bcc { get; set; } = [];

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;

    public bool IsHtml { get; set; } = true;

    public IReadOnlyCollection<EmailAttachmentDto> Attachments { get; set; } = [];
}

public class EmailAttachmentDto
{
    public string FileName { get; set; } = null!;

    public byte[] ContentBytes { get; set; } = [];

    public string ContentType { get; set; } = "application/octet-stream";
}
