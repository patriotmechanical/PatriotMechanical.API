// ═══════════════════════════════════════════════════════════════
// STATE
// ═══════════════════════════════════════════════════════════════

let token = localStorage.getItem("jwt");
let currentUser = null;
let currentCompany = null;
let allCustomers = [];
let currentCustomerTab = "all";
let currentVendorId = null;
let currentSubId = null;

// ═══════════════════════════════════════════════════════════════
// SORTABLE TABLE ENGINE
// ═══════════════════════════════════════════════════════════════

const tableSortState = {};

function makeSortable(tableId) {
    const table = document.getElementById(tableId);
    if (!table) return;
    const headers = table.querySelectorAll("th:not(.no-sort)");
    headers.forEach((th, colIndex) => {
        // Add sort arrow span
        if (!th.querySelector(".sort-arrow")) {
            const arrow = document.createElement("span");
            arrow.className = "sort-arrow";
            th.appendChild(arrow);
        }
        th.onclick = () => sortTable(tableId, colIndex, th);
    });
}

function sortTable(tableId, colIndex, clickedTh) {
    const table = document.getElementById(tableId);
    const tbody = table.querySelector("tbody");
    const rows = Array.from(tbody.querySelectorAll("tr:not(.empty-row)"));
    if (rows.length === 0) return;

    // Determine sort direction
    const currentDir = tableSortState[tableId]?.col === colIndex ? tableSortState[tableId].dir : null;
    const newDir = currentDir === "asc" ? "desc" : "asc";
    tableSortState[tableId] = { col: colIndex, dir: newDir };

    // Update header classes
    table.querySelectorAll("th").forEach(th => th.classList.remove("sort-asc", "sort-desc"));
    clickedTh.classList.add(newDir === "asc" ? "sort-asc" : "sort-desc");

    // Sort rows
    rows.sort((a, b) => {
        let aVal = a.cells[colIndex]?.textContent.trim() || "";
        let bVal = b.cells[colIndex]?.textContent.trim() || "";

        // Strip $ and , for numeric comparison
        const aNum = parseFloat(aVal.replace(/[$,%]/g, "").replace(/,/g, ""));
        const bNum = parseFloat(bVal.replace(/[$,%]/g, "").replace(/,/g, ""));

        if (!isNaN(aNum) && !isNaN(bNum)) {
            return newDir === "asc" ? aNum - bNum : bNum - aNum;
        }

        // Date check
        const aDate = Date.parse(aVal);
        const bDate = Date.parse(bVal);
        if (!isNaN(aDate) && !isNaN(bDate)) {
            return newDir === "asc" ? aDate - bDate : bDate - aDate;
        }

        // String compare
        return newDir === "asc" ? aVal.localeCompare(bVal) : bVal.localeCompare(aVal);
    });

    rows.forEach(row => tbody.appendChild(row));
}

// ═══════════════════════════════════════════════════════════════
// API HELPER
// ═══════════════════════════════════════════════════════════════

async function api(url, options = {}) {
    const headers = { "Content-Type": "application/json", ...options.headers };
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetch(url, { ...options, headers });
    if (res.status === 401) { doLogout(); return null; }
    return res;
}

// ═══════════════════════════════════════════════════════════════
// APP INIT
// ═══════════════════════════════════════════════════════════════

document.addEventListener("DOMContentLoaded", async () => { await checkAppState(); });

async function checkAppState() {
    // Demo mode banner
    if (localStorage.getItem("isDemo") === "true") {
        if (!document.getElementById("demoBanner")) {
            const banner = document.createElement("div");
            banner.id = "demoBanner";
            banner.style.cssText = "background:#d97706; color:white; text-align:center; padding:8px; font-size:13px; font-weight:600; position:fixed; top:0; left:0; right:0; z-index:9999;";
            banner.innerHTML = '🔶 DEMO MODE — Sample data resets daily. <a href="/" style="color:white; text-decoration:underline; margin-left:8px;" onclick="localStorage.removeItem(\'isDemo\'); localStorage.removeItem(\'jwt\');">Exit Demo</a>';
            document.body.prepend(banner);
            document.body.style.paddingTop = "36px";
        }
        // Hide real-data features for demo
        setTimeout(() => {
            const syncBtn = document.getElementById("hardRefreshBtn");
            if (syncBtn) syncBtn.style.display = "none";
            document.querySelectorAll(".nav-link").forEach(link => {
                if (link.textContent.trim().includes("Settings")) link.style.display = "none";
            });
        }, 100);
    }
    try {
        const res = await fetch("/auth/status");
        const data = await res.json();
        if (!data.setupComplete) { showScreen("setupScreen"); return; }
        if (token) {
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
    } catch (err) { console.error("Init error:", err); showScreen("loginScreen"); }
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
    if (!email || !password) { showError("loginError", "Please enter email and password."); return; }
    try {
        const res = await fetch("/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, password }) });
        const data = await res.json();
        if (!res.ok) { showError("loginError", data.message || "Login failed."); return; }
        token = data.token; localStorage.setItem("jwt", token);
        currentUser = data.user; currentCompany = data.company;
        enterApp();
    } catch (err) { showError("loginError", "Connection error. Try again."); }
}

function doLogout() {
    token = null; currentUser = null; currentCompany = null;
    localStorage.removeItem("jwt"); showScreen("loginScreen");
    document.getElementById("loginEmail").value = "";
    document.getElementById("loginPassword").value = "";
}

// ═══════════════════════════════════════════════════════════════
// SETUP WIZARD
// ═══════════════════════════════════════════════════════════════

function goToSetupStep2() {
    hideError("setupError");
    if (!document.getElementById("setupCompanyName").value || !document.getElementById("setupEmail").value || !document.getElementById("setupPassword").value) { showError("setupError", "Please fill in all fields."); return; }
    if (document.getElementById("setupPassword").value.length < 8) { showError("setupError", "Password must be at least 8 characters."); return; }
    document.getElementById("setupStep1").classList.add("hidden");
    document.getElementById("setupStep2").classList.remove("hidden");
}

function goToSetupStep1() { document.getElementById("setupStep2").classList.add("hidden"); document.getElementById("setupStep1").classList.remove("hidden"); }
async function completeSetup() { await doSetup(false); }
async function skipServiceTitan() { await doSetup(true); }

async function doSetup(skipST) {
    hideError("setupError");
    const body = { companyName: document.getElementById("setupCompanyName").value, fullName: document.getElementById("setupFullName").value, email: document.getElementById("setupEmail").value, password: document.getElementById("setupPassword").value };
    if (!skipST) { body.serviceTitanTenantId = document.getElementById("setupTenantId").value; body.serviceTitanClientId = document.getElementById("setupClientId").value; body.serviceTitanClientSecret = document.getElementById("setupClientSecret").value; body.serviceTitanAppKey = document.getElementById("setupAppKey").value; }
    try {
        const res = await fetch("/auth/setup", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
        const data = await res.json();
        if (!res.ok) { showError("setupError", data.message || "Setup failed."); return; }
        token = data.token; localStorage.setItem("jwt", token);
        currentUser = data.user; currentCompany = data.company;
        enterApp(); toast("Setup complete! Welcome aboard.", "success");
    } catch (err) { showError("setupError", "Connection error. Try again."); }
}

// ═══════════════════════════════════════════════════════════════
// ENTER APP
// ═══════════════════════════════════════════════════════════════

function enterApp() {
    showScreen("mainApp");
    document.getElementById("sidebarCompanyName").innerText = currentCompany?.companyName || "Company";
    document.getElementById("sidebarUserName").innerText = currentUser?.fullName || currentUser?.email || "User";
    loadDashboard();
}

// ═══════════════════════════════════════════════════════════════
// VIEW NAVIGATION
// ═══════════════════════════════════════════════════════════════

function showView(viewId, clickedLink) {
    const views = ["dashboardPage", "boardView", "todoView", "customersView", "subsView", "equipmentView", "warrantyView", "pmView", "apView", "pricingView", "adminView"];
    views.forEach(v => { const el = document.getElementById(v); if (el) el.style.display = "none"; });
    document.getElementById(viewId).style.display = "block";
    if (clickedLink) { document.querySelectorAll(".nav-link").forEach(l => l.classList.remove("active")); clickedLink.classList.add("active"); }
    if (viewId === "dashboardPage") loadDashboard();
    if (viewId === "boardView") loadBoard();
    if (viewId === "todoView") loadTodos();
    if (viewId === "warrantyView") loadWarrantyClaims();
    if (viewId === "customersView") loadCustomers();
    if (viewId === "subsView") loadSubcontractors();
    if (viewId === "equipmentView") loadEquipment();
    if (viewId === "pmView") loadPmTracker();
    if (viewId === "apView") { loadAp(); loadVendors(); }
    if (viewId === "adminView") loadAdminSettings();
}

// ═══════════════════════════════════════════════════════════════
// DASHBOARD
// ═══════════════════════════════════════════════════════════════

async function loadDashboard() {
    const res = await api("/dashboard");
    if (!res || !res.ok) return;
    const data = await res.json();

    document.getElementById("totalAR").innerText = "$" + Number(data.totalAR || 0).toLocaleString();

    // Total AP = sum of all total invoice amounts (fall back to totalOwed if totalInvoiceAmount not set)
    const totalApInvoices = data.ap.reduce((sum, v) => sum + Number(v.totalInvoiceAmount || v.totalOwed || 0), 0);
    document.getElementById("totalAP").innerText = "$" + totalApInvoices.toLocaleString();

    const net = Number(data.totalAR || 0) - totalApInvoices;
    const netEl = document.getElementById("netPosition");
    netEl.innerText = "$" + net.toLocaleString();
    netEl.style.color = net >= 0 ? "#4ade80" : "#f87171";

    // AR table
    const arTable = document.getElementById("arTableBody");
    arTable.innerHTML = "";
    data.ar.forEach(c => { arTable.innerHTML += `<tr><td class="bold">${c.name}</td><td class="text-right">$${Number(c.totalOwed).toLocaleString()}</td></tr>`; });
    if (data.ar.length === 0) arTable.innerHTML = '<tr class="empty-row"><td colspan="2">No outstanding receivables</td></tr>';
    makeSortable("dashArTable");

    // AP table
    const apTable = document.getElementById("apTableBody2");
    apTable.innerHTML = "";
    data.ap.forEach(v => {
        const due = v.nextDue ? new Date(v.nextDue).toLocaleDateString() : "-";
        const owedNow = Number(v.totalOwed || 0);
        const totalInv = Number(v.totalInvoiceAmount || v.totalOwed || 0);
        apTable.innerHTML += `<tr>
            <td class="bold">${v.name}</td>
            <td class="text-right">$${owedNow.toLocaleString(undefined, {minimumFractionDigits: 2, maximumFractionDigits: 2})}</td>
            <td class="text-right">$${totalInv.toLocaleString(undefined, {minimumFractionDigits: 2, maximumFractionDigits: 2})}</td>
            <td>${due}</td>
        </tr>`;
    });
    if (data.ap.length === 0) apTable.innerHTML = '<tr class="empty-row"><td colspan="4">No outstanding payables</td></tr>';
    makeSortable("dashApTable");

    // Open Work Orders
    const woTable = document.getElementById("openWoBody");
    woTable.innerHTML = "";
    if (data.openWorkOrders && data.openWorkOrders.length > 0) {
        data.openWorkOrders.forEach(wo => {
            const created = wo.createdAt ? new Date(wo.createdAt).toLocaleDateString() : "-";
            woTable.innerHTML += `<tr><td class="bold">${wo.jobNumber}</td><td>${wo.customerName}</td><td><span class="status-badge open">${wo.status}</span></td><td>${created}</td><td class="text-right">$${Number(wo.totalAmount || 0).toLocaleString()}</td></tr>`;
        });
    } else { woTable.innerHTML = '<tr class="empty-row"><td colspan="5">No open work orders</td></tr>'; }
    makeSortable("dashWoTable");

    // Ops Stats Row
    renderOpsStats(data);
}

function renderOpsStats(data) {
    const row = document.getElementById("opsStatsRow");
    if (!row) return;

    const openWoCount = data.openWorkOrders ? data.openWorkOrders.length : 0;

    // Map board columns to stat categories
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
        return `<div class="ops-stat-card" style="--stat-color:${s.color};">
            <h4>${s.label}</h4>
            <div class="stat-number${zeroClass}" ${onclick}>${s.count}</div>
        </div>`;
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
            html += `<tr><td class="bold">${item.jobNumber || "—"}</td><td>${item.customerName || "—"}</td><td><span class="status-badge open">${item.status || "—"}</span></td><td>${created}</td></tr>`;
        } else if (stat.type === "board") {
            html += `<tr><td class="bold">${item.jobNumber || "—"}</td><td>${item.customerName || "—"}</td></tr>`;
        } else if (stat.type === "pm") {
            const lastPm = item.lastPm ? new Date(item.lastPm).toLocaleDateString() : "Never";
            const days = item.lastPm ? Math.floor((Date.now() - new Date(item.lastPm).getTime()) / 86400000) : "—";
            html += `<tr><td class="bold">${item.jobNumber || "—"}</td><td>${item.customerName || "—"}</td><td>${lastPm}</td><td class="danger">${days}</td></tr>`;
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
    const tbody = document.getElementById("custBody");
    tbody.innerHTML = "";
    let filtered = allCustomers;
    if (currentCustomerTab === "balance") filtered = allCustomers.filter(c => c.totalAR > 0);
    if (currentCustomerTab === "overdue") filtered = allCustomers.filter(c => c.days30 > 0 || c.days60 > 0 || c.days90 > 0);

    filtered.sort((a, b) => b.days90 - a.days90).forEach(c => {
        const row = document.createElement("tr");
        row.className = "clickable-row";
        row.innerHTML = `
            <td class="bold">${c.name}</td>
            <td class="text-right">$${Number(c.totalAR).toLocaleString()}</td>
            <td class="text-right">$${Number(c.current).toLocaleString()}</td>
            <td class="text-right">$${Number(c.days30).toLocaleString()}</td>
            <td class="text-right">$${Number(c.days60).toLocaleString()}</td>
            <td class="text-right danger">$${Number(c.days90).toLocaleString()}</td>`;
        row.onclick = () => openCustomerProfile(c.id);
        tbody.appendChild(row);
    });
    if (filtered.length === 0) tbody.innerHTML = '<tr class="empty-row"><td colspan="6">No customers found</td></tr>';
    makeSortable("custTable");
}

function setCustomerTab(tab) {
    currentCustomerTab = tab;
    document.querySelectorAll("#customersView .tab").forEach(t => t.classList.remove("active"));
    event.target.classList.add("active");
    renderCustomers();
}

// ═══════════════════════════════════════════════════════════════
// CUSTOMER PROFILE MODAL
// ═══════════════════════════════════════════════════════════════

async function openCustomerProfile(id) {
    const res = await api(`/customers/${id}`);
    if (!res || !res.ok) return;
    const c = await res.json();

    document.getElementById("profileName").innerText = c.name;

    // Balance
    const bal = Number(c.balanceOwed || 0);
    document.getElementById("profileBalance").innerText = "$" + bal.toLocaleString();
    document.getElementById("profileBalance").className = bal > 0 ? "big-number danger" : "big-number success-text";

    // Work order count
    document.getElementById("profileWoCount").innerText = c.workOrders ? c.workOrders.length : "0";

    // Last PM + reminder
    const pmSection = document.getElementById("profilePmSection");
    if (c.lastPm) {
        const pmDate = new Date(c.lastPm.completedAt);
        const daysSince = Math.floor((Date.now() - pmDate.getTime()) / (1000 * 60 * 60 * 24));
        document.getElementById("profileLastPm").innerText = pmDate.toLocaleDateString() + ` (${daysSince} days ago)`;

        if (daysSince > 180) {
            pmSection.innerHTML = `
                <div style="background:#7f1d1d22; border:1px solid #7f1d1d; border-radius:8px; padding:14px; margin-top:10px; display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:10px;">
                    <div>
                        <span style="color:#fca5a5; font-weight:600;">⚠ PM Overdue</span>
                        <span class="muted" style="margin-left:8px;">Last PM was ${daysSince} days ago</span>
                    </div>
                    <button class="btn-primary" style="font-size:13px; padding:8px 16px;" onclick="sendPmReminder('${c.id}', '${c.name.replace(/'/g, "\\'")}', ${daysSince})">📧 Send PM Reminder</button>
                </div>`;
        } else {
            pmSection.innerHTML = '';
        }
    } else {
        document.getElementById("profileLastPm").innerText = "None on record";
        pmSection.innerHTML = `
            <div style="background:#7f1d1d22; border:1px solid #7f1d1d; border-radius:8px; padding:14px; margin-top:10px; display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:10px;">
                <div>
                    <span style="color:#fca5a5; font-weight:600;">⚠ No PM on file</span>
                    <span class="muted" style="margin-left:8px;">This customer has never had a PM synced</span>
                </div>
                <button class="btn-primary" style="font-size:13px; padding:8px 16px;" onclick="sendPmReminder('${c.id}', '${c.name.replace(/'/g, "\\'")}', 0)">📧 Send PM Reminder</button>
            </div>`;
    }

    // Contacts
    const contactsDiv = document.getElementById("profileContacts");
    if (c.contacts && c.contacts.length > 0) {
        contactsDiv.innerHTML = c.contacts.map(ct => `
            <div class="contact-item">
                <span class="contact-badge">${ct.type}</span>
                <span>${ct.value}</span>
                ${ct.memo ? `<span class="muted">— ${ct.memo}</span>` : ""}
            </div>
        `).join("");
    } else {
        contactsDiv.innerHTML = '<div class="muted" style="padding:8px 0;">No contacts on file</div>';
    }

    // Locations
    const locsDiv = document.getElementById("profileLocations");
    if (c.locations && c.locations.length > 0) {
        locsDiv.innerHTML = c.locations.map(l => {
            const addr = [l.street, l.unit, l.city, l.state, l.zip].filter(Boolean).join(", ");
            const locContacts = l.contacts && l.contacts.length > 0
                ? l.contacts.map(lc => `<div class="contact-item" style="padding:4px 0;"><span class="contact-badge">${lc.type}</span><span>${lc.value}</span></div>`).join("")
                : "";
            return `<div class="location-card"><div class="loc-name">${l.name || "Service Location"}</div><div class="loc-addr">${addr || "No address"}</div>${locContacts}</div>`;
        }).join("");
    } else {
        locsDiv.innerHTML = '<div class="muted" style="padding:8px 0;">No locations on file</div>';
    }

    // Work Orders
    const woBody = document.getElementById("profileWoBody");
    if (c.workOrders && c.workOrders.length > 0) {
        woBody.innerHTML = c.workOrders.map(wo => {
            const created = wo.createdAt ? new Date(wo.createdAt).toLocaleDateString() : "-";
            const completed = wo.completedAt ? new Date(wo.completedAt).toLocaleDateString() : "-";
            const statusClass = wo.status === "Completed" ? "completed" : (wo.status === "Cancelled" || wo.status === "Canceled") ? "default" : "open";
            return `<tr><td class="bold">${wo.jobNumber}</td><td>${wo.jobTypeName || "-"}</td><td><span class="status-badge ${statusClass}">${wo.status}</span></td><td>${created}</td><td>${completed}</td><td class="text-right">$${Number(wo.totalAmount || 0).toLocaleString()}</td></tr>`;
        }).join("");
    } else { woBody.innerHTML = '<tr class="empty-row"><td colspan="6">No work orders</td></tr>'; }
    makeSortable("profileWoTable");

    // Invoices
    const invBody = document.getElementById("profileInvBody");
    if (c.invoices && c.invoices.length > 0) {
        invBody.innerHTML = c.invoices.map(inv => {
            const bal = Number(inv.balanceRemaining || 0);
            return `<tr><td class="bold">${inv.invoiceNumber}</td><td>${new Date(inv.dueDate).toLocaleDateString()}</td><td class="text-right">$${Number(inv.totalAmount).toFixed(2)}</td><td class="text-right ${bal > 0 ? 'danger' : ''}">$${bal.toFixed(2)}</td><td>${inv.status || "-"}</td></tr>`;
        }).join("");
    } else { invBody.innerHTML = '<tr class="empty-row"><td colspan="5">No invoices</td></tr>'; }
    makeSortable("profileInvTable");

    // Equipment
    const eqDiv = document.getElementById("profileEquipment");
    if (c.equipment && c.equipment.length > 0) {
        eqDiv.innerHTML = `<table class="data-table" id="profileEqTable"><thead><tr><th>Type</th><th>Brand</th><th>Model</th><th>Serial</th><th>Installed</th><th>Warranty</th></tr></thead><tbody>` +
            c.equipment.map(e => {
                const inst = e.installDate ? new Date(e.installDate).toLocaleDateString() : "-";
                const warr = e.warrantyExpiration ? new Date(e.warrantyExpiration).toLocaleDateString() : "-";
                const expired = e.warrantyExpiration && new Date(e.warrantyExpiration) < new Date();
                return `<tr><td>${e.type}</td><td>${e.brand || "-"}</td><td>${e.modelNumber || "-"}</td><td>${e.serialNumber || "-"}</td><td>${inst}</td><td class="${expired ? 'danger' : ''}">${warr}</td></tr>`;
            }).join("") + `</tbody></table>`;
        makeSortable("profileEqTable");
    } else { eqDiv.innerHTML = '<div class="muted" style="padding:8px 0;">No equipment on file</div>'; }

    document.getElementById("customerModal").classList.remove("hidden");
}

function closeModal() { document.getElementById("customerModal").classList.add("hidden"); }

// ═══════════════════════════════════════════════════════════════
// WORK ORDER BOARD (KANBAN)
// ═══════════════════════════════════════════════════════════════

let boardData = [];
let currentCardId = null;
let draggedCardId = null;

async function loadBoard() {
    const res = await api("/board");
    if (!res || !res.ok) return;
    boardData = await res.json();
    renderBoard();
    populateColumnSelect();
}

function renderBoard() {
    const board = document.getElementById("kanbanBoard");
    board.innerHTML = "";

    boardData.forEach(col => {
        const colEl = document.createElement("div");
        colEl.className = "kanban-column";
        colEl.innerHTML = `
            <div class="kanban-col-header" style="--col-color:${col.color}; border-bottom-color:${col.color};">
                <span class="kanban-col-title">${col.name}</span>
                <span class="kanban-col-count">${col.cards.length}</span>
            </div>
            <div class="kanban-col-body" data-column-id="${col.id}"></div>
        `;

        const body = colEl.querySelector(".kanban-col-body");

        // Drag & drop events on column
        body.addEventListener("dragover", e => { e.preventDefault(); body.classList.add("drag-over"); });
        body.addEventListener("dragleave", () => body.classList.remove("drag-over"));
        body.addEventListener("drop", e => { e.preventDefault(); body.classList.remove("drag-over"); dropCard(col.id, body); });

        // Render cards
        col.cards.forEach(card => {
            const cardEl = document.createElement("div");
            cardEl.className = "kanban-card";
            cardEl.draggable = true;
            cardEl.dataset.cardId = card.id;

            const noteCount = card.notes ? card.notes.length : 0;
            const added = new Date(card.addedAt).toLocaleDateString();

            cardEl.innerHTML = `
                <div class="card-job">#${card.jobNumber}</div>
                <div class="card-customer">${card.customerName || "Unknown"}</div>
                <div class="card-date">Added ${added}</div>
                ${noteCount > 0 ? `<span class="card-note-indicator">📝 ${noteCount}</span>` : ""}
            `;

            // Drag events on card
            cardEl.addEventListener("dragstart", e => {
                draggedCardId = card.id;
                cardEl.classList.add("dragging");
                e.dataTransfer.effectAllowed = "move";
            });
            cardEl.addEventListener("dragend", () => { cardEl.classList.remove("dragging"); });

            // Click to open detail
            cardEl.addEventListener("click", () => openCardModal(card, col));

            body.appendChild(cardEl);
        });

        board.appendChild(colEl);
    });
}

function populateColumnSelect() {
    const select = document.getElementById("boardColumnSelect");
    select.innerHTML = "";
    boardData.forEach(col => {
        select.innerHTML += `<option value="${col.id}">${col.name}</option>`;
    });
}

async function dropCard(columnId, bodyEl) {
    if (!draggedCardId) return;
    const cards = bodyEl.querySelectorAll(".kanban-card");
    const sortOrder = cards.length;

    const res = await api(`/board/cards/${draggedCardId}/move`, {
        method: "PUT",
        body: JSON.stringify({ columnId, sortOrder })
    });

    if (res && res.ok) {
        draggedCardId = null;
        await loadBoard();
    }
}

function showAddCardForm() { document.getElementById("addCardForm").classList.remove("hidden"); document.getElementById("boardJobNumber").focus(); }
function hideAddCardForm() { document.getElementById("addCardForm").classList.add("hidden"); }
function showAddColumnForm() { document.getElementById("addColumnForm").classList.remove("hidden"); document.getElementById("boardNewColName").focus(); }
function hideAddColumnForm() { document.getElementById("addColumnForm").classList.add("hidden"); }

async function addBoardCard() {
    const jobNumber = document.getElementById("boardJobNumber").value.trim();
    const columnId = document.getElementById("boardColumnSelect").value;
    const note = document.getElementById("boardInitialNote").value.trim();

    if (!jobNumber) { toast("Enter a job number.", "error"); return; }

    const res = await api("/board/cards", {
        method: "POST",
        body: JSON.stringify({ jobNumber, columnId, note: note || null })
    });

    if (!res) return;
    const data = await res.json();
    if (!res.ok) { toast(data.message || "Failed to add.", "error"); return; }

    document.getElementById("boardJobNumber").value = "";
    document.getElementById("boardInitialNote").value = "";
    hideAddCardForm();
    await loadBoard();
    toast(`Job #${jobNumber} added to board.`, "success");
}

async function addBoardColumn() {
    const name = document.getElementById("boardNewColName").value.trim();
    const color = document.getElementById("boardNewColColor").value;

    if (!name) { toast("Enter a column name.", "error"); return; }

    const res = await api("/board/columns", {
        method: "POST",
        body: JSON.stringify({ name, color })
    });

    if (res && res.ok) {
        document.getElementById("boardNewColName").value = "";
        hideAddColumnForm();
        await loadBoard();
        toast(`Column "${name}" added.`, "success");
    } else { toast("Failed to add column.", "error"); }
}

async function openCardModal(card, col) {
    currentCardId = card.id;
    document.getElementById("cardModalTitle").innerText = `Job #${card.jobNumber}`;
    document.getElementById("cardModalCustomer").innerText = card.customerName || "Unknown";
    document.getElementById("cardModalAdded").innerText = new Date(card.addedAt).toLocaleString();
    document.getElementById("cardModalColumn").innerText = col.name;
    document.getElementById("cardNoteInput").value = "";

    // Render notes
    const notesList = document.getElementById("cardNotesList");
    if (card.notes && card.notes.length > 0) {
        notesList.innerHTML = card.notes.map(n => `
            <div class="note-item">
                <div>${n.text}</div>
                <div class="note-meta">${n.author || "System"} — ${new Date(n.createdAt).toLocaleString()}</div>
            </div>
        `).join("");
    } else {
        notesList.innerHTML = '<div class="muted" style="padding:8px 0;">No notes yet</div>';
    }

    document.getElementById("cardModal").classList.remove("hidden");
}

function closeCardModal() { document.getElementById("cardModal").classList.add("hidden"); currentCardId = null; }

async function addCardNote() {
    const text = document.getElementById("cardNoteInput").value.trim();
    if (!text || !currentCardId) return;

    const res = await api(`/board/cards/${currentCardId}/notes`, {
        method: "POST",
        body: JSON.stringify({ text })
    });

    if (res && res.ok) {
        document.getElementById("cardNoteInput").value = "";
        await loadBoard();
        // Re-open modal with updated data
        const card = boardData.flatMap(c => c.cards).find(c => c.id === currentCardId);
        const col = boardData.find(c => c.cards.some(cd => cd.id === currentCardId));
        if (card && col) openCardModal(card, col);
        toast("Note added.", "success");
    }
}

async function removeBoardCard() {
    if (!currentCardId || !confirm("Remove this work order from the board?")) return;
    const res = await api(`/board/cards/${currentCardId}`, { method: "DELETE" });
    if (res && res.ok) { closeCardModal(); await loadBoard(); toast("Removed from board.", "success"); }
}

// ═══════════════════════════════════════════════════════════════
// SUBCONTRACTORS
// ═══════════════════════════════════════════════════════════════

async function loadSubcontractors() {
    const res = await api("/subcontractors");
    if (!res || !res.ok) return;
    const subs = await res.json();
    const tbody = document.getElementById("subsBody");
    tbody.innerHTML = "";
    subs.forEach(s => {
        tbody.innerHTML += `<tr class="clickable-row" onclick="loadSubDetail('${s.id}', '${s.name}')">
            <td class="bold">${s.name}</td><td>${s.company || "-"}</td><td>${s.trade || "-"}</td>
            <td class="text-right">${Number(s.totalHours).toFixed(1)}</td>
            <td class="text-right">$${Number(s.totalCost).toLocaleString()}</td>
            <td class="no-sort"><button class="btn-table" onclick="event.stopPropagation(); loadSubDetail('${s.id}', '${s.name}')">View</button></td>
        </tr>`;
    });
    if (subs.length === 0) tbody.innerHTML = '<tr class="empty-row"><td colspan="6">No subcontractors yet</td></tr>';
    makeSortable("subsTable");
}

async function addSubcontractor() {
    const name = document.getElementById("subName").value;
    const company = document.getElementById("subCompany").value;
    const trade = document.getElementById("subTrade").value;
    if (!name) { toast("Name is required.", "error"); return; }
    const res = await api("/subcontractors", { method: "POST", body: JSON.stringify({ name, company, trade }) });
    if (res && res.ok) { document.getElementById("subName").value = ""; document.getElementById("subCompany").value = ""; document.getElementById("subTrade").value = ""; await loadSubcontractors(); toast("Subcontractor added.", "success"); }
    else { toast("Failed to add.", "error"); }
}

async function loadSubDetail(id, name) {
    currentSubId = id;
    const res = await api(`/subcontractors/${id}/entries`);
    if (!res || !res.ok) return;
    const data = await res.json();
    document.getElementById("subDetailTitle").innerText = `${data.name} — ${data.company || ""} (${data.trade || ""})`;
    document.getElementById("subDetail").style.display = "block";

    const woRes = await api("/dashboard");
    if (woRes && woRes.ok) {
        const dashboard = await woRes.json();
        const select = document.getElementById("subEntryWorkOrder");
        select.innerHTML = "";
        if (dashboard.openWorkOrders) { dashboard.openWorkOrders.forEach(wo => { select.innerHTML += `<option value="${wo.id}">${wo.jobNumber} - ${wo.customerName}</option>`; }); }
    }
    document.getElementById("subEntryDate").value = new Date().toISOString().split("T")[0];

    const tbody = document.getElementById("subEntriesBody");
    tbody.innerHTML = "";
    data.entries.forEach(e => {
        tbody.innerHTML += `<tr><td>${new Date(e.date).toLocaleDateString()}</td><td>${e.jobNumber}</td><td>${e.customerName}</td><td class="text-right">${Number(e.hours).toFixed(1)}</td><td class="text-right">$${Number(e.hourlyRate).toFixed(2)}</td><td class="text-right">$${Number(e.totalCost).toFixed(2)}</td><td>${e.notes || ""}</td><td class="no-sort"><button class="btn-table btn-danger-text" onclick="deleteSubEntry('${e.id}')">Del</button></td></tr>`;
    });
    if (data.entries.length === 0) tbody.innerHTML = '<tr class="empty-row"><td colspan="8">No entries yet</td></tr>';
    makeSortable("subEntriesTable");
}

async function logSubHours() {
    const workOrderId = document.getElementById("subEntryWorkOrder").value;
    const hours = parseFloat(document.getElementById("subEntryHours").value);
    const hourlyRate = parseFloat(document.getElementById("subEntryRate").value);
    const date = document.getElementById("subEntryDate").value;
    const notes = document.getElementById("subEntryNotes").value;
    if (!workOrderId || isNaN(hours) || isNaN(hourlyRate) || !date) { toast("Fill in all required fields.", "error"); return; }
    const res = await api("/subcontractors/entries", { method: "POST", body: JSON.stringify({ subcontractorId: currentSubId, workOrderId, hours, hourlyRate, date, notes }) });
    if (res && res.ok) { document.getElementById("subEntryHours").value = ""; document.getElementById("subEntryRate").value = ""; document.getElementById("subEntryNotes").value = ""; await loadSubDetail(currentSubId); await loadSubcontractors(); toast("Hours logged.", "success"); }
    else { toast("Failed to log hours.", "error"); }
}

async function deleteSubEntry(id) {
    if (!confirm("Delete this entry?")) return;
    const res = await api(`/subcontractors/entries/${id}`, { method: "DELETE" });
    if (res && res.ok) { await loadSubDetail(currentSubId); await loadSubcontractors(); toast("Entry deleted.", "success"); }
}

// ═══════════════════════════════════════════════════════════════
// EQUIPMENT
// ═══════════════════════════════════════════════════════════════

async function loadEquipment() {
    const custRes = await api("/customers");
    if (custRes && custRes.ok) {
        const custs = await custRes.json();
        const select = document.getElementById("equipCustomerSelect");
        select.innerHTML = "";
        custs.forEach(c => { select.innerHTML += `<option value="${c.id}">${c.name}</option>`; });
    }
    const res = await api("/equipment");
    if (!res || !res.ok) return;
    const equipment = await res.json();
    const tbody = document.getElementById("equipBody");
    tbody.innerHTML = "";
    equipment.forEach(e => {
        const install = e.installDate ? new Date(e.installDate).toLocaleDateString() : "-";
        const warranty = e.warrantyExpiration ? new Date(e.warrantyExpiration).toLocaleDateString() : "-";
        const isExpired = e.warrantyExpiration && new Date(e.warrantyExpiration) < new Date();
        tbody.innerHTML += `<tr>
            <td>${e.type}</td><td>${e.brand || "-"}</td><td>${e.modelNumber || "-"}</td><td>${e.serialNumber || "-"}</td><td>${e.customerName}</td><td>${install}</td>
            <td class="${isExpired ? 'danger' : ''}">${warranty}</td>
            <td class="text-center"><button class="btn-table" onclick="toggleWarranty('${e.id}')">${e.warrantyRegistered ? "Yes ✓" : "No"}</button></td>
            <td class="no-sort"><button class="btn-table btn-danger-text" onclick="deleteEquipment('${e.id}')">Del</button></td>
        </tr>`;
    });
    if (equipment.length === 0) tbody.innerHTML = '<tr class="empty-row"><td colspan="9">No equipment yet</td></tr>';
    makeSortable("equipTable");
}

async function addEquipment() {
    const customerId = document.getElementById("equipCustomerSelect").value;
    const type = document.getElementById("equipType").value;
    if (!customerId || !type) { toast("Customer and type are required.", "error"); return; }
    const res = await api("/equipment", { method: "POST", body: JSON.stringify({ customerId, type, brand: document.getElementById("equipBrand").value, modelNumber: document.getElementById("equipModel").value, serialNumber: document.getElementById("equipSerial").value, installDate: document.getElementById("equipInstallDate").value || null, warrantyExpiration: document.getElementById("equipWarrantyExp").value || null }) });
    if (res && res.ok) { ["equipType","equipBrand","equipModel","equipSerial","equipInstallDate","equipWarrantyExp"].forEach(id => document.getElementById(id).value = ""); await loadEquipment(); toast("Equipment added.", "success"); }
    else { toast("Failed to add equipment.", "error"); }
}

async function toggleWarranty(id) { const res = await api(`/equipment/${id}/warranty-registered`, { method: "PUT" }); if (res && res.ok) await loadEquipment(); }
async function deleteEquipment(id) { if (!confirm("Delete this equipment?")) return; const res = await api(`/equipment/${id}`, { method: "DELETE" }); if (res && res.ok) { await loadEquipment(); toast("Equipment deleted.", "success"); } }

// ═══════════════════════════════════════════════════════════════
// PM TRACKER
// ═══════════════════════════════════════════════════════════════

async function loadPmTracker() {
    const res = await api("/pm");
    if (!res || !res.ok) return;
    const data = await res.json();
    const tbody = document.getElementById("pmBody");
    tbody.innerHTML = "";
    data.forEach(pm => {
        const lastDate = pm.lastPmDate ? new Date(pm.lastPmDate).toLocaleDateString() : "-";
        const days = pm.daysSinceLastPm;
        let statusClass, statusText, daysClass;

        if (days >= 180) {
            statusClass = "status-badge overdue";
            statusText = "OVERDUE";
            daysClass = "danger";
        } else if (days >= 120) {
            statusClass = "status-badge due-soon";
            statusText = "DUE SOON";
            daysClass = "warning-text";
        } else {
            statusClass = "status-active";
            statusText = "OK";
            daysClass = "";
        }

        tbody.innerHTML += `<tr class="clickable-row" onclick="openCustomerProfile('${pm.customerId}')">
            <td class="bold">${pm.customerName}</td><td>${lastDate}</td><td>${pm.lastJobNumber}</td><td>${pm.lastJobType || "-"}</td><td class="text-center">${pm.totalPms}</td>
            <td class="text-right ${daysClass}">${days} days</td>
            <td><span class="${statusClass}">${statusText}</span></td>
        </tr>`;
    });
    if (data.length === 0) tbody.innerHTML = '<tr class="empty-row"><td colspan="7">No PM data available</td></tr>';
    makeSortable("pmTable");
}

// ═══════════════════════════════════════════════════════════════
// ACCOUNTS PAYABLE
// ═══════════════════════════════════════════════════════════════

async function loadAp() {
    const res = await api("/ap");
    if (!res || !res.ok) return;
    const vendors = await res.json();
    const tbody = document.getElementById("apBody");
    tbody.innerHTML = "";
    vendors.forEach(v => {
        tbody.innerHTML += `<tr class="clickable-row" onclick="loadVendorDetail('${v.id}')">
            <td class="bold">${v.name}</td><td class="text-right">$${Number(v.totalInvoiceAmount || 0).toLocaleString()}</td>
            <td class="text-right">$${Number(v.amountDueNow || 0).toLocaleString()}</td>
            <td>${v.nextDue ? new Date(v.nextDue).toLocaleDateString() : "-"}</td>
        </tr>`;
    });
    if (vendors.length === 0) tbody.innerHTML = '<tr class="empty-row"><td colspan="4">No vendors owed</td></tr>';
    makeSortable("apMainTable");
}

async function loadVendors() {
    const res = await api("/ap/vendors");
    if (!res || !res.ok) return;
    const vendors = await res.json();
    const select = document.getElementById("vendorSelect");
    select.innerHTML = "";
    vendors.forEach(v => { select.innerHTML += `<option value="${v.id}">${v.name}</option>`; });
}

async function loadVendorDetail(id) {
    currentVendorId = id;
    const res = await api(`/ap/vendor/${id}`);
    if (!res || !res.ok) return;
    const data = await res.json();
    document.getElementById("vendorDetailTitle").innerText = data.name;
    const tbody = document.getElementById("vendorBillsBody");
    tbody.innerHTML = "";
    data.bills.forEach(b => {
        tbody.innerHTML += `<tr><td class="text-right">$${Number(b.amount).toFixed(2)}</td><td class="text-right">$${Number(b.totalAmount).toFixed(2)}</td><td>${new Date(b.dueDate).toLocaleDateString()}</td><td class="no-sort"><button class="btn-table" onclick="markPaid('${b.id}')">Mark Paid</button><button class="btn-table btn-danger-text" onclick="deleteBill('${b.id}')">Delete</button></td></tr>`;
    });
    document.getElementById("vendorDetail").style.display = "block";
    makeSortable("vendorBillsTable");
}

function toggleNewVendor() { const s = document.getElementById("newVendorSection"); s.style.display = s.style.display === "none" ? "block" : "none"; }

async function addVendor() {
    const name = document.getElementById("newVendorName").value;
    if (!name?.trim()) { toast("Vendor name is required.", "error"); return; }
    const res = await api("/ap/vendors", { method: "POST", body: JSON.stringify({ name }) });
    if (!res || !res.ok) { toast("Failed to save vendor.", "error"); return; }
    const vendor = await res.json();
    document.getElementById("newVendorName").value = ""; document.getElementById("newVendorSection").style.display = "none";
    await loadVendors(); document.getElementById("vendorSelect").value = vendor.id; toast("Vendor added.", "success");
}

async function addBill() {
    const vendorId = document.getElementById("vendorSelect").value;
    const amount = parseFloat(document.getElementById("billAmount").value);
    const totalAmount = parseFloat(document.getElementById("billTotalAmount").value);
    const dueDate = document.getElementById("billDueDate").value;
    if (!vendorId || isNaN(amount) || isNaN(totalAmount) || !dueDate) { toast("Complete all fields.", "error"); return; }
    const res = await api("/ap/bills", { method: "POST", body: JSON.stringify({ vendorId, amount, totalAmount, dueDate }) });
    if (!res || !res.ok) { toast("Failed to save bill.", "error"); return; }
    document.getElementById("billAmount").value = ""; document.getElementById("billTotalAmount").value = ""; document.getElementById("billDueDate").value = "";
    await loadAp(); toast("Bill added.", "success");
}

async function deleteBill(id) { if (!confirm("Delete this bill?")) return; const res = await api(`/ap/bills/${id}`, { method: "DELETE" }); if (!res || !res.ok) { toast("Delete failed.", "error"); return; } await loadAp(); if (currentVendorId) await loadVendorDetail(currentVendorId); toast("Bill deleted.", "success"); }
async function markPaid(id) { const res = await api(`/ap/pay/${id}`, { method: "PUT" }); if (res && res.ok) { await loadAp(); document.getElementById("vendorDetail").style.display = "none"; toast("Marked as paid.", "success"); } }

// ═══════════════════════════════════════════════════════════════
// HARD REFRESH
// ═══════════════════════════════════════════════════════════════

async function hardRefresh() {
    const btn = document.getElementById("hardRefreshBtn"); btn.disabled = true; btn.innerText = "Syncing...";
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
        } catch (refreshErr) {
            console.warn("Refresh failed (non-fatal):", refreshErr);
        }
        btn.innerText = "Syncing contacts...";
        try {
            const crmRes = await api("/crm/sync", { method: "POST" });
            if (crmRes && crmRes.ok) {
                const crmData = await crmRes.json();
                console.log("CRM sync result:", crmData);
            } else {
                console.warn("CRM sync returned non-OK:", crmRes?.status);
            }
        } catch (crmErr) {
            console.warn("CRM sync failed (non-fatal):", crmErr);
        }
        await loadDashboard();
        toast("Data synced successfully.", "success");
    }
    catch (err) { console.error(err); toast("Sync failed.", "error"); }
    btn.disabled = false; btn.innerText = "↻ Sync Data";
}

// ═══════════════════════════════════════════════════════════════
// ADMIN CENTER
// ═══════════════════════════════════════════════════════════════

async function loadAdminSettings() {
    const res = await api("/admin/settings"); if (!res || !res.ok) return;
    const data = await res.json();
    document.getElementById("adminCompanyName").value = data.companyName || "";
    document.getElementById("adminCcFee").value = data.creditCardFeePercent || 2.5;
    const badge = document.getElementById("stConnectionStatus");
    if (data.serviceTitan.isConfigured) { badge.className = "connection-badge connected"; badge.innerText = "✓ Connected"; } else { badge.className = "connection-badge disconnected"; badge.innerText = "✗ Not Configured"; }
    document.getElementById("adminStTenantId").value = data.serviceTitan.tenantId || "";
    document.getElementById("adminStClientId").value = ""; document.getElementById("adminStClientId").placeholder = data.serviceTitan.clientId || "Enter Client ID";
    document.getElementById("adminStClientSecret").value = ""; document.getElementById("adminStClientSecret").placeholder = data.serviceTitan.clientSecret || "Enter Client Secret";
    document.getElementById("adminStAppKey").value = ""; document.getElementById("adminStAppKey").placeholder = data.serviceTitan.appKey || "Enter App Key";
    document.getElementById("adminAutoSync").checked = data.autoSyncEnabled;
    document.getElementById("adminSyncInterval").value = data.syncIntervalMinutes || 60;
    const syncInfo = document.getElementById("lastSyncInfo");
    syncInfo.innerText = data.lastSyncAt ? `Last sync: ${new Date(data.lastSyncAt).toLocaleString()} — Status: ${data.lastSyncStatus || "Unknown"}` : "No sync has run yet.";
    await loadUsers();
}

async function saveCompanySettings() { const res = await api("/admin/settings/company", { method: "PUT", body: JSON.stringify({ companyName: document.getElementById("adminCompanyName").value, creditCardFeePercent: parseFloat(document.getElementById("adminCcFee").value) }) }); if (res && res.ok) { document.getElementById("sidebarCompanyName").innerText = document.getElementById("adminCompanyName").value; toast("Company settings saved.", "success"); } else { toast("Failed to save.", "error"); } }

async function saveServiceTitanSettings() { const body = {}; const t = document.getElementById("adminStTenantId").value; const c = document.getElementById("adminStClientId").value; const s = document.getElementById("adminStClientSecret").value; const a = document.getElementById("adminStAppKey").value; if (t) body.tenantId = t; if (c) body.clientId = c; if (s) body.clientSecret = s; if (a) body.appKey = a; const res = await api("/admin/settings/servicetitan", { method: "PUT", body: JSON.stringify(body) }); if (res && res.ok) { await loadAdminSettings(); toast("ServiceTitan credentials saved.", "success"); } else { toast("Failed to save credentials.", "error"); } }

async function testServiceTitanConnection() { const d = document.getElementById("stTestResult"); d.classList.remove("hidden"); d.innerHTML = '<span style="color:#94a3b8;">Testing...</span>'; const res = await api("/admin/settings/servicetitan/test", { method: "POST" }); if (!res || !res.ok) { d.innerHTML = '<span style="color:#fca5a5;">Failed to test.</span>'; return; } const data = await res.json(); d.innerHTML = data.success ? '<span style="color:#4ade80;">✓ ' + data.message + '</span>' : '<span style="color:#fca5a5;">✗ ' + data.message + '</span>'; }

async function saveSyncSettings() { const res = await api("/admin/settings/sync", { method: "PUT", body: JSON.stringify({ autoSyncEnabled: document.getElementById("adminAutoSync").checked, syncIntervalMinutes: parseInt(document.getElementById("adminSyncInterval").value) }) }); if (res && res.ok) toast("Sync settings saved.", "success"); else toast("Failed to save.", "error"); }

async function changePassword() { const current = document.getElementById("adminCurrentPw").value; const newPw = document.getElementById("adminNewPw").value; if (!current || !newPw) { toast("Fill in both fields.", "error"); return; } const res = await api("/auth/change-password", { method: "POST", body: JSON.stringify({ currentPassword: current, newPassword: newPw }) }); if (res && res.ok) { document.getElementById("adminCurrentPw").value = ""; document.getElementById("adminNewPw").value = ""; toast("Password updated.", "success"); } else { const data = await res.json(); toast(data.message || "Failed.", "error"); } }

// ═══════════════════════════════════════════════════════════════
// USER MANAGEMENT
// ═══════════════════════════════════════════════════════════════

async function loadUsers() {
    const res = await api("/admin/users"); if (!res || !res.ok) return;
    const users = await res.json();
    const tbody = document.getElementById("usersBody");
    tbody.innerHTML = "";
    users.forEach(u => {
        const isMe = u.id === currentUser?.id;
        tbody.innerHTML += `<tr>
            <td class="bold">${u.fullName || "—"}${isMe ? ' <span class="muted" style="font-size:11px;">(you)</span>' : ""}</td>
            <td>${u.email}</td>
            <td><span class="${u.isActive ? 'status-active' : 'status-inactive'}">${u.isActive ? 'Active' : 'Inactive'}</span></td>
            <td>${u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString() : "Never"}</td>
            <td class="no-sort">${isMe ? '<span class="muted">—</span>' : `<button class="btn-table" onclick="toggleUserActive('${u.id}')">${u.isActive ? "Deactivate" : "Reactivate"}</button><button class="btn-table" onclick="promptResetPassword('${u.id}', '${u.email}')">Reset PW</button><button class="btn-table btn-danger-text" onclick="deleteUser('${u.id}', '${u.email}')">Delete</button>`}</td>
        </tr>`;
    });
    makeSortable("usersTable");
}

async function createUser() { const fullName = document.getElementById("newUserName").value; const email = document.getElementById("newUserEmail").value; const password = document.getElementById("newUserPassword").value; if (!email || !password) { toast("Email and password required.", "error"); return; } if (password.length < 8) { toast("Min 8 characters.", "error"); return; } const res = await api("/admin/users", { method: "POST", body: JSON.stringify({ fullName, email, password }) }); if (!res) return; const data = await res.json(); if (!res.ok) { toast(data.message || "Failed.", "error"); return; } document.getElementById("newUserName").value = ""; document.getElementById("newUserEmail").value = ""; document.getElementById("newUserPassword").value = ""; await loadUsers(); toast("User created.", "success"); }

async function toggleUserActive(id) { const res = await api(`/admin/users/${id}/toggle-active`, { method: "PUT" }); if (!res) return; const data = await res.json(); if (!res.ok) { toast(data.message || "Failed.", "error"); return; } await loadUsers(); toast(data.message, "success"); }

async function promptResetPassword(id, email) { const newPw = prompt(`New password for ${email}:`); if (!newPw) return; if (newPw.length < 8) { toast("Min 8 characters.", "error"); return; } const res = await api(`/admin/users/${id}/reset-password`, { method: "PUT", body: JSON.stringify({ newPassword: newPw }) }); if (!res) return; const data = await res.json(); if (!res.ok) { toast(data.message || "Failed.", "error"); return; } toast(`Password reset for ${email}.`, "success"); }

async function deleteUser(id, email) { if (!confirm(`Delete user ${email}?`)) return; const res = await api(`/admin/users/${id}`, { method: "DELETE" }); if (!res) return; const data = await res.json(); if (!res.ok) { toast(data.message || "Failed.", "error"); return; } await loadUsers(); toast("User deleted.", "success"); }

// ═══════════════════════════════════════════════════════════════
// PM REMINDER
// ═══════════════════════════════════════════════════════════════

async function sendPmReminder(customerId, customerName, daysSince) {
    // Fetch customer contacts to find an email
    const res = await api(`/customers/${customerId}`);
    if (!res || !res.ok) { toast("Couldn't load customer info.", "error"); return; }
    const cust = await res.json();

    const emailContact = (cust.contacts || []).find(c => c.type && c.type.toLowerCase().includes("email"));
    const phoneContact = (cust.contacts || []).find(c => c.type && (c.type.toLowerCase().includes("phone") || c.type.toLowerCase().includes("mobile")));

    const companyName = document.getElementById("sidebarCompanyName").innerText || "Patriot Mechanical";
    const daysText = daysSince > 0 ? `${daysSince} days` : "a while";

    const subject = encodeURIComponent(`Preventive Maintenance Reminder - ${companyName}`);
    const body = encodeURIComponent(
`Hi ${customerName},

This is a friendly reminder from ${companyName} that it has been ${daysText} since your last preventive maintenance service.

Regular maintenance helps prevent costly breakdowns, extends equipment life, and keeps your system running efficiently.

We'd love to get you scheduled. Please reply to this email or give us a call to book your next PM visit.

Thank you,
${companyName}`
    );

    // Build options
    let options = [];
    if (emailContact) {
        options.push(`<a href="mailto:${emailContact.value}?subject=${subject}&body=${body}" target="_blank" class="btn-primary" style="display:inline-block; text-decoration:none; padding:10px 20px; font-size:13px; border-radius:8px;">📧 Email ${emailContact.value}</a>`);
    }
    if (phoneContact) {
        const smsBody = encodeURIComponent(`Hi ${customerName}, this is ${companyName}. It's been ${daysText} since your last preventive maintenance. We'd love to get you scheduled — give us a call or reply here!`);
        options.push(`<a href="sms:${phoneContact.value}?body=${smsBody}" target="_blank" class="btn-primary" style="display:inline-block; text-decoration:none; padding:10px 20px; font-size:13px; border-radius:8px; background:#16a34a;">💬 Text ${phoneContact.value}</a>`);
    }

    if (options.length === 0) {
        // No contact info — show the email text to copy
        const plainBody = decodeURIComponent(body);
        navigator.clipboard.writeText(plainBody).then(() => {
            toast("No email/phone on file. Reminder text copied to clipboard!", "success");
        }).catch(() => {
            toast("No contact info found for this customer.", "error");
        });
        return;
    }

    // Show a quick modal with send options
    const pmSection = document.getElementById("profilePmSection");
    pmSection.innerHTML = `
        <div style="background:#0f172a; border:1px solid #334155; border-radius:8px; padding:16px; margin-top:10px;">
            <p style="margin-bottom:12px; color:#e2e8f0; font-weight:600;">Send PM Reminder to ${customerName}</p>
            <div style="display:flex; gap:10px; flex-wrap:wrap;">
                ${options.join("")}
            </div>
            <p style="margin-top:10px; font-size:12px; color:#64748b;">Click to open your email client or messaging app with a pre-written message.</p>
        </div>`;
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

    if (q.length < 2) {
        wrap.classList.add("hidden");
        searchResultsData = [];
        searchSelectedIdx = -1;
        return;
    }

    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(async () => {
        const res = await api(`/search?q=${encodeURIComponent(q)}`);
        if (!res || !res.ok) return;
        const data = await res.json();
        searchResultsData = data.results || [];
        searchSelectedIdx = -1;
        renderSearchResults();
    }, 250); // debounce 250ms
}

function renderSearchResults() {
    const wrap = document.getElementById("searchResults");
    if (searchResultsData.length === 0) {
        wrap.innerHTML = '<div class="search-empty">No results found</div>';
        wrap.classList.remove("hidden");
        return;
    }

    const icons = {
        customer: "👤", workorder: "📋", equipment: "⚙️",
        vendor: "💰", warranty: "🛡️", subcontractor: "🏗️"
    };

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

    if (e.key === "ArrowDown") {
        e.preventDefault();
        searchSelectedIdx = Math.min(searchSelectedIdx + 1, searchResultsData.length - 1);
        renderSearchResults();
    } else if (e.key === "ArrowUp") {
        e.preventDefault();
        searchSelectedIdx = Math.max(searchSelectedIdx - 1, 0);
        renderSearchResults();
    } else if (e.key === "Enter" && searchSelectedIdx >= 0) {
        e.preventDefault();
        selectSearchResult(searchSelectedIdx);
    } else if (e.key === "Escape") {
        closeSearch();
    }
}

function selectSearchResult(idx) {
    const r = searchResultsData[idx];
    if (!r) return;
    closeSearch();

    // Navigate to the appropriate view and open the record
    switch (r.type) {
        case "customer":
            showView("customersView", document.querySelector('[onclick*="customersView"]'));
            setTimeout(() => openCustomerProfile(r.id), 300);
            break;
        case "workorder":
            showView("boardView", document.querySelector('[onclick*="boardView"]'));
            break;
        case "equipment":
            showView("equipmentView", document.querySelector('[onclick*="equipmentView"]'));
            break;
        case "vendor":
            showView("apView", document.querySelector('[onclick*="apView"]'));
            break;
        case "warranty":
            showView("warrantyView", document.querySelector('[onclick*="warrantyView"]'));
            setTimeout(() => openWarrantyModal(r.id), 300);
            break;
        case "subcontractor":
            showView("subsView", document.querySelector('[onclick*="subsView"]'));
            break;
    }
}

function onSearchFocus() {
    const q = document.getElementById("globalSearchInput").value.trim();
    if (q.length >= 2 && searchResultsData.length > 0) {
        document.getElementById("searchResults").classList.remove("hidden");
    }
}

function closeSearch() {
    document.getElementById("searchResults").classList.add("hidden");
    document.getElementById("globalSearchInput").value = "";
    document.getElementById("globalSearchInput").blur();
    searchResultsData = [];
    searchSelectedIdx = -1;
}

// Close search when clicking outside
document.addEventListener("click", function(e) {
    const wrap = document.querySelector(".global-search-wrap");
    if (wrap && !wrap.contains(e.target)) {
        document.getElementById("searchResults").classList.add("hidden");
    }
});

// Ctrl+K keyboard shortcut
document.addEventListener("keydown", function(e) {
    if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        document.getElementById("globalSearchInput").focus();
    }
});

// ═══════════════════════════════════════════════════════════════
// TO-DO LIST
// ═══════════════════════════════════════════════════════════════

async function loadTodos() {
    const res = await api("/todos");
    if (!res || !res.ok) return;
    const todos = await res.json();

    const wrap = document.getElementById("todoList");
    if (todos.length === 0) {
        wrap.innerHTML = '<div class="card" style="background:#1e293b; text-align:center; padding:40px;"><p class="muted">No tasks yet. Add one above.</p></div>';
        return;
    }

    const incomplete = todos.filter(t => !t.isCompleted);
    const completed = todos.filter(t => t.isCompleted);

    let html = '';

    if (incomplete.length > 0) {
        html += incomplete.map(t => renderTodoItem(t)).join("");
    }

    if (completed.length > 0) {
        html += `<div style="margin-top:20px; margin-bottom:8px; font-size:12px; color:#64748b; text-transform:uppercase; letter-spacing:0.5px; font-weight:600;">Completed (${completed.length})</div>`;
        html += completed.map(t => renderTodoItem(t)).join("");
    }

    wrap.innerHTML = html;
}

function renderTodoItem(t) {
    const checked = t.isCompleted ? "checked" : "";
    const textStyle = t.isCompleted ? "text-decoration:line-through; color:#475569;" : "color:#e2e8f0;";
    const descStyle = t.isCompleted ? "text-decoration:line-through; color:#334155;" : "color:#94a3b8;";
    const completedInfo = t.completedAt ? `<span style="font-size:11px; color:#475569; margin-left:8px;">Done ${new Date(t.completedAt).toLocaleDateString()}</span>` : "";

    return `<div class="todo-item" style="display:flex; align-items:flex-start; gap:12px; padding:12px 16px; background:#1e293b; border:1px solid #334155; border-radius:10px; margin-bottom:6px;">
        <input type="checkbox" ${checked} onchange="toggleTodo('${t.id}')" style="width:20px; height:20px; margin-top:2px; accent-color:var(--accent); cursor:pointer; flex-shrink:0;" />
        <div style="flex:1; min-width:0;">
            <div style="${textStyle} font-size:14px; font-weight:500;">${t.title}${completedInfo}</div>
            ${t.description ? `<div style="${descStyle} font-size:12px; margin-top:2px;">${t.description}</div>` : ""}
        </div>
        <button onclick="deleteTodo('${t.id}')" style="background:none; border:none; color:#475569; cursor:pointer; font-size:16px; padding:0 4px; flex-shrink:0;" title="Delete">✕</button>
    </div>`;
}

async function addTodo() {
    const title = document.getElementById("todoTitle").value.trim();
    const description = document.getElementById("todoDesc").value.trim();
    if (!title) { toast("Enter a task.", "error"); return; }

    const res = await api("/todos", {
        method: "POST",
        body: JSON.stringify({ title, description: description || null })
    });

    if (res && res.ok) {
        document.getElementById("todoTitle").value = "";
        document.getElementById("todoDesc").value = "";
        await loadTodos();
        toast("Task added.", "success");
    }
}

async function toggleTodo(id) {
    const res = await api(`/todos/${id}/toggle`, { method: "PUT" });
    if (res && res.ok) await loadTodos();
}

async function deleteTodo(id) {
    const res = await api(`/todos/${id}`, { method: "DELETE" });
    if (res && res.ok) { await loadTodos(); toast("Deleted.", "success"); }
}

// ═══════════════════════════════════════════════════════════════
// WARRANTY CLAIMS
// ═══════════════════════════════════════════════════════════════

const warrantyStatuses = ["Diagnosis", "Claim Filed", "Approved", "Part Ordered", "Part Shipped", "Part Received", "Installed", "Defective Returned", "Closed"];
let currentWarrantyId = null;

async function loadWarrantyClaims() {
    const showClosed = document.getElementById("warrantyShowClosed")?.checked ? "true" : "false";
    const res = await api(`/warranty?includeClosed=${showClosed}`);
    if (!res || !res.ok) return;
    const claims = await res.json();

    const wrap = document.getElementById("warrantyTableWrap");
    if (claims.length === 0) {
        wrap.innerHTML = '<div class="card" style="background:#1e293b; text-align:center; padding:40px;"><p class="muted">No warranty claims yet. Click "+ New Claim" to start tracking.</p></div>';
        return;
    }

    const statusColors = {
        "Diagnosis": "#64748b", "Claim Filed": "#2563eb", "Approved": "#16a34a",
        "Part Ordered": "#d97706", "Part Shipped": "#ea580c", "Part Received": "#0d9488",
        "Installed": "#16a34a", "Defective Returned": "#9333ea", "Closed": "#475569"
    };

    let html = '<table class="data-table"><thead><tr>';
    html += '<th>Part</th><th>Customer</th><th>Job #</th><th>Supplier</th><th>RMA</th><th>Type</th><th>Status</th><th>ETA</th><th>Age</th>';
    html += '</tr></thead><tbody>';

    claims.forEach(c => {
        const age = Math.floor((Date.now() - new Date(c.createdAt).getTime()) / 86400000);
        const color = statusColors[c.status] || "#64748b";
        const eta = c.expectedShipDate ? new Date(c.expectedShipDate).toLocaleDateString() : "—";
        html += `<tr style="cursor:pointer;" onclick="openWarrantyModal('${c.id}')">`;
        html += `<td><strong>${c.partName}</strong>${c.partModelNumber ? '<br><span class="muted" style="font-size:11px;">' + c.partModelNumber + '</span>' : ''}</td>`;
        html += `<td>${c.customerName || "—"}</td>`;
        html += `<td>${c.jobNumber || "—"}</td>`;
        html += `<td>${c.supplier || "—"}</td>`;
        html += `<td>${c.rmaNumber || "—"}</td>`;
        html += `<td>${c.claimType}</td>`;
        html += `<td><span class="status-badge" style="background:${color}; color:white; padding:3px 10px; border-radius:10px; font-size:11px; font-weight:600;">${c.status}</span></td>`;
        html += `<td>${eta}</td>`;
        html += `<td>${age}d</td>`;
        html += '</tr>';
    });

    html += '</tbody></table>';
    wrap.innerHTML = html;
}

function showNewClaimForm() { document.getElementById("newClaimForm").classList.remove("hidden"); document.getElementById("wcPartName").focus(); }
function hideNewClaimForm() { document.getElementById("newClaimForm").classList.add("hidden"); }

async function createWarrantyClaim() {
    const partName = document.getElementById("wcPartName").value.trim();
    if (!partName) { toast("Part name is required.", "error"); return; }

    const body = {
        partName,
        partModelNumber: document.getElementById("wcPartModel").value.trim() || null,
        unitModelNumber: document.getElementById("wcUnitModel").value.trim() || null,
        unitSerialNumber: document.getElementById("wcUnitSerial").value.trim() || null,
        jobNumber: document.getElementById("wcJobNumber").value.trim() || null,
        customerName: document.getElementById("wcCustomer").value.trim() || null,
        supplier: document.getElementById("wcSupplier").value.trim() || null,
        manufacturer: document.getElementById("wcManufacturer").value.trim() || null,
        claimType: document.getElementById("wcClaimType").value,
        note: document.getElementById("wcNote").value.trim() || null
    };

    const res = await api("/warranty", { method: "POST", body: JSON.stringify(body) });
    if (res && res.ok) {
        hideNewClaimForm();
        ["wcPartName","wcPartModel","wcUnitModel","wcUnitSerial","wcJobNumber","wcCustomer","wcSupplier","wcManufacturer","wcNote"].forEach(id => document.getElementById(id).value = "");
        await loadWarrantyClaims();
        toast("Warranty claim created.", "success");
    } else { toast("Failed to create claim.", "error"); }
}

async function openWarrantyModal(id) {
    currentWarrantyId = id;
    const res = await api(`/warranty/${id}`);
    if (!res || !res.ok) return;
    const c = await res.json();

    document.getElementById("wmTitle").innerText = c.partName;
    document.getElementById("wmPart").innerText = c.partName;
    document.getElementById("wmPartModel").innerText = c.partModelNumber || "—";
    document.getElementById("wmUnitModel").innerText = c.unitModelNumber || "—";
    document.getElementById("wmUnitSerial").innerText = c.unitSerialNumber || "—";
    document.getElementById("wmCustomer").innerText = c.customerName || "—";
    document.getElementById("wmJob").innerText = c.jobNumber || "—";
    document.getElementById("wmSupplier").innerText = c.supplier || "—";
    document.getElementById("wmManufacturer").innerText = c.manufacturer || "—";
    document.getElementById("wmRma").innerText = c.rmaNumber || "—";
    document.getElementById("wmType").innerText = c.claimType + (c.creditAmount ? ` ($${c.creditAmount.toFixed(2)})` : "");
    document.getElementById("wmEta").innerText = c.expectedShipDate ? new Date(c.expectedShipDate).toLocaleDateString() : "—";
    document.getElementById("wmReturnJob").innerText = c.returnJobNumber || "—";

    // Fill edit fields
    document.getElementById("wmEditRma").value = c.rmaNumber || "";
    document.getElementById("wmEditEta").value = c.expectedShipDate ? c.expectedShipDate.substring(0, 10) : "";
    document.getElementById("wmEditReturnJob").value = c.returnJobNumber || "";
    document.getElementById("wmEditCredit").value = c.creditAmount || "";

    // Status pipeline
    const pipelineEl = document.getElementById("wmPipeline");
    const currentIdx = warrantyStatuses.indexOf(c.status);
    pipelineEl.innerHTML = '<div style="display:flex; gap:4px; flex-wrap:wrap;">' +
        warrantyStatuses.map((s, i) => {
            let bg = i < currentIdx ? "#16a34a" : i === currentIdx ? "#2563eb" : "#334155";
            let color = i <= currentIdx ? "white" : "#64748b";
            return `<span style="background:${bg}; color:${color}; padding:4px 8px; border-radius:6px; font-size:11px; font-weight:600; white-space:nowrap;">${s}</span>`;
        }).join("") + '</div>';

    // Status buttons
    const btnWrap = document.getElementById("wmStatusButtons");
    btnWrap.innerHTML = warrantyStatuses.map(s =>
        `<button class="preset-btn${s === c.status ? ' active' : ''}" onclick="changeWarrantyStatus('${s}')" ${s === c.status ? 'disabled' : ''}>${s}</button>`
    ).join("");

    // Notes
    const notesList = document.getElementById("wmNotesList");
    if (c.notes && c.notes.length > 0) {
        notesList.innerHTML = c.notes.map(n => `
            <div class="note-item">
                <div>${n.text}</div>
                <div class="note-meta">${n.author || "System"} — ${new Date(n.createdAt).toLocaleString()}</div>
            </div>
        `).join("");
    } else {
        notesList.innerHTML = '<div class="muted" style="padding:8px 0;">No notes yet</div>';
    }

    document.getElementById("wmNoteInput").value = "";
    document.getElementById("warrantyModal").classList.remove("hidden");
}

function closeWarrantyModal() { document.getElementById("warrantyModal").classList.add("hidden"); currentWarrantyId = null; }

async function changeWarrantyStatus(status) {
    if (!currentWarrantyId) return;
    const res = await api(`/warranty/${currentWarrantyId}/status`, {
        method: "PUT", body: JSON.stringify({ status })
    });
    if (res && res.ok) {
        await loadWarrantyClaims();
        await openWarrantyModal(currentWarrantyId);
        toast(`Status: ${status}`, "success");
    }
}

async function updateWarrantyDetails() {
    if (!currentWarrantyId) return;
    const body = {
        rmaNumber: document.getElementById("wmEditRma").value.trim() || null,
        returnJobNumber: document.getElementById("wmEditReturnJob").value.trim() || null,
        creditAmount: parseFloat(document.getElementById("wmEditCredit").value) || null,
        expectedShipDate: document.getElementById("wmEditEta").value || null
    };
    const res = await api(`/warranty/${currentWarrantyId}`, { method: "PUT", body: JSON.stringify(body) });
    if (res && res.ok) {
        await loadWarrantyClaims();
        await openWarrantyModal(currentWarrantyId);
        toast("Details updated.", "success");
    }
}

async function addWarrantyNote() {
    const text = document.getElementById("wmNoteInput").value.trim();
    if (!text || !currentWarrantyId) return;
    const res = await api(`/warranty/${currentWarrantyId}/notes`, {
        method: "POST", body: JSON.stringify({ text })
    });
    if (res && res.ok) {
        await openWarrantyModal(currentWarrantyId);
        toast("Note added.", "success");
    }
}

async function deleteWarrantyClaim() {
    if (!currentWarrantyId || !confirm("Delete this warranty claim?")) return;
    const res = await api(`/warranty/${currentWarrantyId}`, { method: "DELETE" });
    if (res && res.ok) { closeWarrantyModal(); await loadWarrantyClaims(); toast("Claim deleted.", "success"); }
}

// ═══════════════════════════════════════════════════════════════
// PRICING CALCULATOR
// ═══════════════════════════════════════════════════════════════

function getTier(cost) {
    if (cost < 5)   return { mult: 8,    pct: 700 };
    if (cost < 10)  return { mult: 6,    pct: 500 };
    if (cost < 50)  return { mult: 4,    pct: 300 };
    if (cost < 100) return { mult: 2.5,  pct: 150 };
    return              { mult: 1.75, pct: 75  };
}

function calcPanel(prefix, cost, multiplier, markupPct) {
    const includeCC = document.getElementById("ccToggle").checked;
    const base = cost * multiplier;
    const cc = includeCC ? base * 0.025 : 0;
    const sell = base + cc;
    const profit = sell - cost;
    const margin = sell > 0 ? ((profit / sell) * 100) : 0;

    document.getElementById(prefix + "Cost").innerText = "$" + cost.toFixed(2);
    document.getElementById(prefix + "Multiplier").innerText = multiplier.toFixed(2) + "x";
    document.getElementById(prefix + "MarkupPct").innerText = markupPct.toFixed(1) + "%";
    document.getElementById(prefix + "Base").innerText = "$" + base.toFixed(2);
    document.getElementById(prefix + "CC").innerText = "$" + cc.toFixed(2);
    document.getElementById(prefix + "Sell").innerText = "$" + sell.toFixed(2);
    document.getElementById(prefix + "Profit").innerText = "$" + profit.toFixed(2);
    document.getElementById(prefix + "Margin").innerText = margin.toFixed(1) + "%";
}

function calculateAllPricing() {
    const cost = parseFloat(document.getElementById("pricingCost").value);
    if (isNaN(cost) || cost <= 0) return;

    // Auto-tiered
    const tier = getTier(cost);
    calcPanel("at", cost, tier.mult, tier.pct);

    // Custom (if values set)
    const multInput = parseFloat(document.getElementById("customMultiplier").value);
    const pctInput = parseFloat(document.getElementById("customMarkupPct").value);
    if (!isNaN(multInput) && multInput > 0) {
        calcPanel("cm", cost, multInput, (multInput - 1) * 100);
    } else if (!isNaN(pctInput) && pctInput > 0) {
        const mult = 1 + pctInput / 100;
        calcPanel("cm", cost, mult, pctInput);
    }
}

function customFromPct() {
    const pct = parseFloat(document.getElementById("customMarkupPct").value);
    if (!isNaN(pct) && pct > 0) {
        document.getElementById("customMultiplier").value = (1 + pct / 100).toFixed(4);
    }
    clearPresetActive();
    calculateAllPricing();
}

function customFromMult() {
    const mult = parseFloat(document.getElementById("customMultiplier").value);
    if (!isNaN(mult) && mult > 0) {
        document.getElementById("customMarkupPct").value = ((mult - 1) * 100).toFixed(0);
    }
    clearPresetActive();
    calculateAllPricing();
}

function setCustomPreset(mult, pct) {
    document.getElementById("customMultiplier").value = mult.toFixed(4);
    document.getElementById("customMarkupPct").value = pct;
    document.querySelectorAll(".preset-btn").forEach(b => b.classList.remove("active"));
    event.target.classList.add("active");
    calculateAllPricing();
}

function clearPresetActive() {
    document.querySelectorAll(".preset-btn").forEach(b => b.classList.remove("active"));
}

// ═══════════════════════════════════════════════════════════════
// THEME PICKER
// ═══════════════════════════════════════════════════════════════

const themes = {
    red:    { primary: "#dc2626", hover: "#b91c1c", light: "#fca5a5" },
    blue:   { primary: "#2563eb", hover: "#1d4ed8", light: "#93c5fd" },
    green:  { primary: "#16a34a", hover: "#15803d", light: "#86efac" },
    purple: { primary: "#9333ea", hover: "#7e22ce", light: "#c4b5fd" },
    orange: { primary: "#ea580c", hover: "#c2410c", light: "#fdba74" },
    teal:   { primary: "#0d9488", hover: "#0f766e", light: "#5eead4" },
    pink:   { primary: "#db2777", hover: "#be185d", light: "#f9a8d4" },
    amber:  { primary: "#d97706", hover: "#b45309", light: "#fcd34d" }
};

function applyTheme(name) {
    const t = themes[name];
    if (!t) return;
    document.documentElement.style.setProperty("--accent", t.primary);
    document.documentElement.style.setProperty("--accent-hover", t.hover);
    document.documentElement.style.setProperty("--accent-light", t.light);
    localStorage.setItem("theme", name);

    // Update swatch active states
    document.querySelectorAll(".theme-swatch").forEach(s => {
        s.classList.toggle("active", s.dataset.theme === name);
    });
}

// Init theme on load
document.addEventListener("DOMContentLoaded", () => {
    const saved = localStorage.getItem("theme") || "red";
    applyTheme(saved);

    // Bind settings swatches
    document.getElementById("themePicker")?.addEventListener("click", e => {
        const swatch = e.target.closest(".theme-swatch");
        if (swatch) applyTheme(swatch.dataset.theme);
    });

    // Bind sidebar dots
    document.querySelector(".sidebar-theme")?.addEventListener("click", e => {
        const dot = e.target.closest(".theme-dot");
        if (dot) applyTheme(dot.dataset.theme);
    });
});

// ═══════════════════════════════════════════════════════════════
// MOBILE SIDEBAR
// ═══════════════════════════════════════════════════════════════

function toggleSidebar() {
    document.querySelector(".sidebar")?.classList.toggle("open");
    document.querySelector(".sidebar-overlay")?.classList.toggle("open");
}

// Close sidebar when a nav link is clicked on mobile
document.addEventListener("click", e => {
    if (e.target.closest(".nav-link") && window.innerWidth <= 768) {
        document.querySelector(".sidebar")?.classList.remove("open");
        document.querySelector(".sidebar-overlay")?.classList.remove("open");
    }
});

// ═══════════════════════════════════════════════════════════════
// UTILITIES
// ═══════════════════════════════════════════════════════════════

function showError(id, msg) { const el = document.getElementById(id); el.innerText = msg; el.classList.remove("hidden"); }
function hideError(id) { document.getElementById(id).classList.add("hidden"); }
function toast(message, type = "success") { const el = document.createElement("div"); el.className = `toast ${type}`; el.innerText = message; document.body.appendChild(el); setTimeout(() => el.remove(), 3000); }