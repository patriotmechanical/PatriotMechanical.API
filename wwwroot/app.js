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
    const views = ["dashboardPage", "customersView", "apView", "adminView"];

    views.forEach(v => {
        const el = document.getElementById(v);
        if (el) el.style.display = "none";
    });

    document.getElementById(viewId).style.display = "block";

    // Update active nav link
    if (clickedLink) {
        document.querySelectorAll(".nav-link").forEach(l => l.classList.remove("active"));
        clickedLink.classList.add("active");
    }

    if (viewId === "customersView") loadCustomers();
    if (viewId === "apView") { loadAp(); loadVendors(); }
    if (viewId === "dashboardPage") loadDashboard();
    if (viewId === "adminView") loadAdminSettings();
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
        apTable.innerHTML += `<tr><td>${v.name}</td><td>$${Number(v.totalOwed).toLocaleString()}</td><td>${due}</td></tr>`;
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
            row.onclick = () => loadCustomerDetail(c.id, c.name);
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