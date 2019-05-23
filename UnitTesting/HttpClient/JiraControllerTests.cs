using Api.Common.Adapters;
using API.Common.Middleware;
using API.Common.Models;
using API.Contract.Models;
using ExternalTool.Api.Controllers;
using ExternalTool.DataAccess.Access;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Contract.Enums;
using API.Contract.Models.ExternalTool;
using Xunit;

namespace ExternalTool.Tests
{
    /// <summary>
    /// All tests to follow the naming convention: MethodName_StateUnderTest_ExpectedBehaviour
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class JiraControllerTests
    {
        private static Mock<IJiraAccess> _jiraAccess;
        private static Mock<ILoggerAdapter<ErrorWrappingMiddleware>> _logger;

        public JiraControllerTests()
        {
            _jiraAccess = new Mock<IJiraAccess>();
            _logger = new Mock<ILoggerAdapter<ErrorWrappingMiddleware>>();
        }

        [Fact]
        public async Task GetJiraWorkspaceEpicDetailsAsync_ResultsFound_ReturnsListOfSchemaDatabaseModels()
        {
            // Arrange 
            var queryModel = new JiraWorkspaceQueryModel();
            var resultModel = new JiraWorkspaceResultMasterModel
            {
                Issues = new List<JiraWorkspaceResultModel>
                {
                    new JiraWorkspaceResultModel
                    {
                        ApiCustomFields = new Dictionary<JiraApiCustomFieldTypeEnum, string>
                        {
                            {JiraApiCustomFieldTypeEnum.IR_JIRACUSTOMFIELDREF1, "IR_JIRACUSTOMFIELDREF1" }
                        }
                    }
                }
            };

            _jiraAccess.Setup(x => x.GetJiraWorkspaceEpicDetailsAsync(It.IsAny<JiraWorkspaceQueryModel>()))
                .ReturnsAsync(resultModel);

            // Act
            JiraController controller = new JiraController(_jiraAccess.Object);
            var result = await controller.GetJiraWorkspaceEpicDetailsAsync(queryModel);

            // Assert
            var resultValue = result.Value as ApiOkResponse;
            resultValue.Result.Should().BeOfType<JiraWorkspaceResultMasterModel>();
            (resultValue.Result.As<JiraWorkspaceResultMasterModel>()).Issues.First().ApiCustomFields.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetJiraWorkspaceDetailsAsync_AccessMethodThrowsError_LogErrorEventOccursAndErrorStatusCodeReturnedNoExceptionThrown()
        {
            // Arrange
            var queryModel = new JiraWorkspaceQueryModel();
            var exceptionToThrow = new Exception("exception");

            _jiraAccess.Setup(x => x.GetJiraWorkspaceDetailsAsync(It.IsAny<JiraWorkspaceQueryModel>()))
                .ThrowsAsync(exceptionToThrow);

            JiraController controller = new JiraController(_jiraAccess.Object);

            // Set up our Error Handling middleware as a wrapper (the same way it will wrap all incoming Requests)
            var middleware = new ErrorWrappingMiddleware(async (innerHttpContext) =>
            {
                var result = await controller.GetJiraWorkspaceDetailsAsync(queryModel);
            }, _logger.Object);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            //Act
            await middleware.Invoke(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var streamText = reader.ReadToEnd();
            var objResponse = JsonConvert.DeserializeObject<ApiResponse>(streamText);

            //Assert
            _logger.Verify(x =>
                x.LogError(exceptionToThrow, It.IsAny<string>()), Times.Once);

            objResponse
                .Should()
                .BeEquivalentTo(new ApiResponse(HttpStatusCode.InternalServerError, "exception"));

            context.Response.StatusCode
                .Should()
                .Be((int)HttpStatusCode.InternalServerError);
        }
    }
}
