using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using FluentEmail.SendGrid;
using Microsoft.Extensions.Configuration;
using FluentEmail.Core;
using System.Text.RegularExpressions;
using System.Text;
using System;

namespace OrionStudt.ContactFunction
{
    public static class ContactFunction
    {
        [FunctionName("ContactFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log,
            ExecutionContext context)
        {
            var query = req.GetQueryParameterDictionary();
            if (!query.TryGetValue("from", out var from))
                return MissingParameter("from");

            if (!query.TryGetValue("email", out var email))
                return MissingParameter("email");

            if (!query.TryGetValue("message", out var message))
                return MissingParameter("message");

            var company = query.TryGetValue("company", out var companyValue) ? companyValue : "Not Provided";
            log.LogInformation($"From: {from}");
            log.LogInformation($"Email: {email}");
            log.LogInformation($"Company: {company}");
            log.LogInformation($"Message: {message}");

            if (!Regex.IsMatch(email, EmailRegex))
                return InvalidParameter("email");

            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var content = new StringBuilder();
            content.AppendLine("Contact Submission - studt.me");
            content.AppendLine("-------------------------------");
            content.AppendLine($"From: {from}");
            content.AppendLine($"Email: {email}");
            content.AppendLine($"Company: {company}");
            content.AppendLine("-------------------------------");
            content.AppendLine(message);

            var settings = config.GetSection(nameof(SendGridSettings)).Get<SendGridSettings>();
            var sender = new SendGridSender(settings.ApiKey);

            Email.DefaultSender = sender;
            var contact = Email
                .From(settings.FromAddress)
                .To(settings.ToAddress)
                .ReplyTo(email)
                .Subject(string.Format(settings.SubjectFormat, from))
                .Body(content.ToString())
                .Tag("PersonalWebsiteContact");

            var response = await contact.SendAsync(req.HttpContext.RequestAborted);
            if (!response.Successful)
                throw new ApplicationException($"Failed to send email: {string.Join(", ", response.ErrorMessages)}");

            return new OkObjectResult("Email sent.");
        }

        private const string EmailRegex = @"^[a-zA-Z0-9.!#$%&'*+\/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$";

        private static BadRequestObjectResult MissingParameter(string parameterName)
        {
            var details = BadRequestDetails($"Missing parameter: {parameterName}");
            return new BadRequestObjectResult(details);
        }

        private static BadRequestObjectResult InvalidParameter(string parameterName)
        {
            var details = BadRequestDetails($"Invalid parameter: {parameterName}");
            return new BadRequestObjectResult(details);
        }

        private static ProblemDetails BadRequestDetails(string detail)
            => new()
            {
                Status = 400,
                Title = ReasonPhrases.GetReasonPhrase(400),
                Detail = detail,
                Type = "https://httpstatuses.com/400",
            };

        private class SendGridSettings
        {
            public string ApiKey { get; set; }

            public string FromAddress { get; set; }

            public string ToAddress { get; set; }

            public string SubjectFormat { get; set; }
        }
    }
}
