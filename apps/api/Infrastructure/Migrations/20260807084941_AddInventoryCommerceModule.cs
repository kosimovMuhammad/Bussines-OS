using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCommerceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    parentCategoryId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_categories_categories_parentCategoryId",
                        column: x => x.parentCategoryId,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_categories_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    contactName = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                    table.ForeignKey(
                        name: "FK_suppliers_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    cost = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    categoryId = table.Column<string>(type: "text", nullable: true),
                    unit = table.Column<int>(type: "integer", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_categories_categoryId",
                        column: x => x.categoryId,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_products_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    supplierId = table.Column<string>(type: "text", nullable: false),
                    orderNumber = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    orderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    receivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    currency = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_orders_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_supplierId",
                        column: x => x.supplierId,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventories",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    productId = table.Column<string>(type: "text", nullable: false),
                    branchId = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventories", x => x.id);
                    table.CheckConstraint("ck_inventories_quantity_non_negative", "\"quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_inventories_branches_branchId",
                        column: x => x.branchId,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventories_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventories_products_productId",
                        column: x => x.productId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_line_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    purchaseOrderId = table.Column<string>(type: "text", nullable: false),
                    productId = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unitCost = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_line_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_order_line_items_products_productId",
                        column: x => x.productId,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_line_items_purchase_orders_purchaseOrderId",
                        column: x => x.purchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    inventoryId = table.Column<string>(type: "text", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: true),
                    userId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_movements_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_movements_inventories_inventoryId",
                        column: x => x.inventoryId,
                        principalTable: "inventories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_movements_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_companyId",
                table: "categories",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_categories_parentCategoryId",
                table: "categories",
                column: "parentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_inventories_branchId",
                table: "inventories",
                column: "branchId");

            migrationBuilder.CreateIndex(
                name: "IX_inventories_companyId",
                table: "inventories",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_inventories_productId",
                table: "inventories",
                column: "productId",
                unique: true,
                filter: "\"branchId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventories_productId_branchId",
                table: "inventories",
                columns: new[] { "productId", "branchId" },
                unique: true,
                filter: "\"branchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_companyId",
                table: "inventory_movements",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_inventoryId_createdAt",
                table: "inventory_movements",
                columns: new[] { "inventoryId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_userId",
                table: "inventory_movements",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_products_categoryId",
                table: "products",
                column: "categoryId");

            migrationBuilder.CreateIndex(
                name: "IX_products_companyId",
                table: "products",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_products_companyId_sku",
                table: "products",
                columns: new[] { "companyId", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_line_items_productId",
                table: "purchase_order_line_items",
                column: "productId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_line_items_purchaseOrderId",
                table: "purchase_order_line_items",
                column: "purchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_companyId",
                table: "purchase_orders",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_companyId_orderNumber",
                table: "purchase_orders",
                columns: new[] { "companyId", "orderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_status",
                table: "purchase_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplierId",
                table: "purchase_orders",
                column: "supplierId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_companyId",
                table: "suppliers",
                column: "companyId");

            // --- Permission catalog expansion for Phase 4's 5 new modules (Products,
            // Categories, Inventory, Suppliers, PurchaseOrders), same NOT EXISTS-guarded
            // pattern as AddPermissionGuardsPhase/AddSecurityAndVisibilityPhase/AddCompanySettings.
            migrationBuilder.Sql(@"
                INSERT INTO ""permissions"" (""id"", ""key"", ""module"", ""description"")
                SELECT gen_random_uuid()::text, lower(m.module) || '.' || a.action, m.module, initcap(a.action) || ' ' || m.module
                FROM (VALUES ('Products'), ('Categories'), ('Inventory'), ('Suppliers'), ('PurchaseOrders')) AS m(module)
                CROSS JOIN (VALUES ('read'), ('create'), ('update'), ('delete')) AS a(action)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""permissions"" p WHERE p.""key"" = lower(m.module) || '.' || a.action
                );
            ");

            // Owner/Admin get every new permission, same as every other module.
            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Owner', 'Admin') AND p.""module"" IN ('Products', 'Categories', 'Inventory', 'Suppliers', 'PurchaseOrders')
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");

            // Manager/Member/ReadOnly get read-only, same default-read-only convention as
            // every other non-Audit module.
            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Manager', 'Member', 'ReadOnly')
                  AND p.""module"" IN ('Products', 'Categories', 'Inventory', 'Suppliers', 'PurchaseOrders') AND p.""key"" LIKE '%.read'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""role_permissions"" WHERE ""permissionId"" IN (
                    SELECT ""id"" FROM ""permissions"" WHERE ""module"" IN ('Products', 'Categories', 'Inventory', 'Suppliers', 'PurchaseOrders')
                );
                DELETE FROM ""permissions"" WHERE ""module"" IN ('Products', 'Categories', 'Inventory', 'Suppliers', 'PurchaseOrders');
            ");

            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "purchase_order_line_items");

            migrationBuilder.DropTable(
                name: "inventories");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
