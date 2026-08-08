using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Organization;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class OrganizationService(IAppDbContext db) : IOrganizationService
{
    public async Task UpdateAssignmentAsync(string companyId, string actorUserId, Role actorRole, string targetUserId, UpdateAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        var target = await db.Users.SingleOrDefaultAsync(u => u.Id == targetUserId && u.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("User not found");

        // Coarse [RequireModule(Users)][RequireAction(Read)] grants every role access to
        // this endpoint - real authorization (Owner/Admin, or the target's own department
        // manager) happens here.
        if (actorRole is not (Role.Owner or Role.Admin))
        {
            var isDepartmentManager = target.DepartmentId is not null &&
                await db.Departments.AnyAsync(d => d.Id == target.DepartmentId && d.ManagerId == actorUserId, cancellationToken);
            if (!isDepartmentManager)
            {
                throw new ForbiddenException("Only Owner/Admin or the employee's department manager can change assignment");
            }
        }

        if (request.ManagerId == targetUserId)
        {
            throw new ConflictException("A user cannot be their own manager");
        }

        if (request.ManagerId is not null)
        {
            var managerExists = await db.Users.AnyAsync(u => u.Id == request.ManagerId && u.CompanyId == companyId, cancellationToken);
            if (!managerExists)
            {
                throw new NotFoundException("Manager not found in this company");
            }

            await EnsureNoCircularChainAsync(targetUserId, request.ManagerId, cancellationToken);
        }

        if (request.DepartmentId is not null)
        {
            await ValidateDepartmentAsync(companyId, request.DepartmentId, cancellationToken);
        }

        if (request.BranchId is not null)
        {
            var branchExists = await db.Branches.AnyAsync(b => b.Id == request.BranchId && b.CompanyId == companyId, cancellationToken);
            if (!branchExists)
            {
                throw new NotFoundException("Branch not found in this company");
            }
        }

        var teamIds = request.TeamIds.Distinct().ToList();
        if (teamIds.Count > 0)
        {
            var validTeamCount = await db.Teams.CountAsync(t => teamIds.Contains(t.Id) && t.CompanyId == companyId, cancellationToken);
            if (validTeamCount != teamIds.Count)
            {
                throw new NotFoundException("One or more teams not found in this company");
            }
        }

        target.DepartmentId = request.DepartmentId;
        target.BranchId = request.BranchId;
        target.ManagerId = request.ManagerId;
        target.UpdatedAt = DateTime.UtcNow;

        var currentMemberships = await db.TeamMembers.Where(tm => tm.UserId == targetUserId).ToListAsync(cancellationToken);
        foreach (var membership in currentMemberships.Where(tm => !teamIds.Contains(tm.TeamId)))
        {
            db.TeamMembers.Remove(membership);
        }

        foreach (var teamId in teamIds.Where(tid => currentMemberships.All(tm => tm.TeamId != tid)))
        {
            db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = targetUserId, CompanyId = companyId });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrgChartResponse> GetOrgChartAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var departments = await db.Departments.Include(d => d.Manager).ToListAsync(cancellationToken);
        var users = await db.Users.ToListAsync(cancellationToken);

        var employeesByDepartment = users.Where(u => u.DepartmentId is not null).ToLookup(u => u.DepartmentId);
        var departmentsByParent = departments.ToLookup(d => d.ParentDepartmentId);
        var unassigned = users.Where(u => u.DepartmentId is null).Select(ToEmployee).ToList();

        OrgChartNode Build(Department d) => new(
            d.Id, d.Name, d.ManagerId, d.Manager?.Name,
            employeesByDepartment[d.Id].Select(ToEmployee).ToList(),
            departmentsByParent[d.Id].Select(Build).ToList());

        var tree = departmentsByParent[null].Select(Build).ToList();
        return new OrgChartResponse(tree, unassigned);
    }

    public async Task<UserOrgChainDto> GetUserOrgChainAsync(string companyId, string userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("User not found");

        var chain = new List<OrgChartPersonSummary>();
        var currentManagerId = user.ManagerId;
        var guard = 0;

        while (currentManagerId is not null && guard++ < 100)
        {
            var manager = await db.Users.SingleOrDefaultAsync(u => u.Id == currentManagerId, cancellationToken);
            if (manager is null)
            {
                break;
            }

            chain.Add(new OrgChartPersonSummary(manager.Id, manager.Name, manager.AvatarUrl));
            currentManagerId = manager.ManagerId;
        }

        var directReports = await db.Users.Where(u => u.ManagerId == userId)
            .Select(u => new OrgChartPersonSummary(u.Id, u.Name, u.AvatarUrl))
            .ToListAsync(cancellationToken);

        return new UserOrgChainDto(new OrgChartPersonSummary(user.Id, user.Name, user.AvatarUrl), chain, directReports);
    }

    private static OrgChartEmployee ToEmployee(User u) => new(u.Id, u.Name, u.AvatarUrl, u.ManagerId);

    private async Task EnsureNoCircularChainAsync(string targetUserId, string proposedManagerId, CancellationToken cancellationToken)
    {
        var currentId = proposedManagerId;
        var guard = 0;

        while (currentId is not null && guard++ < 100)
        {
            if (currentId == targetUserId)
            {
                throw new ConflictException("This assignment would create a circular reporting chain");
            }

            currentId = await db.Users.Where(u => u.Id == currentId).Select(u => u.ManagerId).SingleOrDefaultAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsFlatAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var departments = await db.Departments.Include(d => d.Manager).OrderBy(d => d.Name).ToListAsync(cancellationToken);
        return departments.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<DepartmentTreeNode>> ListDepartmentsTreeAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var departments = await db.Departments.Include(d => d.Manager).OrderBy(d => d.Name).ToListAsync(cancellationToken);
        var byParent = departments.ToLookup(d => d.ParentDepartmentId);

        DepartmentTreeNode Build(Department d) => new(
            d.Id, d.Name, d.Description, d.ManagerId, d.Manager?.Name, d.CreatedAt, d.UpdatedAt,
            byParent[d.Id].Select(Build).ToList());

        return byParent[null].Select(Build).ToList();
    }

    public async Task<DepartmentDto> GetDepartmentAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.Include(d => d.Manager).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Department not found");
        return ToDto(department);
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(string companyId, CreateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateDepartmentAsync(companyId, request.ParentDepartmentId, cancellationToken);
        await ValidateUserInCompanyAsync(companyId, request.ManagerId, cancellationToken);
        await EnsureNameAvailableAsync(companyId, request.ParentDepartmentId, request.Name, existingId: null, cancellationToken);

        var department = new Department
        {
            CompanyId = companyId,
            Name = request.Name,
            Description = request.Description,
            ParentDepartmentId = request.ParentDepartmentId,
            ManagerId = request.ManagerId,
        };

        db.Departments.Add(department);
        await db.SaveChangesAsync(cancellationToken);

        department.Manager = department.ManagerId is null ? null : await db.Users.FindAsync([department.ManagerId], cancellationToken);
        return ToDto(department);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(string companyId, string id, UpdateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.Include(d => d.Manager).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Department not found");

        if (request.ParentDepartmentId is not null)
        {
            if (request.ParentDepartmentId == id)
            {
                throw new ConflictException("A department cannot be its own parent");
            }

            await ValidateDepartmentAsync(companyId, request.ParentDepartmentId, cancellationToken);
        }

        if (request.ManagerId is not null)
        {
            await ValidateUserInCompanyAsync(companyId, request.ManagerId, cancellationToken);
        }

        if (request.Name is not null || request.ParentDepartmentId is not null)
        {
            var newParentId = request.ParentDepartmentId ?? department.ParentDepartmentId;
            var newName = request.Name ?? department.Name;
            await EnsureNameAvailableAsync(companyId, newParentId, newName, id, cancellationToken);
        }

        if (request.Name is not null)
        {
            department.Name = request.Name;
        }

        if (request.Description is not null)
        {
            department.Description = request.Description;
        }

        if (request.ParentDepartmentId is not null)
        {
            department.ParentDepartmentId = request.ParentDepartmentId;
        }

        if (request.ManagerId is not null)
        {
            department.ManagerId = request.ManagerId;
        }

        department.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(department);
    }

    public async Task RemoveDepartmentAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Department not found");

        var hasEmployees = await db.Users.AnyAsync(u => u.DepartmentId == id, cancellationToken);
        var hasSubDepartments = await db.Departments.AnyAsync(d => d.ParentDepartmentId == id, cancellationToken);
        if (hasEmployees || hasSubDepartments)
        {
            throw new ConflictException("Cannot delete a department with active employees or sub-departments");
        }

        db.Departments.Remove(department);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TeamDto>> ListTeamsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var teams = await db.Teams.Include(t => t.TeamLead).OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return teams.Select(ToDto).ToList();
    }

    public async Task<TeamDto> GetTeamAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.Include(t => t.TeamLead).SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Team not found");
        return ToDto(team);
    }

    public async Task<TeamDto> CreateTeamAsync(string companyId, CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateDepartmentAsync(companyId, request.DepartmentId, cancellationToken);
        await ValidateUserInCompanyAsync(companyId, request.TeamLeadId, cancellationToken);

        var team = new Team
        {
            CompanyId = companyId,
            DepartmentId = request.DepartmentId,
            Name = request.Name,
            Description = request.Description,
            TeamLeadId = request.TeamLeadId,
        };

        db.Teams.Add(team);
        await db.SaveChangesAsync(cancellationToken);

        team.TeamLead = team.TeamLeadId is null ? null : await db.Users.FindAsync([team.TeamLeadId], cancellationToken);
        return ToDto(team);
    }

    public async Task<TeamDto> UpdateTeamAsync(string companyId, string id, UpdateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.Include(t => t.TeamLead).SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Team not found");

        if (request.DepartmentId is not null)
        {
            await ValidateDepartmentAsync(companyId, request.DepartmentId, cancellationToken);
        }

        if (request.TeamLeadId is not null)
        {
            await ValidateUserInCompanyAsync(companyId, request.TeamLeadId, cancellationToken);
        }

        if (request.Name is not null)
        {
            team.Name = request.Name;
        }

        if (request.Description is not null)
        {
            team.Description = request.Description;
        }

        if (request.DepartmentId is not null)
        {
            team.DepartmentId = request.DepartmentId;
        }

        if (request.TeamLeadId is not null)
        {
            team.TeamLeadId = request.TeamLeadId;
        }

        team.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        team.TeamLead = team.TeamLeadId is null ? null : await db.Users.FindAsync([team.TeamLeadId], cancellationToken);
        return ToDto(team);
    }

    public async Task RemoveTeamAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var team = await db.Teams.SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Team not found");

        db.Teams.Remove(team);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TeamMemberDto>> ListTeamMembersAsync(string companyId, string teamId, CancellationToken cancellationToken = default)
    {
        var teamExists = await db.Teams.AnyAsync(t => t.Id == teamId, cancellationToken);
        if (!teamExists)
        {
            throw new NotFoundException("Team not found");
        }

        var members = await db.TeamMembers.Include(tm => tm.User)
            .Where(tm => tm.TeamId == teamId)
            .OrderBy(tm => tm.JoinedAt)
            .ToListAsync(cancellationToken);

        return members.Select(tm => new TeamMemberDto(tm.UserId, tm.User.Name, tm.JoinedAt)).ToList();
    }

    public async Task AddTeamMemberAsync(string companyId, string teamId, AddTeamMemberRequest request, CancellationToken cancellationToken = default)
    {
        var teamExists = await db.Teams.AnyAsync(t => t.Id == teamId, cancellationToken);
        if (!teamExists)
        {
            throw new NotFoundException("Team not found");
        }

        await ValidateUserInCompanyAsync(companyId, request.UserId, cancellationToken, required: true);

        var alreadyMember = await db.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == request.UserId, cancellationToken);
        if (alreadyMember)
        {
            throw new ConflictException("User is already a member of this team");
        }

        db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = request.UserId, CompanyId = companyId });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveTeamMemberAsync(string companyId, string teamId, string userId, CancellationToken cancellationToken = default)
    {
        var member = await db.TeamMembers.SingleOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Team membership not found");

        db.TeamMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BranchDto>> ListBranchesAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var branches = await db.Branches.OrderBy(b => b.Name).ToListAsync(cancellationToken);
        return branches.Select(ToDto).ToList();
    }

    public async Task<BranchDto> GetBranchAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException("Branch not found");
        return ToDto(branch);
    }

    public async Task<BranchDto> CreateBranchAsync(string companyId, CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = new Branch
        {
            CompanyId = companyId,
            Name = request.Name,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Dushanbe" : request.Timezone,
            IsHeadquarters = request.IsHeadquarters,
        };

        if (request.IsHeadquarters)
        {
            await UnsetExistingHeadquartersAsync(companyId, cancellationToken);
        }

        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(branch);
    }

    public async Task<BranchDto> UpdateBranchAsync(string companyId, string id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException("Branch not found");

        if (request.Name is not null)
        {
            branch.Name = request.Name;
        }

        if (request.Address is not null)
        {
            branch.Address = request.Address;
        }

        if (request.City is not null)
        {
            branch.City = request.City;
        }

        if (request.Country is not null)
        {
            branch.Country = request.Country;
        }

        if (request.Timezone is not null)
        {
            branch.Timezone = request.Timezone;
        }

        if (request.IsHeadquarters is true && !branch.IsHeadquarters)
        {
            await UnsetExistingHeadquartersAsync(companyId, cancellationToken);
            branch.IsHeadquarters = true;
        }
        else if (request.IsHeadquarters is false)
        {
            branch.IsHeadquarters = false;
        }

        branch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(branch);
    }

    public async Task RemoveBranchAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException("Branch not found");

        var hasUsers = await db.Users.AnyAsync(u => u.BranchId == id, cancellationToken);
        if (hasUsers)
        {
            throw new ConflictException("Cannot delete a branch with users still assigned to it");
        }

        db.Branches.Remove(branch);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UnsetExistingHeadquartersAsync(string companyId, CancellationToken cancellationToken)
    {
        var current = await db.Branches.Where(b => b.CompanyId == companyId && b.IsHeadquarters).ToListAsync(cancellationToken);
        foreach (var branch in current)
        {
            branch.IsHeadquarters = false;
        }
    }

    private async Task ValidateDepartmentAsync(string companyId, string? departmentId, CancellationToken cancellationToken)
    {
        if (departmentId is null)
        {
            return;
        }

        var exists = await db.Departments.AnyAsync(d => d.Id == departmentId && d.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Department not found in this company");
        }
    }

    private async Task ValidateUserInCompanyAsync(string companyId, string? userId, CancellationToken cancellationToken, bool required = false)
    {
        if (userId is null)
        {
            if (required)
            {
                throw new NotFoundException("User not found in this company");
            }

            return;
        }

        var exists = await db.Users.AnyAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("User not found in this company");
        }
    }

    private async Task EnsureNameAvailableAsync(string companyId, string? parentDepartmentId, string name, string? existingId, CancellationToken cancellationToken)
    {
        var duplicate = await db.Departments.AnyAsync(d =>
            d.CompanyId == companyId && d.ParentDepartmentId == parentDepartmentId && d.Name == name && d.Id != (existingId ?? string.Empty),
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException("A department with this name already exists at this level");
        }
    }

    private static DepartmentDto ToDto(Department d) => new(
        d.Id, d.Name, d.Description, d.ParentDepartmentId, d.ManagerId, d.Manager?.Name, d.CreatedAt, d.UpdatedAt);

    private static TeamDto ToDto(Team t) => new(
        t.Id, t.DepartmentId, t.Name, t.Description, t.TeamLeadId, t.TeamLead?.Name, t.CreatedAt, t.UpdatedAt);

    private static BranchDto ToDto(Branch b) => new(
        b.Id, b.Name, b.Address, b.City, b.Country, b.Timezone, b.IsHeadquarters, b.CreatedAt, b.UpdatedAt);
}
