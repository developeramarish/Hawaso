﻿using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Azunt.Web.Infrastructures.Auth;

/// <summary>
/// 테넌트 및 마스터 데이터베이스에 AspNetUsers 테이블을 생성 및 보강하고,
/// 필요한 컬럼들을 추가하는 클래스입니다. (KOR)
/// This class ensures the AspNetUsers table is created and extended in both tenant and master databases. (ENG)
/// </summary>
public class TenantSchemaEnhancerEnsureUsersTable
{
    private readonly string _masterConnectionString;
    private readonly ILogger<TenantSchemaEnhancerEnsureUsersTable> _logger;

    /// <summary>
    /// 생성자: 마스터 연결 문자열과 로깅 서비스를 주입받습니다. (KOR)
    /// Constructor: Injects master connection string and logger service. (ENG)
    /// </summary>
    public TenantSchemaEnhancerEnsureUsersTable(
        string masterConnectionString,
        ILogger<TenantSchemaEnhancerEnsureUsersTable> logger)
    {
        _masterConnectionString = masterConnectionString;
        _logger = logger;
    }

    /// <summary>
    /// 모든 테넌트 데이터베이스에 대해 AspNetUsers 테이블을 생성 또는 보강합니다. (KOR)
    /// Ensures the AspNetUsers table exists and is extended in all tenant databases. (ENG)
    /// </summary>
    public void EnhanceTenantDatabases()
    {
        var tenantConnectionStrings = GetTenantConnectionStrings();

        foreach (var connStr in tenantConnectionStrings)
        {
            try
            {
                EnsureUsersTable(connStr);
                _logger.LogInformation($"AspNetUsers table processed (tenant DB): {connStr}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{connStr}] Error processing tenant DB");
            }
        }
    }

    /// <summary>
    /// 마스터 데이터베이스에 대해 AspNetUsers 테이블을 생성 또는 보강합니다. (KOR)
    /// Ensures the AspNetUsers table exists and is extended in the master database. (ENG)
    /// </summary>
    public void EnhanceMasterDatabase()
    {
        try
        {
            EnsureUsersTable(_masterConnectionString);
            _logger.LogInformation("AspNetUsers table processed (master DB)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing master DB");
        }
    }

    /// <summary>
    /// 마스터 DB에서 테넌트 DB의 연결 문자열을 조회합니다. (KOR)
    /// Retrieves tenant DB connection strings from the master DB. (ENG)
    /// </summary>
    private List<string> GetTenantConnectionStrings()
    {
        var result = new List<string>();

        using (var connection = new SqlConnection(_masterConnectionString))
        {
            connection.Open();
            var cmd = new SqlCommand("SELECT ConnectionString FROM dbo.Tenants", connection);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var connectionString = reader["ConnectionString"]?.ToString();
                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        result.Add(connectionString);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 특정 DB 연결 문자열에 대해 AspNetUsers 테이블 존재 여부를 확인하고,
    /// 없으면 기본 구조를 생성하고, 확장 컬럼을 동적으로 추가합니다. (KOR)
    /// Ensures AspNetUsers table and its expected columns exist in the target DB. (ENG)
    /// </summary>
    private void EnsureUsersTable(string connectionString)
    {
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // 테이블 존재 여부 확인
            var cmdCheckTable = new SqlCommand(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = 'AspNetUsers'", connection);

            int tableExists = (int)cmdCheckTable.ExecuteScalar();

            // 테이블 생성
            if (tableExists == 0)
            {
                var createCmd = new SqlCommand(@"
                    CREATE TABLE [dbo].[AspNetUsers] (
                        [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                        [UserName] NVARCHAR(256) NULL,
                        [NormalizedUserName] NVARCHAR(256) NULL,
                        [Email] NVARCHAR(256) NULL,
                        [NormalizedEmail] NVARCHAR(256) NULL,
                        [EmailConfirmed] BIT NOT NULL,
                        [PasswordHash] NVARCHAR(MAX) NULL,
                        [SecurityStamp] NVARCHAR(MAX) NULL,
                        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
                        [PhoneNumber] NVARCHAR(MAX) NULL,
                        [PhoneNumberConfirmed] BIT NOT NULL,
                        [TwoFactorEnabled] BIT NOT NULL,
                        [LockoutEnd] DATETIMEOFFSET(7) NULL,
                        [LockoutEnabled] BIT NOT NULL,
                        [AccessFailedCount] INT NOT NULL
                    );

                    CREATE NONCLUSTERED INDEX [EmailIndex]
                    ON [dbo].[AspNetUsers]([NormalizedEmail] ASC);

                    CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex]
                    ON [dbo].[AspNetUsers]([NormalizedUserName] ASC)
                    WHERE ([NormalizedUserName] IS NOT NULL);
                ", connection);

                createCmd.ExecuteNonQuery();
                _logger.LogInformation("AspNetUsers table created.");
            }

            // 누락된 확장 컬럼 보강
            var expectedColumns = new Dictionary<string, string>
            {
                // 사용자 기본 정보
                ["FirstName"] = "NVARCHAR(256) NULL",
                ["LastName"] = "NVARCHAR(256) NULL",
                ["MiddleName"] = "NVARCHAR(35) NULL",
                ["AliasNames"] = "NVARCHAR(MAX) NULL",
                ["NameSuffix"] = "NVARCHAR(MAX) NULL",
                ["Address"] = "NVARCHAR(70) NULL",
                ["City"] = "NVARCHAR(70) NULL",
                ["County"] = "NVARCHAR(MAX) NULL",
                ["PostalCode"] = "NVARCHAR(35) NULL",
                ["State"] = "NVARCHAR(2) NULL",
                ["Timezone"] = "NVARCHAR(MAX) NULL",
                ["UsernameChangeLimit"] = "INT NULL DEFAULT(0)",
                ["Photo"] = "NVARCHAR(MAX) NULL",
                ["ProfilePicture"] = "VARBINARY(MAX) NULL",
                ["PersonalEmail"] = "NVARCHAR(254) NULL",

                // 출생 정보
                ["DOB"] = "NVARCHAR(MAX) NULL",
                ["Age"] = "INT NULL",
                ["BirthCity"] = "NVARCHAR(70) NULL",
                ["BirthState"] = "NVARCHAR(2) NULL",
                ["BirthCountry"] = "NVARCHAR(70) NULL",
                ["BirthCounty"] = "NVARCHAR(MAX) NULL",
                ["BirthPlace"] = "NVARCHAR(MAX) NULL",
                ["Gender"] = "NVARCHAR(35) NULL",
                ["MaritalStatus"] = "NVARCHAR(MAX) NULL",
                ["UsCitizen"] = "NVARCHAR(MAX) NULL",
                ["PhysicalMarks"] = "NVARCHAR(MAX) NULL",
                ["Height"] = "NVARCHAR(MAX) NULL",
                ["HeightFeet"] = "NVARCHAR(MAX) NULL",
                ["HeightInches"] = "NVARCHAR(MAX) NULL",
                ["Weight"] = "NVARCHAR(MAX) NULL",
                ["EyeColor"] = "NVARCHAR(MAX) NULL",
                ["HairColor"] = "NVARCHAR(MAX) NULL",

                // 연락처
                ["PrimaryPhone"] = "NVARCHAR(35) NULL",
                ["SecondaryPhone"] = "NVARCHAR(35) NULL",
                ["MobilePhone"] = "NVARCHAR(MAX) NULL",
                ["HomePhone"] = "NVARCHAR(MAX) NULL",
                ["WorkPhone"] = "NVARCHAR(MAX) NULL",
                ["WorkFax"] = "NVARCHAR(MAX) NULL",

                // 직장 및 비즈니스
                ["OfficeAddress"] = "NVARCHAR(MAX) NULL",
                ["OfficeCity"] = "NVARCHAR(MAX) NULL",
                ["OfficeState"] = "NVARCHAR(MAX) NULL",
                ["Department"] = "NVARCHAR(MAX) NULL",
                ["Title"] = "NVARCHAR(MAX) NULL",
                ["BusinessStructure"] = "NVARCHAR(MAX) NULL",
                ["BusinessStructureOther"] = "NVARCHAR(MAX) NULL",

                // 신분 및 인증 정보
                ["SSN"] = "NVARCHAR(MAX) NULL",
                ["DriverLicenseNumber"] = "NVARCHAR(35) NULL",
                ["DriverLicenseState"] = "NVARCHAR(2) NULL",
                ["DriverLicenseExpiration"] = "DATETIME2(7) NULL",
                ["LicenseNumber"] = "NVARCHAR(35) NULL",

                // 시스템/보안
                ["IsEnrollment"] = "BIT NOT NULL DEFAULT(0)",
                ["IsEnabled"] = "BIT NULL",
                ["IsPrimary"] = "BIT NULL",
                ["IsKodeeSupport"] = "BIT NULL",
                ["ConfidentialAccess"] = "BIT NULL",
                ["Group1Access"] = "BIT NULL",
                ["Group2Access"] = "BIT NULL",
                ["Group3Access"] = "BIT NULL",
                ["ShowStartUpMsg"] = "BIT NULL DEFAULT(0)",
                ["OpensToAppointments"] = "BIT NULL",
                ["LastInvitationSent"] = "DATETIME NULL",
                ["DateTimePasswordUpdated"] = "DATETIMEOFFSET(7) NULL",
                ["PswToOverwrite"] = "TINYINT NULL DEFAULT(1)",

                // 과거 비밀번호
                ["OldPsw1"] = "NVARCHAR(MAX) NULL",
                ["OldPsw2"] = "NVARCHAR(MAX) NULL",
                ["OldPsw3"] = "NVARCHAR(MAX) NULL",
                ["OldPsw4"] = "NVARCHAR(MAX) NULL",
                ["OldPsw5"] = "NVARCHAR(MAX) NULL",
                ["OldPsw6"] = "NVARCHAR(MAX) NULL",
                ["OldPsw7"] = "NVARCHAR(MAX) NULL",
                ["OldPsw8"] = "NVARCHAR(MAX) NULL",
                ["OldPsw9"] = "NVARCHAR(MAX) NULL",

                // 로그인 제약 정보
                ["IpAddress1"] = "NVARCHAR(MAX) NULL",
                ["IpAddress2"] = "NVARCHAR(MAX) NULL",
                ["LimitIP"] = "BIT NULL",
                ["RefreshToken"] = "NVARCHAR(MAX) NULL",
                ["RefreshTokenExpiryTime"] = "DATETIME NULL",

                // 멀티테넌시/권한 정보
                ["TenantID"] = "BIGINT NULL DEFAULT(0)",
                ["TenantName"] = "NVARCHAR(MAX) DEFAULT('Azunt')",
                ["RoleID"] = "BIGINT NULL",
                ["DivisionId"] = "BIGINT NULL DEFAULT(0)",
                ["DivisionName"] = "NVARCHAR(255) NULL DEFAULT('')",

                // 기타
                ["CriminalHistory"] = "NVARCHAR(MAX) NULL",
                ["Name"] = "NVARCHAR(MAX) NULL",
                ["RegistrationDate"] = "DATETIMEOFFSET NULL DEFAULT SYSDATETIMEOFFSET()",
                ["ShowInDropdown"] = "BIT NULL DEFAULT(0)",
                ["ConcurrencyToken"] = "ROWVERSION NULL"
            };

            foreach (var kvp in expectedColumns)
            {
                var columnName = kvp.Key;
                var columnType = kvp.Value;

                var cmdCheckColumn = new SqlCommand(@"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = @ColumnName", connection);
                cmdCheckColumn.Parameters.AddWithValue("@ColumnName", columnName);

                int columnExists = (int)cmdCheckColumn.ExecuteScalar();

                if (columnExists == 0)
                {
                    var alterCmd = new SqlCommand(
                        $"ALTER TABLE [dbo].[AspNetUsers] ADD [{columnName}] {columnType}", connection);
                    alterCmd.ExecuteNonQuery();

                    _logger.LogInformation($"Column added: {columnName} ({columnType})");
                }
            }
        }
    }

    /// <summary>
    /// Program.cs 또는 Startup.cs에서 호출되는 진입점입니다. (KOR)
    /// Entry point to be called from Program.cs or Startup.cs. (ENG)
    /// </summary>
    public static void Run(IServiceProvider services, bool forMaster)
    {
        try
        {
            var logger = services.GetRequiredService<ILogger<TenantSchemaEnhancerEnsureUsersTable>>();
            var config = services.GetRequiredService<IConfiguration>();
            var masterConnectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(masterConnectionString))
            {
                throw new InvalidOperationException("Master connection string 'DefaultConnection' is not configured.");
            }

            var enhancer = new TenantSchemaEnhancerEnsureUsersTable(masterConnectionString, logger);

            if (forMaster)
            {
                enhancer.EnhanceMasterDatabase();
            }
            else
            {
                enhancer.EnhanceTenantDatabases();
            }
        }
        catch (Exception ex)
        {
            var fallbackLogger = services.GetService<ILogger<TenantSchemaEnhancerEnsureUsersTable>>();
            fallbackLogger?.LogError(ex, "Error while processing AspNetUsers table.");
        }
    }
}
