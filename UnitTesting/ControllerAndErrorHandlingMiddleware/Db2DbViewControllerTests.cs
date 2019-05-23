using Api.Common.Adapters;
using API.Common.Middleware;
using API.Common.Models;
using API.Contract.Models;
using API.Contract.Models.Db2;
using Db2.Controllers;
using Db2.DataAccess.Access;
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
using Xunit;

namespace Db2.Tests
{
    /// <summary>
    /// All tests to follow the naming convention: MethodName_StateUnderTest_ExpectedBehaviour
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Db2DbViewControllerTests
    {
        private static Mock<IDb2DbViewAccess> _dbViewAccess;
        private static Mock<ILoggerAdapter<ErrorWrappingMiddleware>> _logger;

        private static readonly string TableNameUnderTest = "TESTU.POT_POLICY";

        public Db2DbViewControllerTests()
        {
            _dbViewAccess = new Mock<IDb2DbViewAccess>();
            _logger = new Mock<ILoggerAdapter<ErrorWrappingMiddleware>>();
        }

        [Fact]
        public async Task GetTableAsync_ResultsFound_ReturnsDynamicListOfResults()
        {
            // Arrange
            var apiQueryModel = new ApiQueryModel();

            var returnModel = new Tuple<bool, List<dynamic>>(false, new List<dynamic>());

            _dbViewAccess
                .Setup(x => x.GetTableAsync(It.IsAny<ApiQueryModel>()))
                .ReturnsAsync(() => returnModel);

            // Act
            Db2DbViewController controller = new Db2DbViewController(_dbViewAccess.Object);
            var result = await controller.GetTableAsync(apiQueryModel);

            // Assert
            var resultValue = result.Value as ApiOkResponse;
            var resultValueResult = resultValue.Result.As<Tuple<bool, List<dynamic>>>();
            resultValueResult.Item1.Should().Equals(false);
            resultValueResult.Item2.Should().BeOfType<List<dynamic>>();
        }

        [Fact]
        public async Task GetTableAsync_AccessMethodThrowsError_LogEventOccursAndErrorStatusCodeReturnedNoExceptionThrown()
        {
            // Arrange
            var exceptionToThrow = new Exception("exception");
            var apiQueryModel = new ApiQueryModel();

            _dbViewAccess.Setup(x => x.GetTableAsync(apiQueryModel))
                .ThrowsAsync(exceptionToThrow);

            Db2DbViewController controller = new Db2DbViewController(_dbViewAccess.Object);

            // Set up our Error Handling middleware as a wrapper (the same way it will wrap all incoming Requests)
            var middleware = new ErrorWrappingMiddleware(async (innerHttpContext) =>
            {
                var result = await controller.GetTableAsync(apiQueryModel);
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

        [Fact]
        public async Task GetTimestampAsync_ResultsFound_ReturnsListOfModels()
        {
            // Arrange
            var apiQueryModel = new ApiQueryModel();

            var returnModel = new Tuple<bool, List<DbViewTimestampReturnModel>>(true, new List<DbViewTimestampReturnModel>
            {
                new DbViewTimestampReturnModel()
            });

            _dbViewAccess
                .Setup(x => x.GetTimestampAsync(It.IsAny<ApiQueryModel>()))
                .ReturnsAsync(() => returnModel);

            // Act
            Db2DbViewController controller = new Db2DbViewController(_dbViewAccess.Object);
            var result = await controller.GetTimestampAsync(apiQueryModel);

            // Assert
            var resultValue = result.Value as ApiOkResponse;
            var resultValueResult = resultValue.Result.As<Tuple<bool, List<DbViewTimestampReturnModel>>>();
            resultValueResult.Item2.Count.Should().Equals(1);
            resultValueResult.Item2.First().Should().BeOfType<DbViewTimestampReturnModel>();
        }

        [Fact]
        public async Task GetTimestampAsync_AccessMethodThrowsError_LogEventOccursAndErrorStatusCodeReturnedNoExceptionThrown()
        {
            // Arrange
            var exceptionToThrow = new Exception("exception");
            var apiQueryModel = new ApiQueryModel();

            _dbViewAccess.Setup(x => x.GetTimestampAsync(apiQueryModel))
                .ThrowsAsync(exceptionToThrow);

            Db2DbViewController controller = new Db2DbViewController(_dbViewAccess.Object);

            // Set up our Error Handling middleware as a wrapper (the same way it will wrap all incoming Requests)
            var middleware = new ErrorWrappingMiddleware(async (innerHttpContext) =>
            {
                var result = await controller.GetTimestampAsync(apiQueryModel);
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

        [Fact]
        public async Task VerifyTableListInPartitionAsync_ResultsFound_ReturnsListOfModels()
        {
            // Arrange
            var queryModel = new DbViewTimestampTableListModel();

            var returnModel = new List<string>
            {
                "Result 1"
            };

            _dbViewAccess
                .Setup(x => x.VerifyTableListInPartitionAsync(It.IsAny<DbViewTimestampTableListModel>()))
                .ReturnsAsync(() => returnModel);

            // Act
            Db2DbViewController controller = new Db2DbViewController(_dbViewAccess.Object);
            var result = await controller.VerifyTableListInPartitionAsync(queryModel);

            // Assert
            var resultValue = result.Value as ApiOkResponse;
            var resultValueResult = resultValue.Result.As<List<string>>();
            resultValueResult.Count.Should().Equals(1);
            resultValueResult.First().Should().BeOfType<string>();
        }

        [Fact]
        public async Task VerifyTableListInPartitionAsync_AccessMethodThrowsError_LogEventOccursAndErrorStatusCodeReturnedNoExceptionThrown()
        {
            // Arrange
            var exceptionToThrow = new Exception("exception");
            var queryModel = new DbViewTimestampTableListModel();

            _dbViewAccess.Setup(x => x.VerifyTableListInPartitionAsync(queryModel))
                .ThrowsAsync(exceptionToThrow);

            Db2DbViewController controller = new Db2DbViewController(_dbViewAccess.Object);

            // Set up our Error Handling middleware as a wrapper (the same way it will wrap all incoming Requests)
            var middleware = new ErrorWrappingMiddleware(async (innerHttpContext) =>
            {
                var result = await controller.VerifyTableListInPartitionAsync(queryModel);
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

        [Fact]
        public async Task GetDynamicQueryAsync_ResultsFound_ReturnsListOfModels()
        {
            // Arrange
            var queryModel = "query string";

            var returnModel = new List<dynamic>
            {
                "Result 1"
            };

            _dbViewAccess
                .Setup(x => x.GetDynamicQueryAsync(It.IsAny<string>()))
                .ReturnsAsync(() => returnModel);

            // Act
            Db2DbViewController controller = new Db2DbViewController(_dbViewAccess.Object);
            var result = await controller.GetDynamicQueryAsync(queryModel);

            // Assert
            var resultValue = result.Value as ApiOkResponse;
            var resultValueResult = resultValue.Result.As<List<dynamic>>();
            resultValueResult.Count.Should().Equals(1);
        }

        [Fact]
        public async Task GetDynamicQueryAsync_AccessMethodThrowsError_LogEventOccursAndErrorStatusCodeReturnedNoExceptionThrown()
        {
            // Arrange
            var exceptionToThrow = new Exception("exception");
            var queryModel = "query string";

            _dbViewAccess.Setup(x => x.GetDynamicQueryAsync(queryModel))
                .ThrowsAsync(exceptionToThrow);

            Db2DbViewController controller = new Db2DbViewController(_dbViewAccess.Object);

            // Set up our Error Handling middleware as a wrapper (the same way it will wrap all incoming Requests)
            var middleware = new ErrorWrappingMiddleware(async (innerHttpContext) =>
            {
                var result = await controller.GetDynamicQueryAsync(queryModel);
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
