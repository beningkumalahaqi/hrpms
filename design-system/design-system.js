(() => {
  "use strict";

  const money = new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 0
  });

  const sampleEmployees = [
    { id: "EMP-001", name: "Elena Petrova", department: "Engineering" },
    { id: "EMP-002", name: "Owen Bennett", department: "Engineering" },
    { id: "EMP-004", name: "Ingrid Holm", department: "Engineering" },
    { id: "EMP-006", name: "Nora Alvarez", department: "Operations" },
    { id: "EMP-008", name: "Priya Nair", department: "Finance" },
    { id: "EMP-014", name: "Lena Fischer", department: "Marketing" }
  ];

  const departmentData = {
    Engineering: {
      positions: ["Engineering Manager", "Senior Engineer", "Software Engineer", "QA Engineer"],
      managers: ["Elena Petrova"]
    },
    Operations: {
      positions: ["Operations Manager", "Operations Specialist", "Project Coordinator"],
      managers: ["Marcus Lee"]
    },
    Finance: {
      positions: ["Finance Manager", "Financial Analyst", "Accounts Specialist"],
      managers: ["Priya Nair"]
    },
    People: {
      positions: ["People Director", "HR Business Partner", "HR Coordinator"],
      managers: ["Sofia Martinez"]
    },
    Marketing: {
      positions: ["Marketing Manager", "Content Strategist", "Marketing Analyst"],
      managers: ["Daniel Kim"]
    }
  };

  const budgetRecords = [
    { department: "Engineering", employees: 3, budget: 23000, expense: 24500 },
    { department: "Operations", employees: 3, budget: 18000, expense: 16600 },
    { department: "Finance", employees: 3, budget: 17500, expense: 17800 },
    { department: "People", employees: 2, budget: 12000, expense: 11500 },
    { department: "Marketing", employees: 3, budget: 16000, expense: 15200 }
  ];

  const tableView = {
    sort: "department",
    direction: "asc",
    page: 1,
    status: "All",
    pageSize: 3
  };

  const main = document.getElementById("main-content");
  const sidebar = document.getElementById("dsSidebar");
  const menuButton = document.getElementById("dsMenuButton");
  const navScrim = document.getElementById("dsNavScrim");
  const toastRegion = document.getElementById("dsToastRegion");
  const sectionSelect = document.getElementById("dsSectionSelect");
  const tocLinks = Array.from(document.querySelectorAll(".ds-toc a"));
  const tocTargets = tocLinks
    .map(link => document.getElementById(link.getAttribute("href").slice(1)))
    .filter(Boolean);
  const formDialog = document.getElementById("dsFormDialog");
  const confirmDialog = document.getElementById("dsConfirmDialog");
  const demoForm = document.getElementById("dsDemoForm");
  let lastDialogTrigger = null;
  let tocFrame = 0;

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, character => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
      "'": "&#39;"
    })[character]);
  }

  function icon(name, size = "control") {
    return `<svg class="od-icon od-icon-${size}" aria-hidden="true"><use href="#i-${name}"/></svg>`;
  }

  function budgetBadge(over) {
    return `<span class="badge ${over ? "over" : "within"}">${icon(over ? "alert" : "check-circle", "status")}<span>${over ? "Over budget" : "Within budget"}</span></span>`;
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
    else if (returnFocus && mobile && sidebar.contains(document.activeElement)) menuButton.focus();
  }

  function showToast(title, message, type = "success") {
    const toast = document.createElement("div");
    const iconName = type === "success" ? "check-circle" : type === "error" ? "alert" : "clock";
    toast.className = `toast ${type}`;
    toast.innerHTML = `${icon(iconName)}<div class="toast-content"><strong>${escapeHtml(title)}</strong><span>${escapeHtml(message)}</span></div><button class="icon-button" type="button" data-ds-dismiss-toast aria-label="Dismiss notification">${icon("close")}</button>`;
    toastRegion.append(toast);
    window.setTimeout(() => toast.remove(), 5000);
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

  function setDemoError(fieldId, errorId, message) {
    const field = document.getElementById(fieldId);
    const error = document.getElementById(errorId);
    field?.setAttribute("aria-invalid", "true");
    if (error) {
      error.textContent = message;
      error.hidden = false;
    }
  }

  function clearDemoError(fieldId, errorId) {
    const field = document.getElementById(fieldId);
    const error = document.getElementById(errorId);
    field?.removeAttribute("aria-invalid");
    if (error) {
      error.textContent = "";
      error.hidden = true;
    }
  }

  function openDialog(type) {
    const dialog = type === "confirm" ? confirmDialog : formDialog;
    lastDialogTrigger = document.activeElement;
    if (type === "form") {
      demoForm.reset();
      clearDemoError("dsDemoName", "dsDemoNameError");
      clearDemoError("dsDemoSalary", "dsDemoSalaryError");
      dialog.showModal();
      document.getElementById("dsDemoName").focus();
    } else {
      dialog.showModal();
      dialog.querySelector(".dialog-actions .button")?.focus();
    }
  }

  function closeDialog(id) {
    const dialog = document.getElementById(id);
    if (dialog?.open) dialog.close();
  }

  function saveDemoEmployee() {
    clearDemoError("dsDemoName", "dsDemoNameError");
    clearDemoError("dsDemoSalary", "dsDemoSalaryError");
    const name = document.getElementById("dsDemoName").value.trim();
    const salaryValue = document.getElementById("dsDemoSalary").value;
    const salary = Number(salaryValue);
    let firstInvalid = null;

    if (name.length < 2) {
      setDemoError("dsDemoName", "dsDemoNameError", "Enter the employee’s full name.");
      firstInvalid = document.getElementById("dsDemoName");
    }
    if (!salaryValue || !Number.isFinite(salary) || salary <= 0) {
      setDemoError("dsDemoSalary", "dsDemoSalaryError", "Enter a monthly salary greater than $0.");
      firstInvalid = firstInvalid || document.getElementById("dsDemoSalary");
    } else if (salary > 100000) {
      setDemoError("dsDemoSalary", "dsDemoSalaryError", "Monthly salary cannot exceed $100,000. Check the amount.");
      firstInvalid = firstInvalid || document.getElementById("dsDemoSalary");
    }
    if (firstInvalid) {
      firstInvalid.focus();
      return;
    }
    formDialog.close();
    showToast("Sample employee added", `${name} would now appear in the employee directory.`);
  }

  function renderSearchResults() {
    const container = document.getElementById("dsSearchResults");
    const input = document.getElementById("dsEmployeeSearch");
    if (!container || !input) return;
    const query = input.value.trim().toLowerCase();
    const records = sampleEmployees.filter(employee =>
      !query || employee.name.toLowerCase().includes(query) || employee.id.toLowerCase().includes(query)
    );
    container.innerHTML = records.length
      ? `<div class="ds-search-result"><div><strong>${escapeHtml(records[0].name)}</strong><span>${escapeHtml(records[0].id)} · ${escapeHtml(records[0].department)}</span></div><span class="badge active">${icon("check-circle", "status")}<span>Active</span></span></div>${records.slice(1).map(employee => `<div class="ds-search-result"><div><strong>${escapeHtml(employee.name)}</strong><span>${escapeHtml(employee.id)} · ${escapeHtml(employee.department)}</span></div><span class="badge active">${icon("check-circle", "status")}<span>Active</span></span></div>`).join("")}`
      : '<div class="ds-search-empty">No sample employees match this search. Clear the field to restore the list.</div>';
  }

  function updateDependentFields() {
    const department = document.getElementById("dsDepartment").value;
    const position = document.getElementById("dsPosition");
    const manager = document.getElementById("dsManager");
    const data = departmentData[department];
    position.disabled = !data;
    manager.disabled = !data;
    position.innerHTML = data
      ? `<option value="">Select position</option>${data.positions.map(item => `<option>${escapeHtml(item)}</option>`).join("")}`
      : '<option value="">Select a department first</option>';
    manager.innerHTML = data
      ? `<option value="">Not assigned</option>${data.managers.map(item => `<option>${escapeHtml(item)}</option>`).join("")}`
      : '<option value="">Select a department first</option>';
  }

  function sortedBudgetRecords() {
    const records = budgetRecords
      .map(record => ({ ...record, variance: record.budget - record.expense }))
      .filter(record => {
        if (tableView.status === "Over") return record.expense > record.budget;
        if (tableView.status === "Within") return record.expense <= record.budget;
        return true;
      });
    const direction = tableView.direction === "asc" ? 1 : -1;
    return records.sort((first, second) => {
      const a = first[tableView.sort];
      const b = second[tableView.sort];
      if (typeof a === "number" && typeof b === "number") return (a - b) * direction;
      return String(a).localeCompare(String(b)) * direction;
    });
  }

  function sortHeader(key, label, numeric = false) {
    const active = tableView.sort === key;
    const sortIcon = active ? (tableView.direction === "asc" ? "sort-asc" : "sort-desc") : "sort";
    return `<th scope="col" class="${numeric ? "numeric" : ""}" aria-sort="${active ? (tableView.direction === "asc" ? "ascending" : "descending") : "none"}"><button class="sort-button" type="button" data-ds-sort="${key}">${escapeHtml(label)}<span class="sort-indicator" aria-hidden="true">${icon(sortIcon, "sort")}</span><span class="sr-only">${active ? `Sorted ${tableView.direction === "asc" ? "ascending" : "descending"}` : "Not sorted"}</span></button></th>`;
  }

  function paginationMarkup(total) {
    const totalPages = Math.max(1, Math.ceil(total / tableView.pageSize));
    const start = total === 0 ? 0 : (tableView.page - 1) * tableView.pageSize + 1;
    const end = Math.min(tableView.page * tableView.pageSize, total);
    const pages = Array.from({ length: totalPages }, (_, index) => index + 1)
      .map(page => `<button class="button ${page === tableView.page ? "primary" : "secondary"} page-number" type="button" data-ds-page="${page}" aria-label="Page ${page}" ${page === tableView.page ? 'aria-current="page"' : ""}>${page}</button>`)
      .join("");
    return `<div class="pagination"><span class="pagination-info">Showing ${start} to ${end} of ${total} departments · Page ${tableView.page} of ${totalPages}</span><div class="pagination-controls"><button class="button secondary" type="button" data-ds-page="${tableView.page - 1}" ${tableView.page === 1 ? "disabled" : ""}>${icon("chevron-left")}<span>Previous</span></button>${pages}<button class="button secondary" type="button" data-ds-page="${tableView.page + 1}" ${tableView.page === totalPages ? "disabled" : ""}><span>Next</span>${icon("chevron-right")}</button></div></div>`;
  }

  function renderBudgetTable() {
    const records = sortedBudgetRecords();
    const totalPages = Math.max(1, Math.ceil(records.length / tableView.pageSize));
    tableView.page = Math.min(tableView.page, totalPages);
    const pageRecords = records.slice((tableView.page - 1) * tableView.pageSize, tableView.page * tableView.pageSize);
    const rows = pageRecords.map(record => {
      const variance = record.budget - record.expense;
      const over = record.expense > record.budget;
      return `<tr class="${over ? "over-budget" : ""}"><td><span class="cell-primary">${escapeHtml(record.department)}</span></td><td class="numeric">${record.employees}</td><td class="numeric">${money.format(record.budget)}</td><td class="numeric">${money.format(record.expense)}</td><td class="numeric ${variance < 0 ? "negative" : "positive"}">${variance < 0 ? "−" : ""}${money.format(Math.abs(variance))}</td><td>${budgetBadge(over)}</td></tr>`;
    }).join("");
    const cards = pageRecords.map(record => {
      const variance = record.budget - record.expense;
      const over = record.expense > record.budget;
      return `<article class="record-card ${over ? "over-budget" : ""}"><div class="record-header"><div class="record-title"><strong>${escapeHtml(record.department)}</strong><span>${record.employees} active employees</span></div>${budgetBadge(over)}</div><dl class="record-details"><div><dt>Monthly budget</dt><dd class="numeric">${money.format(record.budget)}</dd></div><div><dt>Salary expense</dt><dd class="numeric">${money.format(record.expense)}</dd></div><div><dt>Variance</dt><dd class="numeric ${variance < 0 ? "negative" : "positive"}">${variance < 0 ? "−" : ""}${money.format(Math.abs(variance))}</dd></div></dl></article>`;
    }).join("");
    document.getElementById("dsBudgetTable").innerHTML = pageRecords.length
      ? `<table class="data-table"><caption class="sr-only">Sample department salary budgets and expenses</caption><thead><tr>${sortHeader("department", "Department")}${sortHeader("employees", "Active employees", true)}${sortHeader("budget", "Monthly budget", true)}${sortHeader("expense", "Salary expense", true)}${sortHeader("variance", "Variance", true)}<th scope="col">Budget status</th></tr></thead><tbody>${rows}</tbody></table>`
      : '<div class="empty-state"><h2>No departments match this filter</h2><p>Choose all departments to restore the budget summary.</p></div>';
    document.getElementById("dsBudgetRecords").innerHTML = pageRecords.length
      ? cards
      : '<div class="empty-state"><h2>No departments match this filter</h2><p>Choose all departments to restore the budget summary.</p></div>';
    document.getElementById("dsBudgetPagination").innerHTML = paginationMarkup(records.length);
  }

  function setTocActive(id) {
    tocLinks.forEach(link => {
      const active = link.getAttribute("href") === `#${id}`;
      link.classList.toggle("is-active", active);
      if (active) link.setAttribute("aria-current", "location");
      else link.removeAttribute("aria-current");
    });
    if (sectionSelect.value !== id) sectionSelect.value = id;
  }

  function syncToc() {
    tocFrame = 0;
    if (!tocTargets.length) return;
    const marker = window.scrollY + 180;
    let active = tocTargets[0].id;
    tocTargets.forEach(target => {
      const top = target.getBoundingClientRect().top + window.scrollY;
      if (top <= marker) active = target.id;
    });
    if (window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 4) {
      active = tocTargets[tocTargets.length - 1].id;
    }
    setTocActive(active);
  }

  menuButton.addEventListener("click", () => setMobileNavigation(menuButton.getAttribute("aria-expanded") !== "true"));
  navScrim.addEventListener("click", () => setMobileNavigation(false));
  window.addEventListener("resize", () => setMobileNavigation(false, false));
  document.addEventListener("keydown", event => {
    if (event.key === "Escape" && document.body.classList.contains("nav-open")) setMobileNavigation(false);
  });
  sidebar.querySelectorAll('a[href^="#"]').forEach(link => {
    link.addEventListener("click", () => setMobileNavigation(false, false));
  });

  document.getElementById("dsEmployeeSearch").addEventListener("input", renderSearchResults);
  document.getElementById("dsDepartment").addEventListener("change", updateDependentFields);

  document.getElementById("dsBudgetFilter").addEventListener("change", event => {
    tableView.status = event.target.value;
    tableView.page = 1;
    renderBudgetTable();
  });

  document.getElementById("dsBudgetDemo").addEventListener("click", event => {
    const sortButton = event.target.closest("[data-ds-sort]");
    if (sortButton) {
      const key = sortButton.dataset.dsSort;
      if (tableView.sort === key) tableView.direction = tableView.direction === "asc" ? "desc" : "asc";
      else {
        tableView.sort = key;
        tableView.direction = "asc";
      }
      tableView.page = 1;
      renderBudgetTable();
      document.querySelector(`[data-ds-sort="${key}"]`)?.focus();
      return;
    }
    const pageButton = event.target.closest("[data-ds-page]");
    if (pageButton && !pageButton.disabled) {
      tableView.page = Number(pageButton.dataset.dsPage);
      renderBudgetTable();
      const currentPage = document.querySelector(`[data-ds-page="${tableView.page}"]`);
      const fallback = document.querySelector("#dsBudgetPagination .pagination-controls button:not([disabled])");
      (currentPage?.offsetParent ? currentPage : fallback)?.focus();
    }
  });

  document.addEventListener("click", async event => {
    const openButton = event.target.closest("[data-ds-open]");
    if (openButton) {
      openDialog(openButton.dataset.dsOpen);
      return;
    }

    const closeButton = event.target.closest("[data-ds-close]");
    if (closeButton) {
      closeDialog(closeButton.dataset.dsClose);
      return;
    }

    const revealButton = event.target.closest("[data-ds-reveal]");
    if (revealButton) {
      const target = document.querySelector(revealButton.dataset.dsReveal);
      if (target) {
        target.hidden = !target.hidden;
        revealButton.setAttribute("aria-expanded", String(!target.hidden));
      }
      return;
    }

    const toastButton = event.target.closest("[data-ds-toast]");
    if (toastButton) {
      const type = toastButton.dataset.dsToast;
      if (type === "success") showToast("Success state", "This toast confirms a completed action in the workspace.");
      else if (type === "error") showToast("Error state", "This toast reports a failed action and the next step.", "error");
      else showToast("Refresh requested", "The live workspace would reload this view. This reference keeps its sample data.", "info");
      return;
    }

    const tokenButton = event.target.closest("[data-ds-copy-token]");
    if (tokenButton) {
      const token = tokenButton.dataset.token;
      const value = tokenButton.dataset.value;
      const copied = await copyText(`${token}: ${value};`);
      showToast(copied ? "Token copied" : "Copy unavailable", copied ? `${token} is on your clipboard.` : "Select the token text and copy it manually.", copied ? "success" : "error");
      return;
    }

    const responseButton = event.target.closest("[data-ds-copy-response]");
    if (responseButton) {
      const response = document.getElementById("dsApiResponse").textContent;
      const copied = await copyText(response);
      showToast(copied ? "Response copied" : "Copy unavailable", copied ? "The sample JSON response is on your clipboard." : "Select the response text and copy it manually.", copied ? "success" : "error");
      return;
    }

    const clearSearchButton = event.target.closest("[data-ds-clear-search]");
    if (clearSearchButton) {
      document.getElementById("dsEmployeeSearch").value = "";
      renderSearchResults();
      showToast("Search cleared", "All sample employees are visible again.");
      document.getElementById("dsEmployeeSearch").focus();
      return;
    }

    const retryButton = event.target.closest("[data-ds-retry]");
    if (retryButton) {
      showToast("Retry requested", "The workspace would request this view again. Sample data remains available.");
      return;
    }

    if (event.target.closest("[data-ds-dismiss-toast]")) {
      event.target.closest(".toast")?.remove();
    }
  });

  document.getElementById("dsDemoSave").addEventListener("click", saveDemoEmployee);
  demoForm.addEventListener("submit", event => event.preventDefault());
  ["dsDemoName", "dsDemoSalary"].forEach(id => {
    document.getElementById(id).addEventListener("input", () => {
      const isName = id === "dsDemoName";
      clearDemoError(id, isName ? "dsDemoNameError" : "dsDemoSalaryError");
    });
  });

  document.getElementById("dsConfirmDeactivate").addEventListener("click", () => {
    confirmDialog.close();
    showToast("Sample employee deactivated", "Nora Alvarez would no longer count toward active totals in the workspace.");
  });

  [formDialog, confirmDialog].forEach(dialog => {
    dialog.addEventListener("click", event => {
      if (event.target === dialog) dialog.close();
    });
    dialog.addEventListener("close", () => {
      if (lastDialogTrigger && document.contains(lastDialogTrigger)) lastDialogTrigger.focus();
    });
  });

  sectionSelect.addEventListener("change", () => {
    const target = document.getElementById(sectionSelect.value);
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    target?.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
  });

  tocLinks.forEach(link => {
    link.addEventListener("click", () => setTocActive(link.getAttribute("href").slice(1)));
  });

  window.addEventListener("scroll", () => {
    if (!tocFrame) tocFrame = window.requestAnimationFrame(syncToc);
  }, { passive: true });

  renderSearchResults();
  renderBudgetTable();
  syncToc();
  setMobileNavigation(false, false);
  main.focus({ preventScroll: true });
})();
