using API.Contract.Enums;
using API.Contract.Models.ExternalTool;
using ExternalTool.Common;
using ExternalTool.DataAccess.Access;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ExternalTool.Tests
{
    /// <summary>
    /// All tests to follow the naming convention: MethodName_StateUnderTest_ExpectedBehaviour
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class JiraAccessTests
    {
        private static Mock<IOptions<AppSettings>> _configuration;
        private static Mock<IHttpClientFactory> _httpClientFactoryMock;

        private readonly JiraWorkspaceQueryModel _queryModel;

        public JiraAccessTests()
        {
            _configuration = new Mock<IOptions<AppSettings>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();

            var appSettings = new AppSettings
            {
                JiraRestApiBaseUrlFormatted = "https://{0}/rest/api/2/search?jql={1}"
            };

            _configuration.Setup(x => x.Value).Returns(appSettings);

            _queryModel = new JiraWorkspaceQueryModel
            {
                WorkspaceKey = "BGT",
                EpicName = "EPICNAME1",
                InstanceName = "jiradummyinstance1.allstate.com",
                InstanceAdministratorUserName = "jiraDummyUser",
                InstanceAdministratorPassword = "RMUqxlEUHugFpZZjQLxN/g==",
                InstanceAdministratorPasswordSalt = "bLDo574ekWA="
            };
        }

        [Fact]
        public async Task GetJiraWorkspaceEpicDetailsAsync_RequiredWorkspaceKeyNotSupplied_ThrowsError()
        {
            // Arrange - take the WorkspaceKey out to cause that failure
            _queryModel.WorkspaceKey = null;

            // Act
            var jiraAccess = new JiraAccess(_configuration.Object, _httpClientFactoryMock.Object);

            // Act + Assert
            var exception = await Assert.ThrowsAsync<MissingFieldException>(async () => await jiraAccess.GetJiraWorkspaceEpicDetailsAsync(_queryModel));

            Assert.Equal(JiraAccess.WorkspaceKeyMissingException, exception.Message);
        }

        [Fact]
        public async Task GetJiraWorkspaceEpicDetailsAsync_RequiredInstanceAdminDetailsNotSupplied_ThrowsError()
        {
            // Remove some of the Auth params to cause failure
            _queryModel.InstanceAdministratorUserName = null;

            // Act
            var jiraAccess = new JiraAccess(_configuration.Object, _httpClientFactoryMock.Object);

            // Act + Assert
            var exception = await Assert.ThrowsAsync<MissingFieldException>(async () => await jiraAccess.GetJiraWorkspaceEpicDetailsAsync(_queryModel));

            Assert.Equal(JiraAccess.AuthenticationParamsMissingException, exception.Message);
        }

        [Fact]
        public async Task GetJiraWorkspaceEpicDetailsAsync_RequiredEpicNameCustomFieldNotSupplied_ThrowsError()
        {
            // Arrange - no custom field with EpicName supplied, should cause exception

            // Act
            var jiraAccess = new JiraAccess(_configuration.Object, _httpClientFactoryMock.Object);

            // Act + Assert
            var exception = await Assert.ThrowsAsync<MissingFieldException>(async () => await jiraAccess.GetJiraWorkspaceEpicDetailsAsync(_queryModel));

            Assert.Equal(JiraAccess.EpicNameCustomFieldMissingException, exception.Message);
        }

        [Fact]
        public async Task GetJiraWorkspaceDetailsAsync_RequiredEpicNameNotSupplied_ThrowsError()
        {
            // Act
            var jiraAccess = new JiraAccess(_configuration.Object, _httpClientFactoryMock.Object);

            // Act + Assert
            var exception = await Assert.ThrowsAsync<MissingFieldException>(async () => await jiraAccess.GetJiraWorkspaceDetailsAsync(_queryModel));

            Assert.Equal(JiraAccess.EpicLinkCustomFieldMissingException, exception.Message);
        }

        [Fact]
        public async Task GetJiraWorkspaceDetailsAsync_RequiredEpicLinkNotSupplied_ThrowsError()
        {
            // Arrange - take the EpicName out to cause that failure
            _queryModel.EpicName = null;

            // Act
            var jiraAccess = new JiraAccess(_configuration.Object, _httpClientFactoryMock.Object);

            // Act + Assert
            var exception = await Assert.ThrowsAsync<MissingFieldException>(async () => await jiraAccess.GetJiraWorkspaceDetailsAsync(_queryModel));

            Assert.Equal(JiraAccess.EpicNameMissingException, exception.Message);
        }

        [Fact]
        public async Task GetJiraWorkspaceEpicDetailsAsync_NoPaging_ReturnsCorrectNumberOfResultsFromApiCall()
        {
            // Arrange - set up the mock HttpClient to return the desired results model.
            var fakeHttpMessageHandler = new FakeHttpMessageHandler(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(GetDummyJira_EpicDetails_ApiResponse()), Encoding.UTF8, "application/json")
            });

            var fakeHttpClient = new HttpClient(fakeHttpMessageHandler);

            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(fakeHttpClient);

            // Add the required 'Epic Name' custom field to the query model.
            var customFieldForEpicName = "customfield_10007";
            _queryModel.ApiCustomFields.Add(new ApiCustomFieldModel
            {
                GenesysReference = JiraApiCustomFieldTypeEnum.IR_JIRACUSTOMFIELDREF2,
                ApiCustomFieldReference = customFieldForEpicName
            });

            // Act
            var jiraAccess = new JiraAccess(_configuration.Object, _httpClientFactoryMock.Object);
            var result = await jiraAccess.GetJiraWorkspaceEpicDetailsAsync(_queryModel);

            // Assert
            result.Issues.Count.Equals(2);
        }

        [Fact]
        public async Task GetJiraWorkspaceDetailsAsync_HappyPath_ReturnsCorrectNumberOfResultsFromApiCall()
        {
            // Arrange - set up the mock HttpClient to return the desired results model.
            var fakeHttpMessageHandler = new FakeHttpMessageHandler(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonConvert.SerializeObject(GetDummyJira_WorkspaceDetails_ApiResponse()), Encoding.UTF8, "application/json")
            });

            var fakeHttpClient = new HttpClient(fakeHttpMessageHandler);

            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(fakeHttpClient);

            // Add the required 'Epic Link' custom field to the query model.
            var customFieldForEpicLink = "customfield_10006";
            _queryModel.ApiCustomFields.Add(new ApiCustomFieldModel
            {
                GenesysReference = JiraApiCustomFieldTypeEnum.IR_JIRACUSTOMFIELDREF1,
                ApiCustomFieldReference = customFieldForEpicLink
            });

            // Add the optional 'Sprint Details' custom field to the query model.
            var customFieldForSprintDetails = "customfield_10005";
            _queryModel.ApiCustomFields.Add(new ApiCustomFieldModel
            {
                GenesysReference = JiraApiCustomFieldTypeEnum.IR_JIRACUSTOMFIELDREF3,
                ApiCustomFieldReference = customFieldForSprintDetails
            });

            // Add the optional 'Bug Severity' custom field to the query model.
            var customFieldForBugSeverity = "customfield_11403";
            _queryModel.ApiCustomFields.Add(new ApiCustomFieldModel
            {
                GenesysReference = JiraApiCustomFieldTypeEnum.IR_JIRACUSTOMFIELDREF4,
                ApiCustomFieldReference = customFieldForBugSeverity
            });

            // Act
            var jiraAccess = new JiraAccess(_configuration.Object, _httpClientFactoryMock.Object);
            var result = await jiraAccess.GetJiraWorkspaceDetailsAsync(_queryModel);

            // Assert
            result.Issues.Count.Equals(2);
        }

        [Fact]
        public async Task GetJiraWorkspaceDetailsAsync_ApiReturnsErrorMessage_MethodReturnsNotOkStatusCodeAndErrorMessageArray_DoesNotThrowError()
        {
            // Arrange - set up the mock HttpClient to return the desired results model.
            var errorResponseObject = new
            {
                errorMessages = new string[]
                {
                    "The value 'BGTxx' does not exist for the field 'project'."
                }
            };

            var fakeHttpMessageHandler = new FakeHttpMessageHandler(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(JsonConvert.SerializeObject(errorResponseObject), Encoding.UTF8, "application/json")
            });

            var fakeHttpClient = new HttpClient(fakeHttpMessageHandler);

            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(fakeHttpClient);

            // Add the required 'Epic Link' custom field to the query model.
            var customFieldForEpicLink = "customfield_10006";
            _queryModel.ApiCustomFields.Add(new ApiCustomFieldModel
            {
                GenesysReference = JiraApiCustomFieldTypeEnum.IR_JIRACUSTOMFIELDREF1,
                ApiCustomFieldReference = customFieldForEpicLink
            });

            // Add the optional 'Sprint Details' custom field to the query model.
            var customFieldForSprintDetails = "customfield_10005";
            _queryModel.ApiCustomFields.Add(new ApiCustomFieldModel
            {
                GenesysReference = JiraApiCustomFieldTypeEnum.IR_JIRACUSTOMFIELDREF3,
                ApiCustomFieldReference = customFieldForSprintDetails
            });

            // Add the optional 'Bug Severity' custom field to the query model.
            var customFieldForBugSeverity = "customfield_11403";
            _queryModel.ApiCustomFields.Add(new ApiCustomFieldModel
            {
                GenesysReference = JiraApiCustomFieldTypeEnum.IR_JIRACUSTOMFIELDREF4,
                ApiCustomFieldReference = customFieldForBugSeverity
            });

            // Act
            var jiraAccess = new JiraAccess(_configuration.Object, _httpClientFactoryMock.Object);
            var result = await jiraAccess.GetJiraWorkspaceDetailsAsync(_queryModel);

            // Assert
            result.ErrorMessages.Count.Equals(1);
            result.Issues.Count.Equals(0);
        }

        /// <summary>
        /// Dynamic object to represent the Json response structure we get from a Jira call, querying on 'issueType="Epic"' in the JQL query
        /// </summary>
        /// <returns></returns>
        private dynamic GetDummyJira_EpicDetails_ApiResponse()
        {
            dynamic jiraApiReturnObject = new
            {
                total = 2,
                issues = new[]
                {
                    new {
                        key = "BGT-312",
                        fields = new
                        {
                            customfield_10007 = "UINMS000002-R001"
                        }
                    },
                    new {
                        key = "BGT-313",
                        fields = new
                        {
                            customfield_10007 = "UINMS000002-R002"
                        }
                    }
                }
            };

            return jiraApiReturnObject;
        }

        /// <summary>
        /// Dynamic object to represent the Json response structure we get from a Jira call, querying for issues in a Workspace (what Jira calls a Project).
        /// </summary>
        /// <returns></returns>
        private dynamic GetDummyJira_WorkspaceDetails_ApiResponse()
        {
            dynamic jiraApiReturnObject = new ExpandoObject();
            jiraApiReturnObject.issues = new[]
            {
                new {
                    key = "BGT-312",
                    issuetype = "Story",
                    fields = new
                    {
                        customfield_10005 = new string[]
                        {
                            "com.atlassian.greenhopper.service.sprint.Sprint@513fa39e[id=9838,rapidViewId=<null>,state=ACTIVE,name=Sprint 40 - JIRA Admins/Status,startDate=2019-05-02T05:52:57.125-05:00,endDate=2019-05-17T05:52:00.000-05:00,completeDate=<null>,sequence=9838,goal=<null>]"
                        },
                        // Bug severity - not needed for a story
                        customfield_11403 = ""
                    }
                },
                new {
                    key = "BGT-313",
                    issuetype = "Bug",
                    fields = new
                    {
                        customfield_10005 = new string[]
                        {
                            "com.atlassian.greenhopper.service.sprint.Sprint@513fa39e[id=9999,rapidViewId=<null>,state=ACTIVE,name=Sprint XXX,startDate=2019-05-02T05:52:57.125-05:00,endDate=2019-05-17T05:52:00.000-05:00,completeDate=<null>,sequence=9838,goal=<null>]"
                        },
                        // Bug severity
                        customfield_11403 = "Major"
                    }
                }
            };

            return jiraApiReturnObject;
        }
    }
}
