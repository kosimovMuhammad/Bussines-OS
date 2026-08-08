using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasOne(c => c.Company)
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade - same reasoning as Department.ParentDepartment: deleting a
        // category with sub-categories must fail, not silently orphan them (the service
        // blocks this before it ever reaches the DB).
        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.CompanyId);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasOne(p => p.Company)
            .WithMany()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Restrict - deleting a category shouldn't be blocked by every product
        // under it; those products just become uncategorized (the service still blocks
        // deleting a category with sub-categories, but not one with products).
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(p => p.Price).HasColumnType("numeric");
        builder.Property(p => p.Cost).HasColumnType("numeric");

        builder.HasIndex(p => p.CompanyId);
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => new { p.CompanyId, p.Sku }).IsUnique();
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasOne(s => s.Company)
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.CompanyId);
    }
}

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        // DB-level backstop, not just the application-layer check in
        // InventoryService.AdjustStockAsync - closes the race window between two
        // concurrent stock-out requests both reading the same "before" quantity.
        builder.ToTable("inventories", t => t.HasCheckConstraint("ck_inventories_quantity_non_negative", "\"quantity\" >= 0"));

        builder.HasOne(i => i.Company)
            .WithMany()
            .HasForeignKey(i => i.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict - a product with stock history can't be deleted out from under it (the
        // service also blocks this explicitly; this is the DB-level backstop, same
        // reasoning as Contact->Deal).
        builder.HasOne(i => i.Product)
            .WithMany(p => p.InventoryRecords)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Branch)
            .WithMany()
            .HasForeignKey(i => i.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => i.CompanyId);

        // Two partial unique indexes, not one plain one - BranchId is nullable (companies
        // that don't use Branches get a single company-wide stock row per product), and a
        // plain unique index on (ProductId, BranchId) would let Postgres treat every NULL
        // BranchId as distinct, allowing duplicate branchless rows for the same product.
        builder.HasIndex(i => new { i.ProductId, i.BranchId })
            .IsUnique()
            .HasFilter("\"branchId\" IS NOT NULL");

        builder.HasIndex(i => i.ProductId)
            .IsUnique()
            .HasFilter("\"branchId\" IS NULL");
    }
}

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements");

        builder.HasOne(m => m.Company)
            .WithMany()
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Inventory)
            .WithMany(i => i.Movements)
            .HasForeignKey(m => m.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.CompanyId);
        builder.HasIndex(m => new { m.InventoryId, m.CreatedAt });
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.HasOne(o => o.Company)
            .WithMany()
            .HasForeignKey(o => o.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade - deleting a Supplier must not silently wipe out its
        // purchase order history, same reasoning as Contact->Deal.
        builder.HasOne(o => o.Supplier)
            .WithMany(s => s.PurchaseOrders)
            .HasForeignKey(o => o.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.CompanyId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => new { o.CompanyId, o.OrderNumber }).IsUnique();
    }
}

public class PurchaseOrderLineItemConfiguration : IEntityTypeConfiguration<PurchaseOrderLineItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLineItem> builder)
    {
        builder.ToTable("purchase_order_line_items");

        builder.HasOne(l => l.PurchaseOrder)
            .WithMany(o => o.LineItems)
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict - a product referenced by a purchase order's line items can't be
        // deleted out from under it, same reasoning as Inventory->Product.
        builder.HasOne(l => l.Product)
            .WithMany(p => p.PurchaseOrderLineItems)
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.UnitCost).HasColumnType("numeric");

        builder.HasIndex(l => l.PurchaseOrderId);
    }
}
