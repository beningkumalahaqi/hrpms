(() => {
  "use strict";

  const departments = [
    { name: "Engineering", budget: 23000, positions: ["Engineering Manager", "Senior Engineer", "Software Engineer", "QA Engineer"] },
    { name: "Operations", budget: 18000, positions: ["Operations Manager", "Operations Specialist", "Project Coordinator"] },
    { name: "Finance", budget: 17500, positions: ["Finance Manager", "Financial Analyst", "Accounts Specialist"] },
    { name: "People", budget: 12000, positions: ["People Director", "HR Business Partner", "HR Coordinator"] },
    { name: "Marketing", budget: 16000, positions: ["Marketing Manager", "Content Strategist", "Marketing Analyst"] }
  ];

  const state = {
    route: "dashboard",
    selectedProjectId: null,
    employees: [
      { id: "EMP-001", name: "Elena Petrova", department: "Engineering", position: "Engineering Manager", managerId: null, salary: 9500, status: "Active" },
      { id: "EMP-002", name: "Owen Bennett", department: "Engineering", position: "Senior Engineer", managerId: "EMP-001", salary: 7800, status: "Active" },
      { id: "EMP-003", name: "Malik Haddad", department: "Engineering", position: "Software Engineer", managerId: "EMP-001", salary: 7200, status: "Active" },
      { id: "EMP-004", name: "Ingrid Holm", department: "Engineering", position: "QA Engineer", managerId: "EMP-001", salary: 5600, status: "Inactive" },
      { id: "EMP-005", name: "Marcus Lee", department: "Operations", position: "Operations Manager", managerId: null, salary: 7200, status: "Active" },
      { id: "EMP-006", name: "Nora Alvarez", department: "Operations", position: "Operations Specialist", managerId: "EMP-005", salary: 4800, status: "Active" },
      { id: "EMP-007", name: "Jonas Berg", department: "Operations", position: "Project Coordinator", managerId: "EMP-005", salary: 4600, status: "Active" },
      { id: "EMP-008", name: "Priya Nair", department: "Finance", position: "Finance Manager", managerId: null, salary: 7600, status: "Active" },
      { id: "EMP-009", name: "Theo Martin", department: "Finance", position: "Financial Analyst", managerId: "EMP-008", salary: 5200, status: "Active" },
      { id: "EMP-010", name: "Anika Shah", department: "Finance", position: "Accounts Specialist", managerId: "EMP-008", salary: 5000, status: "Active" },
      { id: "EMP-011", name: "Sofia Martinez", department: "People", position: "People Director", managerId: null, salary: 7000, status: "Active" },
      { id: "EMP-012", name: "Ruth Williams", department: "People", position: "HR Coordinator", managerId: "EMP-011", salary: 4500, status: "Active" },
      { id: "EMP-013", name: "Daniel Kim", department: "Marketing", position: "Marketing Manager", managerId: null, salary: 6800, status: "Active" },
      { id: "EMP-014", name: "Lena Fischer", department: "Marketing", position: "Content Strategist", managerId: "EMP-013", salary: 4300, status: "Active" },
      { id: "EMP-015", name: "Sam Okafor", department: "Marketing", position: "Marketing Analyst", managerId: "EMP-013", salary: 4100, status: "Active" }
    ],
    projects: [
      { id: "PRJ-2401", name: "HRIS Consolidation", department: "People", start: "2026-01-12", end: "2026-11-30", status: "Active", description: "Consolidate employee records and departmental reporting into one governed workspace." },
      { id: "PRJ-2402", name: "Finance Data Migration", department: "Finance", start: "2026-03-02", end: "2026-12-18", status: "Active", description: "Migrate approved departmental financial data with reconciliation checkpoints." },
      { id: "PRJ-2403", name: "Field Operations Rollout", department: "Operations", start: "2026-04-15", end: "2027-01-29", status: "Active", description: "Standardize project coordination practices across regional operations teams." },
      { id: "PRJ-2404", name: "Partner Portal Refresh", department: "Marketing", start: "2026-12-01", end: "2027-03-26", status: "Planning", description: "Revise partner-facing content and the supporting publishing workflow." },
      { id: "PRJ-2405", name: "Workforce Planning", department: "People", start: "2027-01-11", end: "2027-04-30", status: "Planning", description: "Prepare a consistent annual planning process for department managers." },
      { id: "PRJ-2406", name: "Budget Controls", department: "Finance", start: "2026-01-05", end: "2026-06-26", status: "Completed", description: "Introduce shared monthly budget ownership and variance review controls." },
      { id: "PRJ-2407", name: "Vendor Onboarding", department: "Operations", start: "2026-02-09", end: "2026-05-15", status: "Cancelled", description: "A paused operational initiative retained for historical reference." },
      { id: "PRJ-2408", name: "Engineering Handbook", department: "Engineering", start: "2026-08-03", end: "2026-12-11", status: "Active", description: "Document engineering responsibilities and cross-department delivery practices." }
    ],
    assignments: [
      { projectId: "PRJ-2401", employeeId: "EMP-011", role: "HR lead", allocation: 40, active: true },
      { projectId: "PRJ-2401", employeeId: "EMP-012", role: "Coordinator", allocation: 60, active: true },
      { projectId: "PRJ-2401", employeeId: "EMP-001", role: "Solution architect", allocation: 20, active: true },
      { projectId: "PRJ-2402", employeeId: "EMP-008", role: "Finance lead", allocation: 50, active: true },
      { projectId: "PRJ-2402", employeeId: "EMP-009", role: "Data analyst", allocation: 70, active: true },
      { projectId: "PRJ-2402", employeeId: "EMP-002", role: "Integration engineer", allocation: 30, active: true },
      { projectId: "PRJ-2403", employeeId: "EMP-005", role: "Operations lead", allocation: 40, active: true },
      { projectId: "PRJ-2403", employeeId: "EMP-006", role: "Regional specialist", allocation: 80, active: true },
      { projectId: "PRJ-2403", employeeId: "EMP-007", role: "Project coordinator", allocation: 50, active: true },
      { projectId: "PRJ-2408", employeeId: "EMP-001", role: "Executive sponsor", allocation: 40, active: true },
      { projectId: "PRJ-2408", employeeId: "EMP-002", role: "Technical editor", allocation: 50, active: true },
      { projectId: "PRJ-2408", employeeId: "EMP-003", role: "Contributor", allocation: 30, active: true }
    ],
    employeeView: { query: "", department: "All", sort: "name", direction: "asc", page: 1, pageSize: 6 },
    projectView: { query: "", department: "All", status: "All", sort: "code", direction: "asc", page: 1, pageSize: 5 },
    apiView: { query: "", selected: "get-employees" }
  };

  const apiEndpoints = [
    {
      id: "get-employees", method: "GET", path: "/api/employees", summary: "List employees",
      description: "Returns employees with optional text and department filters.",
      parameters: [["query", "Optional. Matches an employee name or employee number."], ["department", "Optional. Filters by an exact department name."], ["page", "Optional. One-based result page. Defaults to 1."]],
      statuses: [[200, "Employees returned"], [400, "Invalid filter"], [401, "Authentication required"]],
      response: `{
  "data": [
    {
      "employeeNumber": "EMP-001",
      "name": "Elena Petrova",
      "department": "Engineering",
      "status": "Active"
    }
  ],
  "page": 1,
  "pageSize": 20
}`
    },
    {
      id: "post-employees", method: "POST", path: "/api/employees", summary: "Create an employee",
      description: "Creates an employee after validating department, position, manager, and salary values.",
      parameters: [["employeeNumber", "Required. Unique organization employee number."], ["department", "Required. Existing department name."], ["salary", "Required. Positive gross monthly salary."]],
      statuses: [[201, "Employee created"], [400, "Validation failed"], [409, "Employee number exists"]],
      response: `{
  "employeeNumber": "EMP-016",
  "status": "Active",
  "message": "Employee created"
}`
    },
    {
      id: "patch-employees", method: "PATCH", path: "/api/employees/{employeeNumber}", summary: "Update an employee",
      description: "Updates mutable employee fields. Status changes require the appropriate administrator permission.",
      parameters: [["employeeNumber", "Required path parameter identifying the employee."], ["position", "Optional. Position valid for the selected department."], ["status", "Optional. Active or Inactive."]],
      statuses: [[200, "Employee updated"], [400, "Validation failed"], [404, "Employee not found"]],
      response: `{
  "employeeNumber": "EMP-001",
  "position": "Engineering Manager",
  "status": "Active"
}`
    },
    {
      id: "get-projects", method: "GET", path: "/api/projects", summary: "List projects",
      description: "Returns projects with their active team member counts.",
      parameters: [["status", "Optional. Active, Planning, Completed, or Cancelled."], ["department", "Optional. Filters by owning department."], ["page", "Optional. One-based result page. Defaults to 1."]],
      statuses: [[200, "Projects returned"], [400, "Invalid filter"], [401, "Authentication required"]],
      response: `{
  "data": [
    {
      "projectCode": "PRJ-2401",
      "name": "HRIS Consolidation",
      "status": "Active",
      "activeTeamMembers": 3
    }
  ]
}`
    },
    {
      id: "post-assignments", method: "POST", path: "/api/projects/{projectCode}/assignments", summary: "Assign a team member",
      description: "Creates a project assignment when the employee remains within the 100% active allocation limit.",
      parameters: [["projectCode", "Required path parameter identifying the project."], ["employeeNumber", "Required. Active employee to assign."], ["allocation", "Required. Percentage from 1 to 100, including existing allocations."]],
      statuses: [[201, "Assignment created"], [400, "Allocation exceeds 100%"], [404, "Project or employee not found"]],
      response: `{
  "projectCode": "PRJ-2401",
  "employeeNumber": "EMP-002",
  "allocation": 20,
  "totalAllocation": 100
}`
    },
    {
      id: "get-budgets", method: "GET", path: "/api/departments/budgets", summary: "Summarize department budgets",
      description: "Returns monthly salary budgets, current active employee expenses, and variance by department.",
      parameters: [["month", "Optional. Accounting month in YYYY-MM format. Defaults to the current month."]],
      statuses: [[200, "Budget summary returned"], [400, "Invalid month"], [403, "Insufficient permission"]],
      response: `{
  "month": "2026-11",
  "data": [
    {
      "department": "Engineering",
      "budget": 23000,
      "salaryExpense": 24500,
      "variance": -1500
    }
  ]
}`
    }
  ];

  const main = document.getElementById("main-content");
  const topbarPage = document.getElementById("topbarPage");
  const sidebar = document.getElementById("sidebar");
  const menuButton = document.getElementById("menuButton");
  const navScrim = document.getElementById("navScrim");
  const employeeDialog = document.getElementById("employeeDialog");
  const employeeForm = document.getElementById("employeeForm");
  const assignmentDialog = document.getElementById("assignmentDialog");
  const assignmentForm = document.getElementById("assignmentForm");
  const confirmDialog = document.getElementById("confirmDialog");
  const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 });
  const dateFormatter = new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", year: "numeric" });
  let renderToken = 0;
  let editingEmployeeId = null;
  let pendingDeactivateId = null;
  let assignmentProjectId = null;

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[character]);
  }

  function icon(name, size = "control") {
    const paths = {
      plus: '<path d="M12 5v14M5 12h14"/>',
      refresh: '<path d="M20 6v5h-5M4 18v-5h5M6.1 9a7 7 0 0 1 11.6-2.6L20 11M4 13l2.3 4.6A7 7 0 0 0 18 15"/>',
      chevronLeft: '<path d="m15 5-7 7 7 7"/>',
      chevronRight: '<path d="m9 5 7 7-7 7"/>',
      close: '<path d="m5 5 14 14M19 5 5 19"/>',
      copy: '<rect x="8" y="8" width="12" height="12"/><path d="M16 8V4H4v12h4"/>',
      search: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/>',
      users: '<circle cx="9" cy="8" r="3.5"/><path d="M2.5 20v-2.2A5.8 5.8 0 0 1 8.3 12h1.4a5.8 5.8 0 0 1 5.8 5.8V20M16 5.5a3.5 3.5 0 0 1 0 7M17.5 14a5 5 0 0 1 4 4.9V20"/>',
      briefcase: '<rect x="3" y="7" width="18" height="13"/><path d="M8 7V4h8v3M3 12h18M10 12v2h4v-2"/>',
      departments: '<path d="M4 21V4h11v17M15 9h5v12M2 21h20M8 8h3M8 12h3M8 16h3M18 13v2M18 18v2"/>',
      salary: '<rect x="3" y="5" width="18" height="14"/><path d="M12 8v8M15 9.5c-.7-1-1.7-1.5-3-1.5-1.7 0-3 .9-3 2s1 1.7 3 2 3 1 3 2-1.3 2-3 2c-1.3 0-2.4-.5-3-1.5"/>',
      sort: '<path d="m8 6 4-4 4 4M12 2v20M8 18l4 4 4-4"/>',
      sortAsc: '<path d="M12 19V5m-5 5 5-5 5 5"/>',
      sortDesc: '<path d="M12 5v14m-5-5 5 5 5-5"/>',
      checkCircle: '<circle cx="12" cy="12" r="9"/><path d="m8 12 3 3 5-6"/>',
      clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
      cancel: '<circle cx="12" cy="12" r="9"/><path d="m8 8 8 8M16 8l-8 8"/>',
      alert: '<path d="M12 3 2.5 20h19L12 3Z"/><path d="M12 9v5M12 17v.5"/>'
    };
    return `<svg class="od-icon od-icon-${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">${paths[name] || ""}</svg>`;
  }

  function formatDate(value) {
    return dateFormatter.format(new Date(`${value}T00:00:00`));
  }

  function getEmployee(id) {
    return state.employees.find(employee => employee.id === id);
  }

  function getProject(id) {
    return state.projects.find(project => project.id === id);
  }

  function managerName(id) {
    const manager = getEmployee(id);
    return manager ? manager.name : "Not assigned";
  }

  function statusBadge(status) {
    const icons = { Active: "checkCircle", Completed: "checkCircle", Planning: "clock", Cancelled: "cancel", Inactive: "cancel" };
    return `<span class="badge ${escapeHtml(status.toLowerCase())}">${icon(icons[status] || "checkCircle", "status")}<span>${escapeHtml(status)}</span></span>`;
  }

  function budgetBadge(over) {
    return `<span class="badge ${over ? "over" : "within"}">${icon(over ? "alert" : "checkCircle", "status")}<span>${over ? "Over budget" : "Within budget"}</span></span>`;
  }

  function activeAssignments(projectId) {
    return state.assignments.filter(assignment => assignment.active && assignment.projectId === projectId && getEmployee(assignment.employeeId)?.status === "Active");
  }

  function currentAllocation(employeeId) {
    return state.assignments.reduce((total, assignment) => {
      const project = getProject(assignment.projectId);
      const counts = assignment.active && project && ["Active", "Planning"].includes(project.status);
      return total + (counts && assignment.employeeId === employeeId ? assignment.allocation : 0);
    }, 0);
  }

  function pageHeader(title, description, actions = "", breadcrumb = "Workspace") {
    return `<header class="page-header">
      <div class="page-heading">
        <nav class="breadcrumbs" aria-label="Breadcrumb"><span>${escapeHtml(breadcrumb)}</span><span aria-hidden="true">/</span><span>${escapeHtml(title)}</span></nav>
        <h1>${escapeHtml(title)}</h1>
        <p class="page-description">${escapeHtml(description)}</p>
      </div>
      ${actions ? `<div class="page-actions">${actions}</div>` : ""}
    </header>`;
  }

  function refreshButton() {
    return `<button class="button secondary" type="button" data-action="refresh">${icon("refresh")}<span>Refresh</span></button>`;
  }

  function loadingMarkup() {
    return `<section class="panel loading-state" aria-busy="true" aria-live="polite">
      <h1>Loading workspace data</h1>
      <p>Preparing the current view. Controls will be available shortly.</p>
      <div class="loading-blocks" aria-hidden="true"><span class="loading-block"></span><span class="loading-block"></span><span class="loading-block"></span></div>
    </section>`;
  }

  function errorMarkup() {
    return `<section class="panel error-state" role="alert">
      <h1>The view could not be loaded</h1>
      <p>The local sample data is still available. Retry this view to continue.</p>
      <button class="button primary" type="button" data-action="refresh">Retry loading</button>
    </section>`;
  }

  function emptyMarkup(title, message, action = "") {
    return `<div class="empty-state"><h2>${escapeHtml(title)}</h2><p>${escapeHtml(message)}</p>${action}</div>`;
  }

  function renderWithLoading() {
    const token = ++renderToken;
    main.innerHTML = loadingMarkup();
    window.setTimeout(() => {
      if (token === renderToken) renderRoute();
    }, 320);
  }

  function navigate(route, projectId = null) {
    state.route = route;
    state.selectedProjectId = projectId;
    updateNavigation();
    setMobileNavigation(false, false);
    renderWithLoading();
    main.focus({ preventScroll: true });
  }

  function updateNavigation() {
    const activeRoute = state.route === "project-detail" ? "projects" : state.route;
    const titles = { dashboard: "Dashboard", employees: "Employees", projects: "Projects", "project-detail": "Project details", api: "API Documentation" };
    document.querySelectorAll(".nav-item[data-route]").forEach(button => {
      const active = button.dataset.route === activeRoute;
      button.classList.toggle("is-active", active);
      if (active) button.setAttribute("aria-current", "page");
      else button.removeAttribute("aria-current");
    });
    topbarPage.textContent = titles[state.route] || "Workspace";
    document.title = `${titles[state.route] || "Workspace"} | HR Workspace`;
  }

  function renderRoute(focusId = "", selection = null) {
    try {
      if (state.route === "dashboard") renderDashboard();
      else if (state.route === "employees") renderEmployees();
      else if (state.route === "projects") renderProjects();
      else if (state.route === "project-detail") renderProjectDetail();
      else if (state.route === "api") renderApi();
      else navigate("dashboard");
      if (focusId) {
        const control = document.getElementById(focusId);
        if (control) {
          control.focus({ preventScroll: true });
          if (selection !== null && typeof control.setSelectionRange === "function") control.setSelectionRange(selection, selection);
        }
      }
    } catch (error) {
      console.error("Workspace rendering failed", error);
      main.innerHTML = errorMarkup();
    }
  }

  function departmentSummary() {
    return departments.map(department => {
      const employees = state.employees.filter(employee => employee.department === department.name && employee.status === "Active");
      const expense = employees.reduce((total, employee) => total + employee.salary, 0);
      return { ...department, headcount: employees.length, expense, variance: department.budget - expense, over: expense > department.budget };
    });
  }

  function statCard(label, value, meta, iconName, featured = false) {
    return `<article class="stat-card od-stat${featured ? " featured" : ""}"><div class="stat-label-row"><span class="stat-label">${escapeHtml(label)}</span>${icon(iconName)}</div><strong class="stat-value">${escapeHtml(value)}</strong><span class="stat-meta">${escapeHtml(meta)}</span></article>`;
  }

  function renderDashboard() {
    const activeEmployees = state.employees.filter(employee => employee.status === "Active");
    const activeProjects = state.projects.filter(project => project.status === "Active");
    const salaryExpense = activeEmployees.reduce((total, employee) => total + employee.salary, 0);
    const summaries = departmentSummary();
    const rows = summaries.map(summary => `<tr class="${summary.over ? "over-budget" : ""}">
      <td><span class="cell-primary">${escapeHtml(summary.name)}</span></td>
      <td class="numeric">${summary.headcount}</td>
      <td class="numeric">${money.format(summary.budget)}</td>
      <td class="numeric">${money.format(summary.expense)}</td>
      <td class="numeric ${summary.variance < 0 ? "negative" : "positive"}">${summary.variance < 0 ? "−" : ""}${money.format(Math.abs(summary.variance))}</td>
      <td>${budgetBadge(summary.over)}</td>
    </tr>`).join("");
    const cards = summaries.map(summary => `<article class="record-card ${summary.over ? "over-budget" : ""}">
      <div class="record-header"><div class="record-title"><strong>${escapeHtml(summary.name)}</strong><span>${summary.headcount} active employees</span></div>${budgetBadge(summary.over)}</div>
      <dl class="record-details"><div><dt>Monthly budget</dt><dd class="numeric">${money.format(summary.budget)}</dd></div><div><dt>Salary expense</dt><dd class="numeric">${money.format(summary.expense)}</dd></div><div><dt>Variance</dt><dd class="numeric ${summary.variance < 0 ? "negative" : "positive"}">${summary.variance < 0 ? "−" : ""}${money.format(Math.abs(summary.variance))}</dd></div></dl>
    </article>`).join("");
    main.innerHTML = `<div class="page">
      ${pageHeader("Dashboard", "Monitor workforce totals and compare monthly salary expenses with department budgets.", refreshButton())}
      <section class="stats-grid" aria-label="Workplace overview">
        ${statCard("Total employees", String(state.employees.length), `${activeEmployees.length} active employees`, "users")}
        ${statCard("Active projects", String(activeProjects.length), `${state.projects.length} projects in total`, "briefcase")}
        ${statCard("Departments", String(departments.length), "With monthly salary budgets", "departments")}
        ${statCard("Monthly salary expenses", money.format(salaryExpense), "Active employees, sample USD values", "salary", true)}
      </section>
      <section class="panel" aria-labelledby="budgetTitle">
        <div class="panel-header"><div class="panel-heading"><h2 id="budgetTitle">Department budget summary</h2><p>Soft red rows identify salary expenses above the approved monthly budget.</p></div><span class="data-chip">November 2026</span></div>
        <div class="desktop-table table-wrap"><table class="data-table"><caption class="sr-only">Department monthly salary budgets and expenses</caption><thead><tr><th scope="col">Department</th><th scope="col" class="numeric">Active employees</th><th scope="col" class="numeric">Monthly budget</th><th scope="col" class="numeric">Salary expense</th><th scope="col" class="numeric">Variance</th><th scope="col">Budget status</th></tr></thead><tbody>${rows}</tbody></table></div>
        <div class="mobile-records">${cards}</div>
      </section>
    </div>`;
  }

  function filteredEmployees() {
    const view = state.employeeView;
    const query = view.query.trim().toLowerCase();
    const records = state.employees.filter(employee => {
      const matchesQuery = !query || employee.name.toLowerCase().includes(query) || employee.id.toLowerCase().includes(query);
      return matchesQuery && (view.department === "All" || employee.department === view.department);
    });
    return records.sort((a, b) => {
      let first = a[view.sort];
      let second = b[view.sort];
      if (view.sort === "salary") return (first - second) * (view.direction === "asc" ? 1 : -1);
      return String(first).localeCompare(String(second)) * (view.direction === "asc" ? 1 : -1);
    });
  }

  function sortHeader(key, label, view, kind, numeric = false) {
    const active = view.sort === key;
    const direction = active ? view.direction : "none";
    const sortIcon = active ? (direction === "asc" ? "sortAsc" : "sortDesc") : "sort";
    return `<th scope="col" class="${numeric ? "numeric" : ""}" aria-sort="${active ? (direction === "asc" ? "ascending" : "descending") : "none"}"><button class="sort-button" type="button" data-action="sort-${kind}" data-sort="${key}">${escapeHtml(label)}<span class="sort-indicator" aria-hidden="true">${icon(sortIcon, "sort")}</span><span class="sr-only">${active ? `Sorted ${direction === "asc" ? "ascending" : "descending"}` : "Not sorted"}</span></button></th>`;
  }

  function departmentOptions(selected = "All", includeAll = true) {
    return `${includeAll ? '<option value="All">All departments</option>' : '<option value="">Select department</option>'}${departments.map(department => `<option value="${escapeHtml(department.name)}" ${selected === department.name ? "selected" : ""}>${escapeHtml(department.name)}</option>`).join("")}`;
  }

  function paginationMarkup(kind, total, view) {
    const totalPages = Math.max(1, Math.ceil(total / view.pageSize));
    const start = total === 0 ? 0 : (view.page - 1) * view.pageSize + 1;
    const end = Math.min(view.page * view.pageSize, total);
    const pages = Array.from({ length: totalPages }, (_, index) => index + 1).map(page => `<button class="button ${page === view.page ? "primary" : "secondary"} page-number" type="button" data-action="${kind}-page" data-page="${page}" aria-label="Page ${page}" ${page === view.page ? 'aria-current="page"' : ""}>${page}</button>`).join("");
    return `<div class="pagination"><span class="pagination-info">Showing ${start} to ${end} of ${total} records · Page ${view.page} of ${totalPages}</span><div class="pagination-controls"><button class="button secondary" type="button" data-action="${kind}-page" data-page="${view.page - 1}" ${view.page === 1 ? "disabled" : ""}>${icon("chevronLeft")}<span>Previous</span></button>${pages}<button class="button secondary" type="button" data-action="${kind}-page" data-page="${view.page + 1}" ${view.page === totalPages ? "disabled" : ""}><span>Next</span>${icon("chevronRight")}</button></div></div>`;
  }

  function renderEmployees() {
    const view = state.employeeView;
    const records = filteredEmployees();
    const totalPages = Math.max(1, Math.ceil(records.length / view.pageSize));
    if (view.page > totalPages) view.page = totalPages;
    const pageRecords = records.slice((view.page - 1) * view.pageSize, view.page * view.pageSize);
    const rows = pageRecords.map(employee => `<tr>
      <td><span class="cell-primary">${escapeHtml(employee.name)}</span><span class="cell-secondary mono">${escapeHtml(employee.id)}</span></td>
      <td>${escapeHtml(employee.department)}</td><td>${escapeHtml(employee.position)}</td><td>${escapeHtml(managerName(employee.managerId))}</td><td class="numeric">${money.format(employee.salary)}</td><td>${statusBadge(employee.status)}</td>
      <td><div class="button-row"><button class="button text" type="button" data-action="view-employee" data-id="${employee.id}">View</button><button class="button text" type="button" data-action="edit-employee" data-id="${employee.id}">Edit</button></div></td>
    </tr>`).join("");
    const cards = pageRecords.map(employee => `<article class="record-card">
      <div class="record-header"><div class="record-title"><strong>${escapeHtml(employee.name)}</strong><span class="mono">${escapeHtml(employee.id)}</span></div>${statusBadge(employee.status)}</div>
      <dl class="record-details"><div><dt>Department</dt><dd>${escapeHtml(employee.department)}</dd></div><div><dt>Position</dt><dd>${escapeHtml(employee.position)}</dd></div><div><dt>Manager</dt><dd>${escapeHtml(managerName(employee.managerId))}</dd></div><div><dt>Monthly salary</dt><dd class="numeric">${money.format(employee.salary)}</dd></div></dl>
      <div class="record-actions"><button class="button secondary" type="button" data-action="view-employee" data-id="${employee.id}">View details</button><button class="button secondary" type="button" data-action="edit-employee" data-id="${employee.id}">Edit employee</button></div>
    </article>`).join("");
    const content = pageRecords.length ? `<div class="desktop-table table-wrap"><table class="data-table"><caption class="sr-only">Employee directory</caption><thead><tr>${sortHeader("name", "Employee", view, "employees")}${sortHeader("department", "Department", view, "employees")}<th scope="col">Position</th><th scope="col">Manager</th>${sortHeader("salary", "Monthly salary", view, "employees", true)}${sortHeader("status", "Status", view, "employees")}<th scope="col">Actions</th></tr></thead><tbody>${rows}</tbody></table></div><div class="mobile-records">${cards}</div><div class="panel-footer">${paginationMarkup("employees", records.length, view)}</div>` : emptyMarkup("No employees match these filters", "Clear the search and department filter to restore the employee directory.", '<button class="button secondary" type="button" data-action="clear-employee-filters">Clear filters</button>');
    main.innerHTML = `<div class="page">
      ${pageHeader("Employee Management", "Search employee records, maintain workplace details, and manage employment status.", `<button class="button primary" type="button" data-action="add-employee">${icon("plus")}<span>Add employee</span></button>${refreshButton()}`)}
      <section class="filter-panel" aria-label="Employee filters">
        <div class="field od-field"><label for="employeeSearch">Search employees</label><div class="input-prefix search-control">${icon("search")}<input id="employeeSearch" type="search" value="${escapeHtml(view.query)}" placeholder="Name or employee number" autocomplete="off"></div></div>
        <div class="field od-field"><label for="employeeDepartmentFilter">Department</label><select id="employeeDepartmentFilter">${departmentOptions(view.department)}</select></div>
        <div class="field od-field"><label for="employeeSort">Sort records</label><select id="employeeSort"><option value="name-asc" ${view.sort === "name" && view.direction === "asc" ? "selected" : ""}>Name, A to Z</option><option value="id-asc" ${view.sort === "id" ? "selected" : ""}>Employee number</option><option value="salary-desc" ${view.sort === "salary" && view.direction === "desc" ? "selected" : ""}>Salary, high to low</option><option value="salary-asc" ${view.sort === "salary" && view.direction === "asc" ? "selected" : ""}>Salary, low to high</option><option value="status-asc" ${view.sort === "status" ? "selected" : ""}>Employment status</option></select></div>
      </section>
      <section class="panel" aria-labelledby="employeeDirectoryTitle"><div class="panel-header"><div class="panel-heading"><h2 id="employeeDirectoryTitle">Employee directory</h2><p>Sample records with monthly salaries in USD.</p></div><span class="data-chip">${records.length} results</span></div>${content}</section>
    </div>`;
  }

  function filteredProjects() {
    const view = state.projectView;
    const query = view.query.trim().toLowerCase();
    return state.projects.filter(project => {
      const matchesQuery = !query || project.name.toLowerCase().includes(query) || project.id.toLowerCase().includes(query);
      return matchesQuery && (view.department === "All" || project.department === view.department) && (view.status === "All" || project.status === view.status);
    }).sort((a, b) => {
      const first = a[view.sort];
      const second = b[view.sort];
      return String(first).localeCompare(String(second)) * (view.direction === "asc" ? 1 : -1);
    });
  }

  function renderProjects() {
    const view = state.projectView;
    const records = filteredProjects();
    const totalPages = Math.max(1, Math.ceil(records.length / view.pageSize));
    if (view.page > totalPages) view.page = totalPages;
    const pageRecords = records.slice((view.page - 1) * view.pageSize, view.page * view.pageSize);
    const rows = pageRecords.map(project => `<tr><td><span class="cell-primary">${escapeHtml(project.name)}</span><span class="cell-secondary mono">${escapeHtml(project.id)}</span></td><td>${escapeHtml(project.department)}</td><td><span class="od-nowrap">${formatDate(project.start)}</span><span class="cell-secondary">to ${formatDate(project.end)}</span></td><td>${statusBadge(project.status)}</td><td class="numeric">${activeAssignments(project.id).length}</td><td><button class="button text" type="button" data-action="view-project" data-id="${project.id}">View project</button></td></tr>`).join("");
    const cards = pageRecords.map(project => `<article class="record-card"><div class="record-header"><div class="record-title"><strong>${escapeHtml(project.name)}</strong><span class="mono">${escapeHtml(project.id)}</span></div>${statusBadge(project.status)}</div><dl class="record-details"><div><dt>Department</dt><dd>${escapeHtml(project.department)}</dd></div><div><dt>Active team</dt><dd>${activeAssignments(project.id).length} members</dd></div><div><dt>Start date</dt><dd class="od-nowrap">${formatDate(project.start)}</dd></div><div><dt>End date</dt><dd class="od-nowrap">${formatDate(project.end)}</dd></div></dl><div class="record-actions"><button class="button secondary" type="button" data-action="view-project" data-id="${project.id}">View project</button></div></article>`).join("");
    const content = pageRecords.length ? `<div class="desktop-table table-wrap"><table class="data-table"><caption class="sr-only">Project portfolio</caption><thead><tr>${sortHeader("name", "Project", view, "projects")}${sortHeader("department", "Department", view, "projects")}<th scope="col">Project dates</th>${sortHeader("status", "Status", view, "projects")}<th scope="col" class="numeric">Active team</th><th scope="col">Actions</th></tr></thead><tbody>${rows}</tbody></table></div><div class="mobile-records">${cards}</div><div class="panel-footer">${paginationMarkup("projects", records.length, view)}</div>` : emptyMarkup("No projects match these filters", "Clear the search and filters to restore the project portfolio.", '<button class="button secondary" type="button" data-action="clear-project-filters">Clear filters</button>');
    main.innerHTML = `<div class="page">
      ${pageHeader("Project Management", "Track project ownership, delivery dates, status, and active team allocations.", refreshButton())}
      <section class="filter-panel projects-filter" aria-label="Project filters"><div class="field od-field"><label for="projectSearch">Search projects</label><div class="input-prefix search-control">${icon("search")}<input id="projectSearch" type="search" value="${escapeHtml(view.query)}" placeholder="Project name or code" autocomplete="off"></div></div><div class="field od-field"><label for="projectDepartmentFilter">Department</label><select id="projectDepartmentFilter">${departmentOptions(view.department)}</select></div><div class="field od-field"><label for="projectStatusFilter">Status</label><select id="projectStatusFilter"><option value="All">All statuses</option>${["Active", "Planning", "Completed", "Cancelled"].map(status => `<option value="${status}" ${view.status === status ? "selected" : ""}>${status}</option>`).join("")}</select></div><div class="field od-field"><label for="projectSort">Sort records</label><select id="projectSort"><option value="code-asc" ${view.sort === "code" ? "selected" : ""}>Project code</option><option value="name-asc" ${view.sort === "name" ? "selected" : ""}>Project name</option><option value="department-asc" ${view.sort === "department" ? "selected" : ""}>Department</option><option value="status-asc" ${view.sort === "status" ? "selected" : ""}>Project status</option></select></div></section>
      <section class="panel" aria-labelledby="projectPortfolioTitle"><div class="panel-header"><div class="panel-heading"><h2 id="projectPortfolioTitle">Project portfolio</h2><p>Sample projects with active assignment counts.</p></div><span class="data-chip">${records.length} results</span></div>${content}</section>
    </div>`;
  }

  function renderProjectDetail() {
    const project = getProject(state.selectedProjectId);
    if (!project) {
      main.innerHTML = `<div class="page">${pageHeader("Project not found", "The requested sample project is no longer available.")}${emptyMarkup("Return to the project portfolio", "Choose an available project to view its details and assignments.", '<button class="button primary" type="button" data-route="projects">Back to projects</button>')}</div>`;
      return;
    }
    const team = activeAssignments(project.id);
    const canAssign = ["Active", "Planning"].includes(project.status);
    const assignButton = canAssign ? `<button class="button primary" type="button" data-action="assign-member" data-id="${project.id}">${icon("plus")}<span>Assign team member</span></button>` : "";
    const rows = team.map(assignment => {
      const employee = getEmployee(assignment.employeeId);
      if (!employee) return "";
      return `<tr><td><span class="cell-primary">${escapeHtml(employee.name)}</span><span class="cell-secondary mono">${escapeHtml(employee.id)}</span></td><td>${escapeHtml(assignment.role)}</td><td class="numeric">${assignment.allocation}%</td><td class="numeric">${currentAllocation(employee.id)}%</td></tr>`;
    }).join("");
    const cards = team.map(assignment => {
      const employee = getEmployee(assignment.employeeId);
      if (!employee) return "";
      return `<article class="record-card"><div class="record-header"><div class="record-title"><strong>${escapeHtml(employee.name)}</strong><span>${escapeHtml(employee.id)} · ${escapeHtml(employee.department)}</span></div><span class="badge method">${assignment.allocation}% here</span></div><dl class="record-details"><div><dt>Project role</dt><dd>${escapeHtml(assignment.role)}</dd></div><div><dt>Total active workload</dt><dd>${currentAllocation(employee.id)}%</dd></div></dl></article>`;
    }).join("");
    const teamContent = team.length ? `<div class="desktop-table table-wrap"><table class="data-table"><caption class="sr-only">Assigned team members</caption><thead><tr><th scope="col">Employee</th><th scope="col">Project role</th><th scope="col" class="numeric">Project allocation</th><th scope="col" class="numeric">Total active workload</th></tr></thead><tbody>${rows}</tbody></table></div><div class="mobile-records">${cards}</div>` : emptyMarkup("No team members assigned", canAssign ? "Assign the first team member to define project capacity." : "This project has no active assignments.", canAssign ? `<button class="button primary" type="button" data-action="assign-member" data-id="${project.id}">Assign team member</button>` : "");
    main.innerHTML = `<div class="page">
      <header class="page-header"><div class="page-heading"><nav class="breadcrumbs" aria-label="Breadcrumb"><button type="button" data-route="projects">Projects</button><span aria-hidden="true">/</span><span>${escapeHtml(project.id)}</span></nav><h1>${escapeHtml(project.name)}</h1><p class="page-description">${escapeHtml(project.description)}</p></div><div class="page-actions"><button class="button secondary" type="button" data-route="projects">Back to projects</button>${assignButton}</div></header>
      <section class="panel" aria-labelledby="projectInfoTitle"><div class="panel-header"><div class="panel-heading"><h2 id="projectInfoTitle">Project information</h2><p>Ownership, timing, and current delivery status.</p></div>${statusBadge(project.status)}</div><div class="panel-body"><dl class="project-meta"><div><dt>Project code</dt><dd class="mono">${escapeHtml(project.id)}</dd></div><div><dt>Owning department</dt><dd>${escapeHtml(project.department)}</dd></div><div><dt>Start date</dt><dd>${formatDate(project.start)}</dd></div><div><dt>End date</dt><dd>${formatDate(project.end)}</dd></div><div><dt>Active team members</dt><dd>${team.length}</dd></div><div><dt>Assignment availability</dt><dd>${canAssign ? "Open for team assignments" : "Closed for new assignments"}</dd></div></dl></div></section>
      <section class="panel" aria-labelledby="teamTitle"><div class="panel-header"><div class="panel-heading"><h2 id="teamTitle">Assigned team members</h2><p>Total active workload includes assignments across Active and Planning projects.</p></div><span class="data-chip">${team.length} assigned</span></div>${teamContent}</section>
    </div>`;
  }

  function filteredEndpoints() {
    const query = state.apiView.query.trim().toLowerCase();
    return apiEndpoints.filter(endpoint => !query || `${endpoint.method} ${endpoint.path} ${endpoint.summary}`.toLowerCase().includes(query));
  }

  function renderApi() {
    const endpoints = filteredEndpoints();
    if (!endpoints.some(endpoint => endpoint.id === state.apiView.selected)) state.apiView.selected = endpoints[0]?.id || null;
    const selected = apiEndpoints.find(endpoint => endpoint.id === state.apiView.selected);
    const list = endpoints.map(endpoint => `<button class="endpoint-item ${endpoint.id === state.apiView.selected ? "is-active" : ""}" type="button" data-action="select-api" data-id="${endpoint.id}" ${endpoint.id === state.apiView.selected ? 'aria-current="true"' : ""}><span class="badge method">${endpoint.method}</span><strong>${escapeHtml(endpoint.path)}</strong><span>${escapeHtml(endpoint.summary)}</span></button>`).join("");
    const detail = selected ? `<div class="panel-body api-detail"><div class="od-stack"><div class="endpoint-heading"><span class="badge method">${selected.method}</span><span class="endpoint-path">${escapeHtml(selected.path)}</span></div><p class="muted">${escapeHtml(selected.description)}</p></div><section class="api-section"><h3>Parameters</h3><table class="parameter-list"><tbody>${selected.parameters.map(([name, description]) => `<tr><th scope="row" class="mono">${escapeHtml(name)}</th><td>${escapeHtml(description)}</td></tr>`).join("")}</tbody></table></section><section class="api-section"><h3>Response status</h3><div class="status-list">${selected.statuses.map(([code, text]) => `<span class="status-code"><strong>${code}</strong><span>${escapeHtml(text)}</span></span>`).join("")}</div></section><section class="api-section"><div class="od-row"><h3 class="od-fill">Example response</h3><button class="button secondary" type="button" data-action="copy-endpoint" data-id="${selected.id}">${icon("copy")}<span>Copy response</span></button></div><pre class="code-block"><code>${escapeHtml(selected.response)}</code></pre></section></div>` : "";
    main.innerHTML = `<div class="page">
      ${pageHeader("API Documentation", "Review REST endpoints, parameters, response statuses, and sample payloads for the workspace.", refreshButton())}
      <section class="filter-panel" aria-label="API endpoint filters"><div class="field od-field"><label for="apiSearch">Search endpoints</label><div class="input-prefix search-control">${icon("search")}<input id="apiSearch" type="search" value="${escapeHtml(state.apiView.query)}" placeholder="Method, path, or description" autocomplete="off"></div></div></section>
      ${endpoints.length ? `<div class="api-layout"><section class="panel" aria-labelledby="endpointListTitle"><div class="panel-header"><div class="panel-heading"><h2 id="endpointListTitle">Endpoints</h2><p>${endpoints.length} available in this sample contract.</p></div></div><div class="endpoint-list">${list}</div></section><section class="panel" aria-label="Endpoint details">${detail}</section></div>` : `<section class="panel">${emptyMarkup("No endpoints match your search", "Clear the search to restore the REST endpoint directory.", '<button class="button secondary" type="button" data-action="clear-api-search">Clear search</button>')}</section>`}
    </div>`;
  }

  function setMobileNavigation(open, returnFocus = true) {
    const mobile = window.matchMedia("(max-width: 1023px)").matches;
    const shouldOpen = mobile && open;
    document.body.classList.toggle("nav-open", shouldOpen);
    navScrim.hidden = !shouldOpen;
    menuButton.setAttribute("aria-expanded", String(shouldOpen));
    if (mobile && !shouldOpen) sidebar.setAttribute("inert", "");
    else sidebar.removeAttribute("inert");
    if (shouldOpen) sidebar.querySelector(".nav-item.is-active")?.focus();
    else if (returnFocus && mobile && document.activeElement && sidebar.contains(document.activeElement)) menuButton.focus();
  }

  function showToast(title, message, type = "success") {
    const toast = document.createElement("div");
    toast.className = `toast ${type}`;
    toast.innerHTML = `${icon(type === "success" ? "checkCircle" : "alert")}<div class="toast-content"><strong>${escapeHtml(title)}</strong><span>${escapeHtml(message)}</span></div><button class="icon-button" type="button" aria-label="Dismiss notification">${icon("close")}</button>`;
    document.getElementById("toastRegion").append(toast);
    window.setTimeout(() => toast.remove(), 5000);
  }

  function closeDialog(id) {
    const dialog = document.getElementById(id);
    if (dialog?.open && dialog.dataset.busy !== "true") dialog.close();
  }

  function clearFormErrors(form) {
    form.querySelectorAll("[aria-invalid]").forEach(field => field.removeAttribute("aria-invalid"));
    form.querySelectorAll(".field-error").forEach(error => {
      error.hidden = true;
      error.textContent = "";
    });
    const summary = form.querySelector(".error-summary");
    if (summary) {
      summary.hidden = true;
      summary.innerHTML = "";
    }
  }

  function setFieldError(fieldId, errorId, message) {
    const field = document.getElementById(fieldId);
    const error = document.getElementById(errorId);
    if (!field || !error) return message;
    field.setAttribute("aria-invalid", "true");
    error.textContent = message;
    error.hidden = false;
    return message;
  }

  function clearFieldError(fieldId, errorId) {
    const field = document.getElementById(fieldId);
    const error = document.getElementById(errorId);
    field?.removeAttribute("aria-invalid");
    if (error) {
      error.hidden = true;
      error.textContent = "";
    }
  }

  function showErrorSummary(form, errors) {
    const summary = form.querySelector(".error-summary");
    if (!summary) return;
    if (!errors.length) {
      summary.hidden = true;
      summary.innerHTML = "";
      return;
    }
    summary.innerHTML = `<strong>Review ${errors.length} ${errors.length === 1 ? "field" : "fields"} before saving.</strong><ul>${errors.map(error => `<li><a href="#${error.id}">${escapeHtml(error.message)}</a></li>`).join("")}</ul>`;
    summary.hidden = false;
    summary.focus();
  }

  function populatePositions(department, selected = "") {
    const select = document.getElementById("employeePosition");
    const definition = departments.find(item => item.name === department);
    const positions = definition?.positions || [];
    select.innerHTML = positions.length ? `<option value="">Select position</option>${positions.map(position => `<option value="${escapeHtml(position)}" ${selected === position ? "selected" : ""}>${escapeHtml(position)}</option>`).join("")}` : '<option value="">Select a department first</option>';
    select.disabled = !positions.length;
    if (selected && positions.includes(selected)) select.value = selected;
  }

  function populateManagers(department, selected = "", excludeId = null) {
    const select = document.getElementById("employeeManager");
    const managers = state.employees.filter(employee => employee.department === department && employee.status === "Active" && employee.id !== excludeId && (employee.position.includes("Manager") || employee.position === "People Director"));
    select.innerHTML = `<option value="">Not assigned</option>${managers.map(manager => `<option value="${manager.id}" ${selected === manager.id ? "selected" : ""}>${escapeHtml(manager.name)} · ${escapeHtml(manager.position)}</option>`).join("")}`;
    select.disabled = !department;
    if (selected && managers.some(manager => manager.id === selected)) select.value = selected;
  }

  function openEmployeeDialog(employeeId = null) {
    editingEmployeeId = employeeId;
    const employee = employeeId ? getEmployee(employeeId) : null;
    employeeForm.reset();
    clearFormErrors(employeeForm);
    document.getElementById("employeeDialogTitle").textContent = employee ? "Edit employee" : "Add employee";
    document.getElementById("employeeDialogDescription").textContent = employee ? `Update ${employee.name}’s workplace details.` : "Enter the employee’s workplace details.";
    const departmentSelect = document.getElementById("employeeDepartment");
    departmentSelect.innerHTML = departmentOptions(employee?.department || "", false);
    departmentSelect.value = employee?.department || "";
    populatePositions(employee?.department || "", employee?.position || "");
    populateManagers(employee?.department || "", employee?.managerId || "", employeeId);
    document.getElementById("employeeNumber").value = employee?.id || "";
    document.getElementById("employeeNumber").readOnly = Boolean(employee);
    document.getElementById("employeeName").value = employee?.name || "";
    document.getElementById("employeeSalary").value = employee?.salary || "";
    document.getElementById("employeeStatus").value = employee?.status || "Active";
    employeeDialog.dataset.busy = "false";
    employeeDialog.showModal();
    document.getElementById(employee ? "employeeName" : "employeeNumber").focus();
  }

  function validateEmployeeField(field) {
    const errors = [];
    const value = field.value.trim();
    if (field.id === "employeeNumber") {
      if (!value) errors.push({ id: field.id, message: "Enter an employee number." });
      else if (state.employees.some(employee => employee.id.toLowerCase() === value.toLowerCase() && employee.id !== editingEmployeeId)) errors.push({ id: field.id, message: "This employee number already exists. Use a unique number." });
    }
    if (field.id === "employeeName" && value.length < 2) errors.push({ id: field.id, message: "Enter the employee’s full name." });
    if (field.id === "employeeDepartment" && !value) errors.push({ id: field.id, message: "Select a department." });
    if (field.id === "employeePosition" && !value) errors.push({ id: field.id, message: "Select a position for this department." });
    if (field.id === "employeeSalary") {
      const salary = Number(value);
      if (!value || !Number.isFinite(salary) || salary <= 0) errors.push({ id: field.id, message: "Enter a monthly salary greater than $0." });
      else if (salary > 100000) errors.push({ id: field.id, message: "Monthly salary cannot exceed $100,000. Check the amount." });
    }
    const errorIds = { employeeNumber: "employeeNumberError", employeeName: "employeeNameError", employeeDepartment: "departmentError", employeePosition: "positionError", employeeSalary: "salaryError" };
    if (errors.length) setFieldError(field.id, errorIds[field.id], errors[0].message);
    else clearFieldError(field.id, errorIds[field.id]);
    return errors;
  }

  function setDialogBusy(dialog, busy, activeButton) {
    dialog.dataset.busy = String(busy);
    dialog.querySelectorAll("button, input, select").forEach(control => { control.disabled = busy; });
    if (activeButton) {
      if (busy) {
        activeButton.dataset.idleLabel = activeButton.textContent;
        activeButton.textContent = "Saving…";
      } else activeButton.textContent = activeButton.dataset.idleLabel || activeButton.textContent;
    }
  }

  function saveEmployee() {
    if (employeeDialog.dataset.busy === "true") return;
    clearFormErrors(employeeForm);
    const fields = ["employeeNumber", "employeeName", "employeeDepartment", "employeePosition", "employeeSalary"].map(id => document.getElementById(id));
    const errors = fields.flatMap(validateEmployeeField);
    showErrorSummary(employeeForm, errors);
    if (errors.length) return;
    const values = {
      id: document.getElementById("employeeNumber").value.trim().toUpperCase(),
      name: document.getElementById("employeeName").value.trim(),
      department: document.getElementById("employeeDepartment").value,
      position: document.getElementById("employeePosition").value,
      managerId: document.getElementById("employeeManager").value || null,
      salary: Number(document.getElementById("employeeSalary").value),
      status: document.getElementById("employeeStatus").value
    };
    const saveButton = document.getElementById("saveEmployee");
    setDialogBusy(employeeDialog, true, saveButton);
    window.setTimeout(() => {
      if (editingEmployeeId) {
        const index = state.employees.findIndex(employee => employee.id === editingEmployeeId);
        if (index >= 0) state.employees[index] = values;
      } else state.employees.push(values);
      setDialogBusy(employeeDialog, false, saveButton);
      employeeDialog.close();
      renderRoute();
      showToast(editingEmployeeId ? "Employee updated" : "Employee added", `${values.name} is now reflected across the workspace.`);
      document.querySelector('[data-action="add-employee"]')?.focus();
    }, 320);
  }

  function openEmployeeDetails(employeeId) {
    const employee = getEmployee(employeeId);
    if (!employee) return;
    document.getElementById("employeeDetailsTitle").textContent = employee.name;
    document.getElementById("employeeDetailsContent").innerHTML = `<div class="od-stack"><div class="detail-heading"><p>${escapeHtml(employee.id)} · ${escapeHtml(employee.department)}</p>${statusBadge(employee.status)}</div><dl class="details-grid"><div><dt>Position</dt><dd>${escapeHtml(employee.position)}</dd></div><div><dt>Manager</dt><dd>${escapeHtml(managerName(employee.managerId))}</dd></div><div><dt>Monthly salary</dt><dd>${money.format(employee.salary)}</dd></div><div><dt>Employment status</dt><dd>${escapeHtml(employee.status)}</dd></div></dl>${employee.status === "Active" ? `<div class="button-row"><button class="button danger" type="button" data-action="deactivate-employee" data-id="${employee.id}">Deactivate employee</button></div>` : ""}</div>`;
    document.getElementById("editFromDetails").dataset.id = employee.id;
    document.getElementById("employeeDetailsDialog").showModal();
  }

  function openDeactivation(employeeId) {
    const employee = getEmployee(employeeId);
    if (!employee) return;
    pendingDeactivateId = employeeId;
    document.getElementById("confirmDescription").textContent = `${employee.name} will no longer count toward active workforce totals or monthly salary expenses. Their record and project history will remain available.`;
    if (document.getElementById("employeeDetailsDialog").open) document.getElementById("employeeDetailsDialog").close();
    confirmDialog.showModal();
    document.querySelector('[data-close-dialog="confirmDialog"].button')?.focus();
  }

  function openAssignmentDialog(projectId) {
    const project = getProject(projectId);
    if (!project || !["Active", "Planning"].includes(project.status)) return;
    assignmentProjectId = projectId;
    assignmentForm.reset();
    clearFormErrors(assignmentForm);
    document.getElementById("assignmentProjectName").textContent = `${project.id} · ${project.name}`;
    const employees = state.employees.filter(employee => employee.status === "Active").sort((a, b) => a.name.localeCompare(b.name));
    document.getElementById("assignmentEmployee").innerHTML = `<option value="">Select employee</option>${employees.map(employee => `<option value="${employee.id}">${escapeHtml(employee.name)} · ${escapeHtml(employee.department)} · ${currentAllocation(employee.id)}% allocated</option>`).join("")}`;
    assignmentDialog.dataset.busy = "false";
    updateAssignmentWorkload();
    assignmentDialog.showModal();
    document.getElementById("assignmentEmployee").focus();
  }

  function updateAssignmentWorkload() {
    const employeeId = document.getElementById("assignmentEmployee").value;
    const allocation = Number(document.getElementById("assignmentAllocation").value) || 0;
    const container = document.getElementById("assignmentWorkload");
    if (!employeeId) {
      container.className = "workload-card od-stack";
      container.innerHTML = "<p>Select an employee to see their current active allocation and remaining capacity.</p>";
      return;
    }
    const employee = getEmployee(employeeId);
    const current = currentAllocation(employeeId);
    const proposed = current + allocation;
    const over = proposed > 100;
    container.className = `workload-card od-stack ${over ? "warning" : ""}`;
    container.innerHTML = `<div class="workload-header"><span>${escapeHtml(employee.name)}</span><span>${current}% currently allocated</span></div><progress max="100" value="${Math.min(current, 100)}" aria-label="Current active allocation for ${escapeHtml(employee.name)}">${current}%</progress><p>${over ? `This assignment would reach ${proposed}%, exceeding the 100% limit by ${proposed - 100}%.` : allocation ? `With this assignment: ${proposed}%. Remaining capacity: ${100 - proposed}%.` : `Remaining capacity before this assignment: ${100 - current}%.`}</p>`;
  }

  function validateAssignment() {
    clearFormErrors(assignmentForm);
    const employeeId = document.getElementById("assignmentEmployee").value;
    const role = document.getElementById("assignmentRole").value.trim();
    const allocation = Number(document.getElementById("assignmentAllocation").value);
    const errors = [];
    if (!employeeId) errors.push({ id: "assignmentEmployee", message: "Select an employee for this assignment." });
    else if (state.assignments.some(assignment => assignment.active && assignment.projectId === assignmentProjectId && assignment.employeeId === employeeId)) errors.push({ id: "assignmentEmployee", message: "This employee is already assigned to the project. Choose another employee." });
    if (role.length < 2) errors.push({ id: "assignmentRole", message: "Enter a clear project role, such as Technical lead." });
    if (!Number.isFinite(allocation) || allocation < 1 || allocation > 100) errors.push({ id: "assignmentAllocation", message: "Enter an allocation between 1% and 100%." });
    else if (employeeId && currentAllocation(employeeId) + allocation > 100) {
      const available = 100 - currentAllocation(employeeId);
      errors.push({ id: "assignmentAllocation", message: `Total active allocation would exceed 100%. Reduce this assignment to ${available}% or less.` });
    }
    if (!employeeId) setFieldError("assignmentEmployee", "assignmentEmployeeError", errors.find(error => error.id === "assignmentEmployee")?.message || "Select an employee.");
    else if (errors.some(error => error.id === "assignmentEmployee")) setFieldError("assignmentEmployee", "assignmentEmployeeError", errors.find(error => error.id === "assignmentEmployee").message);
    if (errors.some(error => error.id === "assignmentRole")) setFieldError("assignmentRole", "assignmentRoleError", errors.find(error => error.id === "assignmentRole").message);
    if (errors.some(error => error.id === "assignmentAllocation")) setFieldError("assignmentAllocation", "assignmentAllocationError", errors.find(error => error.id === "assignmentAllocation").message);
    showErrorSummary(assignmentForm, errors);
    return { valid: errors.length === 0, employeeId, role, allocation };
  }

  function saveAssignment() {
    if (assignmentDialog.dataset.busy === "true") return;
    const result = validateAssignment();
    if (!result.valid) return;
    const saveButton = document.getElementById("saveAssignment");
    setDialogBusy(assignmentDialog, true, saveButton);
    window.setTimeout(() => {
      state.assignments.push({ projectId: assignmentProjectId, employeeId: result.employeeId, role: result.role, allocation: result.allocation, active: true });
      const employee = getEmployee(result.employeeId);
      setDialogBusy(assignmentDialog, false, saveButton);
      assignmentDialog.close();
      renderRoute();
      showToast("Team member assigned", `${employee.name} now has ${currentAllocation(employee.id)}% total active allocation.`);
      document.querySelector('[data-action="assign-member"]')?.focus();
    }, 320);
  }

  async function copyText(text) {
    try {
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
        return true;
      }
      const textarea = document.createElement("textarea");
      textarea.value = text;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.append(textarea);
      textarea.select();
      const copied = document.execCommand("copy");
      textarea.remove();
      return copied;
    } catch {
      return false;
    }
  }

  document.querySelectorAll("[data-route]").forEach(button => button.addEventListener("click", () => navigate(button.dataset.route)));
  menuButton.addEventListener("click", () => setMobileNavigation(menuButton.getAttribute("aria-expanded") !== "true"));
  navScrim.addEventListener("click", () => setMobileNavigation(false));
  window.addEventListener("resize", () => setMobileNavigation(false, false));
  document.addEventListener("keydown", event => {
    if (event.key === "Escape" && document.body.classList.contains("nav-open")) setMobileNavigation(false);
  });

  document.addEventListener("click", async event => {
    const close = event.target.closest("[data-close-dialog]");
    if (close) {
      closeDialog(close.dataset.closeDialog);
      return;
    }
    const route = event.target.closest("main [data-route]");
    if (route) {
      navigate(route.dataset.route);
      return;
    }
    const errorLink = event.target.closest(".error-summary a");
    if (errorLink) {
      event.preventDefault();
      document.querySelector(errorLink.getAttribute("href"))?.focus();
      return;
    }
    const button = event.target.closest("[data-action]");
    if (!button) return;
    const action = button.dataset.action;
    const id = button.dataset.id;
    if (action === "refresh") renderWithLoading();
    else if (action === "add-employee") openEmployeeDialog();
    else if (action === "edit-employee") openEmployeeDialog(id);
    else if (action === "view-employee") openEmployeeDetails(id);
    else if (action === "deactivate-employee") openDeactivation(id);
    else if (action === "view-project") navigate("project-detail", id);
    else if (action === "assign-member") openAssignmentDialog(id);
    else if (action === "clear-employee-filters") {
      state.employeeView.query = "";
      state.employeeView.department = "All";
      state.employeeView.page = 1;
      renderRoute("employeeSearch");
    } else if (action === "clear-project-filters") {
      state.projectView.query = "";
      state.projectView.department = "All";
      state.projectView.status = "All";
      state.projectView.page = 1;
      renderRoute("projectSearch");
    } else if (action === "clear-api-search") {
      state.apiView.query = "";
      renderRoute("apiSearch");
    } else if (action === "employees-page" || action === "projects-page") {
      const view = action === "employees-page" ? state.employeeView : state.projectView;
      view.page = Number(button.dataset.page);
      renderRoute();
      document.querySelector(`.panel-footer [data-action="${action}"][data-page="${view.page}"]`)?.focus();
    } else if (action === "sort-employees" || action === "sort-projects") {
      const view = action === "sort-employees" ? state.employeeView : state.projectView;
      const key = button.dataset.sort;
      if (view.sort === key) view.direction = view.direction === "asc" ? "desc" : "asc";
      else {
        view.sort = key;
        view.direction = "asc";
      }
      view.page = 1;
      renderRoute();
    } else if (action === "select-api") {
      state.apiView.selected = id;
      renderRoute();
      document.querySelector(`[data-action="select-api"][data-id="${id}"]`)?.focus();
    } else if (action === "copy-endpoint") {
      const endpoint = apiEndpoints.find(item => item.id === id);
      const copied = endpoint ? await copyText(endpoint.response) : false;
      showToast(copied ? "Response copied" : "Copy unavailable", copied ? "The sample JSON response is on your clipboard." : "Select the response text and copy it manually.", copied ? "success" : "error");
    }
  });

  main.addEventListener("input", event => {
    const target = event.target;
    if (!["employeeSearch", "projectSearch", "apiSearch"].includes(target.id)) return;
    const cursor = target.selectionStart;
    if (target.id === "employeeSearch") {
      state.employeeView.query = target.value;
      state.employeeView.page = 1;
    } else if (target.id === "projectSearch") {
      state.projectView.query = target.value;
      state.projectView.page = 1;
    } else state.apiView.query = target.value;
    renderRoute(target.id, cursor);
  });

  main.addEventListener("change", event => {
    const target = event.target;
    if (target.id === "employeeDepartmentFilter") {
      state.employeeView.department = target.value;
      state.employeeView.page = 1;
      renderRoute(target.id);
    } else if (target.id === "employeeSort") {
      [state.employeeView.sort, state.employeeView.direction] = target.value.split("-");
      state.employeeView.page = 1;
      renderRoute(target.id);
    } else if (target.id === "projectDepartmentFilter") {
      state.projectView.department = target.value;
      state.projectView.page = 1;
      renderRoute(target.id);
    } else if (target.id === "projectStatusFilter") {
      state.projectView.status = target.value;
      state.projectView.page = 1;
      renderRoute(target.id);
    } else if (target.id === "projectSort") {
      [state.projectView.sort, state.projectView.direction] = target.value.split("-");
      state.projectView.page = 1;
      renderRoute(target.id);
    }
  });

  document.getElementById("employeeDepartment").addEventListener("change", event => {
    populatePositions(event.target.value);
    populateManagers(event.target.value, "", editingEmployeeId);
    clearFieldError("employeeDepartment", "departmentError");
  });
  employeeForm.querySelectorAll("input, select").forEach(field => field.addEventListener("blur", () => {
    if (["employeeNumber", "employeeName", "employeeDepartment", "employeePosition", "employeeSalary"].includes(field.id)) validateEmployeeField(field);
  }));
  employeeForm.addEventListener("keydown", event => {
    if (event.key === "Enter" && event.target.tagName !== "BUTTON") {
      event.preventDefault();
      saveEmployee();
    }
  });
  employeeForm.addEventListener("submit", event => event.preventDefault());
  document.getElementById("saveEmployee").addEventListener("click", saveEmployee);
  document.getElementById("editFromDetails").addEventListener("click", event => {
    document.getElementById("employeeDetailsDialog").close();
    openEmployeeDialog(event.currentTarget.dataset.id);
  });
  document.getElementById("confirmDeactivate").addEventListener("click", () => {
    const employee = getEmployee(pendingDeactivateId);
    if (!employee) return;
    employee.status = "Inactive";
    confirmDialog.close();
    renderRoute();
    showToast("Employee deactivated", `${employee.name} no longer counts toward active totals or salary expenses.`);
    document.querySelector('[data-action="add-employee"]')?.focus();
  });

  document.getElementById("assignmentEmployee").addEventListener("change", () => {
    clearFieldError("assignmentEmployee", "assignmentEmployeeError");
    updateAssignmentWorkload();
  });
  document.getElementById("assignmentAllocation").addEventListener("input", updateAssignmentWorkload);
  document.getElementById("assignmentAllocation").addEventListener("blur", () => {
    const allocation = Number(document.getElementById("assignmentAllocation").value);
    const employeeId = document.getElementById("assignmentEmployee").value;
    if (!employeeId || !allocation) return;
    if (currentAllocation(employeeId) + allocation > 100) setFieldError("assignmentAllocation", "assignmentAllocationError", `Total active allocation would exceed 100%. Reduce this assignment to ${100 - currentAllocation(employeeId)}% or less.`);
    else clearFieldError("assignmentAllocation", "assignmentAllocationError");
  });
  assignmentForm.addEventListener("keydown", event => {
    if (event.key === "Enter" && event.target.tagName !== "BUTTON") {
      event.preventDefault();
      saveAssignment();
    }
  });
  assignmentForm.addEventListener("submit", event => event.preventDefault());
  document.getElementById("saveAssignment").addEventListener("click", saveAssignment);

  [employeeDialog, assignmentDialog].forEach(dialog => dialog.addEventListener("cancel", event => {
    if (dialog.dataset.busy === "true") event.preventDefault();
  }));
  document.querySelectorAll("dialog").forEach(dialog => dialog.addEventListener("click", event => {
    if (event.target === dialog) closeDialog(dialog.id);
  }));
  document.getElementById("toastRegion").addEventListener("click", event => {
    if (event.target.closest("button")) event.target.closest(".toast")?.remove();
  });

  setMobileNavigation(false, false);
  updateNavigation();
  renderRoute();
})();
