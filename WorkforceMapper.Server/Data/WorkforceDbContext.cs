using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Data;

public class WorkforceDbContext(DbContextOptions<WorkforceDbContext> options) : DbContext(options)
{
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductApplication> ProductApplications => Set<ProductApplication>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectProduct> ProjectProducts => Set<ProjectProduct>();
    public DbSet<Mapping> Mappings => Set<Mapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mapping>().HasKey(m => new { m.ResourceId, m.ActivityId, m.Month });
        modelBuilder.Entity<ProductApplication>().HasKey(pa => new { pa.ProductId, pa.ApplicationId });
        modelBuilder.Entity<ProjectProduct>().HasKey(pp => new { pp.ProjectId, pp.ProductId });

        modelBuilder.Entity<Resource>().HasIndex(r => r.ResourceId).IsUnique();

        modelBuilder.Entity<Mapping>()
            .HasOne(m => m.Resource).WithMany(r => r.Mappings).HasForeignKey(m => m.ResourceId);
        modelBuilder.Entity<Mapping>()
            .HasOne(m => m.Activity).WithMany(a => a.Mappings).HasForeignKey(m => m.ActivityId);

        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Application).WithMany(app => app.Activities)
            .HasForeignKey(a => a.ApplicationId).IsRequired(false);
        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Project).WithMany(p => p.Activities)
            .HasForeignKey(a => a.ProjectId).IsRequired(false);

        modelBuilder.Entity<ProductApplication>()
            .HasOne(pa => pa.Product).WithMany(p => p.ProductApplications).HasForeignKey(pa => pa.ProductId);
        modelBuilder.Entity<ProductApplication>()
            .HasOne(pa => pa.Application).WithMany(a => a.ProductApplications).HasForeignKey(pa => pa.ApplicationId);

        modelBuilder.Entity<ProjectProduct>()
            .HasOne(pp => pp.Project).WithMany(p => p.ProjectProducts).HasForeignKey(pp => pp.ProjectId);
        modelBuilder.Entity<ProjectProduct>()
            .HasOne(pp => pp.Product).WithMany(p => p.ProjectProducts).HasForeignKey(pp => pp.ProductId);

        SeedApplications(modelBuilder);
        SeedProducts(modelBuilder);
        SeedProductApplications(modelBuilder);
        SeedProjects(modelBuilder);
        SeedProjectProducts(modelBuilder);
        SeedActivities(modelBuilder);
    }

    private static void SeedApplications(ModelBuilder m) =>
        m.Entity<Application>().HasData(
            new { Id = 1,  Name = "CoreDataWarehouse" },
            new { Id = 2,  Name = "APIGateway" },
            new { Id = 3,  Name = "SurveyEngine" },
            new { Id = 4,  Name = "AuthService" },
            new { Id = 5,  Name = "ObservabilityStack" },
            new { Id = 6,  Name = "InsightHub" },
            new { Id = 7,  Name = "DataMapper" },
            new { Id = 8,  Name = "QuotaManager" },
            new { Id = 9,  Name = "SegmentBuilder" },
            new { Id = 10, Name = "FieldOps" },
            new { Id = 11, Name = "WebCollect" },
            new { Id = 12, Name = "SampleRouter" },
            new { Id = 13, Name = "CallCentre Suite" },
            new { Id = 14, Name = "DiaryTracker" },
            new { Id = 15, Name = "ClientPortal" },
            new { Id = 16, Name = "ReportFactory" },
            new { Id = 17, Name = "DeliveryTracker" },
            new { Id = 18, Name = "PanelIQ" }
        );

    private static void SeedProducts(ModelBuilder m) =>
        m.Entity<Product>().HasData(
            new { Id = 1, Name = "DataLicensing",              ArrUsd = 6200000m },
            new { Id = 2, Name = "MediaMeasurement",           ArrUsd = 5100000m },
            new { Id = 3, Name = "RetailInsights",             ArrUsd = 4600000m },
            new { Id = 4, Name = "BrandTracking Platform",     ArrUsd = 4200000m },
            new { Id = 5, Name = "CustomerSatisfaction Suite", ArrUsd = 3800000m },
            new { Id = 6, Name = "ConsumerPanel",              ArrUsd = 3400000m },
            new { Id = 7, Name = "FieldworkServices",          ArrUsd = 1600000m }
        );

    private static void SeedProductApplications(ModelBuilder m) =>
        m.Entity<ProductApplication>().HasData(
            new { ProductId = 1, ApplicationId = 1  },
            new { ProductId = 1, ApplicationId = 2  },
            new { ProductId = 1, ApplicationId = 15 },
            new { ProductId = 2, ApplicationId = 14 },
            new { ProductId = 2, ApplicationId = 7  },
            new { ProductId = 2, ApplicationId = 6  },
            new { ProductId = 2, ApplicationId = 1  },
            new { ProductId = 3, ApplicationId = 3  },
            new { ProductId = 3, ApplicationId = 9  },
            new { ProductId = 3, ApplicationId = 6  },
            new { ProductId = 3, ApplicationId = 1  },
            new { ProductId = 4, ApplicationId = 3  },
            new { ProductId = 4, ApplicationId = 18 },
            new { ProductId = 4, ApplicationId = 6  },
            new { ProductId = 4, ApplicationId = 1  },
            new { ProductId = 5, ApplicationId = 3  },
            new { ProductId = 5, ApplicationId = 11 },
            new { ProductId = 5, ApplicationId = 16 },
            new { ProductId = 5, ApplicationId = 1  },
            new { ProductId = 6, ApplicationId = 18 },
            new { ProductId = 6, ApplicationId = 11 },
            new { ProductId = 6, ApplicationId = 7  },
            new { ProductId = 6, ApplicationId = 12 },
            new { ProductId = 7, ApplicationId = 10 },
            new { ProductId = 7, ApplicationId = 8  },
            new { ProductId = 7, ApplicationId = 12 },
            new { ProductId = 7, ApplicationId = 13 }
        );

    private static void SeedProjects(ModelBuilder m) =>
        m.Entity<Project>().HasData(
            new { Id = 1,  Name = "CoreDataWarehouse Cloud Migration" },
            new { Id = 2,  Name = "AuthService Replacement" },
            new { Id = 3,  Name = "Security & Compliance Programme" },
            new { Id = 4,  Name = "DevPortal Rebuild" },
            new { Id = 5,  Name = "ML Forecasting Platform" },
            new { Id = 6,  Name = "Real-Time Analytics Initiative" },
            new { Id = 7,  Name = "SegmentBuilder Enhancement" },
            new { Id = 8,  Name = "CallCentre Suite Replacement" },
            new { Id = 9,  Name = "DiaryTracker Mobile Rewrite" },
            new { Id = 10, Name = "Panel Automation Programme" },
            new { Id = 11, Name = "Client Onboarding Automation" },
            new { Id = 12, Name = "API Delivery Programme" }
        );

    private static void SeedProjectProducts(ModelBuilder m) =>
        m.Entity<ProjectProduct>().HasData(
            new { ProjectId = 1,  ProductId = 1 },
            new { ProjectId = 1,  ProductId = 2 },
            new { ProjectId = 1,  ProductId = 3 },
            new { ProjectId = 1,  ProductId = 4 },
            new { ProjectId = 1,  ProductId = 5 },
            new { ProjectId = 2,  ProductId = 1 },
            new { ProjectId = 2,  ProductId = 2 },
            new { ProjectId = 2,  ProductId = 3 },
            new { ProjectId = 2,  ProductId = 4 },
            new { ProjectId = 2,  ProductId = 5 },
            new { ProjectId = 2,  ProductId = 6 },
            new { ProjectId = 2,  ProductId = 7 },
            new { ProjectId = 3,  ProductId = 1 },
            new { ProjectId = 3,  ProductId = 2 },
            new { ProjectId = 3,  ProductId = 3 },
            new { ProjectId = 3,  ProductId = 4 },
            new { ProjectId = 3,  ProductId = 5 },
            new { ProjectId = 3,  ProductId = 6 },
            new { ProjectId = 3,  ProductId = 7 },
            new { ProjectId = 5,  ProductId = 2 },
            new { ProjectId = 5,  ProductId = 3 },
            new { ProjectId = 5,  ProductId = 4 },
            new { ProjectId = 6,  ProductId = 1 },
            new { ProjectId = 6,  ProductId = 2 },
            new { ProjectId = 7,  ProductId = 3 },
            new { ProjectId = 8,  ProductId = 7 },
            new { ProjectId = 9,  ProductId = 2 },
            new { ProjectId = 10, ProductId = 4 },
            new { ProjectId = 10, ProductId = 6 },
            new { ProjectId = 11, ProductId = 5 },
            new { ProjectId = 11, ProductId = 1 },
            new { ProjectId = 12, ProductId = 1 }
        );

    private static void SeedActivities(ModelBuilder m) =>
        m.Entity<Activity>().HasData(
            new { Id = 1,  Name = "CoreDataWarehouse — Application Support",        ActivityType = "Application Support",        ApplicationId = (int?)1,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 2,  Name = "CoreDataWarehouse — Application Minor Changes",  ActivityType = "Application Minor Changes",  ApplicationId = (int?)1,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 3,  Name = "APIGateway — Application Support",               ActivityType = "Application Support",        ApplicationId = (int?)2,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 4,  Name = "SurveyEngine — Application Support",             ActivityType = "Application Support",        ApplicationId = (int?)3,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 5,  Name = "AuthService — Application Support",              ActivityType = "Application Support",        ApplicationId = (int?)4,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 6,  Name = "ObservabilityStack — Application Support",       ActivityType = "Application Support",        ApplicationId = (int?)5,  ProjectId = (int?)null, Division = "Platform Engineering" },
            new { Id = 7,  Name = "CoreDataWarehouse Cloud Migration",                   ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)1, Division = "Platform Engineering" },
            new { Id = 8,  Name = "AuthService Replacement",                             ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)2, Division = "Platform Engineering" },
            new { Id = 9,  Name = "Security & Compliance Programme",                     ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)3, Division = "Platform Engineering" },
            new { Id = 10, Name = "DevPortal Rebuild",                                   ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)4, Division = "Platform Engineering" },
            new { Id = 11, Name = "InsightHub — Application Support",               ActivityType = "Application Support",        ApplicationId = (int?)6,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 12, Name = "InsightHub — Application Minor Changes",         ActivityType = "Application Minor Changes",  ApplicationId = (int?)6,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 13, Name = "DataMapper — Application Support",               ActivityType = "Application Support",        ApplicationId = (int?)7,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 14, Name = "QuotaManager — Application Support",             ActivityType = "Application Support",        ApplicationId = (int?)8,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 15, Name = "SegmentBuilder — Application Support",           ActivityType = "Application Support",        ApplicationId = (int?)9,  ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 16, Name = "PanelIQ — Application Support",                  ActivityType = "Application Support",        ApplicationId = (int?)18, ProjectId = (int?)null, Division = "Analytics" },
            new { Id = 17, Name = "ML Forecasting Platform",                             ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)5, Division = "Analytics" },
            new { Id = 18, Name = "Real-Time Analytics Initiative",                      ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)6, Division = "Analytics" },
            new { Id = 19, Name = "SegmentBuilder Enhancement",                          ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)7, Division = "Analytics" },
            new { Id = 20, Name = "FieldOps — Application Support",                 ActivityType = "Application Support",        ApplicationId = (int?)10, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 21, Name = "WebCollect — Application Support",               ActivityType = "Application Support",        ApplicationId = (int?)11, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 22, Name = "SampleRouter — Application Support",             ActivityType = "Application Support",        ApplicationId = (int?)12, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 23, Name = "CallCentre Suite — Application Support",         ActivityType = "Application Support",        ApplicationId = (int?)13, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 24, Name = "DiaryTracker — Application Support",             ActivityType = "Application Support",        ApplicationId = (int?)14, ProjectId = (int?)null, Division = "Data Collection" },
            new { Id = 25, Name = "CallCentre Suite Replacement",                        ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)8,  Division = "Data Collection" },
            new { Id = 26, Name = "DiaryTracker Mobile Rewrite",                         ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)9,  Division = "Data Collection" },
            new { Id = 27, Name = "Panel Automation Programme",                          ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)10, Division = "Data Collection" },
            new { Id = 28, Name = "ClientPortal — Application Support",             ActivityType = "Application Support",        ApplicationId = (int?)15, ProjectId = (int?)null, Division = "Client Services" },
            new { Id = 29, Name = "ReportFactory — Application Support",            ActivityType = "Application Support",        ApplicationId = (int?)16, ProjectId = (int?)null, Division = "Client Services" },
            new { Id = 30, Name = "ReportFactory — Application Minor Changes",      ActivityType = "Application Minor Changes",  ApplicationId = (int?)16, ProjectId = (int?)null, Division = "Client Services" },
            new { Id = 31, Name = "DeliveryTracker — Application Support",          ActivityType = "Application Support",        ApplicationId = (int?)17, ProjectId = (int?)null, Division = "Client Services" },
            new { Id = 32, Name = "Client Onboarding Automation",                        ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)11, Division = "Client Services" },
            new { Id = 33, Name = "API Delivery Programme",                              ActivityType = "Project Changes",            ApplicationId = (int?)null, ProjectId = (int?)12, Division = "Client Services" }
        );
}
