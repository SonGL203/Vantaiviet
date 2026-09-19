using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VantaiViet.CoreApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityKyc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "RegistrationApplications",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PhoneVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "InProgress"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationApplications", x => x.Id);
                    table.CheckConstraint("CK_RegistrationApplications_1", "\"PhoneNumber\" ~ '^\\+[1-9][0-9]{7,14}$'");
                    table.CheckConstraint("CK_RegistrationApplications_2", "\"Status\" IN ('InProgress','Completed','Expired','Cancelled')");
                    table.CheckConstraint("CK_RegistrationApplications_3", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.CheckConstraint("CK_RegistrationApplications_4", "(\"Status\" = 'Completed') = (\"CompletedAt\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationSessions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RegistrationApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationSessions", x => x.Id);
                    table.CheckConstraint("CK_RegistrationSessions_1", "octet_length(\"TokenHash\") = 32");
                    table.CheckConstraint("CK_RegistrationSessions_2", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_RegistrationSessions_RegistrationApplications_RegistrationA~",
                        column: x => x.RegistrationApplicationId,
                        principalSchema: "public",
                        principalTable: "RegistrationApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RegistrationApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AccountStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    SecurityStamp = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Failures", "\"AccessFailedCount\" >= 0");
                    table.CheckConstraint("CK_Users_Name", "length(btrim(\"DisplayName\")) > 0");
                    table.CheckConstraint("CK_Users_Password", "length(btrim(\"PasswordHash\")) > 0");
                    table.CheckConstraint("CK_Users_Phone", "\"PhoneNumber\" ~ '^\\+[1-9][0-9]{7,14}$'");
                    table.CheckConstraint("CK_Users_Status", "\"AccountStatus\" IN ('Active','Suspended','Disabled')");
                    table.ForeignKey(
                        name: "FK_Users_RegistrationApplications_RegistrationApplicationId",
                        column: x => x.RegistrationApplicationId,
                        principalSchema: "public",
                        principalTable: "RegistrationApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "public",
                        principalTable: "Roles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.CheckConstraint("CK_AuditEvents_1", "\"Outcome\" IN ('Succeeded','Failed','Denied')");
                    table.ForeignKey(
                        name: "FK_AuditEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AuthSessions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RefreshTokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSessions", x => x.Id);
                    table.CheckConstraint("CK_AuthSessions_1", "octet_length(\"RefreshTokenHash\") = 32");
                    table.CheckConstraint("CK_AuthSessions_2", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_AuthSessions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KycApplications",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RegistrationApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RejectionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycApplications", x => x.Id);
                    table.CheckConstraint("CK_KycApplications_1", "\"Status\" IN ('Draft','Submitted','UnderReview','Approved','Rejected')");
                    table.CheckConstraint("CK_KycApplications_2", "\"Status\" = 'Draft' OR \"SubmittedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_KycApplications_3", "\"Status\" NOT IN ('Approved','Rejected') OR (\"ReviewedAt\" IS NOT NULL AND \"ReviewerReference\" IS NOT NULL AND length(btrim(\"ReviewerReference\")) > 0)");
                    table.CheckConstraint("CK_KycApplications_4", "\"Status\" <> 'Rejected' OR (\"RejectionCode\" IS NOT NULL AND length(btrim(\"RejectionCode\")) > 0)");
                    table.CheckConstraint("CK_KycApplications_5", "\"ReviewedAt\" IS NULL OR (\"SubmittedAt\" IS NOT NULL AND \"ReviewedAt\" >= \"SubmittedAt\")");
                    table.CheckConstraint("CK_KycApplications_6", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_KycApplications_RegistrationApplications_RegistrationApplic~",
                        column: x => x.RegistrationApplicationId,
                        principalSchema: "public",
                        principalTable: "RegistrationApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KycApplications_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "public",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "public",
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VerificationChallenges",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RegistrationApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Destination = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CodeHmac = table.Column<byte[]>(type: "bytea", nullable: false),
                    HmacKeyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationChallenges", x => x.Id);
                    table.CheckConstraint("CK_VerificationChallenges_1", "(\"RegistrationApplicationId\" IS NOT NULL) <> (\"UserId\" IS NOT NULL)");
                    table.CheckConstraint("CK_VerificationChallenges_2", "(\"RegistrationApplicationId\" IS NOT NULL AND \"Purpose\" = 'RegistrationPhone') OR (\"UserId\" IS NOT NULL AND \"Purpose\" IN ('PasswordReset','VerifyEmail'))");
                    table.CheckConstraint("CK_VerificationChallenges_3", "octet_length(\"CodeHmac\") = 32");
                    table.CheckConstraint("CK_VerificationChallenges_4", "\"MaxAttempts\" > 0 AND \"AttemptCount\" BETWEEN 0 AND \"MaxAttempts\"");
                    table.CheckConstraint("CK_VerificationChallenges_5", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_VerificationChallenges_RegistrationApplications_Registratio~",
                        column: x => x.RegistrationApplicationId,
                        principalSchema: "public",
                        principalTable: "RegistrationApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VerificationChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KycDocuments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeleteAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycDocuments", x => x.Id);
                    table.CheckConstraint("CK_KycDocuments_1", "\"DocumentType\" IN ('IdentityFront','IdentityBack','Selfie')");
                    table.CheckConstraint("CK_KycDocuments_2", "length(btrim(\"StorageKey\")) > 0");
                    table.CheckConstraint("CK_KycDocuments_3", "\"SizeBytes\" > 0");
                    table.CheckConstraint("CK_KycDocuments_4", "\"DeleteAfter\" IS NULL OR \"DeleteAfter\" > \"UploadedAt\"");
                    table.CheckConstraint("CK_KycDocuments_5", "\"DeletedAt\" IS NULL OR \"DeletedAt\" >= \"UploadedAt\"");
                    table.ForeignKey(
                        name: "FK_KycDocuments_KycApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "public",
                        principalTable: "KycApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KycIdentityDetails",
                schema: "public",
                columns: table => new
                {
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    IdentityNumberEncrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    EncryptionKeyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdentityNumberHmac = table.Column<byte[]>(type: "bytea", nullable: false),
                    HmacKeyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycIdentityDetails", x => x.ApplicationId);
                    table.CheckConstraint("CK_KycIdentityDetails_1", "length(btrim(\"FullName\")) > 0");
                    table.CheckConstraint("CK_KycIdentityDetails_2", "octet_length(\"IdentityNumberEncrypted\") > 0");
                    table.CheckConstraint("CK_KycIdentityDetails_3", "octet_length(\"IdentityNumberHmac\") = 32");
                    table.CheckConstraint("CK_KycIdentityDetails_4", "\"IssuedOn\" IS NULL OR \"IssuedOn\" >= \"DateOfBirth\"");
                    table.CheckConstraint("CK_KycIdentityDetails_5", "\"ExpiresOn\" IS NULL OR \"IssuedOn\" IS NULL OR \"ExpiresOn\" >= \"IssuedOn\"");
                    table.ForeignKey(
                        name: "FK_KycIdentityDetails_KycApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "public",
                        principalTable: "KycApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Actor",
                schema: "public",
                table: "AuditEvents",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Target",
                schema: "public",
                table: "AuditEvents",
                columns: new[] { "TargetType", "TargetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "AuthSessions_RefreshTokenHash_key",
                schema: "public",
                table: "AuthSessions",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_Family",
                schema: "public",
                table: "AuthSessions",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_User",
                schema: "public",
                table: "AuthSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Kyc_Reviewer",
                schema: "public",
                table: "KycApplications",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Kyc_ReviewQueue",
                schema: "public",
                table: "KycApplications",
                columns: new[] { "Status", "SubmittedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_Kyc_ActiveApplication",
                schema: "public",
                table: "KycApplications",
                column: "RegistrationApplicationId",
                unique: true,
                filter: "\"Status\" IN ('Draft','Submitted','UnderReview','Approved')");

            migrationBuilder.CreateIndex(
                name: "KycDocuments_ApplicationId_DocumentType_key",
                schema: "public",
                table: "KycDocuments",
                columns: new[] { "ApplicationId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "KycDocuments_StorageKey_key",
                schema: "public",
                table: "KycDocuments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KycIdentity_IdentityNumber",
                schema: "public",
                table: "KycIdentityDetails",
                columns: new[] { "HmacKeyId", "IdentityNumberHmac" });

            migrationBuilder.CreateIndex(
                name: "UX_Registration_ActivePhone",
                schema: "public",
                table: "RegistrationApplications",
                column: "PhoneNumber",
                unique: true,
                filter: "\"Status\" = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationSessions_Application",
                schema: "public",
                table: "RegistrationSessions",
                column: "RegistrationApplicationId");

            migrationBuilder.CreateIndex(
                name: "RegistrationSessions_TokenHash_key",
                schema: "public",
                table: "RegistrationSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "public",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "Roles_NormalizedName_key",
                schema: "public",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "public",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "public",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "public",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "Users_PhoneNumber_key",
                schema: "public",
                table: "Users",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Users_RegistrationApplicationId_key",
                schema: "public",
                table: "Users",
                column: "RegistrationApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_NormalizedEmail",
                schema: "public",
                table: "Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Users_NormalizedUserName",
                schema: "public",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_Registration",
                schema: "public",
                table: "VerificationChallenges",
                columns: new[] { "RegistrationApplicationId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_User",
                schema: "public",
                table: "VerificationChallenges",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });
            migrationBuilder.Sql(InitialRegistrationSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InitialRegistrationSql.Down);
            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AuthSessions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "KycDocuments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "KycIdentityDetails",
                schema: "public");

            migrationBuilder.DropTable(
                name: "RegistrationSessions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "public");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "public");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "public");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "VerificationChallenges",
                schema: "public");

            migrationBuilder.DropTable(
                name: "KycApplications",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "public");

            migrationBuilder.DropTable(
                name: "RegistrationApplications",
                schema: "public");
        }
    }
}

