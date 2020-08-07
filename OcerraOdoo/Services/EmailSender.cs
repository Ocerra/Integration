using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using MimeKit;
using OcerraOdoo.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.Services
{
    public class EmailSender
    {
        
        public EmailSender()
        {
            
        }

        private static BodyBuilder GetMessageBody(EmailOptions options)
        {
            var body = new BodyBuilder()
            {
                HtmlBody = options.Body,
            };
            
            if(options.FilePath != null)
                body.Attachments.Add(options.FilePath);
            
            return body;
        }

        private static MimeMessage GetMessage(EmailOptions options)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(options.From.Name, options.From.Email));
            
            foreach(var toAddress in options.To)
                message.To.Add(new MailboxAddress(toAddress.Name ?? string.Empty, toAddress.Email));
            
            message.Subject = options.Subject;
            message.Body = GetMessageBody(options).ToMessageBody();
            return message;
        }

        private static MemoryStream GetMessageStream(EmailOptions options)
        {
            var stream = new MemoryStream();
            GetMessage(options).WriteTo(stream);
            return stream;
        }

        public async Task<EmailResult> SendEmail(EmailOptions options)
        {

            // Replace USWest2 with the AWS Region you're using for Amazon SES.
            // Acceptable values are EUWest1, USEast1, and USWest2.
            using (var client = new AmazonSimpleEmailServiceClient(Settings.Default.AccessKey.FromBase64(), Settings.Default.SecretKey.FromBase64(), RegionEndpoint.USEast1))
            {
                var sendRequest = new SendRawEmailRequest { RawMessage = new RawMessage(GetMessageStream(options)) };
                try
                {
                    Console.WriteLine("Sending email using Amazon SES...");
                    var response = await client.SendRawEmailAsync(sendRequest);
                    return new EmailResult()
                    {
                        EmailId = response.MessageId,
                        StatusCode = response.HttpStatusCode.ToString()
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine("The email was not sent. " + ex.ToString());
                    return new EmailResult()
                    {
                        Error = ex.ToString(),
                        StatusCode = "500"
                    };
                }
            }
        }

        public async Task<EmailResult> SendEmailWithoutAttachment(EmailOptions options)
        {

            // Replace USWest2 with the AWS Region you're using for Amazon SES.
            // Acceptable values are EUWest1, USEast1, and USWest2.
            using (var client = new AmazonSimpleEmailServiceClient(Settings.Default.AccessKey.FromBase64(), Settings.Default.SecretKey.FromBase64(), RegionEndpoint.USEast1))
            {
                var sendRequest = new SendEmailRequest
                {
                    Source = options.From.ToString(),
                    Destination = new Destination
                    {
                        ToAddresses = options.To.Select(a => a.ToString()).ToList()
                    },
                    Message = new Message
                    {
                        Subject = new Content(options.Subject),
                        Body = new Body
                        {
                            Html = options.BodyHtml ? new Content
                            {
                                Charset = "UTF-8",
                                Data = options.Body
                            } : null,
                            Text = !options.BodyHtml ? new Content
                            {
                                Charset = "UTF-8",
                                Data = options.Body
                            } : null
                        },
                        
                    },
                    // If you are not using a configuration set, comment
                    // or remove the following line 
                    //ConfigurationSetName = configSet
                };
                try
                {
                    Console.WriteLine("Sending email using Amazon SES...");
                    var response = await client.SendEmailAsync(sendRequest);
                    return new EmailResult()
                    {
                        EmailId = response.MessageId,
                        StatusCode = response.HttpStatusCode.ToString()
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine("The email was not sent. " + ex.ToString());
                    return new EmailResult()
                    {
                        Error = ex.ToString(),
                        StatusCode = "500"
                    };
                }
            }
        }
    }

    public class EmailOptions
    {
        public EmailAddress From { get; set; }
        public ListEmailAddress To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public bool BodyHtml { get; set; }

        public string FilePath { get; set; }
    }

    public class EmailAddress
    {
        public EmailAddress()
        {

        }

        public EmailAddress(string email)
        {
            this.Email = email;
        }

        public EmailAddress(string name, string email)
        {
            this.Name = name;
            this.Email = email;

        }
        public string Name { get; set; }
        public string Email { get; set; }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(Name) ? $"{Name} <{Email}>" : Email;
        }
    }

    public class ListEmailAddress : List<EmailAddress>
    {
        public ListEmailAddress()
        {

        }

        public ListEmailAddress(EmailAddress address)
        {
            this.Add(address);
        }
    }

    public class EmailResult
    {
        public string EmailId { get; set; }
        public string StatusCode { get; set; }

        public string Error { get; set; }
    }
}
