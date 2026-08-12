const state = { summary: null, history: [], period: 7, query: "" };
const $ = (id) => document.getElementById(id);

const formatDuration = (ms) => {
  if (!ms || ms <= 0) return "-";
  const seconds = ms / 1000;
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const total = Math.floor(seconds);
  const minutes = Math.floor(total / 60);
  const remain = total % 60;
  if (minutes < 60) return `${String(minutes).padStart(2, "0")}:${String(remain).padStart(2, "0")}`;
  const hours = Math.floor(minutes / 60);
  return `${String(hours).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}:${String(remain).padStart(2, "0")}`;
};

const formatTime = (value) => {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";
  return new Intl.DateTimeFormat("ko-KR", { month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hour12: false }).format(date);
};

const escapeHtml = (value) => String(value ?? "").replace(/[&<>'"]/g, (char) => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", "'":"&#39;", '"':"&quot;" }[char]));

async function loadDashboard() {
  $("refreshButton").disabled = true;
  try {
    const [summaryResponse, historyResponse] = await Promise.all([
      fetch(`/api/summary?days=${state.period}`, { cache: "no-store" }),
      fetch("/api/history?limit=300", { cache: "no-store" })
    ]);
    state.summary = await summaryResponse.json();
    state.history = await historyResponse.json();
    renderAll();
  } catch (error) {
    console.error(error);
  } finally {
    $("refreshButton").disabled = false;
  }
}

function renderAll() {
  const summary = state.summary;
  if (!summary) return;
  $("totalRuns").textContent = summary.total.toLocaleString("ko-KR");
  $("passRuns").textContent = summary.pass.toLocaleString("ko-KR");
  $("failedRuns").textContent = (summary.fail + summary.stopped).toLocaleString("ko-KR");
  $("failureDelta").textContent = summary.skipped ? `SKIP ${summary.skipped} · 미실행 별도` : "검토 필요";
  $("passRate").textContent = `${summary.passRate}%`;
  $("avgDuration").textContent = `평균 소요 ${formatDuration(summary.averageDurationMs)}`;
  $("passDelta").textContent = summary.total ? `${summary.pass}/${summary.total} 정상 완료` : "실행 기록 없음";
  $("historyPath").textContent = summary.logFolder || "ADB_Logs";
  renderTrend(summary.daily);
  renderDonut(summary);
  renderFailures(summary.topFailures);
  renderScenarioQuality(summary.scenarioQuality);
  renderRuns();
}

function renderTrend(daily) {
  const host = $("trendChart");
  if (!daily?.some((item) => item.total > 0)) {
    host.innerHTML = '<div class="empty">선택 기간에 실행 이력이 없습니다.</div>';
    return;
  }
  const width = 900, height = 250, left = 42, right = 18, top = 16, bottom = 34;
  const chartWidth = width - left - right, chartHeight = height - top - bottom;
  const maxTotal = Math.max(1, ...daily.map((item) => item.total));
  const slot = chartWidth / daily.length;
  const barWidth = Math.min(24, slot * .34);
  const points = [];
  let bars = "", labels = "", grids = "";
  for (let i = 0; i < 4; i++) {
    const y = top + chartHeight * i / 3;
    grids += `<line class="grid-line" x1="${left}" y1="${y}" x2="${width-right}" y2="${y}" />`;
  }
  daily.forEach((item, index) => {
    const center = left + slot * index + slot / 2;
    const totalHeight = item.total ? Math.max(5, chartHeight * item.total / maxTotal) : 2;
    let cursor = top + chartHeight;
    const passHeight = item.total ? totalHeight * item.pass / item.total : 0;
    const failHeight = item.total ? totalHeight * item.fail / item.total : 0;
    const stopHeight = item.total ? totalHeight * item.stopped / item.total : 0;
    if (passHeight) { cursor -= passHeight; bars += `<rect class="bar-pass" x="${center-barWidth/2}" y="${cursor}" width="${barWidth}" height="${passHeight}" rx="2" />`; }
    if (failHeight) { cursor -= failHeight; bars += `<rect class="bar-fail" x="${center-barWidth/2}" y="${cursor}" width="${barWidth}" height="${failHeight}" rx="2" />`; }
    if (stopHeight) { cursor -= stopHeight; bars += `<rect class="bar-stop" x="${center-barWidth/2}" y="${cursor}" width="${barWidth}" height="${stopHeight}" rx="2" />`; }
    if (!item.total) bars += `<rect x="${center-barWidth/2}" y="${top+chartHeight-2}" width="${barWidth}" height="2" rx="1" fill="#f1f5f9" />`;
    const rateY = top + chartHeight - chartHeight * item.passRate / 100;
    points.push([center, rateY]);
    labels += `<text class="axis-label" x="${center}" y="${height-9}" text-anchor="middle">${item.label}</text>`;
  });
  const line = points.map((point) => point.join(",")).join(" ");
  const dots = points.map(([x,y]) => `<circle class="trend-point" cx="${x}" cy="${y}" r="3.5" />`).join("");
  host.innerHTML = `<svg class="trend-svg" viewBox="0 0 ${width} ${height}" preserveAspectRatio="none">${grids}${bars}<polyline class="trend-line" points="${line}" />${dots}${labels}</svg>`;
}

function renderDonut(summary) {
  const total = Math.max(1, summary.total);
  const pass = summary.pass / total * 100;
  const fail = summary.fail / total * 100;
  const stop = summary.stopped / total * 100;
  const passEnd = pass;
  const failEnd = pass + fail;
  const stopEnd = pass + fail + stop;
  $("outcomeDonut").style.background = summary.total
    ? `conic-gradient(var(--success) 0 ${passEnd}%, var(--danger) ${passEnd}% ${failEnd}%, var(--warning) ${failEnd}% ${stopEnd}%, var(--surface-raised) ${stopEnd}% 100%)`
    : "var(--surface-raised)";
  $("donutRate").textContent = `${summary.passRate}%`;
  $("legendPass").textContent = summary.pass;
  $("legendFail").textContent = summary.fail;
  $("legendStop").textContent = summary.stopped;
}

function renderFailures(items) {
  const host = $("failureRanking");
  if (!items?.length) {
    host.innerHTML = '<div class="empty">선택 기간에 실패한 시나리오가 없습니다.</div>';
    return;
  }
  const max = Math.max(...items.map((item) => item.count));
  host.innerHTML = items.map((item, index) => `
    <div class="rank-item">
      <div class="rank-top"><span>${index + 1}. ${escapeHtml(item.scenario)}</span><b>${item.count}회</b></div>
      <div class="rank-bar"><i style="width:${item.count / max * 100}%"></i></div>
    </div>`).join("");
}

function renderScenarioQuality(items) {
  const host = $("scenarioQuality");
  if (!items?.length) {
    host.innerHTML = '<div class="empty">시나리오 품질 데이터가 없습니다.</div>';
    return;
  }
  host.innerHTML = items.slice(0, 10).map((item) => `
    <div class="scenario-row">
      <div class="scenario-title"><span>${escapeHtml(item.scenario)}</span><b>${item.passRate}%</b></div>
      <div class="quality-track"><i style="width:${item.passRate}%"></i></div>
      <div class="scenario-meta"><span>PASS ${item.pass} · FAIL ${item.fail} · STOP ${item.stopped}${item.skipped ? ` · SKIP ${item.skipped}` : ""}</span><span>평균 ${formatDuration(item.averageDurationMs)}</span></div>
    </div>`).join("");
}

function renderRuns() {
  const query = state.query.trim().toLowerCase();
  const records = state.history.filter((item) => !query || item.scenario.toLowerCase().includes(query)).slice(0, 80);
  const body = $("runsTableBody");
  if (!records.length) {
    body.innerHTML = '<tr><td colspan="6"><div class="empty">표시할 실행 기록이 없습니다.</div></td></tr>';
    return;
  }
  body.innerHTML = records.map((item, index) => {
    const statusClass = item.status.toLowerCase();
    const environment = [item.deviceModel, item.osVersion].filter(Boolean).join(" · ") || "-";
    return `<tr data-index="${index}"><td><span class="status-badge ${statusClass}">${item.status}</span></td><td title="${escapeHtml(item.scenario)}">${escapeHtml(item.scenario)}</td><td title="${escapeHtml(environment)}">${escapeHtml(environment)}</td><td>${item.totalSteps.toLocaleString("ko-KR")}</td><td>${formatDuration(item.durationMs)}</td><td>${formatTime(item.timestamp)}</td></tr>`;
  }).join("");
  [...body.querySelectorAll("tr[data-index]")].forEach((row) => row.addEventListener("click", () => showRun(records[Number(row.dataset.index)])));
}

function artifactUrl(folder, file) {
  if (!folder) return "";
  const path = `${folder.replace(/[\\/]$/, "")}/${file}`;
  return `/api/artifact?path=${encodeURIComponent(path)}`;
}

function renderStepDetail(steps) {
  if (!steps?.length) return '<div class="empty compact">이 실행에는 Step 단위 기록이 없습니다.</div>';
  return `<div class="step-list">${steps.map((step) => {
    const status = (step.status || "").toLowerCase();
    const visual = step.artifactFolder ? `
      <div class="artifact-grid">
        <a href="${artifactUrl(step.artifactFolder, "screen.png")}" target="_blank"><img src="${artifactUrl(step.artifactFolder, "screen.png")}" alt="실행 화면"><span>실행 화면</span></a>
        <a href="${artifactUrl(step.artifactFolder, "diff.png")}" target="_blank"><img src="${artifactUrl(step.artifactFolder, "diff.png")}" alt="Diff"><span>Diff</span></a>
        <a class="artifact-file" href="${artifactUrl(step.artifactFolder, "ui_tree.xml")}" target="_blank">UI Tree 열기</a>
      </div>` : "";
    return `<div class="step-item ${status}">
      <div class="step-head"><span class="status-badge ${status}">${escapeHtml(step.status || "-")}</span><b>#${step.index} · LOOP ${step.loop || 1}</b><span>${formatDuration(step.durationMs)}</span>${step.matchRate != null ? `<span>Match ${Number(step.matchRate).toFixed(2)}%</span>` : ""}</div>
      <div class="step-raw">${escapeHtml(step.raw || "")}</div>
      ${step.message ? `<div class="step-message">${escapeHtml(step.message)}</div>` : ""}
      ${visual}
    </div>`;
  }).join("")}</div>`;
}

function showRun(item) {
  $("dialogTitle").textContent = item.scenario;
  $("dialogContent").innerHTML = `
    <dl class="detail-grid">
      <dt>결과</dt><dd><span class="status-badge ${item.status.toLowerCase()}">${item.status}</span></dd>
      <dt>Batch ID</dt><dd>${escapeHtml(item.batchId || "-")}</dd>
      <dt>실행 시각</dt><dd>${formatTime(item.timestamp)}</dd>
      <dt>기기</dt><dd>${escapeHtml(item.deviceModel || "-")}</dd>
      <dt>Serial</dt><dd>${escapeHtml(item.deviceSerial || "-")}</dd>
      <dt>OS</dt><dd>${escapeHtml(item.osVersion || "-")}</dd>
      <dt>단계</dt><dd>${item.totalSteps.toLocaleString("ko-KR")}</dd>
      <dt>소요 시간</dt><dd>${formatDuration(item.durationMs)}</dd>
    </dl>
    ${item.failMessage ? `<div class="error-box">${escapeHtml(item.failMessage)}</div>` : ""}
    <div class="detail-section-title">STEP 실행 결과</div>
    ${renderStepDetail(item.steps)}`;
  $("runDialog").showModal();
}

$("periodSelect").addEventListener("change", (event) => { state.period = Number(event.target.value); loadDashboard(); });
$("refreshButton").addEventListener("click", loadDashboard);
[...document.querySelectorAll(".export-button")].forEach((button) => button.addEventListener("click", () => { window.location.href = `/api/export?format=${encodeURIComponent(button.dataset.format)}&days=${state.period}`; }));
$("searchInput").addEventListener("input", (event) => { state.query = event.target.value; renderRuns(); });
$("closeDialog").addEventListener("click", () => $("runDialog").close());
$("runDialog").addEventListener("click", (event) => { if (event.target === $("runDialog")) $("runDialog").close(); });

[...document.querySelectorAll(".nav-item")].forEach((button) => {
  button.addEventListener("click", () => {
    document.querySelectorAll(".nav-item").forEach((item) => item.classList.remove("active"));
    button.classList.add("active");
    document.getElementById(button.dataset.section)?.scrollIntoView({ behavior: "smooth", block: "start" });
  });
});

setInterval(loadDashboard, 15000);
loadDashboard();
