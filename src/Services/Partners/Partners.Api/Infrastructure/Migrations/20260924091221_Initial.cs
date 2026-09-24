using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partners.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:ltree", ",,");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    external_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    parent_external_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    path = table.Column<string>(type: "ltree", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.external_id);
                    table.CheckConstraint("ck_users_external_id_format", "external_id ~ '^[A-Za-z0-9_]{1,64}$'");
                    table.ForeignKey(
                        name: "fk_users_users_parent_external_id",
                        column: x => x.parent_external_id,
                        principalTable: "users",
                        principalColumn: "external_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_parent_external_id",
                table: "users",
                column: "parent_external_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_path",
                table: "users",
                column: "path")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
