using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using FluentEmail.Core;
using System.Text.RegularExpressions;
using System.Text;
using System;
using FluentEmail.Core.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace OrionStudt.ContactFunction
{
    public class ContactFunction
    {
        private readonly SendGridSettings _settings;
        private readonly ISender _emailSender;
        private readonly ILogger<ContactFunction> _logger;

        public ContactFunction(
            IOptions<SendGridSettings> settingsAccessor,
            ISender emailSender,
            ILogger<ContactFunction> logger)
        {
            this._settings = settingsAccessor.Value;
            this._emailSender = emailSender;
            this._logger = logger;
        }

        [FunctionName(nameof(ContactFunction))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req)
        {
            if (req.ContentType != "application/json" || req.ContentLength == 0)
                return ImproperRequest();

            var json = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
                return ImproperRequest();

            Submission submission = null;
            try
            {
                submission = JsonSerializer.Deserialize<Submission>(json, options: new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Invalid JSON: {json}", json);
                return InvalidJson();
            }

            if (string.IsNullOrWhiteSpace(submission.From))
                return MissingParameter(nameof(submission.From));

            if (string.IsNullOrWhiteSpace(submission.Email))
                return MissingParameter(nameof(submission.Email));

            if (string.IsNullOrWhiteSpace(submission.Message))
                return MissingParameter(nameof(submission.Message));

            this._logger.LogInformation("From: {0}", submission.From);
            this._logger.LogInformation("Email: {0}", submission.Email);
            this._logger.LogInformation("Company: {0}", submission.GetCompany());
            this._logger.LogInformation("Message: {0}", submission.Message);

            if (!Regex.IsMatch(submission.Email, EmailRegex))
                return InvalidParameter("email");

            var content = new StringBuilder();
            content.AppendLine($"Contact Submission - {this._settings.Domain}");
            content.AppendLine("-------------------------------");
            content.AppendLine($"From: {submission.From}");
            content.AppendLine($"Email: {submission.Email}");
            content.AppendLine($"Company: {submission.GetCompany()}");
            content.AppendLine("-------------------------------");
            content.AppendLine(submission.Message);

            var subject = string.Format(this._settings.SubjectFormat, submission.From);
            var contact = Email
                .From(this._settings.FromAddress)
                .To(this._settings.ToAddress)
                .ReplyTo(submission.Email)
                .Subject(subject)
                .Body(content.ToString())
                .Tag(this._settings.Category);

            this._logger.LogInformation("Sending email [{0}] to [{1}]..", subject, this._settings.ToAddress);
            var response = await this._emailSender.SendAsync(contact, req.HttpContext.RequestAborted);
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

        private static BadRequestObjectResult ImproperRequest()
            => new(BadRequestDetails("JSON body expected."));

        private static BadRequestObjectResult InvalidJson()
            => new(BadRequestDetails("Invalid JSON."));

        private static ProblemDetails BadRequestDetails(string detail)
            => new()
            {
                Status = 400,
                Title = ReasonPhrases.GetReasonPhrase(400),
                Detail = detail,
                Type = "https://httpstatuses.com/400",
            };

        private class Submission
        {
            public string From { get; set; }

            public string Email { get; set; }

            public string Company { get; set; }

            public string Message { get; set; }

            public string GetCompany()
                => string.IsNullOrWhiteSpace(this.Company)
                ? "Not Provided"
                : this.Company;
        }
    }
}
