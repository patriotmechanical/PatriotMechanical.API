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
    const views = ["dashboardPage", "customersView", "subsView", "equipmentView", "pmView", "apView", "pricingView", "adminView"];
    views.forEach(v => { const el = document.getElementById(v); if (el) el.style.display = "none"; });
    document.getElementById(viewId).style.display = "block";
    if (clickedLink) { document.querySelectorAll(".nav-link").forEach(l => l.classList.remove("active")); clickedLink.classList.add("active"); }
    if (viewId === "dashboardPage") loadDashboard();
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

    // Last PM
    if (c.lastPm) {
        document.getElementById("profileLastPm").innerText = new Date(c.lastPm.completedAt).toLocaleDateString();
    } else {
        document.getElementById("profileLastPm").innerText = "None";
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
        tbody.innerHTML += `<tr class="clickable-row" onclick="openCustomerProfile('${pm.customerId}')">
            <td class="bold">${pm.customerName}</td><td>${lastDate}</td><td>${pm.lastJobNumber}</td><td>${pm.lastJobType || "-"}</td><td class="text-center">${pm.totalPms}</td>
            <td class="text-right ${pm.isOverdue ? 'danger' : ''}">${pm.daysSinceLastPm} days</td>
            <td><span class="${pm.isOverdue ? 'status-badge overdue' : 'status-active'}">${pm.isOverdue ? 'OVERDUE' : 'OK'}</span></td>
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
    try { await api("/servicetitan/sync/customers", { method: "POST" }); await api("/servicetitan/sync/jobs", { method: "POST" }); await api("/servicetitan/sync/invoices", { method: "POST" }); await loadDashboard(); toast("Data synced successfully.", "success"); }
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
// PRICING CALCULATOR
// ═══════════════════════════════════════════════════════════════

async function calculatePrice() {
    const cost = parseFloat(document.getElementById("pricingCost").value);
    if (isNaN(cost) || cost <= 0) { toast("Enter a valid cost.", "error"); return; }

    const res = await api(`/pricing/calculate?cost=${cost}`);
    if (!res || !res.ok) { toast("Pricing calculation failed.", "error"); return; }
    const data = await res.json();

    document.getElementById("pricingResult").classList.remove("hidden");
    document.getElementById("pricingMultiplier").innerText = data.multiplier + "x";
    document.getElementById("pricingBase").innerText = "$" + Number(data.basePrice).toFixed(2);
    document.getElementById("pricingFinal").innerText = "$" + Number(data.finalPrice).toFixed(2);
    document.getElementById("pricingProfit").innerText = "$" + (Number(data.finalPrice) - Number(data.cost)).toFixed(2);
    document.getElementById("pricingMargin").innerText = ((1 - Number(data.cost) / Number(data.finalPrice)) * 100).toFixed(1) + "%";
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