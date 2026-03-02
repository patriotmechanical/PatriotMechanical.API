// ═══════════════════════════════════════════════════════════════
// STATE
// ═══════════════════════════════════════════════════════════════

let token = localStorage.getItem("jwt");
let currentUser = null;
let currentCompany = null;
let allCustomers = [];
let currentCustomerTab = "all";
let currentVendorId = null;

// ═══════════════════════════════════════════════════════════════
// API HELPER
// ═══════════════════════════════════════════════════════════════

async function api(url, options = {}) {
    const headers = { "Content-Type": "application/json", ...options.headers };

    if (token) {
        headers["Authorization"] = `Bearer ${token}`;
    }

    const res = await fetch(url, { ...options, headers });

    if (res.status === 401) {
        doLogout();
        return null;
    }

    return res;
}

// ═══════════════════════════════════════════════════════════════
// APP INIT
// ═══════════════════════════════════════════════════════════════

document.addEventListener("DOMContentLoaded", async () => {
    await checkAppState();
});

async function checkAppState() {
    try {
        const res = await fetch("/auth/status");
        const data = await res.json();

        if (!data.setupComplete) {
            showScreen("setupScreen");
            return;
        }

        if (token) {
            // Validate the token
            const meRes = await api("/auth/me");
            if (meRes && meRes.ok) {
                const meData = await meRes.json();
                currentUser = meData.user;
                currentCompany = meData.company;
                enterApp();
                return;
            }
        }

        showScreen("loginScreen");
    } catch (err) {
        console.error("Init error:", err);
        showScreen("loginScreen");
    }
}

function showScreen(screenId) {
    document.getElementById("loginScreen").classList.add("hidden");
    document.getElementById("setupScreen").classList.add("hidden");
    document.getElementById("mainApp").classList.add("hidden");
    document.getElementById(screenId).classList.remove("hidden");
}

// ═══════════════════════════════════════════════════════════════
// LOGIN
// ═══════════════════════════════════════════════════════════════

async function doLogin() {
    const email = document.getElementById("loginEmail").value;
    const password = document.getElementById("loginPassword").value;

    hideError("loginError");

    if (!email || !password) {
        showError("loginError", "Please enter email and password.");
        return;
    }

    try {
        const res = await fetch("/auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, password })
        });

        const data = await res.json();

        if (!res.ok) {
            showError("loginError", data.message || "Login failed.");
            return;
        }

        token = data.token;
        localStorage.setItem("jwt", token);
        currentUser = data.user;
        currentCompany = data.company;
        enterApp();
    } catch (err) {
        showError("loginError", "Connection error. Try again.");
    }
}

function doLogout() {
    token = null;
    currentUser = null;
    currentCompany = null;
    localStorage.removeItem("jwt");
    showScreen("loginScreen");
    document.getElementById("loginEmail").value = "";
    document.getElementById("loginPassword").value = "";
}

// ═══════════════════════════════════════════════════════════════
// SETUP WIZARD
// ═══════════════════════════════════════════════════════════════

function goToSetupStep2() {
    const name = document.getElementById("setupCompanyName").value;
    const email = document.getElementById("setupEmail").value;
    const pw = document.getElementById("setupPassword").value;

    hideError("setupError");

    if (!name || !email || !pw) {
        showError("setupError", "Please fill in all fields.");
        return;
    }

    if (pw.length < 8) {
        showError("setupError", "Password must be at least 8 characters.");
        return;
    }

    document.getElementById("setupStep1").classList.add("hidden");
    document.getElementById("setupStep2").classList.remove("hidden");
}

function goToSetupStep1() {
    document.getElementById("setupStep2").classList.add("hidden");
    document.getElementById("setupStep1").classList.remove("hidden");
}

async function completeSetup() {
    await doSetup(false);
}

async function skipServiceTitan() {
    await doSetup(true);
}

async function doSetup(skipST) {
    hideError("setupError");

    const body = {
        companyName: document.getElementById("setupCompanyName").value,
        fullName: document.getElementById("setupFullName").value,
        email: document.getElementById("setupEmail").value,
        password: document.getElementById("setupPassword").value
    };

    if (!skipST) {
        body.serviceTitanTenantId = document.getElementById("setupTenantId").value;
        body.serviceTitanClientId = document.getElementById("setupClientId").value;
        body.serviceTitanClientSecret = document.getElementById("setupClientSecret").value;
        body.serviceTitanAppKey = document.getElementById("setupAppKey").value;
    }

    try {
        const res = await fetch("/auth/setup", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body)
        });

        const data = await res.json();

        if (!res.ok) {
            showError("setupError", data.message || data || "Setup failed.");
            return;
        }

        token = data.token;
        localStorage.setItem("jwt", token);
        currentUser = data.user;
        currentCompany = data.company;
        enterApp();
        toast("Setup complete! Welcome aboard.", "success");
    } catch (err) {
        showError("setupError", "Connection error. Try again.");
    }
}

// ═══════════════════════════════════════════════════════════════
// ENTER APP (after login/setup)
// ═══════════════════════════════════════════════════════════════

function enterApp() {
    showScreen("mainApp");

    // Set sidebar info
    document.getElementById("sidebarCompanyName").innerText =
        currentCompany?.companyName || "Company";
    document.getElementById("sidebarUserName").innerText =
        currentUser?.fullName || currentUser?.email || "User";

    // Load dashboard
    loadDashboard();
}

// ═══════════════════════════════════════════════════════════════
// VIEW NAVIGATION
// ═══════════════════════════════════════════════════════════════

function showView(viewId, clickedLink) {
    const views = ["dashboardPage", "customersView", "apView", "adminView", "boardView", "todoView", "subsView", "equipmentView", "warrantyView", "pmView", "pricingView"];

    views.forEach(v => {
        const el = document.getElementById(v);
        if (el) el.style.display = "none";
    });

    document.getElementById(viewId).style.display = "block";

    if (clickedLink) {
        document.querySelectorAll(".nav-link").forEach(l => l.classList.remove("active"));
        clickedLink.classList.add("active");
    }

    if (viewId === "customersView") loadCustomers();
    if (viewId === "apView") { loadAp(); loadVendors(); }
    if (viewId === "dashboardPage") loadDashboard();
    if (viewId === "adminView") loadAdminSettings();
    if (viewId === "boardView") loadBoard();
    if (viewId === "todoView") loadTodos();
    if (viewId === "subsView") loadSubcontractors();
    if (viewId === "equipmentView") loadEquipment();
    if (viewId === "warrantyView") loadWarranty();
    if (viewId === "pmView") loadPm();
    if (viewId === "pricingView") initPricing();
    setTimeout(applySortableToAll, 300);
}

// ═══════════════════════════════════════════════════════════════
// DASHBOARD
// ═══════════════════════════════════════════════════════════════

async function loadDashboard() {
    const res = await api("/dashboard");
    if (!res || !res.ok) return;

    const data = await res.json();

    document.getElementById("totalAR").innerText =
        "$" + Number(data.totalAR || 0).toLocaleString();
    document.getElementById("totalAP").innerText =
        "$" + Number(data.totalAP || 0).toLocaleString();

    const net = Number(data.netPosition || 0);
    const netEl = document.getElementById("netPosition");
    netEl.innerText = "$" + net.toLocaleString();
    netEl.style.color = net >= 0 ? "#4ade80" : "#f87171";

    const arTable = document.getElementById("arTable");
    arTable.innerHTML = "";
    data.ar.forEach(c => {
        arTable.innerHTML += `<tr><td>${c.name}</td><td>$${Number(c.totalOwed).toLocaleString()}</td></tr>`;
    });

    const apTable = document.getElementById("apTable");
    apTable.innerHTML = "";
    data.ap.forEach(v => {
        const due = v.nextDue ? new Date(v.nextDue).toLocaleDateString() : "-";
        apTable.innerHTML += `<tr><td>${v.name}</td><td>$${Number(v.totalInvoiceAmount).toLocaleString()}</td><td>$${Number(v.totalOwed).toLocaleString()}</td><td>${due}</td></tr>`;
    });

    // Open Work Orders table
    const woTable = document.getElementById("openWoBody");
    if (woTable && data.openWorkOrders) {
        woTable.innerHTML = "";
        if (data.openWorkOrders.length > 0) {
            data.openWorkOrders.forEach(wo => {
                const created = wo.createdAt ? new Date(wo.createdAt).toLocaleDateString() : "-";
                woTable.innerHTML += `<tr><td><b>${wo.jobNumber}</b></td><td>${wo.customerName}</td><td><span class="status-badge">${wo.status}</span></td><td>${created}</td><td>$${Number(wo.totalAmount || 0).toLocaleString()}</td></tr>`;
            });
        } else {
            woTable.innerHTML = '<tr><td colspan="5" style="text-align:center; color:#64748b;">No open work orders</td></tr>';
        }
    }

    // Ops Stats Row
    renderOpsStats(data);
    setTimeout(applyDashboardSorting, 100);
}

function renderOpsStats(data) {
    const row = document.getElementById("opsStatsRow");
    if (!row) return;

    const openWoCount = data.openWorkOrders ? data.openWorkOrders.length : 0;

    const columnMap = {};
    if (data.boardColumns) {
        data.boardColumns.forEach(col => {
            columnMap[col.name.toLowerCase()] = { count: col.cards.length, cards: col.cards, color: col.color };
        });
    }

    const getCol = (name) => {
        const key = name.toLowerCase();
        for (const [k, v] of Object.entries(columnMap)) {
            if (k.includes(key) || key.includes(k)) return v;
        }
        return { count: 0, cards: [], color: "#475569" };
    };

    const needSchedule = getCol("schedule");
    const waitingParts = getCol("waiting parts");
    const waitingQuote = getCol("waiting quote");
    const needReturn = getCol("need to return");
    const overduePmCount = data.overduePms ? data.overduePms.length : 0;

    const stats = [
        { label: "Open WOs", count: openWoCount, color: "#2563eb", items: data.openWorkOrders, type: "wo" },
        { label: "Need to Schedule", count: needSchedule.count, color: needSchedule.color || "#2563eb", items: needSchedule.cards, type: "board" },
        { label: "Overdue PMs", count: overduePmCount, color: "#dc2626", items: data.overduePms, type: "pm" },
        { label: "Waiting Quote", count: waitingQuote.count, color: waitingQuote.color || "#9333ea", items: waitingQuote.cards, type: "board" },
        { label: "Waiting Parts", count: waitingParts.count, color: waitingParts.color || "#d97706", items: waitingParts.cards, type: "board" },
        { label: "Need Return", count: needReturn.count, color: needReturn.color || "#dc2626", items: needReturn.cards, type: "board" },
    ];

    row.innerHTML = stats.map((s, i) => {
        const zeroClass = s.count === 0 ? " zero" : "";
        const onclick = s.count > 0 ? `onclick="openOpsDrilldown(${i})"` : "";
        return `<div class="ops-stat-card" style="--stat-color:${s.color};"><h4>${s.label}</h4><div class="stat-number${zeroClass}" ${onclick}>${s.count}</div></div>`;
    }).join("");

    window._opsStats = stats;
}

function openOpsDrilldown(idx) {
    const stat = window._opsStats[idx];
    if (!stat || stat.count === 0) return;

    document.getElementById("opsModalTitle").innerText = stat.label + " (" + stat.count + ")";
    const body = document.getElementById("opsModalBody");

    let html = '<table class="data-table"><thead><tr><th>Job #</th><th>Customer</th>';
    if (stat.type === "wo") html += "<th>Status</th><th>Created</th>";
    if (stat.type === "pm") html += "<th>Last PM</th><th>Days Since</th>";
    html += "</tr></thead><tbody>";

    stat.items.forEach(item => {
        if (stat.type === "wo") {
            const created = item.createdAt ? new Date(item.createdAt).toLocaleDateString() : "—";
            html += `<tr><td><b>${item.jobNumber || "—"}</b></td><td>${item.customerName || "—"}</td><td>${item.status || "—"}</td><td>${created}</td></tr>`;
        } else if (stat.type === "board") {
            html += `<tr><td><b>${item.jobNumber || "—"}</b></td><td>${item.customerName || "—"}</td></tr>`;
        } else if (stat.type === "pm") {
            const lastPm = item.lastPm ? new Date(item.lastPm).toLocaleDateString() : "Never";
            const days = item.lastPm ? Math.floor((Date.now() - new Date(item.lastPm).getTime()) / 86400000) : "—";
            html += `<tr><td><b>${item.jobNumber || "—"}</b></td><td>${item.customerName || "—"}</td><td>${lastPm}</td><td style="color:#f87171;">${days}</td></tr>`;
        }
    });

    html += "</tbody></table>";
    body.innerHTML = html;
    document.getElementById("opsModal").classList.remove("hidden");
}

// ═══════════════════════════════════════════════════════════════
// CUSTOMERS
// ═══════════════════════════════════════════════════════════════

async function loadCustomers() {
    const res = await api("/customers");
    if (!res || !res.ok) return;

    allCustomers = await res.json();
    renderCustomers();
}

function renderCustomers() {
    const tbody = document.getElementById("customerTableBody");
    tbody.innerHTML = "";

    let filtered = allCustomers;

    if (currentCustomerTab === "balance")
        filtered = allCustomers.filter(c => c.totalAR > 0);
    if (currentCustomerTab === "overdue")
        filtered = allCustomers.filter(c => c.days30 > 0 || c.days60 > 0 || c.days90 > 0);

    filtered
        .sort((a, b) => b.days90 - a.days90)
        .forEach(c => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td class="customer-link">${c.name}</td>
                <td>$${Number(c.totalAR).toLocaleString()}</td>
                <td>$${Number(c.current).toLocaleString()}</td>
                <td>$${Number(c.days30).toLocaleString()}</td>
                <td>$${Number(c.days60).toLocaleString()}</td>
                <td class="danger">$${Number(c.days90).toLocaleString()}</td>
            `;
            row.onclick = () => openCustomerProfile(c.id);
            tbody.appendChild(row);
        });
}

function setCustomerTab(tab) {
    currentCustomerTab = tab;
    document.querySelectorAll(".tab").forEach(t => t.classList.remove("active"));
    event.target.classList.add("active");
    renderCustomers();
}

async function loadCustomerDetail(id, name) {
    const res = await api(`/customers/${id}`);
    if (!res || !res.ok) return;

    const data = await res.json();
    document.getElementById("modalTitle").innerText = name || "Customer";

    const tbody = document.getElementById("invoiceBody");
    tbody.innerHTML = "";

    data.invoices.forEach(inv => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td>${inv.invoiceNumber}</td>
            <td>${new Date(inv.dueDate).toLocaleDateString()}</td>
            <td>$${inv.totalAmount.toFixed(2)}</td>
            <td>$${inv.balanceRemaining.toFixed(2)}</td>
        `;
        tbody.appendChild(row);
    });

    document.getElementById("customerModal").classList.remove("hidden");
}

function closeModal() {
    document.getElementById("customerModal").classList.add("hidden");
}

// ═══════════════════════════════════════════════════════════════
// ACCOUNTS PAYABLE
// ═══════════════════════════════════════════════════════════════

async function loadAp() {
    const res = await api("/ap");
    if (!res || !res.ok) return;

    const vendors = await res.json();
    const table = document.getElementById("apTableBody");
    table.innerHTML = "";

    vendors.forEach(v => {
        const totalInvoices = Number(v.totalInvoiceAmount || 0);
        const amountDueNow = Number(v.amountDueNow || 0);
        const nextDueText = v.nextDue
            ? new Date(v.nextDue).toLocaleDateString()
            : "-";

        const row = document.createElement("tr");
        row.innerHTML = `
            <td><a href="#" onclick="loadVendorDetail('${v.id}')" style="color:#60a5fa;">${v.name}</a></td>
            <td>$${totalInvoices.toLocaleString()}</td>
            <td>$${amountDueNow.toLocaleString()}</td>
            <td>${nextDueText}</td>
        `;
        table.appendChild(row);
    });
}

async function loadVendors() {
    const res = await api("/ap/vendors");
    if (!res || !res.ok) return;

    const vendors = await res.json();
    const select = document.getElementById("vendorSelect");
    select.innerHTML = "";

    vendors.forEach(v => {
        const option = document.createElement("option");
        option.value = v.id;
        option.textContent = v.name;
        select.appendChild(option);
    });
}

async function loadVendorDetail(id) {
    currentVendorId = id;
    const res = await api(`/ap/vendor/${id}`);
    if (!res || !res.ok) return;

    const data = await res.json();
    document.getElementById("vendorDetailTitle").innerText = data.name;

    const tbody = document.getElementById("vendorBillsTable");
    tbody.innerHTML = "";

    data.bills.forEach(b => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td>$${Number(b.amount).toFixed(2)}</td>
            <td>$${Number(b.totalAmount).toFixed(2)}</td>
            <td>${new Date(b.dueDate).toLocaleDateString()}</td>
            <td>
                <button class="btn-secondary" style="padding:4px 10px; font-size:12px;" onclick="markPaid('${b.id}')">Mark Paid</button>
                <button class="btn-secondary" style="padding:4px 10px; font-size:12px;" onclick="deleteBill('${b.id}')">Delete</button>
            </td>
        `;
        tbody.appendChild(row);
    });

    document.getElementById("vendorDetail").style.display = "block";
}

function toggleNewVendor() {
    const section = document.getElementById("newVendorSection");
    section.style.display = section.style.display === "none" ? "block" : "none";
}

async function addVendor() {
    const name = document.getElementById("newVendorName").value;
    if (!name || name.trim() === "") {
        toast("Vendor name is required.", "error");
        return;
    }

    const res = await api("/ap/vendors", {
        method: "POST",
        body: JSON.stringify({ name })
    });

    if (!res || !res.ok) {
        toast("Failed to save vendor.", "error");
        return;
    }

    const vendor = await res.json();
    document.getElementById("newVendorName").value = "";
    document.getElementById("newVendorSection").style.display = "none";

    await loadVendors();
    document.getElementById("vendorSelect").value = vendor.id;
    toast("Vendor added.", "success");
}

async function addBill() {
    const vendorId = document.getElementById("vendorSelect").value;
    const amount = parseFloat(document.getElementById("billAmount").value);
    const totalAmount = parseFloat(document.getElementById("billTotalAmount").value);
    const dueDate = document.getElementById("billDueDate").value;

    if (!vendorId || isNaN(amount) || isNaN(totalAmount) || !dueDate) {
        toast("Complete all fields.", "error");
        return;
    }

    const res = await api("/ap/bills", {
        method: "POST",
        body: JSON.stringify({ vendorId, amount, totalAmount, dueDate })
    });

    if (!res || !res.ok) {
        toast("Failed to save bill.", "error");
        return;
    }

    document.getElementById("billAmount").value = "";
    document.getElementById("billTotalAmount").value = "";
    document.getElementById("billDueDate").value = "";

    await loadAp();
    toast("Bill added.", "success");
}

async function deleteBill(id) {
    if (!confirm("Delete this bill?")) return;

    const res = await api(`/ap/bills/${id}`, { method: "DELETE" });
    if (!res || !res.ok) {
        toast("Delete failed.", "error");
        return;
    }

    await loadAp();
    if (currentVendorId) await loadVendorDetail(currentVendorId);
    toast("Bill deleted.", "success");
}

async function markPaid(id) {
    const res = await api(`/ap/pay/${id}`, { method: "PUT" });
    if (res && res.ok) {
        await loadAp();
        document.getElementById("vendorDetail").style.display = "none";
        toast("Marked as paid.", "success");
    }
}

// ═══════════════════════════════════════════════════════════════
// HARD REFRESH (sync)
// ═══════════════════════════════════════════════════════════════

async function hardRefresh() {
    const btn = document.getElementById("hardRefreshBtn");
    btn.disabled = true;
    btn.innerText = "Syncing...";

    try {
        btn.innerText = "Syncing customers...";
        await api("/servicetitan/sync/customers", { method: "POST" });
        btn.innerText = "Syncing jobs...";
        await api("/servicetitan/sync/jobs", { method: "POST" });
        btn.innerText = "Syncing invoices...";
        await api("/servicetitan/sync/invoices", { method: "POST" });
        btn.innerText = "Refreshing recent changes...";
        try {
            const refreshRes = await api("/servicetitan/refresh", { method: "POST" });
            if (refreshRes && refreshRes.ok) {
                const refreshData = await refreshRes.json();
                console.log("Refresh result:", refreshData.message);
            }
        } catch (refreshErr) { console.warn("Refresh failed (non-fatal):", refreshErr); }
        btn.innerText = "Syncing contacts...";
        try {
            const crmRes = await api("/crm/sync", { method: "POST" });
            if (crmRes && crmRes.ok) { console.log("CRM sync complete"); }
        } catch (crmErr) { console.warn("CRM sync failed (non-fatal):", crmErr); }
        await loadDashboard();
        toast("Data synced successfully.", "success");
    } catch (err) {
        console.error(err);
        toast("Sync failed.", "error");
    }

    btn.disabled = false;
    btn.innerText = "↻ Sync Data";
}

// ═══════════════════════════════════════════════════════════════
// ADMIN CENTER
// ═══════════════════════════════════════════════════════════════

async function loadAdminSettings() {
    const res = await api("/admin/settings");
    if (!res || !res.ok) return;

    const data = await res.json();

    // Company
    document.getElementById("adminCompanyName").value = data.companyName || "";
    document.getElementById("adminCcFee").value = data.creditCardFeePercent || 2.5;

    // ServiceTitan
    const badge = document.getElementById("stConnectionStatus");
    if (data.serviceTitan.isConfigured) {
        badge.className = "connection-badge connected";
        badge.innerText = "✓ Connected";
    } else {
        badge.className = "connection-badge disconnected";
        badge.innerText = "✗ Not Configured";
    }

    document.getElementById("adminStTenantId").value = data.serviceTitan.tenantId || "";
    document.getElementById("adminStClientId").value = "";
    document.getElementById("adminStClientId").placeholder =
        data.serviceTitan.clientId || "Enter Client ID";
    document.getElementById("adminStClientSecret").value = "";
    document.getElementById("adminStClientSecret").placeholder =
        data.serviceTitan.clientSecret || "Enter Client Secret";
    document.getElementById("adminStAppKey").value = "";
    document.getElementById("adminStAppKey").placeholder =
        data.serviceTitan.appKey || "Enter App Key";

    // Sync
    document.getElementById("adminAutoSync").checked = data.autoSyncEnabled;
    document.getElementById("adminSyncInterval").value = data.syncIntervalMinutes || 60;

    const syncInfo = document.getElementById("lastSyncInfo");
    if (data.lastSyncAt) {
        const dt = new Date(data.lastSyncAt).toLocaleString();
        syncInfo.innerText = `Last sync: ${dt} — Status: ${data.lastSyncStatus || "Unknown"}`;
    } else {
        syncInfo.innerText = "No sync has run yet.";
    }

    // Load users list
    await loadUsers();
}

async function saveCompanySettings() {
    const res = await api("/admin/settings/company", {
        method: "PUT",
        body: JSON.stringify({
            companyName: document.getElementById("adminCompanyName").value,
            creditCardFeePercent: parseFloat(document.getElementById("adminCcFee").value)
        })
    });

    if (res && res.ok) {
        // Update sidebar
        document.getElementById("sidebarCompanyName").innerText =
            document.getElementById("adminCompanyName").value;
        toast("Company settings saved.", "success");
    } else {
        toast("Failed to save.", "error");
    }
}

async function saveServiceTitanSettings() {
    const tenantId = document.getElementById("adminStTenantId").value;
    const clientId = document.getElementById("adminStClientId").value;
    const clientSecret = document.getElementById("adminStClientSecret").value;
    const appKey = document.getElementById("adminStAppKey").value;

    // Only send non-empty values
    const body = {};
    if (tenantId) body.tenantId = tenantId;
    if (clientId) body.clientId = clientId;
    if (clientSecret) body.clientSecret = clientSecret;
    if (appKey) body.appKey = appKey;

    const res = await api("/admin/settings/servicetitan", {
        method: "PUT",
        body: JSON.stringify(body)
    });

    if (res && res.ok) {
        await loadAdminSettings();
        toast("ServiceTitan credentials saved.", "success");
    } else {
        toast("Failed to save credentials.", "error");
    }
}

async function testServiceTitanConnection() {
    const resultDiv = document.getElementById("stTestResult");
    resultDiv.classList.remove("hidden");
    resultDiv.innerHTML = '<span style="color:#94a3b8;">Testing connection...</span>';

    const res = await api("/admin/settings/servicetitan/test", { method: "POST" });

    if (!res || !res.ok) {
        resultDiv.innerHTML = '<span style="color:#fca5a5;">Failed to test. Save credentials first.</span>';
        return;
    }

    const data = await res.json();
    if (data.success) {
        resultDiv.innerHTML = '<span style="color:#4ade80;">✓ ' + data.message + '</span>';
    } else {
        resultDiv.innerHTML = '<span style="color:#fca5a5;">✗ ' + data.message + '</span>';
    }
}

async function saveSyncSettings() {
    const res = await api("/admin/settings/sync", {
        method: "PUT",
        body: JSON.stringify({
            autoSyncEnabled: document.getElementById("adminAutoSync").checked,
            syncIntervalMinutes: parseInt(document.getElementById("adminSyncInterval").value)
        })
    });

    if (res && res.ok) {
        toast("Sync settings saved.", "success");
    } else {
        toast("Failed to save.", "error");
    }
}

async function changePassword() {
    const current = document.getElementById("adminCurrentPw").value;
    const newPw = document.getElementById("adminNewPw").value;

    if (!current || !newPw) {
        toast("Fill in both fields.", "error");
        return;
    }

    const res = await api("/auth/change-password", {
        method: "POST",
        body: JSON.stringify({
            currentPassword: current,
            newPassword: newPw
        })
    });

    if (res && res.ok) {
        document.getElementById("adminCurrentPw").value = "";
        document.getElementById("adminNewPw").value = "";
        toast("Password updated.", "success");
    } else {
        const data = await res.json();
        toast(data.message || "Failed to change password.", "error");
    }
}

// ═══════════════════════════════════════════════════════════════
// USER MANAGEMENT
// ═══════════════════════════════════════════════════════════════

async function loadUsers() {
    const res = await api("/admin/users");
    if (!res || !res.ok) return;

    const users = await res.json();
    const tbody = document.getElementById("usersTableBody");
    tbody.innerHTML = "";

    users.forEach(u => {
        const lastLogin = u.lastLoginAt
            ? new Date(u.lastLoginAt).toLocaleDateString()
            : "Never";

        const isMe = u.id === currentUser?.id;
        const statusClass = u.isActive ? "status-active" : "status-inactive";
        const statusText = u.isActive ? "Active" : "Inactive";

        const row = document.createElement("tr");
        row.innerHTML = `
            <td>${u.fullName || "—"}${isMe ? ' <span style="color:#64748b; font-size:11px;">(you)</span>' : ""}</td>
            <td>${u.email}</td>
            <td><span class="${statusClass}">${statusText}</span></td>
            <td>${lastLogin}</td>
            <td>
                ${isMe ? '<span style="color:#475569; font-size:12px;">—</span>' : `
                    <button class="btn-table" onclick="toggleUserActive('${u.id}')">${u.isActive ? "Deactivate" : "Reactivate"}</button>
                    <button class="btn-table" onclick="promptResetPassword('${u.id}', '${u.email}')">Reset PW</button>
                    <button class="btn-table btn-danger-text" onclick="deleteUser('${u.id}', '${u.email}')">Delete</button>
                `}
            </td>
        `;
        tbody.appendChild(row);
    });
}

async function createUser() {
    const fullName = document.getElementById("newUserName").value;
    const email = document.getElementById("newUserEmail").value;
    const password = document.getElementById("newUserPassword").value;

    if (!email || !password) {
        toast("Email and password are required.", "error");
        return;
    }

    if (password.length < 8) {
        toast("Password must be at least 8 characters.", "error");
        return;
    }

    const res = await api("/admin/users", {
        method: "POST",
        body: JSON.stringify({ fullName, email, password })
    });

    if (!res) return;

    const data = await res.json();

    if (!res.ok) {
        toast(data.message || "Failed to create user.", "error");
        return;
    }

    document.getElementById("newUserName").value = "";
    document.getElementById("newUserEmail").value = "";
    document.getElementById("newUserPassword").value = "";

    await loadUsers();
    toast("User created.", "success");
}

async function toggleUserActive(id) {
    const res = await api(`/admin/users/${id}/toggle-active`, { method: "PUT" });
    if (!res) return;

    const data = await res.json();

    if (!res.ok) {
        toast(data.message || "Failed.", "error");
        return;
    }

    await loadUsers();
    toast(data.message, "success");
}

async function promptResetPassword(id, email) {
    const newPw = prompt(`Enter new password for ${email}:`);
    if (!newPw) return;

    if (newPw.length < 8) {
        toast("Password must be at least 8 characters.", "error");
        return;
    }

    const res = await api(`/admin/users/${id}/reset-password`, {
        method: "PUT",
        body: JSON.stringify({ newPassword: newPw })
    });

    if (!res) return;

    const data = await res.json();

    if (!res.ok) {
        toast(data.message || "Failed.", "error");
        return;
    }

    toast(`Password reset for ${email}.`, "success");
}

async function deleteUser(id, email) {
    if (!confirm(`Delete user ${email}? This cannot be undone.`)) return;

    const res = await api(`/admin/users/${id}`, { method: "DELETE" });
    if (!res) return;

    const data = await res.json();

    if (!res.ok) {
        toast(data.message || "Failed.", "error");
        return;
    }

    await loadUsers();
    toast("User deleted.", "success");
}

// ═══════════════════════════════════════════════════════════════
// UTILITIES
// ═══════════════════════════════════════════════════════════════

function showError(elementId, msg) {
    const el = document.getElementById(elementId);
    el.innerText = msg;
    el.classList.remove("hidden");
}

function hideError(elementId) {
    document.getElementById(elementId).classList.add("hidden");
}

// ═══════════════════════════════════════════════════════════════
// WO BOARD
// ═══════════════════════════════════════════════════════════════

async function loadBoard() {
    const res = await api("/board");
    if (!res || !res.ok) return;
    const data = await res.json();
    const container = document.getElementById("boardContainer");
    container.innerHTML = "";
    data.forEach(col => {
        const colEl = document.createElement("div");
        colEl.className = "board-column";
        colEl.dataset.columnId = col.id;
        colEl.style.borderTopColor = col.color || "#475569";
        colEl.innerHTML = `
            <div class="board-column-header">
                <span class="board-column-title">${col.name.toUpperCase()}</span>
                <span class="board-column-count">${col.cards.length}</span>
            </div>
            <div class="board-cards" ondragover="event.preventDefault()" ondrop="dropCard(event, '${col.id}')">
                ${col.cards.map(card => `
                    <div class="board-card" draggable="true" ondragstart="dragCard(event, '${card.id}')" onclick="openCardDetail('${card.id}')">
                        <div class="board-card-job">#${card.jobNumber}</div>
                        <div class="board-card-customer">${card.customerName}</div>
                        <div class="board-card-date">Added ${new Date(card.addedAt).toLocaleDateString()}</div>
                        ${card.noteCount > 0 ? `<span class="board-card-notes">📄 ${card.noteCount}</span>` : ''}
                    </div>
                `).join("")}
            </div>
        `;
        container.appendChild(colEl);
    });
}

let draggedCardId = null;
function dragCard(e, cardId) { draggedCardId = cardId; }
async function dropCard(e, columnId) {
    e.preventDefault();
    if (!draggedCardId) return;
    await api(`/board/cards/${draggedCardId}/move`, { method: "PUT", body: JSON.stringify({ columnId, sortOrder: 0 }) });
    draggedCardId = null;
    loadBoard();
}

async function openCardDetail(cardId) {
    const res = await api(`/board/cards/${cardId}`);
    if (!res || !res.ok) return;
    const card = await res.json();
    document.getElementById("cardDetailTitle").innerText = `#${card.jobNumber} — ${card.customerName}`;
    let html = `<p style="color:#94a3b8; margin-bottom:15px;">Column: ${card.columnName || "—"}</p>`;
    html += `<h4 style="margin-bottom:8px;">Notes</h4>`;
    if (card.notes && card.notes.length > 0) {
        card.notes.forEach(n => {
            html += `<div style="background:#0f172a; padding:10px; border-radius:8px; margin-bottom:8px;">
                <div style="font-size:11px; color:#64748b;">${n.author} — ${new Date(n.createdAt).toLocaleString()}</div>
                <div style="color:#e2e8f0; margin-top:4px;">${n.text}</div>
            </div>`;
        });
    } else {
        html += `<p style="color:#475569;">No notes yet.</p>`;
    }
    html += `<div style="display:flex; gap:8px; margin-top:15px;">
        <input type="text" id="cardNoteInput" placeholder="Add a note..." style="flex:1;" />
        <button class="btn-primary" onclick="addCardNote('${cardId}')">Add</button>
    </div>`;
    html += `<button class="btn-secondary" onclick="deleteCard('${cardId}')" style="margin-top:15px; width:100%; color:#f87171;">Remove from Board</button>`;
    document.getElementById("cardDetailBody").innerHTML = html;
    document.getElementById("cardDetailModal").classList.remove("hidden");
}

async function addCardNote(cardId) {
    const text = document.getElementById("cardNoteInput").value.trim();
    if (!text) return;
    await api(`/board/cards/${cardId}/notes`, { method: "POST", body: JSON.stringify({ text }) });
    openCardDetail(cardId);
}

async function deleteCard(cardId) {
    await api(`/board/cards/${cardId}`, { method: "DELETE" });
    document.getElementById("cardDetailModal").classList.add("hidden");
    loadBoard();
}

function openAddCardModal() {
    loadBoard();
    api("/board").then(async res => {
        if (!res || !res.ok) return;
        const cols = await res.json();
        const sel = document.getElementById("addCardColumn");
        sel.innerHTML = cols.map(c => `<option value="${c.id}">${c.name}</option>`).join("");
    });
    document.getElementById("addCardModal").classList.remove("hidden");
}

async function submitAddCard() {
    const columnId = document.getElementById("addCardColumn").value;
    const jobNumber = document.getElementById("addCardJob").value.trim();
    const customerName = document.getElementById("addCardCustomer").value.trim();
    if (!jobNumber || !customerName) { toast("Job # and customer required", "error"); return; }
    await api("/board/cards", { method: "POST", body: JSON.stringify({ columnId, jobNumber, customerName }) });
    document.getElementById("addCardModal").classList.add("hidden");
    loadBoard();
}

function openAddColumnModal() {
    document.getElementById("addColumnModal").classList.remove("hidden");
}

async function submitAddColumn() {
    const name = document.getElementById("addColumnName").value.trim();
    const color = document.getElementById("addColumnColor").value;
    if (!name) { toast("Column name required", "error"); return; }
    await api("/board/columns", { method: "POST", body: JSON.stringify({ name, color }) });
    document.getElementById("addColumnModal").classList.add("hidden");
    loadBoard();
}

// ═══════════════════════════════════════════════════════════════
// TO-DO LIST
// ═══════════════════════════════════════════════════════════════

async function loadTodos() {
    const res = await api("/todos");
    if (!res || !res.ok) return;
    const todos = await res.json();
    const list = document.getElementById("todoList");
    list.innerHTML = todos.map(t => `
        <div class="todo-item${t.isComplete ? ' done' : ''}" style="display:flex; align-items:center; gap:12px; padding:12px 16px; background:#1e293b; border:1px solid #334155; border-radius:10px; margin-bottom:8px;">
            <input type="checkbox" ${t.isComplete ? 'checked' : ''} onchange="toggleTodo('${t.id}')" />
            <div style="flex:1;">
                <div style="${t.isComplete ? 'text-decoration:line-through; color:#475569;' : 'color:#e2e8f0;'} font-weight:600;">${t.title}</div>
                ${t.description ? `<div style="font-size:12px; color:#64748b;">${t.description}</div>` : ''}
            </div>
            <button onclick="deleteTodo('${t.id}')" style="background:none; border:none; color:#475569; cursor:pointer; font-size:18px;">✕</button>
        </div>
    `).join("");
}

async function addTodo() {
    const title = document.getElementById("todoTitle").value.trim();
    const description = document.getElementById("todoDesc").value.trim();
    if (!title) return;
    await api("/todos", { method: "POST", body: JSON.stringify({ title, description: description || null }) });
    document.getElementById("todoTitle").value = "";
    document.getElementById("todoDesc").value = "";
    loadTodos();
}

async function toggleTodo(id) { await api(`/todos/${id}/toggle`, { method: "PUT" }); loadTodos(); }
async function deleteTodo(id) { await api(`/todos/${id}`, { method: "DELETE" }); loadTodos(); }

// ═══════════════════════════════════════════════════════════════
// SUBCONTRACTORS
// ═══════════════════════════════════════════════════════════════

async function loadSubcontractors() {
    const res = await api("/subcontractors");
    if (!res || !res.ok) return;
    const subs = await res.json();
    const tbody = document.getElementById("subsTableBody");
    tbody.innerHTML = subs.map(s => `
        <tr>
            <td>${s.name}</td><td>${s.company || "—"}</td><td>${s.trade || "—"}</td>
            <td>${s.totalHours}</td><td>$${Number(s.totalCost).toLocaleString()}</td>
            <td><button class="btn-sm" onclick="openSubDetail('${s.id}')">View</button></td>
        </tr>
    `).join("");
}

async function addSubcontractor() {
    const name = document.getElementById("subName").value.trim();
    const company = document.getElementById("subCompany").value.trim();
    const trade = document.getElementById("subTrade").value.trim();
    if (!name) { toast("Name required", "error"); return; }
    await api("/subcontractors", { method: "POST", body: JSON.stringify({ name, company, trade }) });
    document.getElementById("subName").value = "";
    document.getElementById("subCompany").value = "";
    document.getElementById("subTrade").value = "";
    loadSubcontractors();
}

async function openSubDetail(id) {
    const res = await api(`/subcontractors/${id}`);
    if (!res || !res.ok) return;
    const sub = await res.json();
    document.getElementById("subDetailTitle").innerText = sub.name;
    let html = `<p style="color:#94a3b8;">${sub.company || ""} — ${sub.trade || ""}</p>`;
    html += `<h4 style="margin:15px 0 8px;">Log Hours</h4>`;
    html += `<div style="display:flex; gap:8px; margin-bottom:15px;">
        <input type="text" id="subEntryJob" placeholder="Job #" style="flex:1;" />
        <input type="number" id="subEntryHours" placeholder="Hours" step="0.5" style="width:80px;" />
        <input type="number" id="subEntryRate" placeholder="Rate/hr" step="1" style="width:80px;" />
        <button class="btn-primary" onclick="addSubEntry('${id}')">Log</button>
    </div>`;
    html += `<table class="data-table"><thead><tr><th>Job #</th><th>Hours</th><th>Rate</th><th>Cost</th><th>Date</th></tr></thead><tbody>`;
    if (sub.entries) {
        sub.entries.forEach(e => {
            html += `<tr><td>${e.jobNumber || "—"}</td><td>${e.hours}</td><td>$${e.rate}</td><td>$${(e.hours * e.rate).toFixed(2)}</td><td>${new Date(e.date).toLocaleDateString()}</td></tr>`;
        });
    }
    html += `</tbody></table>`;
    document.getElementById("subDetailBody").innerHTML = html;
    document.getElementById("subDetailModal").classList.remove("hidden");
}

async function addSubEntry(subId) {
    const jobNumber = document.getElementById("subEntryJob").value.trim();
    const hours = parseFloat(document.getElementById("subEntryHours").value);
    const rate = parseFloat(document.getElementById("subEntryRate").value);
    if (!hours || !rate) { toast("Hours and rate required", "error"); return; }
    await api(`/subcontractors/${subId}/entries`, { method: "POST", body: JSON.stringify({ jobNumber, hours, rate }) });
    openSubDetail(subId);
}

// ═══════════════════════════════════════════════════════════════
// EQUIPMENT
// ═══════════════════════════════════════════════════════════════

async function loadEquipment() {
    const res = await api("/equipment");
    if (!res || !res.ok) return;
    const equip = await res.json();
    const tbody = document.getElementById("equipTableBody");
    tbody.innerHTML = equip.map(e => {
        const installed = e.installDate ? new Date(e.installDate).toLocaleDateString() : "—";
        const warranty = e.warrantyExpiration ? new Date(e.warrantyExpiration).toLocaleDateString() : "—";
        return `<tr>
            <td>${e.type || "—"}</td><td>${e.brand || "—"}</td><td>${e.modelNumber || "—"}</td><td>${e.serialNumber || "—"}</td>
            <td>${e.customerName || "—"}</td><td>${installed}</td><td>${warranty}</td>
            <td>${e.isRegistered ? '<span style="color:#4ade80;">Yes ✓</span>' : '<span style="color:#f87171;">No</span>'}</td>
            <td><button class="btn-sm" style="color:#f87171;" onclick="deleteEquipment('${e.id}')">Del</button></td>
        </tr>`;
    }).join("");

    // Load customer dropdown for add form
    const custRes = await api("/customers");
    if (custRes && custRes.ok) {
        const customers = await custRes.json();
        const sel = document.getElementById("equipCustomerSelect");
        sel.innerHTML = customers.map(c => `<option value="${c.id}">${c.name}</option>`).join("");
    }
}

async function addEquipment() {
    const customerId = document.getElementById("equipCustomerSelect").value;
    const data = {
        customerId, type: document.getElementById("equipType").value,
        brand: document.getElementById("equipBrand").value, modelNumber: document.getElementById("equipModel").value,
        serialNumber: document.getElementById("equipSerial").value,
        installDate: document.getElementById("equipInstall").value || null,
        warrantyExpiration: document.getElementById("equipWarranty").value || null
    };
    await api("/equipment", { method: "POST", body: JSON.stringify(data) });
    loadEquipment();
}

async function deleteEquipment(id) { await api(`/equipment/${id}`, { method: "DELETE" }); loadEquipment(); }

// ═══════════════════════════════════════════════════════════════
// WARRANTY CLAIMS
// ═══════════════════════════════════════════════════════════════

async function loadWarranty() {
    const showClosed = document.getElementById("showClosedWarranty")?.checked;
    const res = await api("/warranty");
    if (!res || !res.ok) return;
    let claims = await res.json();
    if (!showClosed) claims = claims.filter(c => c.status !== "Closed");
    const tbody = document.getElementById("warrantyTableBody");
    tbody.innerHTML = claims.map(c => {
        const age = Math.floor((Date.now() - new Date(c.createdAt).getTime()) / 86400000);
        const eta = c.eta ? new Date(c.eta).toLocaleDateString() : "—";
        const statusColors = { Diagnosis: "#d97706", "Parts Ordered": "#ea580c", "Parts Received": "#2563eb", Scheduled: "#9333ea", Completed: "#16a34a", Closed: "#475569" };
        const color = statusColors[c.status] || "#475569";
        return `<tr onclick="openWarrantyModal('${c.id}')" style="cursor:pointer;">
            <td><b>${c.partName}</b><br/><span style="font-size:11px; color:#64748b;">${c.description || ""}</span></td>
            <td>${c.customerName || "—"}</td><td>${c.jobNumber || "—"}</td><td>${c.supplier || "—"}</td>
            <td>${c.rmaNumber || "—"}</td><td>${c.type}</td>
            <td><span style="background:${color}; color:white; padding:3px 10px; border-radius:12px; font-size:11px; font-weight:600;">${c.status}</span></td>
            <td>${eta}</td><td>${age}d</td>
        </tr>`;
    }).join("");
}

function openNewWarrantyForm() {
    document.getElementById("newWarrantyForm").classList.toggle("hidden");
}

async function createWarranty() {
    const data = {
        partName: document.getElementById("wPartName").value.trim(),
        customerName: document.getElementById("wCustomerName").value.trim(),
        jobNumber: document.getElementById("wJobNumber").value.trim(),
        supplier: document.getElementById("wSupplier").value.trim(),
        rmaNumber: document.getElementById("wRma").value.trim(),
        type: document.getElementById("wType").value
    };
    if (!data.partName) { toast("Part name required", "error"); return; }
    await api("/warranty", { method: "POST", body: JSON.stringify(data) });
    document.getElementById("newWarrantyForm").classList.add("hidden");
    loadWarranty();
}

async function openWarrantyModal(id) {
    const res = await api(`/warranty/${id}`);
    if (!res || !res.ok) return;
    const c = await res.json();
    document.getElementById("warrantyModalTitle").innerText = c.partName;
    const statuses = ["Diagnosis", "Parts Ordered", "Parts Received", "Scheduled", "Completed", "Closed"];
    const currentIdx = statuses.indexOf(c.status);
    let pipeline = '<div style="display:flex; gap:4px; margin-bottom:20px;">';
    statuses.forEach((s, i) => {
        const active = i <= currentIdx;
        pipeline += `<div style="flex:1; text-align:center; padding:6px 4px; border-radius:6px; font-size:10px; font-weight:600; cursor:pointer; background:${active ? '#2563eb' : '#1e293b'}; color:${active ? 'white' : '#475569'};" onclick="updateWarrantyStatus('${id}', '${s}')">${s}</div>`;
    });
    pipeline += '</div>';

    let html = pipeline;
    html += `<div style="display:grid; grid-template-columns:1fr 1fr; gap:10px; margin-bottom:20px;">
        <div><span style="color:#64748b; font-size:11px;">Customer:</span> ${c.customerName || "—"}</div>
        <div><span style="color:#64748b; font-size:11px;">Job #:</span> ${c.jobNumber || "—"}</div>
        <div><span style="color:#64748b; font-size:11px;">Supplier:</span> ${c.supplier || "—"}</div>
        <div><span style="color:#64748b; font-size:11px;">RMA:</span> ${c.rmaNumber || "—"}</div>
        <div><span style="color:#64748b; font-size:11px;">Type:</span> ${c.type}</div>
        <div><span style="color:#64748b; font-size:11px;">Created:</span> ${new Date(c.createdAt).toLocaleDateString()}</div>
    </div>`;
    html += `<h4 style="margin-bottom:8px;">Notes</h4>`;
    if (c.notes && c.notes.length > 0) {
        c.notes.forEach(n => {
            html += `<div style="background:#0f172a; padding:10px; border-radius:8px; margin-bottom:6px;">
                <div style="font-size:11px; color:#64748b;">${n.author || "—"} — ${new Date(n.createdAt).toLocaleString()}</div>
                <div style="color:#e2e8f0; margin-top:4px;">${n.text}</div>
            </div>`;
        });
    }
    html += `<div style="display:flex; gap:8px; margin-top:12px;">
        <input type="text" id="warrantyNoteInput" placeholder="Add a note..." style="flex:1;" />
        <button class="btn-primary" onclick="addWarrantyNote('${id}')">Add</button>
    </div>`;
    document.getElementById("warrantyModalBody").innerHTML = html;
    document.getElementById("warrantyModal").classList.remove("hidden");
}

async function updateWarrantyStatus(id, status) {
    await api(`/warranty/${id}/status`, { method: "PUT", body: JSON.stringify({ status }) });
    openWarrantyModal(id);
    loadWarranty();
}

async function addWarrantyNote(id) {
    const text = document.getElementById("warrantyNoteInput").value.trim();
    if (!text) return;
    await api(`/warranty/${id}/notes`, { method: "POST", body: JSON.stringify({ text }) });
    openWarrantyModal(id);
}

// ═══════════════════════════════════════════════════════════════
// PM TRACKER
// ═══════════════════════════════════════════════════════════════

async function loadPm() {
    const res = await api("/pm");
    if (!res || !res.ok) return;
    const data = await res.json();
    const tbody = document.getElementById("pmTableBody");
    tbody.innerHTML = data.map(p => {
        const daysSince = p.daysSinceLastPm;
        let statusColor = "#4ade80", statusText = "OK";
        if (daysSince >= 180) { statusColor = "#f87171"; statusText = "OVERDUE"; }
        else if (daysSince >= 120) { statusColor = "#fb923c"; statusText = "DUE SOON"; }
        return `<tr>
            <td>${p.customerName}</td>
            <td>${p.lastPmDate ? new Date(p.lastPmDate).toLocaleDateString() : "Never"}</td>
            <td>${p.lastJobNumber || "—"}</td><td>${p.jobTypeName || "—"}</td><td>${p.totalPmJobs}</td>
            <td>${daysSince} days</td>
            <td><span style="background:${statusColor}; color:${daysSince >= 120 ? 'white' : '#111'}; padding:3px 12px; border-radius:12px; font-size:11px; font-weight:700;">${statusText}</span></td>
        </tr>`;
    }).join("");
}

// ═══════════════════════════════════════════════════════════════
// PRICING CALCULATOR
// ═══════════════════════════════════════════════════════════════

const pricingTiers = [
    { max: 5, mult: 8, pct: 700 }, { max: 10, mult: 6, pct: 500 }, { max: 25, mult: 4, pct: 300 },
    { max: 50, mult: 3, pct: 200 }, { max: 100, mult: 2.5, pct: 150 }, { max: Infinity, mult: 1.75, pct: 75 }
];

function initPricing() { calculatePricing(); renderTierRef(); }

function calculatePricing() {
    const cost = parseFloat(document.getElementById("pricingCost")?.value) || 0;
    const ccOn = document.getElementById("pricingCcToggle")?.checked;
    const ccRate = ccOn ? 0.025 : 0;

    // Auto tier
    let tier = pricingTiers.find(t => cost < t.max) || pricingTiers[pricingTiers.length - 1];
    const autoBase = cost * tier.mult;
    const autoCc = autoBase * ccRate;
    const autoSell = autoBase + autoCc;
    const autoProfit = autoSell - cost;
    const autoMargin = autoSell > 0 ? ((autoProfit / autoSell) * 100).toFixed(1) : "0.0";

    const autoEl = document.getElementById("autoTierResults");
    if (autoEl) autoEl.innerHTML = buildResultTable(cost, tier.mult, tier.pct, autoBase, autoCc, autoSell, autoProfit, autoMargin);

    // Custom
    const mult = parseFloat(document.getElementById("customMultiplier")?.value) || 1;
    const pct = ((mult - 1) * 100).toFixed(1);
    const customBase = cost * mult;
    const customCc = customBase * ccRate;
    const customSell = customBase + customCc;
    const customProfit = customSell - cost;
    const customMargin = customSell > 0 ? ((customProfit / customSell) * 100).toFixed(1) : "0.0";

    const customEl = document.getElementById("customResults");
    if (customEl) customEl.innerHTML = buildResultTable(cost, mult, pct, customBase, customCc, customSell, customProfit, customMargin);
}

function buildResultTable(cost, mult, pct, base, cc, sell, profit, margin) {
    return `<table style="width:100%;"><tbody>
        <tr><td style="padding:8px;">Cost</td><td style="text-align:right; padding:8px;">$${cost.toFixed(2)}</td></tr>
        <tr><td style="padding:8px;">Multiplier</td><td style="text-align:right; padding:8px;">${mult.toFixed(2)}x</td></tr>
        <tr><td style="padding:8px;">Markup %</td><td style="text-align:right; padding:8px;">${pct}%</td></tr>
        <tr><td style="padding:8px;">Base Price</td><td style="text-align:right; padding:8px;">$${base.toFixed(2)}</td></tr>
        ${cc > 0 ? `<tr><td style="padding:8px;">CC Surcharge (2.5%)</td><td style="text-align:right; padding:8px;">$${cc.toFixed(2)}</td></tr>` : ''}
        <tr style="background:#dc2626;"><td style="padding:8px; font-weight:700;">Sell Price</td><td style="text-align:right; padding:8px; font-weight:700;">$${sell.toFixed(2)}</td></tr>
        <tr><td style="padding:8px;">Gross Profit</td><td style="text-align:right; padding:8px; color:#4ade80;">$${profit.toFixed(2)}</td></tr>
        <tr><td style="padding:8px;">Margin</td><td style="text-align:right; padding:8px; color:#4ade80;">${margin}%</td></tr>
    </tbody></table>`;
}

function syncFromPct() {
    const pct = parseFloat(document.getElementById("customMarkupPct").value) || 0;
    document.getElementById("customMultiplier").value = (1 + pct / 100).toFixed(4);
    calculatePricing();
}

function syncFromMult() {
    const mult = parseFloat(document.getElementById("customMultiplier").value) || 1;
    document.getElementById("customMarkupPct").value = ((mult - 1) * 100).toFixed(1);
    calculatePricing();
}

function setCustomPreset(mult, pct) {
    document.getElementById("customMultiplier").value = mult;
    document.getElementById("customMarkupPct").value = pct;
    calculatePricing();
}

function renderTierRef() {
    const el = document.getElementById("tierReference");
    if (!el) return;
    const labels = ["Under $5", "$5 – $10", "$10 – $50", "$50 – $100", "Over $100"];
    el.innerHTML = `<p style="text-align:center; font-size:12px; color:#64748b; margin-bottom:8px;">Markup Tiers Reference</p>
        <table style="width:100%; font-size:13px;"><thead><tr><th>Cost Range</th><th>Multiplier</th><th>Markup %</th></tr></thead><tbody>
        ${pricingTiers.slice(0, 5).map((t, i) => `<tr><td style="text-align:center; padding:6px;">${labels[i]}</td><td style="text-align:center; padding:6px;">${t.mult}x</td><td style="text-align:center; padding:6px;">${t.pct}%</td></tr>`).join("")}
        </tbody></table>`;
}

// ═══════════════════════════════════════════════════════════════
// CUSTOMER PROFILE
// ═══════════════════════════════════════════════════════════════

async function openCustomerProfile(id) {
    const res = await api(`/customers/${id}`);
    if (!res || !res.ok) return;
    const c = await res.json();
    document.getElementById("customerProfileTitle").innerText = c.name;

    let html = '';

    // Top stats row
    const balance = Number(c.balanceOwed || 0);
    html += `<div style="display:grid; grid-template-columns:1fr 1fr 1fr; gap:12px; margin-bottom:20px;">
        <div style="background:#0f172a; padding:14px 16px; border-radius:10px; border:1px solid #334155;">
            <div style="font-size:11px; color:#64748b; text-transform:uppercase; font-weight:600;">Balance Owed</div>
            <div style="font-size:22px; font-weight:800; color:${balance > 0 ? '#f87171' : '#4ade80'}; margin-top:4px;">$${balance.toLocaleString()}</div>
        </div>
        <div style="background:#0f172a; padding:14px 16px; border-radius:10px; border:1px solid #334155;">
            <div style="font-size:11px; color:#64748b; text-transform:uppercase; font-weight:600;">Work Orders</div>
            <div style="font-size:22px; font-weight:800; color:#e2e8f0; margin-top:4px;">${c.workOrders ? c.workOrders.length : 0}</div>
        </div>
        <div style="background:#0f172a; padding:14px 16px; border-radius:10px; border:1px solid #334155;">
            <div style="font-size:11px; color:#64748b; text-transform:uppercase; font-weight:600;">Last PM</div>
            <div style="font-size:14px; font-weight:700; margin-top:6px; color:${c.lastPm ? '#e2e8f0' : '#f87171'};">${c.lastPm ? new Date(c.lastPm.completedAt).toLocaleDateString() + ' — ' + c.lastPm.jobTypeName : 'No PM on record'}</div>
        </div>
    </div>`;

    // PM reminder banner
    if (c.lastPm) {
        const daysSince = Math.floor((Date.now() - new Date(c.lastPm.completedAt).getTime()) / 86400000);
        if (daysSince >= 120) {
            const overdue = daysSince >= 180;
            html += `<div style="background:${overdue ? '#7f1d1d' : '#78350f'}; border:1px solid ${overdue ? '#dc2626' : '#d97706'}; border-radius:10px; padding:14px 18px; margin-bottom:20px; display:flex; justify-content:space-between; align-items:center;">
                <div>
                    <div style="font-weight:700; color:${overdue ? '#fca5a5' : '#fde68a'};">${overdue ? '⚠ PM OVERDUE' : '⏰ PM DUE SOON'} — ${daysSince} days since last PM</div>
                    <div style="font-size:12px; color:#94a3b8; margin-top:2px;">Last PM: Job #${c.lastPm.jobNumber} on ${new Date(c.lastPm.completedAt).toLocaleDateString()}</div>
                </div>
                <button class="btn-primary" onclick="sendPmReminder('${id}', '${c.name.replace(/'/g, "\\'")}')">📧 Schedule PM</button>
            </div>`;
        }
    } else {
        html += `<div style="background:#7f1d1d; border:1px solid #dc2626; border-radius:10px; padding:14px 18px; margin-bottom:20px; display:flex; justify-content:space-between; align-items:center;">
            <div>
                <div style="font-weight:700; color:#fca5a5;">⚠ NO PM ON RECORD</div>
                <div style="font-size:12px; color:#94a3b8; margin-top:2px;">This customer has never had a preventive maintenance visit</div>
            </div>
            <button class="btn-primary" onclick="sendPmReminder('${id}', '${c.name.replace(/'/g, "\\'")}')">📧 Schedule PM</button>
        </div>`;
    }

    // Contacts section
    if (c.contacts && c.contacts.length > 0) {
        html += `<div style="margin-bottom:20px;">
            <h4 style="font-size:13px; text-transform:uppercase; color:#64748b; font-weight:700; margin-bottom:10px; letter-spacing:0.5px;">Contacts</h4>`;
        c.contacts.forEach(ct => {
            const icon = ct.type === 'Email' ? '✉' : ct.type === 'Phone' ? '📞' : ct.type === 'MobilePhone' ? '📱' : '•';
            const val = ct.value || '';
            let link = val;
            if (ct.type === 'Email') link = `<a href="mailto:${val}" style="color:#60a5fa; text-decoration:none;">${val}</a>`;
            else if (ct.type === 'Phone' || ct.type === 'MobilePhone') link = `<a href="tel:${val}" style="color:#60a5fa; text-decoration:none;">${val}</a>`;
            html += `<div style="display:flex; align-items:center; gap:10px; padding:8px 12px; background:#0f172a; border:1px solid #1e293b; border-radius:8px; margin-bottom:4px;">
                <span style="font-size:16px;">${icon}</span>
                <span style="color:#94a3b8; font-size:11px; width:80px; text-transform:uppercase;">${ct.type || 'Contact'}</span>
                <span>${link}</span>
                ${ct.memo ? `<span style="color:#475569; font-size:11px; margin-left:auto;">${ct.memo}</span>` : ''}
            </div>`;
        });
        html += `</div>`;
    }

    // Service Locations
    if (c.locations && c.locations.length > 0) {
        html += `<div style="margin-bottom:20px;">
            <h4 style="font-size:13px; text-transform:uppercase; color:#64748b; font-weight:700; margin-bottom:10px; letter-spacing:0.5px;">Service Locations</h4>`;
        c.locations.forEach(loc => {
            const addr = [loc.street, loc.unit, loc.city, loc.state, loc.zip].filter(Boolean).join(', ');
            html += `<div style="background:#0f172a; border:1px solid #1e293b; border-radius:10px; padding:12px 16px; margin-bottom:8px;">
                <div style="font-weight:600; color:#e2e8f0;">${loc.name || addr}</div>
                ${loc.name ? `<div style="font-size:12px; color:#94a3b8; margin-top:2px;">📍 ${addr}</div>` : ''}`;
            if (loc.contacts && loc.contacts.length > 0) {
                loc.contacts.forEach(lc => {
                    const lcIcon = lc.type === 'Email' ? '✉' : '📞';
                    let lcLink = lc.value;
                    if (lc.type === 'Email') lcLink = `<a href="mailto:${lc.value}" style="color:#60a5fa; text-decoration:none;">${lc.value}</a>`;
                    else lcLink = `<a href="tel:${lc.value}" style="color:#60a5fa; text-decoration:none;">${lc.value}</a>`;
                    html += `<div style="font-size:12px; color:#94a3b8; margin-top:4px;">${lcIcon} ${lc.type}: ${lcLink}</div>`;
                });
            }
            html += `</div>`;
        });
        html += `</div>`;
    }

    // Equipment
    if (c.equipment && c.equipment.length > 0) {
        html += `<div style="margin-bottom:20px;">
            <h4 style="font-size:13px; text-transform:uppercase; color:#64748b; font-weight:700; margin-bottom:10px; letter-spacing:0.5px;">Equipment</h4>
            <table class="data-table"><thead><tr><th>Type</th><th>Brand</th><th>Model</th><th>Serial</th><th>Installed</th><th>Warranty</th></tr></thead><tbody>`;
        c.equipment.forEach(e => {
            const installed = e.installDate ? new Date(e.installDate).toLocaleDateString() : '—';
            const warranty = e.warrantyExpiration ? new Date(e.warrantyExpiration).toLocaleDateString() : '—';
            html += `<tr><td>${e.type || '—'}</td><td>${e.brand || '—'}</td><td>${e.modelNumber || '—'}</td><td>${e.serialNumber || '—'}</td><td>${installed}</td><td>${warranty}</td></tr>`;
        });
        html += `</tbody></table></div>`;
    }

    // Work Orders
    if (c.workOrders && c.workOrders.length > 0) {
        html += `<div style="margin-bottom:20px;">
            <h4 style="font-size:13px; text-transform:uppercase; color:#64748b; font-weight:700; margin-bottom:10px; letter-spacing:0.5px;">Work Orders</h4>
            <table class="data-table"><thead><tr><th>Job #</th><th>Status</th><th>Type</th><th>Created</th><th>Amount</th></tr></thead><tbody>`;
        c.workOrders.forEach(wo => {
            html += `<tr><td>${wo.jobNumber || '—'}</td><td>${wo.status || '—'}</td><td>${wo.jobTypeName || '—'}</td><td>${wo.createdAt ? new Date(wo.createdAt).toLocaleDateString() : '—'}</td><td>${wo.totalAmount ? '$' + Number(wo.totalAmount).toLocaleString() : '—'}</td></tr>`;
        });
        html += `</tbody></table></div>`;
    }

    // Invoices
    if (c.invoices && c.invoices.length > 0) {
        html += `<div style="margin-bottom:10px;">
            <h4 style="font-size:13px; text-transform:uppercase; color:#64748b; font-weight:700; margin-bottom:10px; letter-spacing:0.5px;">Invoices</h4>
            <table class="data-table"><thead><tr><th>Invoice #</th><th>Date</th><th>Total</th><th>Balance</th><th>Status</th></tr></thead><tbody>`;
        c.invoices.forEach(inv => {
            const bal = Number(inv.balanceRemaining || 0);
            html += `<tr><td>${inv.invoiceNumber || '—'}</td><td>${inv.invoiceDate ? new Date(inv.invoiceDate).toLocaleDateString() : '—'}</td><td>$${Number(inv.totalAmount || 0).toLocaleString()}</td><td style="color:${bal > 0 ? '#f87171' : '#4ade80'};">$${bal.toLocaleString()}</td><td>${inv.status || '—'}</td></tr>`;
        });
        html += `</tbody></table></div>`;
    }

    document.getElementById("customerProfileBody").innerHTML = html;
    document.getElementById("customerProfileModal").classList.remove("hidden");
}

// PM Reminder — find best contact and compose message
async function sendPmReminder(customerId, customerName) {
    const res = await api(`/customers/${customerId}`);
    if (!res || !res.ok) return;
    const c = await res.json();

    const message = `Hi, this is Patriot Mechanical. We'd like to schedule your next preventive maintenance visit for ${customerName}. Please let us know a date and time that works for you. Thank you!`;

    // Find email contact
    const email = (c.contacts || []).find(ct => ct.type === 'Email');
    const phone = (c.contacts || []).find(ct => ct.type === 'Phone' || ct.type === 'MobilePhone');

    if (email) {
        const subject = encodeURIComponent(`Schedule PM — ${customerName}`);
        const body = encodeURIComponent(message);
        window.open(`mailto:${email.value}?subject=${subject}&body=${body}`, '_blank');
    } else if (phone) {
        window.open(`sms:${phone.value}?body=${encodeURIComponent(message)}`, '_blank');
    } else {
        // No contacts — copy to clipboard
        navigator.clipboard.writeText(message);
        toast("Message copied to clipboard — no email or phone on file");
    }
}

// ═══════════════════════════════════════════════════════════════
// SORTABLE TABLES
// ═══════════════════════════════════════════════════════════════

function makeSortable(tableSelector) {
    const table = document.querySelector(tableSelector);
    if (!table) return;
    const headers = table.querySelectorAll("thead th");
    headers.forEach((th, colIdx) => {
        th.style.cursor = "pointer";
        th.style.userSelect = "none";
        th.addEventListener("click", () => {
            const tbody = table.querySelector("tbody");
            const rows = Array.from(tbody.querySelectorAll("tr"));
            const asc = th.dataset.sort !== "asc";
            headers.forEach(h => { h.dataset.sort = ""; h.textContent = h.textContent.replace(/ [▲▼]/g, ""); });
            th.dataset.sort = asc ? "asc" : "desc";
            th.textContent += asc ? " ▲" : " ▼";
            rows.sort((a, b) => {
                const aText = a.cells[colIdx]?.textContent.trim() || "";
                const bText = b.cells[colIdx]?.textContent.trim() || "";
                const aNum = parseFloat(aText.replace(/[$,]/g, ""));
                const bNum = parseFloat(bText.replace(/[$,]/g, ""));
                if (!isNaN(aNum) && !isNaN(bNum)) return asc ? aNum - bNum : bNum - aNum;
                return asc ? aText.localeCompare(bText) : bText.localeCompare(aText);
            });
            rows.forEach(r => tbody.appendChild(r));
        });
    });
}

// Auto-apply sorting to all .data-table tables after content loads
function applySortableToAll() {
    document.querySelectorAll(".data-table").forEach((t, i) => {
        t.id = t.id || `sortable-table-${i}`;
        makeSortable(`#${t.id}`);
    });
}

// Also make the AR/AP dashboard tables sortable
function applyDashboardSorting() {
    document.querySelectorAll("#customerTable, #usersTable, table").forEach((t, i) => {
        if (!t.id) t.id = `auto-sort-${i}`;
        makeSortable(`#${t.id}`);
    });
}

function toast(message, type = "success") {
    const el = document.createElement("div");
    el.className = `toast ${type}`;
    el.innerText = message;
    document.body.appendChild(el);
    setTimeout(() => el.remove(), 3000);
}

// ═══════════════════════════════════════════════════════════════
// GLOBAL SEARCH
// ═══════════════════════════════════════════════════════════════

let searchTimeout = null;
let searchSelectedIdx = -1;
let searchResultsData = [];

async function onGlobalSearch() {
    const q = document.getElementById("globalSearchInput").value.trim();
    const wrap = document.getElementById("searchResults");
    if (q.length < 2) { wrap.classList.add("hidden"); searchResultsData = []; searchSelectedIdx = -1; return; }
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(async () => {
        const res = await api(`/search?q=${encodeURIComponent(q)}`);
        if (!res || !res.ok) return;
        const data = await res.json();
        searchResultsData = data.results || [];
        searchSelectedIdx = -1;
        renderSearchResults();
    }, 250);
}

function renderSearchResults() {
    const wrap = document.getElementById("searchResults");
    if (searchResultsData.length === 0) { wrap.innerHTML = '<div class="search-empty">No results found</div>'; wrap.classList.remove("hidden"); return; }
    const icons = { customer: "👤", workorder: "📋", equipment: "⚙️", vendor: "💰", warranty: "🛡️", subcontractor: "🏗️" };
    wrap.innerHTML = searchResultsData.map((r, i) =>
        `<div class="search-result-item${i === searchSelectedIdx ? ' selected' : ''}" onclick="selectSearchResult(${i})" onmouseenter="searchSelectedIdx=${i}; renderSearchResults();">
            <div class="search-result-icon">${icons[r.type] || "•"}</div>
            <div class="search-result-text">
                <div class="search-result-title">${r.title}</div>
                <div class="search-result-subtitle">${r.subtitle}</div>
            </div>
            <span class="search-result-type">${r.type}</span>
        </div>`
    ).join("");
    wrap.classList.remove("hidden");
}

function onSearchKeydown(e) {
    const wrap = document.getElementById("searchResults");
    if (wrap.classList.contains("hidden")) return;
    if (e.key === "ArrowDown") { e.preventDefault(); searchSelectedIdx = Math.min(searchSelectedIdx + 1, searchResultsData.length - 1); renderSearchResults(); }
    else if (e.key === "ArrowUp") { e.preventDefault(); searchSelectedIdx = Math.max(searchSelectedIdx - 1, 0); renderSearchResults(); }
    else if (e.key === "Enter" && searchSelectedIdx >= 0) { e.preventDefault(); selectSearchResult(searchSelectedIdx); }
    else if (e.key === "Escape") { closeSearch(); }
}

function selectSearchResult(idx) {
    const r = searchResultsData[idx]; if (!r) return; closeSearch();
    switch (r.type) {
        case "customer": showView("customersView", document.querySelector('[onclick*="customersView"]')); setTimeout(() => openCustomerProfile(r.id), 300); break;
        case "workorder": showView("boardView", document.querySelector('[onclick*="boardView"]')); break;
        case "equipment": showView("equipmentView", document.querySelector('[onclick*="equipmentView"]')); break;
        case "vendor": showView("apView", document.querySelector('[onclick*="apView"]')); break;
        case "warranty": showView("warrantyView", document.querySelector('[onclick*="warrantyView"]')); setTimeout(() => openWarrantyModal(r.id), 300); break;
        case "subcontractor": showView("subsView", document.querySelector('[onclick*="subsView"]')); break;
    }
}

function onSearchFocus() {
    const q = document.getElementById("globalSearchInput").value.trim();
    if (q.length >= 2 && searchResultsData.length > 0) document.getElementById("searchResults").classList.remove("hidden");
}

function closeSearch() {
    document.getElementById("searchResults").classList.add("hidden");
    document.getElementById("globalSearchInput").value = "";
    document.getElementById("globalSearchInput").blur();
    searchResultsData = []; searchSelectedIdx = -1;
}

document.addEventListener("click", function(e) {
    const wrap = document.querySelector(".global-search-wrap");
    if (wrap && !wrap.contains(e.target)) document.getElementById("searchResults").classList.add("hidden");
});

document.addEventListener("keydown", function(e) {
    if ((e.ctrlKey || e.metaKey) && e.key === "k") { e.preventDefault(); document.getElementById("globalSearchInput").focus(); }
});