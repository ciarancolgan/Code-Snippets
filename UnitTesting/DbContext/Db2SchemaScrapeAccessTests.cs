using Api.Common.Adapters;
using API.Common.DataAccess;
using API.Common.Models;
using API.Common.Testing;
using API.Contract;
using Db2.DataAccess;
using Db2.DataAccess.Access;
using Db2.DataAccess.Helpers;
using Db2.Tests.TestModels;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Db2.Tests
{
    /// <summary>
    /// All tests to follow the naming convention: MethodName_StateUnderTest_ExpectedBehaviour
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Db2SchemaScrapeAccessTests
    {
        private static Mock<ILoggerAdapter<Db2SchemaScrapeAccess>> _logger;
        private readonly Mock<IDbConnectionManager<Db2Context>> _dbConnection;
        private readonly Mock<IDb2QueryHelper> _db2QueryHelper;
        private readonly TestInMemoryDatabase _testInMemoryDatabase;

        readonly Db2QueryHelper _db2QueryHelperConcrete = new Db2QueryHelper();

        private const string MatchingCreatorString = "TESTXX";
        private const string MatchingAliasCreatorString = "ALIASCREATOR";
        private const string MatchingLocationString = "HOMEDB2E";
        private const string MatchingDatabase1String = "DB1";
        private const string DatabaseNameNotInOurInternalDataSet = "DatabaseNameNotInOurInternalDataSet";

        public Db2SchemaScrapeAccessTests()
        {
            _logger = new Mock<ILoggerAdapter<Db2SchemaScrapeAccess>>();
            _dbConnection = new Mock<IDbConnectionManager<Db2Context>>();
            _db2QueryHelper = new Mock<IDb2QueryHelper>();

            // Using a SQL Lite in-memory database to test the DbContext. 
            _testInMemoryDatabase = new TestInMemoryDatabase();
            _testInMemoryDatabase.Insert(_listSchemaTables);
            _testInMemoryDatabase.Insert(_listSchemaColumns);

            _dbConnection.Setup(c => c.GetDbConnectionFromContextAsync()).ReturnsAsync(_testInMemoryDatabase.OpenConnection());

            // Arrange setup of the common Db2QueryHelper methods.           
            _db2QueryHelper.Setup(x =>
                    x.GetDb2SysTablesName(It.IsAny<string>()))
                // Name of SqlLite in-memory database
                .Returns("main.TestSchemaTableModel");

            _db2QueryHelper.Setup(x =>
                    x.GetDb2SysColumnsName(It.IsAny<string>()))
                // Name of SqlLite in-memory database
                .Returns("main.TestSchemaColumnModel");

            _db2QueryHelper.Setup(x => x.GetColumnDetailsForEnvironmentQueryFormatted(
                    "main.TestSchemaColumnModel"))
                // Return the actual value from this method - no need to mock it.
                .Returns(_db2QueryHelperConcrete.GetColumnDetailsForEnvironmentQueryFormatted(
                    "main.TestSchemaColumnModel"));
        }

        [Theory()]
        [InlineData(null, null)]
        [InlineData("", "")]
        public async Task GetDatabasesByCreatorAsync__RequiredParamsNotSupplied_ThrowsAppropriateError(string creatorString, string locationString)
        {
            // Arrange
            var dbSchemaScrapeAccess = new Db2SchemaScrapeAccess(_logger.Object, _dbConnection.Object, _db2QueryHelper.Object);

            // Act + Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(async () => await dbSchemaScrapeAccess.GetDatabasesByCreatorAsync(
                creatorString, locationString, new List<string>()));
        }

        [Fact]
        public async Task GetDatabasesByCreatorAsync_CorrectMatchingDatabaseNameSupplied_ReturnsCorrectNumberOfRows()
        {
            // Arrange
            var concreteWhitelistDatabaseNames = _db2QueryHelperConcrete.WhitelistDatabaseNamesToBeIngestedInSchemaAppendFilter(
                new List<string> { MatchingDatabase1String },
                SharedConstants.Db2SchemaScrape.Db2SystemDatabaseNameOfAliasTables);

            _db2QueryHelper.Setup(x => x.WhitelistDatabaseNamesToBeIngestedInSchemaAppendFilter(
                    It.IsAny<List<string>>(),
                    SharedConstants.Db2SchemaScrape.Db2SystemDatabaseNameOfAliasTables))
                .Returns(concreteWhitelistDatabaseNames);

            _db2QueryHelper.Setup(x => x.GetAliasTableDetailsWithinWhitelistedDatabaseNamesToMatch(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<List<string>>()))
                // Return the actual value from this method - no need to mock it.
                .Returns(_db2QueryHelperConcrete.GetAliasTableDetailsWithinWhitelistedDatabaseNamesToMatch(
                    "main.TestSchemaTableModel",
                    _listSchemaTables.Where(t => t.TYPE == RelevantTableTypes[0]).Select(t => t.NAME).ToList(),
                    new List<string> { MatchingDatabase1String }));

            _db2QueryHelper.Setup(x => x.GetTableDetailsForEnvironmentQueryFormatted(
                    "main.TestSchemaTableModel",
                    concreteWhitelistDatabaseNames,
                    string.Empty))
                // Return the actual value from this method - no need to mock it.
                .Returns(_db2QueryHelperConcrete.GetTableDetailsForEnvironmentQueryFormatted(
                    "main.TestSchemaTableModel",
                    concreteWhitelistDatabaseNames,
                    string.Empty));

            var schemaTablesThatMatchOurQuery = _listSchemaTables
                .Where(x => x.CREATOR == MatchingCreatorString && RelevantTableTypes.Contains(x.TYPE));

            var dbSchemaScrapeAccess = new Db2SchemaScrapeAccess(_logger.Object, _dbConnection.Object, _db2QueryHelper.Object);

            // Act
            var result = await dbSchemaScrapeAccess.GetDatabasesByCreatorAsync(
                MatchingCreatorString, MatchingLocationString, new List<string>{ MatchingDatabase1String });

            // Assert
            // There are multiple results loaded that match, but they have the same DBNAME, so should only be 1 distinct Database result returned.
            result.As<List<SchemaDatabaseModel>>().Should().Equals(schemaTablesThatMatchOurQuery.Select(x => x.DBNAME).Distinct());
        }

        [Fact]
        public async Task GetDatabasesByCreatorAsync_InCorrectMatchingDatabaseNameSupplied_ReturnsZeroRows()
        {
            // Arrange
            var concreteWhitelistDatabaseNames = _db2QueryHelperConcrete.WhitelistDatabaseNamesToBeIngestedInSchemaAppendFilter(
                new List<string> { DatabaseNameNotInOurInternalDataSet },
                SharedConstants.Db2SchemaScrape.Db2SystemDatabaseNameOfAliasTables);

            _db2QueryHelper.Setup(x => x.WhitelistDatabaseNamesToBeIngestedInSchemaAppendFilter(
                    It.IsAny<List<string>>(),
                    SharedConstants.Db2SchemaScrape.Db2SystemDatabaseNameOfAliasTables))
                .Returns(concreteWhitelistDatabaseNames);

            _db2QueryHelper.Setup(x => x.GetTableDetailsForEnvironmentQueryFormatted(
                    "main.TestSchemaTableModel",
                    concreteWhitelistDatabaseNames,
                    string.Empty))
                // Return the actual value from this method - no need to mock it.
                .Returns(_db2QueryHelperConcrete.GetTableDetailsForEnvironmentQueryFormatted(
                    "main.TestSchemaTableModel",
                    concreteWhitelistDatabaseNames,
                    string.Empty));            

            var dbSchemaScrapeAccess = new Db2SchemaScrapeAccess(_logger.Object, _dbConnection.Object, _db2QueryHelper.Object);

            // Act
            var result = await dbSchemaScrapeAccess.GetDatabasesByCreatorAsync(
                MatchingCreatorString, MatchingLocationString, new List<string> { DatabaseNameNotInOurInternalDataSet });

            // Assert
            // AS we are using a Database name not in our test dataset, should be 0 results.
            result.As<List<SchemaDatabaseModel>>().Count.Should().Be(0);
        }

        [Fact]
        public async Task GetTableRowCountAsync_ResultsFound_ReturnsCorrectNumberOfRows()
        {
            // Arrange
            var dbSchemaScrapeAccess = new Db2SchemaScrapeAccess(_logger.Object, _dbConnection.Object, _db2QueryHelper.Object);

            // Act
            var result = await dbSchemaScrapeAccess.GetTableRowCountAsync(nameof(TestSchemaTableModel));

            // Assert - this is simply the number of objects loaded into the 'TestSchemaTableModel' table. 
            result.As<long?>().Should().Equals(_listSchemaTables.Count);
        }

        [Fact]
        public async Task GetListOfDatabaseNamesInEnvironmentAsync_ResultsFound_ReturnsDistinctResultsForMatchingTypeAndCreator()
        {
            // Arrange
            var dbSchemaScrapeAccess = new Db2SchemaScrapeAccess(_logger.Object, _dbConnection.Object, _db2QueryHelper.Object);

            // Act
            var result = await dbSchemaScrapeAccess.GetListOfDatabaseNamesInEnvironmentAsync(MatchingCreatorString, MatchingLocationString);

            // Assert - should only return Distinct Database names that match Tables or Aliases with the correct Creator value.
            // There are 2 results loaded that match, but they have the same DBNAME, so should only be 1 distinct result returned.
            result.As<List<string>>().Count.Should().Equals(_listSchemaTables.Where(x => x.CREATOR == MatchingCreatorString && RelevantTableTypes.Contains(x.TYPE)).Distinct());
        }

        [Fact]
        public async Task BuildAliasTablesResultSetListForAllEnvironments_ResultsFound_ReturnsCorrectNumberOfRows()
        {
            // Arrange
            _db2QueryHelper.Setup(x => x.GetAliasTableDetailsWithinWhitelistedDatabaseNamesToMatch(
                    It.IsAny<string>(), 
                    It.IsAny<List<string>>(),
                    It.IsAny<List<string>>()))
                // Return the actual value from this method - no need to mock it.
                .Returns(_db2QueryHelperConcrete.GetAliasTableDetailsWithinWhitelistedDatabaseNamesToMatch(
                    "main.TestSchemaTableModel",
                    _listSchemaTables.Where(t => t.TYPE == RelevantTableTypes[0]).Select(t => t.NAME).ToList(),
                    new List<string> { MatchingDatabase1String }));            

            var dbSchemaScrapeAccess = new Db2SchemaScrapeAccess(_logger.Object, _dbConnection.Object, _db2QueryHelper.Object);

            // This is the return collection
            var returnAliasTableCollection = _listSchemaTables
                .Select(x => new SchemaTableModel
                {
                    Type = x.TYPE,
                    AliasTableLocation = x.LOCATION,
                    Name = x.NAME,
                    AliasTableName = x.NAME,
                    AliasTableEnvironment = MatchingAliasCreatorString,
                    DatabaseName = x.DBNAME
                })
                .ToList();

            try
            {
                // Act
                await dbSchemaScrapeAccess.BuildAliasTablesResultSetListForAllEnvironments(
                    MatchingLocationString,
                    new List<string> { MatchingDatabase1String },
                    returnAliasTableCollection,
                    _testInMemoryDatabase.OpenConnection(),
                    string.Empty);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            // Assert 
            returnAliasTableCollection.First().SchemaColumns.Count.Should().Be(1);
        }

        [Fact]
        public async Task BuildAliasTablesResultSetListForAllEnvironments_PhysicalTableExistsInEnvironmentWithSameNameAsAliasTable_MessageLoggedAndPhysicalPreferred()
        {
            // Arrange
            _db2QueryHelper.Setup(x => x.GetAliasTableDetailsWithinWhitelistedDatabaseNamesToMatch(
                    It.IsAny<string>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<List<string>>()))
                // Return the actual value from this method - no need to mock it.
                .Returns(_db2QueryHelperConcrete.GetAliasTableDetailsWithinWhitelistedDatabaseNamesToMatch(
                    "main.TestSchemaTableModel",
                    _listSchemaTables.Where(t => t.TYPE == RelevantTableTypes[0]).Select(t => t.NAME).ToList(),
                    new List<string> { MatchingDatabase1String }));

            // Insert another table with the same name into the in-memory database as the Alias table to act as the 'Physical' table.
            var newTestInMemoryDatabase = new TestInMemoryDatabase();
            _listSchemaTables.Add(new TestSchemaTableModel
            {
                Id = 0,
                NAME = "ALIASTABLE1",
                DBNAME = MatchingDatabase1String,
                // This is type = 'T' for physical Table.
                TYPE = RelevantTableTypes[1],
                CREATOR = MatchingCreatorString
            });

            // Insert a column into this physical table.
            _listSchemaColumns.Add(new TestSchemaColumnModel
            {
                Id = 100,
                NAME = "COLUMN1",
                TBNAME = "ALIASTABLE1",
                COLNO = "1",
                KEYSEQ = "1",
                LENGTH = "1",
                COLTYPE = "1",
                NULLS = "0",
                DEFAULT = "0",
                SCALE = "0",
                TBCREATOR = MatchingCreatorString
            });

            newTestInMemoryDatabase.Insert(_listSchemaTables);
            newTestInMemoryDatabase.Insert(_listSchemaColumns);

            var dbSchemaScrapeAccess = new Db2SchemaScrapeAccess(_logger.Object, _dbConnection.Object, _db2QueryHelper.Object);

            // This is the return collection
            var returnAliasTableCollection = _listSchemaTables
                .Select(x => new SchemaTableModel
                {
                    Type = x.TYPE,
                    AliasTableLocation = x.LOCATION,
                    Name = x.NAME,
                    AliasTableName = x.NAME,
                    AliasTableEnvironment = MatchingCreatorString,
                    DatabaseName = x.DBNAME,
                    SchemaColumns = new List<SchemaColumnModel>
                    {
                        new SchemaColumnModel
                        {
                            Name = ""
                        }
                    }
                })
                .ToList();

            try
            {
                // Act
                await dbSchemaScrapeAccess.BuildAliasTablesResultSetListForAllEnvironments(
                    MatchingLocationString,
                    new List<string> { MatchingDatabase1String },
                    returnAliasTableCollection,
                    newTestInMemoryDatabase.OpenConnection(),
                    string.Empty);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            // Assert 
            _logger.Verify(x => x.LogInformation(
                It.Is<string>(s => s.Contains("A physical Table with existing Columns was found in the original TablesResultSetMasterList")),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        private static readonly List<string> RelevantTableTypes = new List<string>
        {
            "A",
            "T",
            "G"
        };

        private readonly List<TestSchemaTableModel> _listSchemaTables = new List<TestSchemaTableModel>
        {
            new TestSchemaTableModel
            {
                Id = 1,
                NAME = "ALIASTABLE1",
                DBNAME = MatchingDatabase1String,
                TYPE = RelevantTableTypes[0],
                CREATOR = MatchingCreatorString,
                // An Alias table must have a TBCREATOR or else it wouldnt be an Alias.
                TBCREATOR = MatchingAliasCreatorString,
                LOCATION = "ALIASLOCATION1",
                TBNAME = "ALIASTABLE1"
            },
            new TestSchemaTableModel
            {
                Id = 2,
                NAME = "PHYSICALTABLE2",
                DBNAME = MatchingDatabase1String,
                TYPE = RelevantTableTypes[1],
                CREATOR = MatchingCreatorString
            },
            new TestSchemaTableModel
            {
                Id = 3,
                NAME = "TABLE1",
                DBNAME = "DB2_NOT_MATCH",
                TYPE = "TYPE_NOT_MATCH",
                CREATOR = MatchingCreatorString
            },
            new TestSchemaTableModel
            {
                Id = 4,
                NAME = "GLOBALTABLE1",
                DBNAME = "DB3_NOT_MATCH",
                TYPE = RelevantTableTypes[2],
                CREATOR = "CREATOR_NOT_MATCH"
            }
        };

        private readonly List<TestSchemaColumnModel> _listSchemaColumns = new List<TestSchemaColumnModel>
        {
            new TestSchemaColumnModel
            {
                Id = 1,
                NAME = "COLUMN1",
                TBNAME = "TABLE1",
                COLNO = "1",
                KEYSEQ = "1",
                LENGTH = "1",
                COLTYPE = "1",
                NULLS = "0",
                DEFAULT = "0",
                SCALE = "0",
                TBCREATOR = MatchingCreatorString
            },
            new TestSchemaColumnModel
            {
                Id = 2,
                NAME = "ALIASCOLUMN1",
                TBNAME = "ALIASTABLE1",
                COLNO = "1",
                KEYSEQ = "1",
                LENGTH = "1",
                COLTYPE = "1",
                NULLS = "0",
                DEFAULT = "0",
                SCALE = "0",
                TBCREATOR = MatchingAliasCreatorString
            }
        };
    }
}
