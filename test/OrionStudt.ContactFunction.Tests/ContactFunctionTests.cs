using FluentEmail.Core;
using FluentEmail.Core.Interfaces;
using FluentEmail.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OrionStudt.ContactFunction.Tests
{
    [Trait("Type", "Unit")]
    public class ContactFunctionTests
    {
        private static readonly string Domain = "example.com";
        private static readonly string FromAddress = "noreply@example.com";
        private static readonly string ToAddress = "inbox@example.com";
        private static readonly string SubjectFormat = "Test - {0}";
        private static readonly string Category = "Test";

        private static ContactFunction SetupFunction(Action<IFluentEmail>? validationAction = null)
        {
            if (validationAction is null)
                validationAction = email => { };

            var services = new ServiceCollection()
                .AddLogging()
                .Configure<SendGridSettings>(settings =>
                {
                    settings.ApiKey = "doesnt_matter";
                    settings.Domain = Domain;
                    settings.FromAddress = FromAddress;
                    settings.ToAddress = ToAddress;
                    settings.SubjectFormat = SubjectFormat;
                    settings.Category = Category;
                })
                .AddSingleton(validationAction)
                .AddSingleton<ISender, MockSender>()
                .AddSingleton<ContactFunction>()
                .BuildServiceProvider();

            return services.GetRequiredService<ContactFunction>();
        }

        private static HttpRequest SetupRequest(Submission? submission = null)
        {
            var request = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Method = "POST",
                ContentType = "application/json",
            };

            if (submission is not null)
            {
                var options = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };

                var json = JsonSerializer.Serialize(submission, options);
                var stream = new MemoryStream();
                var streamWriter = new StreamWriter(stream);
                streamWriter.Write(json);
                streamWriter.Flush();
                stream.Position = 0;
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                request.Body = stream;
                request.ContentLength = stream.Length;
            }

            return request;
        }

        private static Submission SetupSubmission(
            string from = "John Smith",
            string email = "dummy@example.com",
            string? company = null,
            string message = "Hello World.")
            => new(from, email, company, message);

        [Fact]
        public async Task Submit_without_body_fails()
        {
            var request = SetupRequest();
            var function = SetupFunction();
            var response = await function.Run(request);
            Assert.IsType<BadRequestObjectResult>(response);
        }

        [Fact]
        public async Task Submit_without_From_fails()
        {
            var submission = SetupSubmission(from: null!);
            var request = SetupRequest(submission);
            var function = SetupFunction();
            var response = await function.Run(request);
            Assert.IsType<BadRequestObjectResult>(response);
        }

        [Fact]
        public async Task Submit_without_Email_fails()
        {
            var submission = SetupSubmission(email: null!);
            var request = SetupRequest(submission);
            var function = SetupFunction();
            var response = await function.Run(request);
            Assert.IsType<BadRequestObjectResult>(response);
        }

        [Fact]
        public async Task Submit_without_Message_fails()
        {
            var submission = SetupSubmission(message: null!);
            var request = SetupRequest(submission);
            var function = SetupFunction();
            var response = await function.Run(request);
            Assert.IsType<BadRequestObjectResult>(response);
        }

        [Fact]
        public async Task Submit_invalid_Email_fails()
        {
            var submission = SetupSubmission(email: "not-valid");
            var request = SetupRequest(submission);
            var function = SetupFunction();
            var response = await function.Run(request);
            Assert.IsType<BadRequestObjectResult>(response);
        }
        
        [Fact]
        public async Task Submit_succeeds()
        {
            var submission = SetupSubmission(
                from: "John Smith",
                email: "johnsmith@example.com",
                company: "Apple Tree",
                message: "Hello there from New York.");

            var request = SetupRequest(submission);
            var function = SetupFunction(email =>
            {
                var data = email.Data;
                Assert.Equal(FromAddress, data.FromAddress.EmailAddress);
                Assert.Single(data.ToAddresses);
                Assert.Equal(ToAddress, data.ToAddresses.First().EmailAddress);
                var expectedSubject = string.Format(SubjectFormat, submission.From);
                Assert.Equal(expectedSubject, data.Subject);
                Assert.Single(data.Tags);
                Assert.Equal(Category, data.Tags.First());
                Assert.Contains(submission.Company, data.Body);
                Assert.Contains(submission.Message, data.Body);
            });

            var response = await function.Run(request);
            Assert.IsType<OkObjectResult>(response);
        }

        private class Submission
        {
            public string From { get; }

            public string Email { get; }

            public string? Company { get; }

            public string Message { get; }

            public Submission(
                string from,
                string email,
                string? company,
                string message)
            {
                this.From = from;
                this.Email = email;
                this.Company = company;
                this.Message = message;
            }
        }

        private class MockSender : ISender
        {
            public bool HasSentEmail { get; private set; }

            public int EmailsSent { get; private set; }

            private readonly Action<IFluentEmail> _validationAction;

            public MockSender(Action<IFluentEmail> validationAction)
            {
                this._validationAction = validationAction;
            }

            public SendResponse Send(IFluentEmail email, CancellationToken? token = null)
            {
                this.HasSentEmail = true;
                this._validationAction(email);
                this.EmailsSent++;
                return new SendResponse
                {
                    ErrorMessages = new List<string>(),
                    MessageId = Guid.NewGuid().ToString(),
                };
            }

            public Task<SendResponse> SendAsync(IFluentEmail email, CancellationToken? token = null)
                => Task.FromResult(this.Send(email, token));
        }
    }
}
